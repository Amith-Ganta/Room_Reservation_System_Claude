import { request } from './client'
import type { UsageReportDto } from './types'

export interface UsageReportRange {
  /** ISO 8601 timestamp with an offset; inclusive, applies to the reservation start. */
  from?: string
  /** ISO 8601 timestamp with an offset; inclusive, applies to the reservation start. */
  to?: string
}

/** The API calculates the whole report; empty bounds are simply left out of the query string. */
export function getUsageReport(range: UsageReportRange, signal?: AbortSignal): Promise<UsageReportDto> {
  const query = new URLSearchParams()
  if (range.from !== undefined) {
    query.set('from', range.from)
  }
  if (range.to !== undefined) {
    query.set('to', range.to)
  }
  const text = query.toString()
  return request<UsageReportDto>(text.length > 0 ? `/api/reports/usage?${text}` : '/api/reports/usage', {
    signal,
    expect: 'object',
  })
}
