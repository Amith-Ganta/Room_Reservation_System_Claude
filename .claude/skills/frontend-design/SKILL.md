# Frontend Design Skill -- Room Reservation System

Apply this skill whenever building or modifying code in `frontend/`.

## Stack Constraints (non-negotiable)

- React 18 + Vite + TypeScript (strict, no `any`)
- MUI (`@mui/material`) for all UI; no Tailwind, no other component library
- `react-router-dom` for routing; FullCalendar (free plugins only) for the calendar; `jspdf` + `jspdf-autotable` for PDF export
- All HTTP goes through `src/api/client.ts`; pages never call `fetch` directly
- Dates are sent as ISO 8601 UTC strings (`toISOString()`) and shown in local time
- No business rule or report calculation in the front end: the server validates and computes, the UI displays

## Layout

```
src/api/         client.ts (ApiError), rooms.ts, users.ts, reservations.ts, reports.ts
src/components/  Layout, ErrorAlert, shared dialogs
src/pages/       RoomsPage, UsersPage, ReservationsPage, CalendarPage, ReportPage
src/lib/         formatting helpers, pdf export
src/types.ts     DTO types mirroring the API
```

## Page pattern

Each page keeps `items`, `filters`, `loading` and `error` state, loads in an effect keyed on the filters,
and renders three states: loading (progress), empty (message) and error (`ErrorAlert`).

```tsx
const [items, setItems] = useState<RoomDto[]>([]);
const [loading, setLoading] = useState(false);
const [error, setError] = useState<ApiError | null>(null);

useEffect(() => {
  let cancelled = false;
  setLoading(true);
  getRooms(filters)
    .then(r => !cancelled && setItems(r))
    .catch(e => !cancelled && setError(e as ApiError))
    .finally(() => !cancelled && setLoading(false));
  return () => { cancelled = true; };
}, [filters]);
```

## Error display

The API returns `{ errors: [{ code, message }] }`. `ApiError` carries that list and `ErrorAlert` renders every entry as
`CODE: message`. Rule violations (R1-R7) are shown inside the dialog that triggered them, not in a toast that disappears.

## UI conventions

- Status chips: Booked = primary, Completed = success, Cancelled = default; Active = success, Inactive = default.
- Destructive actions (cancel) go through a confirm dialog; the end-early action is offered only for a started Booked reservation.
- Tables are responsive (`TableContainer` with horizontal scroll on small screens); filters wrap with `Grid`/`Stack`.
- Every input has a label; icon buttons have `aria-label`.
- Keep components small; extract a dialog or filter bar when a page passes about 150 lines.
