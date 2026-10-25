import { describe, expect, it } from 'vitest'
import type { RoomUsageDto, UsageReportDto } from '../api/types'
import {
  FLAG_LABEL,
  REPORT_COLUMNS,
  formatHours,
  formatUtilization,
  generatedText,
  isNumericColumn,
  periodText,
  reportFileName,
  roomRowText,
  thresholdsText,
} from './reportFormat'

function report(overrides: Partial<UsageReportDto> = {}): UsageReportDto {
  return {
    generatedAt: new Date(2030, 5, 14, 9, 30).toISOString(),
    from: null,
    to: null,
    minBookingsToFlag: 5,
    underusedThresholdPercent: 30,
    rooms: [],
    ...overrides,
  }
}

const room: RoomUsageDto = {
  roomId: 'r1',
  roomName: 'Aurora',
  floor: 2,
  totalBookings: 9,
  completedBookings: 6,
  cancelledBookings: 1,
  upcomingBookings: 2,
  plannedHours: 12,
  usedHours: 3.456,
  utilizationPercent: 28.75,
  isOftenBookedRarelyUsed: true,
}

describe('columns', () => {
  it('has ten columns and right-aligns Floor to Utilization', () => {
    expect(REPORT_COLUMNS).toHaveLength(10)
    expect(REPORT_COLUMNS.map((_, index) => isNumericColumn(index))).toEqual([
      false, true, true, true, true, true, true, true, true, false,
    ])
  })
})

describe('number formatting', () => {
  it('shows hours with two decimals', () => {
    expect(formatHours(0)).toBe('0.00')
    expect(formatHours(3.456)).toBe('3.46')
  })

  it('shows utilization with one decimal and a percent sign', () => {
    expect(formatUtilization(28.75)).toBe('28.8%')
    expect(formatUtilization(0)).toBe('0.0%')
  })

  it('shows n/a when there are no planned hours', () => {
    expect(formatUtilization(null)).toBe('n/a')
  })
})

describe('texts', () => {
  it('writes the thresholds the API sent, without changing them', () => {
    expect(thresholdsText(report({ minBookingsToFlag: 7, underusedThresholdPercent: 25 }))).toBe(
      'A room is flagged when it has at least 7 completed or cancelled bookings and used less than 25% of its planned hours.',
    )
  })

  it('writes the generated time in the local format', () => {
    const data = report()

    expect(generatedText(data)).toBe(`Generated ${new Date(data.generatedAt).toLocaleString()}`)
    expect(generatedText(data)).toContain('2030')
  })
})

describe('periodText', () => {
  const from = new Date(2030, 0, 1).toISOString()
  const to = new Date(2030, 0, 31).toISOString()
  const day = (iso: string) => new Date(iso).toLocaleDateString()

  it('says all dates when no bound was sent', () => {
    expect(periodText(report())).toBe('All dates')
  })

  it('describes a start bound only', () => {
    expect(periodText(report({ from }))).toBe(`From ${day(from)}`)
  })

  it('describes an end bound only', () => {
    expect(periodText(report({ to }))).toBe(`To ${day(to)}`)
  })

  it('describes both bounds', () => {
    expect(periodText(report({ from, to }))).toBe(`${day(from)} to ${day(to)}`)
    expect(periodText(report({ from, to }))).toContain('2030')
  })

  it('keeps the original text of a bound that is not a date', () => {
    expect(periodText(report({ from: 'soon' }))).toBe('From soon')
  })
})

describe('roomRowText', () => {
  it('follows the column order and formats the numbers', () => {
    expect(roomRowText(room)).toEqual([
      'Aurora', '2', '9', '6', '1', '2', '12.00', '3.46', '28.8%', FLAG_LABEL,
    ])
    expect(roomRowText(room)).toHaveLength(REPORT_COLUMNS.length)
  })

  it('leaves the flag empty and shows n/a for an unflagged room without planned hours', () => {
    const row = roomRowText({ ...room, utilizationPercent: null, isOftenBookedRarelyUsed: false })

    expect(row[8]).toBe('n/a')
    expect(row[9]).toBe('')
  })
})

describe('reportFileName', () => {
  it('uses the local day of the generated time', () => {
    expect(reportFileName(report({ generatedAt: new Date(2030, 0, 5, 23, 59).toISOString() }))).toBe(
      'room-usage-report-2030-01-05.pdf',
    )
  })

  it('has no date part when the generated time is not valid', () => {
    expect(reportFileName(report({ generatedAt: 'nonsense' }))).toBe('room-usage-report.pdf')
  })
})
