import { request } from './client'
import type { UserDto } from './types'

/** All users, ordered by last name then first name, each with the count calculated by the API. */
export function listUsers(signal?: AbortSignal): Promise<UserDto[]> {
  return request<UserDto[]>('/api/users', { signal, expect: 'array' })
}
