import { describe, expect, it, vi } from 'vitest'
import { LOOKUP_PAGE_SIZE, fetchAllPages, setPageParams } from './paging'
import type { PageParams, PagedResult } from './types'

function page(items: number[], totalPages: number, pageNumber = 1): PagedResult<number> {
  return { items, page: pageNumber, pageSize: 100, totalCount: items.length, totalPages }
}

describe('LOOKUP_PAGE_SIZE', () => {
  it('is the largest page the API allows (Paging:MaxPageSize is 100)', () => {
    expect(LOOKUP_PAGE_SIZE).toBe(100)
  })
})

describe('fetchAllPages', () => {
  it('reads every page in order and joins the items', async () => {
    const pages = [page([1, 2], 3), page([3, 4], 3, 2), page([5], 3, 3)]
    const fetchPage = vi.fn((params: PageParams) => Promise.resolve(pages[(params.page ?? 1) - 1]!))

    const items = await fetchAllPages(fetchPage)

    expect(items).toEqual([1, 2, 3, 4, 5])
    expect(fetchPage.mock.calls.map(([params]) => params)).toEqual([
      { page: 1, pageSize: 100 },
      { page: 2, pageSize: 100 },
      { page: 3, pageSize: 100 },
    ])
  })

  it('makes one call for a single page', async () => {
    const fetchPage = vi.fn(() => Promise.resolve(page([1], 1)))

    expect(await fetchAllPages(fetchPage)).toEqual([1])
    expect(fetchPage).toHaveBeenCalledTimes(1)
  })

  it('returns an empty list when there is no row', async () => {
    const fetchPage = vi.fn(() => Promise.resolve(page([], 0)))

    expect(await fetchAllPages(fetchPage)).toEqual([])
    expect(fetchPage).toHaveBeenCalledTimes(1)
  })

  it('stops at an empty page even when totalPages promises more', async () => {
    const pages = [page([1], 5), page([], 5, 2)]
    const fetchPage = vi.fn((params: PageParams) => Promise.resolve(pages[(params.page ?? 1) - 1]!))

    expect(await fetchAllPages(fetchPage)).toEqual([1])
    expect(fetchPage).toHaveBeenCalledTimes(2)
  })

  it.each([-1, 1.5, Number.NaN])('throws for an invalid totalPages (%s) instead of returning a partial list', async (bad) => {
    const fetchPage = vi.fn(() => Promise.resolve(page([1], bad)))

    await expect(fetchAllPages(fetchPage)).rejects.toThrow('invalid page count')
  })

  it('throws when the list is longer than the page cap', async () => {
    const fetchPage = vi.fn((params: PageParams) => Promise.resolve(page([params.page ?? 0], 5000, params.page)))

    await expect(fetchAllPages(fetchPage)).rejects.toThrow('more than 1000 pages')
    expect(fetchPage).toHaveBeenCalledTimes(1000)
  })

  it('passes the signal to every request and stops at the first error', async () => {
    const controller = new AbortController()
    const fetchPage = vi
      .fn<(params: PageParams, signal?: AbortSignal) => Promise<PagedResult<number>>>()
      .mockResolvedValueOnce(page([1], 3))
      .mockRejectedValueOnce(new Error('network'))

    await expect(fetchAllPages(fetchPage, controller.signal)).rejects.toThrow('network')

    expect(fetchPage).toHaveBeenCalledTimes(2)
    expect(fetchPage.mock.calls.every(([, signal]) => signal === controller.signal)).toBe(true)
  })
})

describe('setPageParams', () => {
  it('adds both values when they are set', () => {
    const query = new URLSearchParams()

    setPageParams(query, { page: 3, pageSize: 50 })

    expect(query.toString()).toBe('page=3&pageSize=50')
  })

  it('adds nothing for values that are not set', () => {
    const query = new URLSearchParams({ floor: '2' })

    setPageParams(query, {})

    expect(query.toString()).toBe('floor=2')
  })

  it('keeps a page size on its own', () => {
    const query = new URLSearchParams()

    setPageParams(query, { pageSize: 10 })

    expect(query.toString()).toBe('pageSize=10')
  })
})
