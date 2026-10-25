import {
  Button,
  Chip,
  MenuItem,
  Paper,
  Snackbar,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material'
import { useEffect, useState } from 'react'
import { fetchAllPages } from '../api/paging'
import { listReservations, cancelReservation, endReservation } from '../api/reservations'
import { listRooms } from '../api/rooms'
import type { ApiErrorItem, ReservationDto, ReservationStatus, RoomDto, UserDto } from '../api/types'
import { listUsers } from '../api/users'
import { ConfirmActionDialog } from '../components/ConfirmActionDialog'
import { ErrorAlert } from '../components/ErrorAlert'
import { ListPager, type PageInfo } from '../components/ListPager'
import { LoadingIndicator } from '../components/LoadingIndicator'
import { ReservationFormDialog } from '../components/ReservationFormDialog'
import { formatDateTime, toIsoWithOffset } from '../lib/dateTime'
import { toErrorItems } from '../lib/errors'

const STATUSES: ReservationStatus[] = ['Booked', 'Completed', 'Cancelled']

type StatusColor = 'primary' | 'success' | 'default'

const STATUS_COLORS: Record<ReservationStatus, StatusColor> = {
  Booked: 'primary',
  Completed: 'success',
  Cancelled: 'default',
}

interface Filters {
  roomId: string
  userId: string
  status: '' | ReservationStatus
  from: string
  to: string
}

const NO_FILTERS: Filters = { roomId: '', userId: '', status: '', from: '', to: '' }

/** The drop-down only offers known values; anything else (including "All") means no status filter. */
function toStatusFilter(value: string): '' | ReservationStatus {
  return STATUSES.find((name) => name === value) ?? ''
}

/** What the open dialog is for: a form (create when `reservation` is null) or a confirm step for one row. */
type DialogState =
  | { kind: 'form'; reservation: ReservationDto | null }
  | { kind: 'cancel'; reservation: ReservationDto }
  | { kind: 'end'; reservation: ReservationDto }
  | null

/** The browser clock only picks which buttons to show; the API decides whether the action is allowed. */
function hasStarted(reservation: ReservationDto): boolean {
  return new Date(reservation.start).getTime() <= Date.now()
}

function describe(reservation: ReservationDto): string {
  return `"${reservation.title}" in ${reservation.roomName}, starting ${formatDateTime(reservation.start)}`
}

export function ReservationsPage() {
  const [filters, setFilters] = useState<Filters>(NO_FILTERS)
  const [reservations, setReservations] = useState<ReservationDto[]>([])
  const [paging, setPaging] = useState<{ page: number; pageSize?: number }>({ page: 1 })
  const [pageInfo, setPageInfo] = useState<PageInfo | null>(null)
  const [loading, setLoading] = useState(true)
  const [errors, setErrors] = useState<ApiErrorItem[]>([])
  // Changing this value re-runs the list effect (Retry, and refresh after a write or a stale answer).
  const [reloadCount, setReloadCount] = useState(0)

  const [rooms, setRooms] = useState<RoomDto[]>([])
  const [users, setUsers] = useState<UserDto[]>([])
  const [lookupsLoading, setLookupsLoading] = useState(true)
  const [lookupErrors, setLookupErrors] = useState<ApiErrorItem[]>([])
  const [lookupReloadCount, setLookupReloadCount] = useState(0)
  const [retryRequested, setRetryRequested] = useState(false)

  const [dialog, setDialog] = useState<DialogState>(null)
  const [notice, setNotice] = useState<string | null>(null)

  // Lookups for the drop-downs: loaded once per visit (and again on Retry).
  useEffect(() => {
    const controller = new AbortController()
    setLookupsLoading(true)
    setLookupErrors([])
    // The drop-downs need every room and user, so all pages are read.
    Promise.all([
      fetchAllPages((params, signal) => listRooms(params, signal), controller.signal),
      fetchAllPages((params, signal) => listUsers(params, signal), controller.signal),
    ])
      .then(([roomResult, userResult]) => {
        if (controller.signal.aborted) {
          return
        }
        setRooms(roomResult)
        setUsers(userResult)
        setLookupsLoading(false)
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) {
          return
        }
        setRooms([])
        setUsers([])
        setLookupErrors(toErrorItems(error))
        setLookupsLoading(false)
      })
    return () => controller.abort()
  }, [lookupReloadCount])

  const { roomId, userId, status, from, to } = filters

  useEffect(() => {
    // Aborting on unmount, retry or a new filter means a late response never updates the page.
    const controller = new AbortController()
    setLoading(true)
    setErrors([])
    listReservations(
      {
        roomId: roomId === '' ? undefined : roomId,
        userId: userId === '' ? undefined : userId,
        status: status === '' ? undefined : status,
        from: toIsoWithOffset(from),
        to: toIsoWithOffset(to),
        page: paging.page,
        pageSize: paging.pageSize,
      },
      controller.signal,
    )
      .then((result) => {
        if (controller.signal.aborted) {
          return
        }
        // After a write or a filter change the list can be shorter than the page asked for: go to the last page.
        if (result.items.length === 0 && result.totalPages > 0 && result.page > result.totalPages) {
          setPaging((current) => ({ ...current, page: result.totalPages }))
          return
        }
        setReservations(result.items)
        setPageInfo({ pageSize: result.pageSize, totalCount: result.totalCount })
        setLoading(false)
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) {
          return
        }
        setReservations([])
        setPageInfo(null)
        setErrors(toErrorItems(error))
        setLoading(false)
      })
    return () => controller.abort()
  }, [roomId, userId, status, from, to, paging, reloadCount])

  function reload() {
    setReloadCount((count) => count + 1)
  }

  function changeFilter(change: Partial<Filters>) {
    setFilters((current) => ({ ...current, ...change }))
    // A new filter always starts on the first page.
    setPaging((current) => (current.page === 1 ? current : { ...current, page: 1 }))
  }

  function finishWrite(message: string) {
    setDialog(null)
    setNotice(message)
    reload()
  }

  const retrying = loading || lookupsLoading
  // After a click the button stays mounted until the retry finishes, so the keyboard focus is not lost.
  // Only a click on Retry sets the flag: a reload after a write or a filter change must not show "Retrying…".
  const showRetry = errors.length > 0 || lookupErrors.length > 0 || (retryRequested && retrying)
  const lookupsReady = !lookupsLoading && lookupErrors.length === 0
  // When the backend is down both loads fail with the same error: show it once.
  const listErrors = errors.filter(
    (item) => !lookupErrors.some((known) => known.code === item.code && known.message === item.message),
  )

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
    if (lookupErrors.length > 0) {
      setLookupReloadCount((count) => count + 1)
    }
    reload()
  }

  return (
    <Stack spacing={2}>
      <Typography variant="h4" component="h1">
        Reservations
      </Typography>

      <Stack direction="row" spacing={2} useFlexGap sx={{ flexWrap: 'wrap', alignItems: 'flex-start' }}>
        <TextField
          select
          size="small"
          label="Room"
          value={filters.roomId}
          onChange={(event) => changeFilter({ roomId: event.target.value })}
          sx={{ minWidth: 180 }}
          slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
        >
          <MenuItem value="">All rooms</MenuItem>
          {rooms.map((room) => (
            <MenuItem key={room.id} value={room.id}>
              {room.name}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          select
          size="small"
          label="Organizer"
          value={filters.userId}
          onChange={(event) => changeFilter({ userId: event.target.value })}
          sx={{ minWidth: 180 }}
          slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
        >
          <MenuItem value="">All users</MenuItem>
          {users.map((user) => (
            <MenuItem key={user.id} value={user.id}>
              {user.firstName} {user.lastName}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          select
          size="small"
          label="Status"
          value={filters.status}
          onChange={(event) => changeFilter({ status: toStatusFilter(event.target.value) })}
          sx={{ minWidth: 160 }}
          slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
        >
          <MenuItem value="">All</MenuItem>
          {STATUSES.map((name) => (
            <MenuItem key={name} value={name}>
              {name}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          size="small"
          label="From"
          type="datetime-local"
          value={filters.from}
          onChange={(event) => changeFilter({ from: event.target.value })}
          slotProps={{ inputLabel: { shrink: true } }}
        />
        <TextField
          size="small"
          label="To"
          type="datetime-local"
          value={filters.to}
          onChange={(event) => changeFilter({ to: event.target.value })}
          slotProps={{ inputLabel: { shrink: true } }}
        />
        <Button variant="contained" disabled={!lookupsReady} onClick={() => setDialog({ kind: 'form', reservation: null })}>
          New reservation
        </Button>
      </Stack>

      <ErrorAlert errors={lookupErrors} />
      <ErrorAlert errors={listErrors} />
      {showRetry && (
        <div>
          {/* aria-disabled (not disabled) keeps keyboard focus on the button while the retry runs. */}
          <Button variant="outlined" aria-disabled={retrying} onClick={retry}>
            {retrying ? 'Retrying…' : 'Retry'}
          </Button>
        </div>
      )}

      <TableContainer component={Paper}>
        <Table size="small" aria-label="Reservations">
          <TableHead>
            <TableRow>
              <TableCell>Title</TableCell>
              <TableCell>Room</TableCell>
              <TableCell>Organizer</TableCell>
              <TableCell>Start</TableCell>
              <TableCell>End</TableCell>
              <TableCell align="right">Attendees</TableCell>
              <TableCell>Status</TableCell>
              <TableCell>Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow>
                <TableCell colSpan={8} align="center">
                  <LoadingIndicator label="Loading reservations" />
                </TableCell>
              </TableRow>
            ) : reservations.length === 0 ? (
              <TableRow>
                <TableCell colSpan={8} align="center">
                  {errors.length > 0 ? 'No reservations to show.' : 'No reservations match.'}
                </TableCell>
              </TableRow>
            ) : (
              reservations.map((reservation) => {
                const booked = reservation.status === 'Booked'
                const started = hasStarted(reservation)
                return (
                  <TableRow key={reservation.id} hover>
                    <TableCell>{reservation.title}</TableCell>
                    <TableCell>{reservation.roomName}</TableCell>
                    <TableCell>{reservation.organizerName}</TableCell>
                    <TableCell>{formatDateTime(reservation.start)}</TableCell>
                    <TableCell>
                      {formatDateTime(reservation.end)}
                      {reservation.endedAt !== null && (
                        <Typography variant="caption" component="div" color="text.secondary">
                          Ended early at {formatDateTime(reservation.endedAt)}
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell align="right">{reservation.attendeeCount}</TableCell>
                    <TableCell>
                      <Chip size="small" label={reservation.status} color={STATUS_COLORS[reservation.status]} />
                    </TableCell>
                    <TableCell>
                      {booked && !started && (
                        <Stack direction="row" spacing={1}>
                          <Button
                            size="small"
                            disabled={!lookupsReady}
                            aria-label={`Edit ${reservation.title}`}
                            onClick={() => setDialog({ kind: 'form', reservation })}
                          >
                            Edit
                          </Button>
                          <Button
                            size="small"
                            color="error"
                            aria-label={`Cancel ${reservation.title}`}
                            onClick={() => setDialog({ kind: 'cancel', reservation })}
                          >
                            Cancel
                          </Button>
                        </Stack>
                      )}
                      {booked && started && (
                        <Button
                          size="small"
                          color="warning"
                          aria-label={`End now: ${reservation.title}`}
                          onClick={() => setDialog({ kind: 'end', reservation })}
                        >
                          End now
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                )
              })
            )}
          </TableBody>
        </Table>
        <ListPager
          label="Reservations"
          page={paging.page}
          info={pageInfo}
          onChange={(page, pageSize) => setPaging((current) => ({ page, pageSize: pageSize ?? current.pageSize }))}
        />
      </TableContainer>

      {dialog?.kind === 'form' && (
        <ReservationFormDialog
          reservation={dialog.reservation}
          rooms={rooms}
          users={users}
          onClose={() => setDialog(null)}
          onSaved={finishWrite}
          onStale={reload}
        />
      )}
      {dialog?.kind === 'cancel' && (
        <ConfirmActionDialog
          title="Cancel reservation"
          description={`Cancel ${describe(dialog.reservation)}?`}
          confirmLabel="Cancel reservation"
          run={() => cancelReservation(dialog.reservation.id)}
          onClose={() => setDialog(null)}
          onDone={() => finishWrite('Reservation cancelled.')}
          onStale={reload}
        />
      )}
      {dialog?.kind === 'end' && (
        <ConfirmActionDialog
          title="End reservation now"
          description={`End ${describe(dialog.reservation)} now? The room becomes free from this moment.`}
          confirmLabel="End now"
          run={() => endReservation(dialog.reservation.id)}
          onClose={() => setDialog(null)}
          onDone={() => finishWrite('Reservation ended.')}
          onStale={reload}
        />
      )}

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
