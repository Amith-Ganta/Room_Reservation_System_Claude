import { Alert, AlertTitle } from '@mui/material'
import type { ApiErrorItem } from '../api/types'

interface ErrorAlertProps {
  errors: ApiErrorItem[]
}

/** Shows every error the server (or the client) returned as `CODE -- message`. */
export function ErrorAlert({ errors }: ErrorAlertProps) {
  if (errors.length === 0) {
    return null
  }
  return (
    <Alert severity="error" role="alert">
      <AlertTitle>{errors.length === 1 ? 'Something went wrong' : 'Some things went wrong'}</AlertTitle>
      <ul style={{ margin: 0, paddingLeft: '1.25rem' }}>
        {errors.map((error, index) => (
          <li key={`${error.code}-${index}`}>
            {error.code} -- {error.message}
          </li>
        ))}
      </ul>
    </Alert>
  )
}
