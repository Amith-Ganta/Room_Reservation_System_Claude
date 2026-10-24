import { Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Typography } from '@mui/material'
import type { ReservationDto, ReservationStatus } from '../api/types'
import { formatDateTime } from '../lib/dateTime'

type StatusColor = 'primary' | 'success' | 'default'

const STATUS_COLORS: Record<ReservationStatus, StatusColor> = {
  Booked: 'primary',
  Completed: 'success',
  Cancelled: 'default',
}

interface ReservationDetailsDialogProps {
  reservation: ReservationDto
  onClose: () => void
}

interface DetailRowProps {
  label: string
  children: string
}

function DetailRow({ label, children }: DetailRowProps) {
  return (
    <div>
      <Typography variant="caption" color="text.secondary" component="div">
        {label}
      </Typography>
      <Typography>{children}</Typography>
    </div>
  )
}

/** Read-only view of one reservation: formatting only. Changing or cancelling stays on the Reservations page. */
export function ReservationDetailsDialog({ reservation, onClose }: ReservationDetailsDialogProps) {
  return (
    <Dialog open onClose={onClose} aria-labelledby="reservation-details-title" fullWidth maxWidth="xs">
      <DialogTitle id="reservation-details-title">{reservation.title === '' ? 'Reservation' : reservation.title}</DialogTitle>
      <DialogContent>
        <Stack spacing={1.5} sx={{ pt: 1 }}>
          <DetailRow label="Room">{reservation.roomName}</DetailRow>
          <DetailRow label="Organizer">{reservation.organizerName}</DetailRow>
          <DetailRow label="Start">{formatDateTime(reservation.start)}</DetailRow>
          <DetailRow label="End">{formatDateTime(reservation.end)}</DetailRow>
          {reservation.endedAt !== null && (
            <DetailRow label="Ended early">{`Ended early at ${formatDateTime(reservation.endedAt)}`}</DetailRow>
          )}
          <DetailRow label="Attendees">{String(reservation.attendeeCount)}</DetailRow>
          <div>
            <Typography variant="caption" color="text.secondary" component="div">
              Status
            </Typography>
            <Chip size="small" label={reservation.status} color={STATUS_COLORS[reservation.status]} />
          </div>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </Dialog>
  )
}
