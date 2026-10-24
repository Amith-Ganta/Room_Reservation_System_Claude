# Frontend Design Skill -- Room Reservation System

Apply this skill whenever building or modifying code in `frontend/`.

## Stack Constraints (non-negotiable)

- React 19 + Vite + TypeScript (strict, no `any`); MUI 9 (`Stack` takes `alignItems` through `sx`; number-field attributes go in `slotProps.htmlInput`)
- MUI (`@mui/material`) for all UI; no Tailwind, no other component library
- `react-router-dom` for routing; FullCalendar (free plugins only) for the calendar; `jspdf` + `jspdf-autotable` for PDF export
- All HTTP goes through `src/api/client.ts`; pages never call `fetch` directly
- Dates are sent as ISO 8601 UTC strings (`toISOString()`) and shown in local time
- No business rule or report calculation in the front end: the server validates and computes, the UI displays

## Layout

```
src/api/         client.ts (ApiError), types.ts (DTO types mirroring the API), rooms.ts, users.ts, reservations.ts, reports.ts
src/components/  Layout, ErrorAlert, ErrorBoundary, LoadingIndicator, shared dialogs
src/pages/       RoomsPage, UsersPage, ReservationsPage, CalendarPage, ReportPage
src/lib/         formatting helpers, pdf export
```

Built so far: `client.ts`, `types.ts`, `rooms.ts`, `users.ts`, `reservations.ts`, `RoomsPage`, `UsersPage`, `ReservationsPage`, `CalendarPage`, `ReservationFormDialog`, `ReservationDetailsDialog`, `ConfirmActionDialog`, `ErrorAlert`, `ErrorBoundary`, `LoadingIndicator`, `Layout`, `lib/equipment.ts`, `lib/dateTime.ts` and `lib/errors.ts`. The other files are planned for spec 12 and are created by the spec that needs them.

## Page pattern

Each page keeps `items`, `filters`, `loading` and `error` state, loads in an effect keyed on the filters,
and renders three states: loading (progress), empty (message) and error (`ErrorAlert`).

```tsx
const [items, setItems] = useState<RoomDto[]>([])
const [loading, setLoading] = useState(true)
const [errors, setErrors] = useState<ApiErrorItem[]>([])

useEffect(() => {
  // Aborting the previous request means an older response can never replace a newer one.
  const controller = new AbortController()
  setLoading(true)
  setErrors([])
  listRooms(filters, controller.signal)
    .then((result) => {
      if (controller.signal.aborted) return
      setItems(result)
      setLoading(false)
    })
    .catch((error: unknown) => {
      if (controller.signal.aborted) return
      setItems([])
      setErrors(
        error instanceof ApiError
          ? error.errors
          : [{ code: 'UNEXPECTED_ERROR', message: 'Something unexpected happened. Please try again.' }],
      )
      setLoading(false)
    })
  return () => controller.abort()
}, [filters])
```

Number fields: validate inline (`error` + `helperText`) and send nothing for an invalid value; debounce typing by 300 ms.

## Error display

The API returns `{ errors: [{ code, message }] }`. `ApiError` carries that list and `ErrorAlert` renders every entry as
`CODE -- message`. A missing server (fetch failure, or a 502/503/504 from the dev proxy) becomes `NETWORK_ERROR` with status 0;
any other unexpected status becomes `HTTP_<status>`. A non-`ApiError` thrown in a page is shown as `UNEXPECTED_ERROR`. Ids are GUID strings, never numbers. Rule violations (R1-R7) are shown inside the dialog that triggered them, not in a toast that disappears.

## UI conventions

- Status chips: Booked = primary, Completed = success, Cancelled = default; Active = success, Inactive = default.
- Destructive actions (cancel) go through a confirm dialog; the end-early action is offered only for a started Booked reservation.
- Tables are responsive (`TableContainer` with horizontal scroll on small screens); filters wrap with `Grid`/`Stack`.
- Every input has a label; icon buttons have `aria-label`.
- Keep components small; extract a dialog or filter bar when a page passes about 150 lines.
