import { request } from './client'
import { setPageParams } from './paging'
import type { PageParams, PagedResult, UserDto } from './types'

/** One page of users, ordered by last name then first name, each with the count calculated by the API. */
export function listUsers(params: PageParams = {}, signal?: AbortSignal): Promise<PagedResult<UserDto>> {
  const query = new URLSearchParams()
  setPageParams(query, params)
  const text = query.toString()
  return request<PagedResult<UserDto>>(text.length > 0 ? `/api/users?${text}` : '/api/users', {
    signal,
    expect: 'object',
  })
}
