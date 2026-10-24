/** Formatting and conversion only: no booking rule is decided from these values in the browser. */

function pad(value: number): string {
  return String(value).padStart(2, '0')
}

/**
 * Turns the value of a `datetime-local` input (local time, no offset) into an ISO 8601 timestamp with an offset,
 * which is what the API requires. Returns undefined when the value is empty or not a real date.
 */
export function toIsoWithOffset(localValue: string): string | undefined {
  if (localValue === '') {
    return undefined
  }
  const date = new Date(localValue)
  return Number.isNaN(date.getTime()) ? undefined : date.toISOString()
}

/** Turns an API timestamp into the local `yyyy-MM-ddTHH:mm` text a `datetime-local` input expects. */
export function toLocalInputValue(iso: string): string {
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) {
    return ''
  }
  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
    `T${pad(date.getHours())}:${pad(date.getMinutes())}`
  )
}

/** Local, readable date and time for the tables and dialogs. */
export function formatDateTime(iso: string): string {
  const date = new Date(iso)
  return Number.isNaN(date.getTime()) ? iso : date.toLocaleString()
}
