import type { ApiErrorBody, ApiErrorItem } from './types'

/** Thrown for every failed request: the server's own errors, or one generated here (status 0 = no response). */
export class ApiError extends Error {
  readonly status: number
  readonly errors: ApiErrorItem[]

  constructor(status: number, errors: ApiErrorItem[]) {
    super(errors.map((error) => `${error.code} -- ${error.message}`).join('; '))
    this.name = 'ApiError'
    this.status = status
    this.errors = errors
  }
}

const GATEWAY_STATUSES = new Set([502, 503, 504])

function networkError(): ApiError {
  return new ApiError(0, [
    { code: 'NETWORK_ERROR', message: 'Cannot reach the server. Check that the API is running.' },
  ])
}

function isErrorItem(value: unknown): value is ApiErrorItem {
  if (typeof value !== 'object' || value === null) {
    return false
  }
  const item = value as Partial<ApiErrorItem>
  return typeof item.code === 'string' && typeof item.message === 'string'
}

function isErrorBody(value: unknown): value is ApiErrorBody {
  if (typeof value !== 'object' || value === null || !('errors' in value)) {
    return false
  }
  const { errors } = value as { errors: unknown }
  return Array.isArray(errors) && errors.length > 0 && errors.every(isErrorItem)
}

function isPlainObject(value: unknown): boolean {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

async function readJson(response: Response): Promise<unknown> {
  const text = await response.text()
  if (text.length === 0) {
    return undefined
  }
  try {
    return JSON.parse(text) as unknown
  } catch {
    return undefined
  }
}

export interface RequestOptions {
  method?: string
  body?: unknown
  signal?: AbortSignal
  /** Shape the body must have on success. A mismatch becomes an UNEXPECTED_RESPONSE error instead of a crash later. */
  expect?: 'array' | 'object'
}

/** Calls the API (relative path, proxied by Vite in development) and returns the parsed JSON body. */
export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, signal, expect } = options
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }

  let response: Response
  try {
    response = await fetch(path, {
      method,
      signal,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch (error) {
    // A cancelled request is not a failure: let the caller see the abort.
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error
    }
    throw networkError()
  }

  const payload = await readJson(response)
  if (!response.ok) {
    if (isErrorBody(payload)) {
      throw new ApiError(response.status, payload.errors)
    }
    // A dev proxy (or gateway) answers 502/503/504 with no API error body when the backend is down.
    if (GATEWAY_STATUSES.has(response.status)) {
      throw networkError()
    }
    throw new ApiError(response.status, [
      {
        code: `HTTP_${response.status}`,
        message: `The server answered ${response.status} ${response.statusText}`.trim(),
      },
    ])
  }
  if (expect === 'array' ? !Array.isArray(payload) : expect === 'object' && !isPlainObject(payload)) {
    throw new ApiError(response.status, [
      { code: 'UNEXPECTED_RESPONSE', message: 'The server answered with data this page cannot read.' },
    ])
  }
  return payload as T
}
