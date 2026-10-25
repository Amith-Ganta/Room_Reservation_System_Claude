import { describe, expect, it, vi } from 'vitest'
import { ApiError, request } from './client'

function stubFetch(response: Response | Error | DOMException) {
  // A jsdom DOMException is not an instance of the Node Error class, so test for Response instead.
  const fetchMock = vi.fn(() =>
    response instanceof Response ? Promise.resolve(response) : Promise.reject(response),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
}

async function failure(promise: Promise<unknown>): Promise<ApiError> {
  try {
    await promise
  } catch (error) {
    expect(error).toBeInstanceOf(ApiError)
    return error as ApiError
  }
  throw new Error('The request should have failed.')
}

describe('request', () => {
  it('returns the parsed body and sends an Accept header', async () => {
    const fetchMock = stubFetch(json({ id: '1' }))

    const result = await request<{ id: string }>('/api/rooms/1')

    expect(result).toEqual({ id: '1' })
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/rooms/1',
      expect.objectContaining({ method: 'GET', headers: { Accept: 'application/json' } }),
    )
  })

  it('passes the abort signal on to fetch', async () => {
    const fetchMock = stubFetch(json({ id: '1' }))
    const controller = new AbortController()

    await request('/api/rooms/1', { signal: controller.signal })

    expect(fetchMock).toHaveBeenCalledWith('/api/rooms/1', expect.objectContaining({ signal: controller.signal }))
  })

  it('sends a JSON body with a content type', async () => {
    const fetchMock = stubFetch(json({ ok: true }))

    await request('/api/reservations', { method: 'POST', body: { title: 'Plan' } })

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/reservations',
      expect.objectContaining({
        method: 'POST',
        body: '{"title":"Plan"}',
        headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
      }),
    )
  })

  it('returns undefined for an empty success body', async () => {
    stubFetch(new Response(null, { status: 204 }))

    expect(await request('/api/reservations/1/cancel', { method: 'POST' })).toBeUndefined()
  })

  it('turns an API error body into an ApiError with the status and every item', async () => {
    stubFetch(
      json(
        {
          errors: [
            { code: 'R5_CAPACITY_EXCEEDED', message: 'Too many people.' },
            { code: 'R1_MAX_DURATION', message: 'Too long.' },
          ],
        },
        422,
      ),
    )

    const error = await failure(request('/api/reservations'))

    expect(error.status).toBe(422)
    expect(error.errors.map((item) => item.code)).toEqual(['R5_CAPACITY_EXCEEDED', 'R1_MAX_DURATION'])
    expect(error.message).toBe('R5_CAPACITY_EXCEEDED -- Too many people.; R1_MAX_DURATION -- Too long.')
  })

  it('builds a generic error when the failure body is not the API error shape', async () => {
    stubFetch(new Response('boom', { status: 500, statusText: 'Internal Server Error' }))

    const error = await failure(request('/api/rooms'))

    expect(error.status).toBe(500)
    expect(error.errors).toEqual([
      { code: 'HTTP_500', message: 'The server answered 500 Internal Server Error' },
    ])
  })

  it('leaves out the status text when the server sent none', async () => {
    stubFetch(new Response('boom', { status: 500, statusText: '' }))

    const error = await failure(request('/api/rooms'))

    expect(error.errors).toEqual([{ code: 'HTTP_500', message: 'The server answered 500' }])
  })

  it.each([
    ['an empty errors list', { errors: [] }],
    ['an item without a message', { errors: [{ code: 'X' }] }],
    ['an item whose code is not text', { errors: [{ code: 1, message: 'Bad.' }] }],
    ['errors that are not a list', { errors: 'nope' }],
  ])('builds a generic error for %s', async (_name, body) => {
    stubFetch(json(body, 500))

    const error = await failure(request('/api/rooms'))

    expect(error.status).toBe(500)
    expect(error.errors[0]?.code).toBe('HTTP_500')
  })

  it.each([502, 503, 504])('reports a gateway status %i without a body as a network error', async (status) => {
    stubFetch(new Response('', { status }))

    const error = await failure(request('/api/rooms'))

    expect(error.status).toBe(0)
    expect(error.errors[0]?.code).toBe('NETWORK_ERROR')
  })

  it('keeps the API error body when a gateway status carries one', async () => {
    stubFetch(json({ errors: [{ code: 'SOMETHING', message: 'From the API.' }] }, 503))

    const error = await failure(request('/api/rooms'))

    expect(error.status).toBe(503)
    expect(error.errors[0]?.code).toBe('SOMETHING')
  })

  it('reports a failed fetch as a network error', async () => {
    stubFetch(new TypeError('Failed to fetch'))

    const error = await failure(request('/api/rooms'))

    expect(error.status).toBe(0)
    expect(error.errors[0]?.code).toBe('NETWORK_ERROR')
  })

  it('lets an abort through unchanged', async () => {
    stubFetch(new DOMException('Aborted', 'AbortError'))

    await expect(request('/api/rooms')).rejects.toMatchObject({ name: 'AbortError' })
  })

  it('rejects a success body whose shape is not the expected one', async () => {
    stubFetch(json({ not: 'an array' }))

    const error = await failure(request('/api/rooms', { expect: 'array' }))

    expect(error.errors[0]?.code).toBe('UNEXPECTED_RESPONSE')
  })

  it('rejects a success body that is not valid JSON when a shape is expected', async () => {
    stubFetch(new Response('<html>not json</html>', { status: 200 }))

    const error = await failure(request('/api/rooms', { expect: 'object' }))

    expect(error.errors[0]?.code).toBe('UNEXPECTED_RESPONSE')
  })

  it('accepts an object when an object is expected, and rejects an array', async () => {
    stubFetch(json({ items: [] }))
    expect(await request('/api/rooms', { expect: 'object' })).toEqual({ items: [] })

    stubFetch(json([]))
    const error = await failure(request('/api/rooms', { expect: 'object' }))
    expect(error.errors[0]?.code).toBe('UNEXPECTED_RESPONSE')
  })
})
