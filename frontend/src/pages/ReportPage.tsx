import {
  Alert,
  Box,
  Button,
  Chip,
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
  alpha,
  useTheme,
} from '@mui/material'
import { useEffect, useState } from 'react'
import { ApiError } from '../api/client'
import { getUsageReport } from '../api/reports'
import type { ApiErrorItem, UsageReportDto } from '../api/types'
import { ErrorAlert } from '../components/ErrorAlert'
import { LoadingIndicator } from '../components/LoadingIndicator'
import { toIsoWithOffset } from '../lib/dateTime'
import { exportReportPdf } from '../lib/exportReportPdf'
import {
  FLAG_LABEL,
  formatHours,
  formatUtilization,
  generatedText,
  isNumericColumn,
  periodText,
  REPORT_COLUMNS,
  thresholdsText,
} from '../lib/reportFormat'

/** Local calendar day from a date input (yyyy-MM-dd) to an ISO timestamp with an offset. Conversion only. */
function dayBoundary(day: string, endOfDay: boolean): string | undefined {
  if (day === '') {
    return undefined
  }
  return toIsoWithOffset(`${day}T${endOfDay ? '23:59:59.999' : '00:00:00.000'}`)
}

/** A filled date input that cannot be turned into a timestamp (for example a six-digit year). */
function isInvalidDay(day: string): boolean {
  return day !== '' && dayBoundary(day, false) === undefined
}

/** Keeps the date pickers to four-digit years. */
const DAY_INPUT_RANGE = { min: '1900-01-01', max: '2999-12-31' }
const INVALID_DAY_TEXT = 'Enter a valid date.'

export function ReportPage() {
  const theme = useTheme()
  const [fromDay, setFromDay] = useState('')
  const [toDay, setToDay] = useState('')
  const [report, setReport] = useState<UsageReportDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [errors, setErrors] = useState<ApiErrorItem[]>([])
  const [exporting, setExporting] = useState(false)
  const [exportFailed, setExportFailed] = useState(false)
  // Changing this value re-runs the load effect (Retry button).
  const [reloadCount, setReloadCount] = useState(0)
  // True from a Retry click until that load ends, so the button shows "Retrying…" only for a retry.
  const [retryRequested, setRetryRequested] = useState(false)

  useEffect(() => {
    // Aborting on a new range, retry or unmount means a late response never replaces a newer one.
    const controller = new AbortController()
    setLoading(true)
    setErrors([])
    getUsageReport({ from: dayBoundary(fromDay, false), to: dayBoundary(toDay, true) }, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) {
          return
        }
        setReport(result)
        setLoading(false)
        setRetryRequested(false)
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) {
          return
        }
        // Never keep a report that does not match the filter.
        setReport(null)
        setErrors(
          error instanceof ApiError
            ? error.errors
            : [{ code: 'UNEXPECTED_ERROR', message: 'Something unexpected happened. Please try again.' }],
        )
        setLoading(false)
        setRetryRequested(false)
      })
    return () => controller.abort()
  }, [fromDay, toDay, reloadCount])

  // After a click the button stays mounted until the retry finishes, so the keyboard focus is not lost.
  const showRetry = errors.length > 0 || (loading && retryRequested)
  const hasRows = !loading && report !== null && report.rooms.length > 0

  async function handleExport() {
    if (!hasRows || exporting || report === null) {
      return
    }
    setExporting(true)
    try {
      await exportReportPdf(report)
    } catch {
      setExportFailed(true)
    } finally {
      setExporting(false)
    }
  }

  return (
    <Stack spacing={2}>
      <Typography variant="h4" component="h1">
        Usage report
      </Typography>
      <Typography color="text.secondary">Rooms booked often but used rarely.</Typography>

      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ alignItems: { sm: 'center' } }}>
        <TextField
          label="From"
          type="date"
          size="small"
          value={fromDay}
          onChange={(event) => setFromDay(event.target.value)}
          error={isInvalidDay(fromDay)}
          helperText={isInvalidDay(fromDay) ? INVALID_DAY_TEXT : undefined}
          slotProps={{ inputLabel: { shrink: true }, htmlInput: DAY_INPUT_RANGE }}
        />
        <TextField
          label="To"
          type="date"
          size="small"
          value={toDay}
          onChange={(event) => setToDay(event.target.value)}
          error={isInvalidDay(toDay)}
          helperText={isInvalidDay(toDay) ? INVALID_DAY_TEXT : undefined}
          slotProps={{ inputLabel: { shrink: true }, htmlInput: DAY_INPUT_RANGE }}
        />
        <Button
          variant="text"
          onClick={() => {
            setFromDay('')
            setToDay('')
          }}
          disabled={fromDay === '' && toDay === ''}
        >
          Clear
        </Button>
        <Box sx={{ flexGrow: 1 }} />
        {/* Truly disabled when there is nothing to export; aria-disabled while preparing keeps keyboard focus. */}
        <Button
          variant="contained"
          disabled={!hasRows}
          aria-disabled={exporting}
          onClick={() => void handleExport()}
        >
          {exporting ? 'Preparing…' : 'Export PDF'}
        </Button>
      </Stack>

      <ErrorAlert errors={errors} />
      {showRetry && (
        <div>
          {/* aria-disabled (not disabled) keeps keyboard focus on the button while the retry runs. */}
          <Button
            variant="outlined"
            aria-disabled={loading}
            onClick={() => {
              if (!loading) {
                setRetryRequested(true)
                setReloadCount((count) => count + 1)
              }
            }}
          >
            {loading ? 'Retrying…' : 'Retry'}
          </Button>
        </div>
      )}

      {report !== null && !loading && (
        <Box>
          <Typography variant="body2">{thresholdsText(report)}</Typography>
          <Typography variant="body2" color="text.secondary">
            {generatedText(report)}. Period: {periodText(report)}.
          </Typography>
        </Box>
      )}

      <TableContainer component={Paper}>
        <Table size="small" aria-label="Room usage">
          <TableHead>
            <TableRow>
              {REPORT_COLUMNS.map((column, index) => (
                <TableCell key={column} align={isNumericColumn(index) ? 'right' : 'left'}>
                  {column}
                </TableCell>
              ))}
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow>
                <TableCell colSpan={REPORT_COLUMNS.length} align="center">
                  <LoadingIndicator label="Loading report" />
                </TableCell>
              </TableRow>
            ) : report === null ? (
              <TableRow>
                <TableCell colSpan={REPORT_COLUMNS.length} align="center">
                  No report to show.
                </TableCell>
              </TableRow>
            ) : report.rooms.length === 0 ? (
              <TableRow>
                <TableCell colSpan={REPORT_COLUMNS.length} align="center">
                  No rooms yet.
                </TableCell>
              </TableRow>
            ) : (
              report.rooms.map((room) => (
                <TableRow
                  key={room.roomId}
                  hover
                  sx={
                    room.isOftenBookedRarelyUsed
                      ? { bgcolor: alpha(theme.palette.warning.main, 0.12) }
                      : undefined
                  }
                >
                  <TableCell>{room.roomName}</TableCell>
                  <TableCell align="right">{room.floor}</TableCell>
                  <TableCell align="right">{room.totalBookings}</TableCell>
                  <TableCell align="right">{room.completedBookings}</TableCell>
                  <TableCell align="right">{room.cancelledBookings}</TableCell>
                  <TableCell align="right">{room.upcomingBookings}</TableCell>
                  <TableCell align="right">{formatHours(room.plannedHours)}</TableCell>
                  <TableCell align="right">{formatHours(room.usedHours)}</TableCell>
                  <TableCell align="right">{formatUtilization(room.utilizationPercent)}</TableCell>
                  <TableCell>
                    {room.isOftenBookedRarelyUsed && <Chip label={FLAG_LABEL} color="warning" size="small" />}
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <Snackbar
        open={exportFailed}
        autoHideDuration={6000}
        onClose={(_event, reason) => {
          if (reason !== 'clickaway') {
            setExportFailed(false)
          }
        }}
      >
        <Alert severity="error" onClose={() => setExportFailed(false)}>
          The PDF could not be created.
        </Alert>
      </Snackbar>
    </Stack>
  )
}
