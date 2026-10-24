import type { DateSelectArg, DatesSetArg, EventClickArg, EventInput } from '@fullcalendar/core'
import dayGridPlugin from '@fullcalendar/daygrid'
import interactionPlugin from '@fullcalendar/interaction'
import type { DateClickArg } from '@fullcalendar/interaction'
import FullCalendar from '@fullcalendar/react'
import timeGridPlugin from '@fullcalendar/timegrid'
import { Box, Button, LinearProgress, MenuItem, Snackbar, Stack, TextField, Typography, useMediaQuery, useTheme } from '@mui/material'
import { useEffect, useMemo, useState } from 'react'
import { listReservations } from '../api/reservations'
import { listRooms } from '../api/rooms'
import type { ApiErrorItem, ReservationDto, ReservationStatus, RoomDto, UserDto } from '../api/types'
import { listUsers } from '../api/users'
import { ErrorAlert } from '../components/ErrorAlert'
import { ReservationDetailsDialog } from '../components/ReservationDetailsDialog'
import { ReservationFormDialog } from '../components/ReservationFormDialog'
import type { ReservationPrefill } from '../components/ReservationFormDialog'
import { toErrorItems } from '../lib/errors'

const PLUGINS = [dayGridPlugin, timeGridPlugin, interactionPlugin]

/** The visible range exactly as FullCalendar reports it: `start` inclusive, `end` exclusive, as ISO timestamps. */
interface VisibleRange {
  start: string
  end: string
}

/** Events together with the room they were loaded for, so the events of an earlier room are never shown. */
interface LoadedEvents {
  roomId: string
  items: ReservationDto[]
}

type DialogState =
  | { kind: 'create'; prefill: ReservationPrefill }
  | { kind: 'details'; reservation: ReservationDto }
  | null

/** Same errors from several requests (for example the backend being down) are shown once. */
function uniqueErrors(lists: ApiErrorItem[][]): ApiErrorItem[] {
  const result: ApiErrorItem[] = []
  for (const item of lists.flat()) {
    if (!result.some((known) => known.code === item.code && known.message === item.message)) {
      result.push(item)
    }
  }
  return result
}

/** The status is also written in the text, so the colour is not the only signal. */
function eventTitle(reservation: ReservationDto): string {
  const title = reservation.title === '' ? 'Reservation' : reservation.title
  return reservation.status === 'Booked' ? title : `${title} (${reservation.status})`
}

export function CalendarPage() {
  const theme = useTheme()
  const narrow = useMediaQuery(theme.breakpoints.down('sm'), { noSsr: true })
  // Only the first render decides the starting view; later resizes never change the view the user chose.
  const [initialView] = useState(narrow ? 'timeGridDay' : 'timeGridWeek')

  const [rooms, setRooms] = useState<RoomDto[]>([])
  const [roomsLoading, setRoomsLoading] = useState(true)
  const [roomErrors, setRoomErrors] = useState<ApiErrorItem[]>([])
  const [roomReloadCount, setRoomReloadCount] = useState(0)

  const [users, setUsers] = useState<UserDto[]>([])
  const [usersLoading, setUsersLoading] = useState(true)
  const [userErrors, setUserErrors] = useState<ApiErrorItem[]>([])
  const [userReloadCount, setUserReloadCount] = useState(0)

  const [roomId, setRoomId] = useState('')
  const [range, setRange] = useState<VisibleRange | null>(null)
  const [loaded, setLoaded] = useState<LoadedEvents>({ roomId: '', items: [] })
  const [loading, setLoading] = useState(false)
  const [eventErrors, setEventErrors] = useState<ApiErrorItem[]>([])
  // Changing this value re-runs the events effect (Retry, and refresh after a write or a stale answer).
  const [reloadCount, setReloadCount] = useState(0)
  const [retryRequested, setRetryRequested] = useState(false)

  const [dialog, setDialog] = useState<DialogState>(null)
  const [notice, setNotice] = useState<string | null>(null)

  // Rooms: loaded once per visit (and again on Retry). The first room is selected.
  useEffect(() => {
    const controller = new AbortController()
    setRoomsLoading(true)
    setRoomErrors([])
    listRooms({}, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) {
          return
        }
        setRooms(result)
        setRoomId((current) => (result.some((room) => room.id === current) ? current : (result[0]?.id ?? '')))
        setRoomsLoading(false)
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) {
          return
        }
        setRooms([])
        setRoomId('')
        setRoomErrors(toErrorItems(error))
        setRoomsLoading(false)
      })
    return () => controller.abort()
  }, [roomReloadCount])

  // Users only fill the Organizer drop-down of the create dialog: the calendar works without them.
  useEffect(() => {
    const controller = new AbortController()
    setUsersLoading(true)
    setUserErrors([])
    listUsers(controller.signal)
      .then((result) => {
        if (controller.signal.aborted) {
          return
        }
        setUsers(result)
        setUsersLoading(false)
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) {
          return
        }
        setUsers([])
        setUserErrors(toErrorItems(error))
        setUsersLoading(false)
      })
    return () => controller.abort()
  }, [userReloadCount])

  const rangeStart = range?.start
  const rangeEnd = range?.end

  // Events of the selected room in the visible range. Aborting on every change means an older response never
  // replaces a newer one.
  useEffect(() => {
    if (roomId === '' || rangeStart === undefined || rangeEnd === undefined) {
      return
    }
    const controller = new AbortController()
    setLoading(true)
    setEventErrors([])
    listReservations({ roomId, from: rangeStart, to: rangeEnd }, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) {
          return
        }
        setLoaded({ roomId, items: result })
        setLoading(false)
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) {
          return
        }
        setLoaded({ roomId, items: [] })
        setEventErrors(toErrorItems(error))
        setLoading(false)
      })
    return () => controller.abort()
  }, [roomId, rangeStart, rangeEnd, reloadCount])

  const reservations = loaded.roomId === roomId ? loaded.items : []

  const events = useMemo<EventInput[]>(() => {
    const { palette } = theme
    return reservations.map((reservation) => {
      const colors: Record<ReservationStatus, { background: string; border: string; text: string }> = {
        Booked: { background: palette.primary.main, border: palette.primary.main, text: palette.primary.contrastText },
        Completed: { background: palette.success.main, border: palette.success.main, text: palette.success.contrastText },
        Cancelled: { background: palette.grey[300], border: palette.grey[500], text: palette.text.secondary },
      }
      const color = colors[reservation.status]
      return {
        id: reservation.id,
        title: eventTitle(reservation),
        start: reservation.start,
        // The room is free from `endedAt` when a reservation was ended early: the same interval the API uses.
        end: reservation.endedAt ?? reservation.end,
        backgroundColor: color.background,
        borderColor: color.border,
        textColor: color.text,
        classNames: reservation.status === 'Cancelled' ? ['reservation-cancelled'] : [],
        extendedProps: { reservation },
      }
    })
  }, [reservations, theme])

  function reloadEvents() {
    setReloadCount((count) => count + 1)
  }

  function handleDatesSet(arg: DatesSetArg) {
    const start = arg.start.toISOString()
    const end = arg.end.toISOString()
    // The same object back means no re-render, so reporting the same range again cannot start a loop.
    setRange((current) => (current !== null && current.start === start && current.end === end ? current : { start, end }))
  }

  function handleEventClick(arg: EventClickArg) {
    const reservation = reservations.find((item) => item.id === arg.event.id)
    if (reservation !== undefined) {
      setDialog({ kind: 'details', reservation })
    }
  }

  function handleSelect(arg: DateSelectArg) {
    setDialog({ kind: 'create', prefill: { roomId, start: arg.start.toISOString(), end: arg.end.toISOString() } })
    arg.view.calendar.unselect()
  }

  function handleDateClick(arg: DateClickArg) {
    if (arg.view.type === 'dayGridMonth') {
      arg.view.calendar.changeView('timeGridDay', arg.date)
    }
  }

  function finishWrite(message: string) {
    setDialog(null)
    setNotice(message)
    reloadEvents()
  }

  const retrying = roomsLoading || usersLoading || loading
  const allErrors = uniqueErrors([roomErrors, userErrors, eventErrors])
  // After a click the button stays mounted until the retry finishes, so the keyboard focus is not lost.
  // Only a click on Retry sets the flag: a reload after a write or a navigation must not show "Retrying…".
  const showRetry = allErrors.length > 0 || (retryRequested && retrying)
  const usersReady = !usersLoading && userErrors.length === 0
  const canCreate = roomId !== '' && usersReady

  // The loads start one render after the click, so the flag is cleared only when `retrying` turns false again.
  useEffect(() => {
    if (!retrying) {
      setRetryRequested(false)
    }
  }, [retrying])

  function retry() {
    if (retrying) {
      return
    }
    setRetryRequested(true)
    if (roomErrors.length > 0) {
      setRoomReloadCount((count) => count + 1)
    }
    if (userErrors.length > 0) {
      setUserReloadCount((count) => count + 1)
    }
    if (eventErrors.length > 0) {
      reloadEvents()
    }
  }

  return (
    <Stack spacing={2}>
      <Typography variant="h4" component="h1">
        Calendar
      </Typography>

      <Stack direction="row" spacing={2} useFlexGap sx={{ flexWrap: 'wrap', alignItems: 'flex-start' }}>
        <TextField
          select
          size="small"
          label="Room"
          value={roomId}
          onChange={(event) => setRoomId(event.target.value)}
          disabled={rooms.length === 0}
          sx={{ minWidth: 200 }}
        >
          {rooms.map((room) => (
            <MenuItem key={room.id} value={room.id}>
              {room.isActive ? room.name : `${room.name} (inactive)`}
            </MenuItem>
          ))}
        </TextField>
        <Button variant="contained" disabled={!canCreate} onClick={() => setDialog({ kind: 'create', prefill: { roomId } })}>
          New reservation
        </Button>
      </Stack>

      <ErrorAlert errors={allErrors} />
      {showRetry && (
        <div>
          {/* aria-disabled (not disabled) keeps keyboard focus on the button while the retry runs. */}
          <Button variant="outlined" aria-disabled={retrying} onClick={retry}>
            {retrying ? 'Retrying…' : 'Retry'}
          </Button>
        </div>
      )}

      {rooms.length === 0 ? (
        <Typography>{roomsLoading ? 'Loading rooms…' : 'No rooms to show.'}</Typography>
      ) : (
        <Box>
          <Box sx={{ height: 4 }}>{loading && <LinearProgress aria-label="Loading reservations" />}</Box>
          <Box
            sx={{
              '--fc-border-color': theme.palette.divider,
              '--fc-button-bg-color': theme.palette.primary.main,
              '--fc-button-border-color': theme.palette.primary.main,
              '--fc-button-hover-bg-color': theme.palette.primary.dark,
              '--fc-button-hover-border-color': theme.palette.primary.dark,
              '--fc-button-active-bg-color': theme.palette.primary.dark,
              '--fc-button-active-border-color': theme.palette.primary.dark,
              '--fc-today-bg-color': theme.palette.action.hover,
              '--fc-now-indicator-color': theme.palette.error.main,
              '--fc-page-bg-color': theme.palette.background.paper,
              fontFamily: theme.typography.fontFamily,
              '& .fc-toolbar-title': { fontSize: theme.typography.h6.fontSize },
              '& .fc-toolbar.fc-header-toolbar': { flexWrap: 'wrap', gap: 1 },
              '& .fc-event:focus-visible': { outline: `2px solid ${theme.palette.text.primary}`, outlineOffset: 1 },
              '& .reservation-cancelled .fc-event-title': { textDecoration: 'line-through' },
            }}
          >
            <FullCalendar
              plugins={PLUGINS}
              initialView={initialView}
              headerToolbar={{ left: 'prev,next today', center: 'title', right: 'dayGridMonth,timeGridWeek,timeGridDay' }}
              timeZone="local"
              scrollTime="07:00:00"
              nowIndicator
              allDaySlot={false}
              height="auto"
              events={events}
              datesSet={handleDatesSet}
              eventClick={handleEventClick}
              selectable={usersReady}
              select={handleSelect}
              dateClick={handleDateClick}
              views={{ dayGridMonth: { selectable: false } }}
            />
          </Box>
        </Box>
      )}

      {dialog?.kind === 'create' && (
        <ReservationFormDialog
          reservation={null}
          prefill={dialog.prefill}
          rooms={rooms}
          users={users}
          onClose={() => setDialog(null)}
          onSaved={finishWrite}
          onStale={reloadEvents}
        />
      )}
      {dialog?.kind === 'details' && <ReservationDetailsDialog reservation={dialog.reservation} onClose={() => setDialog(null)} />}

      <Snackbar
        open={notice !== null}
        autoHideDuration={4000}
        message={notice}
        onClose={(_event, reason) => {
          if (reason !== 'clickaway') {
            setNotice(null)
          }
        }}
      />
    </Stack>
  )
}
