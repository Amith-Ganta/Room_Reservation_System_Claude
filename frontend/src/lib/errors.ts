import { ApiError } from '../api/client'
import type { ApiErrorItem } from '../api/types'

/** The errors to show for anything a call threw: the API's own list, or one generic item for a surprise. */
export function toErrorItems(error: unknown): ApiErrorItem[] {
  return error instanceof ApiError
    ? error.errors
    : [{ code: 'UNEXPECTED_ERROR', message: 'Something unexpected happened. Please try again.' }]
}

/** 404 and 409 mean the row on screen no longer matches the server, so the list should be requested again. */
export function isStaleError(error: unknown): boolean {
  return error instanceof ApiError && (error.status === 404 || error.status === 409)
}
