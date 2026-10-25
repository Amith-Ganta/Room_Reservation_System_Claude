import { describe, expect, it } from 'vitest'
import { ApiError } from '../api/client'
import { isStaleError, toErrorItems } from './errors'

const items = [{ code: 'R2_ROOM_OVERLAP', message: 'The room is taken.' }]

describe('toErrorItems', () => {
  it('returns the list of an ApiError as it is', () => {
    expect(toErrorItems(new ApiError(422, items))).toEqual(items)
  })

  it.each([new Error('boom'), 'text', null, undefined])('gives one generic item for %s', (thrown) => {
    expect(toErrorItems(thrown)).toEqual([
      { code: 'UNEXPECTED_ERROR', message: 'Something unexpected happened. Please try again.' },
    ])
  })
})

describe('isStaleError', () => {
  it.each([404, 409])('is true for an ApiError with status %i', (status) => {
    expect(isStaleError(new ApiError(status, items))).toBe(true)
  })

  it.each([0, 400, 422, 500])('is false for an ApiError with status %i', (status) => {
    expect(isStaleError(new ApiError(status, items))).toBe(false)
  })

  it('is false for anything that is not an ApiError', () => {
    expect(isStaleError(new Error('404'))).toBe(false)
    expect(isStaleError({ status: 404 })).toBe(false)
  })
})
