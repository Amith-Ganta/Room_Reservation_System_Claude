import { request } from './client'
import type { EquipmentName, RoomDto } from './types'

export interface RoomFilters {
  minCapacity?: number
  equipment?: EquipmentName[]
  floor?: number
}

/** Filtering happens in the API; empty filters are simply left out of the query string. */
export function listRooms(filters: RoomFilters, signal?: AbortSignal): Promise<RoomDto[]> {
  const query = new URLSearchParams()
  if (filters.minCapacity !== undefined) {
    query.set('minCapacity', String(filters.minCapacity))
  }
  if (filters.equipment !== undefined && filters.equipment.length > 0) {
    query.set('equipment', filters.equipment.join(','))
  }
  if (filters.floor !== undefined) {
    query.set('floor', String(filters.floor))
  }
  const text = query.toString()
  return request<RoomDto[]>(text.length > 0 ? `/api/rooms?${text}` : '/api/rooms', { signal, expect: 'array' })
}
