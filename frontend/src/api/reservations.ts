import { request } from './client'
import type { CreateReservationRequest, ReservationDto, ReservationStatus } from './types'

export interface ReservationFilters {
  roomId?: string
  userId?: string
  status?: ReservationStatus
  /** ISO 8601 timestamp with an offset; inclusive. */
  from?: string
  /** ISO 8601 timestamp with an offset; exclusive. */
  to?: string
}

/** Filtering happens in the API; empty filters are simply left out of the query string. */
export function listReservations(filters: ReservationFilters, signal?: AbortSignal): Promise<ReservationDto[]> {
  const query = new URLSearchParams()
  if (filters.roomId !== undefined) {
    query.set('roomId', filters.roomId)
  }
  if (filters.userId !== undefined) {
    query.set('userId', filters.userId)
  }
  if (filters.status !== undefined) {
    query.set('status', filters.status)
  }
  if (filters.from !== undefined) {
    query.set('from', filters.from)
  }
  if (filters.to !== undefined) {
    query.set('to', filters.to)
  }
  const text = query.toString()
  return request<ReservationDto[]>(text.length > 0 ? `/api/reservations?${text}` : '/api/reservations', {
    signal,
    expect: 'array',
  })
}

export function createReservation(body: CreateReservationRequest): Promise<ReservationDto> {
  return request<ReservationDto>('/api/reservations', { method: 'POST', body, expect: 'object' })
}

export function updateReservation(id: string, body: CreateReservationRequest): Promise<ReservationDto> {
  return request<ReservationDto>(`/api/reservations/${encodeURIComponent(id)}`, {
    method: 'PUT',
    body,
    expect: 'object',
  })
}

export function cancelReservation(id: string): Promise<ReservationDto> {
  return request<ReservationDto>(`/api/reservations/${encodeURIComponent(id)}/cancel`, {
    method: 'POST',
    expect: 'object',
  })
}

export function endReservation(id: string): Promise<ReservationDto> {
  return request<ReservationDto>(`/api/reservations/${encodeURIComponent(id)}/end`, {
    method: 'POST',
    expect: 'object',
  })
}
