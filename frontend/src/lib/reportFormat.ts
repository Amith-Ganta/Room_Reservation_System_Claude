import type { RoomUsageDto, UsageReportDto } from '../api/types'
import { formatDateTime } from './dateTime'

/** Display text for the usage report, shared by the page and the PDF so both show the same words. */
/** Nothing here decides a flag or a number: the values were calculated by the API. */

export const FLAG_LABEL = 'Often booked, rarely used'

export const REPORT_COLUMNS = [
  'Room',
  'Floor',
  'Total',
  'Completed',
  'Cancelled',
  'Upcoming',
  'Planned hours',
  'Used hours',
  'Utilization',
  'Flag',
] as const

export function formatHours(hours: number): string {
  return hours.toFixed(2)
}

/** A null utilization means the room had no planned hours in the period. */
export function formatUtilization(percent: number | null): string {
  return percent === null ? 'n/a' : `${percent.toFixed(1)}%`
}

export function thresholdsText(report: UsageReportDto): string {
  return (
    `A room is flagged when it has at least ${report.minBookingsToFlag} completed or cancelled bookings ` +
    `and used less than ${report.underusedThresholdPercent}% of its planned hours.`
  )
}

export function generatedText(report: UsageReportDto): string {
  return `Generated ${formatDateTime(report.generatedAt)}`
}

function formatDay(iso: string): string {
  const date = new Date(iso)
  return Number.isNaN(date.getTime()) ? iso : date.toLocaleDateString()
}

/** Describes the period the API echoed back. */
export function periodText(report: UsageReportDto): string {
  if (report.from !== null && report.to !== null) {
    return `${formatDay(report.from)} to ${formatDay(report.to)}`
  }
  if (report.from !== null) {
    return `From ${formatDay(report.from)}`
  }
  if (report.to !== null) {
    return `To ${formatDay(report.to)}`
  }
  return 'All dates'
}

/** One table row as display text, in the same column order as REPORT_COLUMNS. */
export function roomRowText(room: RoomUsageDto): string[] {
  return [
    room.roomName,
    String(room.floor),
    String(room.totalBookings),
    String(room.completedBookings),
    String(room.cancelledBookings),
    String(room.upcomingBookings),
    formatHours(room.plannedHours),
    formatHours(room.usedHours),
    formatUtilization(room.utilizationPercent),
    room.isOftenBookedRarelyUsed ? FLAG_LABEL : '',
  ]
}

/** File name date: the local day of `generatedAt`, so the export needs no clock of its own. */
export function reportFileDate(report: UsageReportDto): string {
  const date = new Date(report.generatedAt)
  if (Number.isNaN(date.getTime())) {
    return 'report'
  }
  const pad = (value: number) => String(value).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}
