import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Stack,
  TextField,
} from '@mui/material'
import { useEffect, useRef, useState } from 'react'
import { createReservation, updateReservation } from '../api/reservations'
import type { ApiErrorItem, ReservationDto, RoomDto, UserDto } from '../api/types'
import { toIsoWithOffset, toLocalInputValue } from '../lib/dateTime'
import { isStaleError, toErrorItems } from '../lib/errors'
import { ErrorAlert } from './ErrorAlert'

interface FormValues {
  roomId: string
  userId: string
  title: string
  start: string
  end: string
  attendees: string
}

interface FieldMessages {
  roomId?: string
  userId?: string
  start?: string
  end?: string
  attendees?: string
}

interface ReservationFormDialogProps {
  /** The reservation to change, or null to create a new one. */
  reservation: ReservationDto | null
  rooms: RoomDto[]
  users: UserDto[]
  onClose: () => void
  onSaved: (message: string) => void
  /** Called when the server says the row is out of date (404 or 409), so the page can reload the list. */
  onStale: () => void
}

function initialValues(reservation: ReservationDto | null): FormValues {
  if (reservation === null) {
    return { roomId: '', userId: '', title: '', start: '', end: '', attendees: '' }
  }
  return {
    roomId: reservation.roomId,
    userId: reservation.userId,
    title: reservation.title,
    start: toLocalInputValue(reservation.start),
    end: toLocalInputValue(reservation.end),
    attendees: String(reservation.attendeeCount),
  }
}

/** Only builds the request value: anything that is not a whole number is a field message, every limit is the API's. */
function toWholeNumber(text: string): number | undefined {
  const trimmed = text.trim()
  const value = /^-?\d+$/.test(trimmed) ? Number(trimmed) : undefined
  return value !== undefined && Number.isSafeInteger(value) ? value : undefined
}

/** Create or change dialog. Render it only while it is open: each opening starts from the reservation (or empty). */
export function ReservationFormDialog({
  reservation,
  rooms,
  users,
  onClose,
  onSaved,
  onStale,
}: ReservationFormDialogProps) {
  const editing = reservation !== null
  const [values, setValues] = useState<FormValues>(() => initialValues(reservation))
  const [fieldMessages, setFieldMessages] = useState<FieldMessages>({})
  const [errors, setErrors] = useState<ApiErrorItem[]>([])
  const [saving, setSaving] = useState(false)
  // A ref is updated at once, so a fast double click cannot send the request twice.
  const busy = useRef(false)
  const mounted = useRef(true)

  useEffect(() => {
    mounted.current = true
    return () => {
      mounted.current = false
    }
  }, [])

  function change(field: keyof FormValues, value: string) {
    setValues((current) => ({ ...current, [field]: value }))
  }

  async function save() {
    if (busy.current) {
      return
    }
    const start = toIsoWithOffset(values.start)
    const end = toIsoWithOffset(values.end)
    const attendeeCount = toWholeNumber(values.attendees)
    const messages: FieldMessages = {}
    if (values.roomId === '') {
      messages.roomId = 'Choose a room.'
    }
    if (values.userId === '') {
      messages.userId = 'Choose an organizer.'
    }
    if (start === undefined) {
      messages.start = 'Enter a start date and time.'
    }
    if (end === undefined) {
      messages.end = 'Enter an end date and time.'
    }
    if (attendeeCount === undefined) {
      messages.attendees = 'Enter a whole number.'
    }
    setFieldMessages(messages)
    if (start === undefined || end === undefined || attendeeCount === undefined || Object.keys(messages).length > 0) {
      return
    }

    busy.current = true
    setSaving(true)
    setErrors([])
    const body = { roomId: values.roomId, userId: values.userId, title: values.title, start, end, attendeeCount }
    try {
      if (reservation === null) {
        await createReservation(body)
      } else {
        await updateReservation(reservation.id, body)
      }
      if (mounted.current) {
        onSaved(reservation === null ? 'Reservation created.' : 'Reservation updated.')
      }
    } catch (error: unknown) {
      if (mounted.current) {
        setErrors(toErrorItems(error))
        if (isStaleError(error)) {
          onStale()
        }
      }
    } finally {
      busy.current = false
      if (mounted.current) {
        setSaving(false)
      }
    }
  }

  function close() {
    if (!busy.current) {
      onClose()
    }
  }

  return (
    <Dialog open onClose={close} aria-labelledby="reservation-form-title" fullWidth maxWidth="sm">
      <DialogTitle id="reservation-form-title">{editing ? 'Edit reservation' : 'New reservation'}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          <ErrorAlert errors={errors} />
          <TextField
            select
            label="Room"
            value={values.roomId}
            onChange={(event) => change('roomId', event.target.value)}
            error={fieldMessages.roomId !== undefined}
            helperText={fieldMessages.roomId}
            required
          >
            {rooms.map((room) => (
              <MenuItem key={room.id} value={room.id}>
                {room.isActive ? room.name : `${room.name} (inactive)`}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            select
            label="Organizer"
            value={values.userId}
            onChange={(event) => change('userId', event.target.value)}
            error={fieldMessages.userId !== undefined}
            helperText={fieldMessages.userId}
            required
          >
            {users.map((user) => (
              <MenuItem key={user.id} value={user.id}>
                {user.firstName} {user.lastName}
              </MenuItem>
            ))}
          </TextField>
          <TextField label="Title" value={values.title} onChange={(event) => change('title', event.target.value)} />
          <TextField
            label="Start"
            type="datetime-local"
            value={values.start}
            onChange={(event) => change('start', event.target.value)}
            error={fieldMessages.start !== undefined}
            helperText={fieldMessages.start}
            slotProps={{ inputLabel: { shrink: true } }}
            required
          />
          <TextField
            label="End"
            type="datetime-local"
            value={values.end}
            onChange={(event) => change('end', event.target.value)}
            error={fieldMessages.end !== undefined}
            helperText={fieldMessages.end}
            slotProps={{ inputLabel: { shrink: true } }}
            required
          />
          <TextField
            label="Attendees"
            value={values.attendees}
            onChange={(event) => change('attendees', event.target.value)}
            error={fieldMessages.attendees !== undefined}
            helperText={fieldMessages.attendees}
            slotProps={{ htmlInput: { inputMode: 'numeric' } }}
            required
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={close} disabled={saving}>
          Cancel
        </Button>
        <Button variant="contained" onClick={() => void save()} disabled={saving}>
          {saving ? 'Saving…' : 'Save'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
