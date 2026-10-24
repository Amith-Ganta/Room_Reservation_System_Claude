import { request } from './client'
import { setPageParams } from './paging'
import type { EquipmentName, PageParams, PagedResult, RoomDto } from './types'

export interface RoomFilters extends PageParams {
  minCapacity?: number
  equipment?: EquipmentName[]
  floor?: number
}

/** Filtering happens in the API; empty filters are simply left out of the query string. */
export function listRooms(filters: RoomFilters, signal?: AbortSignal): Promise<PagedResult<RoomDto>> {
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
  setPageParams(query, filters)
  const text = query.toString()
  return request<PagedResult<RoomDto>>(text.length > 0 ? `/api/rooms?${text}` : '/api/rooms', { signal, expect: 'object' })
}
