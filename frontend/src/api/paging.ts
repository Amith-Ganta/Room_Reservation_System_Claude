import type { PageParams, PagedResult } from './types'

/**
 * Asked for on every lookup request. It must not exceed the API's `Paging:MaxPageSize` (100 by default): the API
 * lowers a larger value to its maximum, so a smaller server maximum only means more pages, never missing rows.
 */
export const LOOKUP_PAGE_SIZE = 100

/** Stops a runaway loop if the API ever reports an absurd `totalPages`. */
const MAX_PAGES = 1000

/**
 * Reads every page of a list. Used where a screen needs the complete set (drop-downs, the calendar), so the API's
 * page size limit never hides rows. It passes the signal to every request and stops at the first error.
 *
 * It never returns a partial list: a malformed `totalPages`, or a list longer than `MAX_PAGES` pages, throws so the
 * screen shows an error instead of silently missing rows. An empty page ends the loop early.
 */
export async function fetchAllPages<T>(
  fetchPage: (params: PageParams, signal?: AbortSignal) => Promise<PagedResult<T>>,
  signal?: AbortSignal,
): Promise<T[]> {
  const items: T[] = []
  let page = 1
  let totalPages = 1
  while (page <= totalPages) {
    if (page > MAX_PAGES) {
      throw new Error(`The list has more than ${MAX_PAGES} pages, so it cannot be loaded in full.`)
    }
    const result = await fetchPage({ page, pageSize: LOOKUP_PAGE_SIZE }, signal)
    if (!Number.isInteger(result.totalPages) || result.totalPages < 0) {
      throw new Error('The server sent an invalid page count.')
    }
    items.push(...result.items)
    if (result.items.length === 0) {
      break
    }
    totalPages = result.totalPages
    page += 1
  }
  return items
}

/** Adds `page` and `pageSize` to a query string when they are set. */
export function setPageParams(query: URLSearchParams, params: PageParams): void {
  if (params.page !== undefined) {
    query.set('page', String(params.page))
  }
  if (params.pageSize !== undefined) {
    query.set('pageSize', String(params.pageSize))
  }
}
