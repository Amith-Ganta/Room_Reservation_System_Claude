import {
  Button,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TableSortLabel,
  Typography,
} from '@mui/material'
import { useEffect, useMemo, useState } from 'react'
import { ApiError } from '../api/client'
import type { ApiErrorItem, UserDto } from '../api/types'
import { listUsers } from '../api/users'
import { ErrorAlert } from '../components/ErrorAlert'
import { ListPager, type PageInfo } from '../components/ListPager'
import { LoadingIndicator } from '../components/LoadingIndicator'

type SortKey = 'lastName' | 'activeReservationCount'
type SortDirection = 'asc' | 'desc'

interface Sort {
  key: SortKey
  direction: SortDirection
}

/** Display order of the rows on the current page only: the API orders the whole list by last name. Returns a new array; ties keep the order the API sent (the sort is stable). */
function sortUsers(users: UserDto[], sort: Sort | null): UserDto[] {
  if (sort === null) {
    return users
  }
  const factor = sort.direction === 'asc' ? 1 : -1
  return [...users].sort((a, b) =>
    sort.key === 'lastName'
      ? factor * a.lastName.localeCompare(b.lastName)
      : factor * (a.activeReservationCount - b.activeReservationCount),
  )
}

export function UsersPage() {
  const [users, setUsers] = useState<UserDto[]>([])
  const [loading, setLoading] = useState(true)
  const [errors, setErrors] = useState<ApiErrorItem[]>([])
  const [sort, setSort] = useState<Sort | null>(null)
  const [paging, setPaging] = useState<{ page: number; pageSize?: number }>({ page: 1 })
  const [pageInfo, setPageInfo] = useState<PageInfo | null>(null)
  // Changing this value re-runs the load effect (Retry button).
  const [reloadCount, setReloadCount] = useState(0)

  useEffect(() => {
    // Aborting on unmount or retry means a late response never updates the page.
    const controller = new AbortController()
    setLoading(true)
    setErrors([])
    listUsers({ page: paging.page, pageSize: paging.pageSize }, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) {
          return
        }
        // After a change elsewhere the list can be shorter than the page asked for: go to the last page that exists.
        if (result.items.length === 0 && result.totalPages > 0 && result.page > result.totalPages) {
          setPaging((current) => ({ ...current, page: result.totalPages }))
          return
        }
        setUsers(result.items)
        setPageInfo({ pageSize: result.pageSize, totalCount: result.totalCount })
        setLoading(false)
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) {
          return
        }
        setUsers([])
        setPageInfo(null)
        setErrors(
          error instanceof ApiError
            ? error.errors
            : [{ code: 'UNEXPECTED_ERROR', message: 'Something unexpected happened. Please try again.' }],
        )
        setLoading(false)
      })
    return () => controller.abort()
  }, [reloadCount, paging])

  // After a click the button stays mounted until the retry finishes, so the keyboard focus is not lost.
  const showRetry = errors.length > 0 || (loading && reloadCount > 0)

  const sortedUsers = useMemo(() => sortUsers(users, sort), [users, sort])

  /** First click on a column sorts ascending; clicking the active column flips the direction. */
  function toggleSort(key: SortKey) {
    setSort((current) =>
      current !== null && current.key === key
        ? { key, direction: current.direction === 'asc' ? 'desc' : 'asc' }
        : { key, direction: 'asc' },
    )
  }

  function sortDirectionFor(key: SortKey): SortDirection | false {
    return sort !== null && sort.key === key ? sort.direction : false
  }

  return (
    <Stack spacing={2}>
      <Typography variant="h4" component="h1">
        Users
      </Typography>

      <ErrorAlert errors={errors} />
      {showRetry && (
        <div>
          {/* aria-disabled (not disabled) keeps keyboard focus on the button while the retry runs. */}
          <Button
            variant="outlined"
            aria-disabled={loading}
            onClick={() => {
              if (!loading) {
                setReloadCount((count) => count + 1)
              }
            }}
          >
            {loading ? 'Retrying…' : 'Retry'}
          </Button>
        </div>
      )}

      <TableContainer component={Paper}>
        <Table size="small" aria-label="Users">
          <TableHead>
            <TableRow>
              <TableCell sortDirection={sortDirectionFor('lastName')}>
                <TableSortLabel
                  active={sortDirectionFor('lastName') !== false}
                  direction={sortDirectionFor('lastName') || 'asc'}
                  onClick={() => toggleSort('lastName')}
                >
                  Last name
                </TableSortLabel>
              </TableCell>
              <TableCell>First name</TableCell>
              <TableCell>Id</TableCell>
              <TableCell align="right" sortDirection={sortDirectionFor('activeReservationCount')}>
                <TableSortLabel
                  active={sortDirectionFor('activeReservationCount') !== false}
                  direction={sortDirectionFor('activeReservationCount') || 'asc'}
                  onClick={() => toggleSort('activeReservationCount')}
                >
                  Active reservations
                </TableSortLabel>
              </TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow>
                <TableCell colSpan={4} align="center">
                  <LoadingIndicator label="Loading users" />
                </TableCell>
              </TableRow>
            ) : sortedUsers.length === 0 ? (
              <TableRow>
                <TableCell colSpan={4} align="center">
                  {errors.length > 0 ? 'No users to show.' : 'No users yet.'}
                </TableCell>
              </TableRow>
            ) : (
              sortedUsers.map((user) => (
                <TableRow key={user.id} hover>
                  <TableCell>{user.lastName}</TableCell>
                  <TableCell>{user.firstName}</TableCell>
                  <TableCell>{user.id}</TableCell>
                  <TableCell align="right">{user.activeReservationCount}</TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
        <ListPager
          label="Users"
          page={paging.page}
          info={pageInfo}
          onChange={(page, pageSize) => setPaging((current) => ({ page, pageSize: pageSize ?? current.pageSize }))}
        />
      </TableContainer>
    </Stack>
  )
}
