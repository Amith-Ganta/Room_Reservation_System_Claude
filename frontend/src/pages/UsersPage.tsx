import {
  Button,
  CircularProgress,
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

type SortKey = 'lastName' | 'activeReservationCount'
type SortDirection = 'asc' | 'desc'

interface Sort {
  key: SortKey
  direction: SortDirection
}

/** Display order only. Returns a new array; ties keep the order the API sent (the sort is stable). */
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
  // Changing this value re-runs the load effect (Retry button).
  const [reloadCount, setReloadCount] = useState(0)

  useEffect(() => {
    // Aborting on unmount or retry means a late response never updates the page.
    const controller = new AbortController()
    setLoading(true)
    setErrors([])
    listUsers(controller.signal)
      .then((result) => {
        if (controller.signal.aborted) {
          return
        }
        setUsers(result)
        setLoading(false)
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) {
          return
        }
        setUsers([])
        setErrors(
          error instanceof ApiError
            ? error.errors
            : [{ code: 'UNEXPECTED_ERROR', message: 'Something unexpected happened. Please try again.' }],
        )
        setLoading(false)
      })
    return () => controller.abort()
  }, [reloadCount])

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
      {errors.length > 0 && (
        <div>
          <Button variant="outlined" onClick={() => setReloadCount((count) => count + 1)}>
            Retry
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
                  <CircularProgress size={28} aria-label="Loading users" />
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
      </TableContainer>
    </Stack>
  )
}
