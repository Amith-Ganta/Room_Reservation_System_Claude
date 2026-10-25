import { describe, expect, it } from 'vitest'
import { formatDateTime, toIsoWithOffset, toLocalInputValue } from './dateTime'

describe('toIsoWithOffset', () => {
  it('returns undefined for an empty value', () => {
    expect(toIsoWithOffset('')).toBeUndefined()
  })

  it('returns undefined for text that is not a date', () => {
    expect(toIsoWithOffset('not a date')).toBeUndefined()
  })

  it('reads the input as local time and returns an ISO timestamp for the same moment', () => {
    const local = new Date(2030, 5, 14, 9, 30)

    const iso = toIsoWithOffset('2030-06-14T09:30')

    expect(iso).toBe(local.toISOString())
    expect(iso).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$/)
  })
})

describe('toLocalInputValue', () => {
  it('returns an empty text for an invalid timestamp', () => {
    expect(toLocalInputValue('nonsense')).toBe('')
  })

  it('writes the local date and time as yyyy-MM-ddTHH:mm with zero padding', () => {
    const iso = new Date(2030, 0, 5, 7, 4).toISOString()

    expect(toLocalInputValue(iso)).toBe('2030-01-05T07:04')
  })

  it('round-trips with toIsoWithOffset', () => {
    const iso = new Date(2031, 11, 31, 23, 59).toISOString()

    expect(toIsoWithOffset(toLocalInputValue(iso))).toBe(iso)
  })
})

describe('formatDateTime', () => {
  it('returns the original text when the timestamp is invalid', () => {
    expect(formatDateTime('nonsense')).toBe('nonsense')
  })

  it('uses the local format of the machine for a valid timestamp', () => {
    const date = new Date(2030, 5, 14, 9, 30)

    const text = formatDateTime(date.toISOString())

    expect(text).toBe(date.toLocaleString())
    expect(text).toContain('2030')
  })
})
