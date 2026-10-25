# Spec 10 -- Reservations Page

## Overview
A Reservations page where a user can see every reservation, filter the list, create a reservation, change one,
cancel one and end a running one early. All booking rules (R1-R7) stay in the back end: the page sends the form and
shows whatever the API answers, so a rule error appears inside the dialog with its code and message and the user can
correct the form without losing what was typed. This is the third page on the pattern from Specs 08 and 09 (API
client, `AbortController` loading, loading / empty / error states).

## Depends on
Specs 03 (rooms API), 05 (reservations API), 06 (users API), 08 (frontend scaffold) and 09 (Users page, for the count check).

## Endpoints / Pages
| Route | Page |
|-------|------|
| `/reservations` | Reservations table with filters and the create / edit / cancel / end actions |

API used:
| Call | Purpose |
|------|---------|
| `GET /api/reservations?roomId=&userId=&status=&from=&to=` | list, newest start first; every filter optional |
| `POST /api/reservations` | create (201) |
| `PUT /api/reservations/{id}` | change a Booked reservation that has not started (200) |
| `POST /api/reservations/{id}/cancel` | cancel a Booked reservation that has not started (200) |
| `POST /api/reservations/{id}/end` | end a running reservation now (200) |
| `GET /api/rooms`, `GET /api/users` | fill the room and organizer drop-downs (no query parameters) |

Request body of create and change (`CreateReservationRequest`):
```json
{ "roomId": "guid", "userId": "guid", "title": "Sprint planning",
  "start": "2026-11-02T09:00:00.000Z", "end": "2026-11-02T10:00:00.000Z", "attendeeCount": 6 }
```
Response (`ReservationDto`): `id, roomId, roomName, userId, organizerName, title, start, end, endedAt (nullable),
attendeeCount, status` where `status` is `Booked`, `Completed` or `Cancelled`.

Error answers the page must show: 422 with one or more of `R1_MAX_DURATION`, `R2_ROOM_OVERLAP`, `R3_USER_OVERLAP`,
`R4_PAST_START`, `R5_CAPACITY_EXCEEDED`, `R6_BEYOND_HORIZON`, `R7_ROOM_INACTIVE`, `INVALID_TIME_RANGE`,
`TITLE_REQUIRED`, `TITLE_TOO_LONG`, `INVALID_ATTENDEES`; 404 `ROOM_NOT_FOUND`, `USER_NOT_FOUND`,
`RESERVATION_NOT_FOUND`; 409 `RESERVATION_NOT_BOOKED`, `RESERVATION_ALREADY_STARTED`, `RESERVATION_NOT_STARTED`;
400 `INVALID_REQUEST`. The page does not need to know these codes: it shows `code -- message` for any of them.

### List and filters
- Filters: room (drop-down, "All rooms"), organizer (drop-down, "All users"), status (drop-down: All, Booked,
  Completed, Cancelled), "From" and "To" (`datetime-local` inputs, both optional).
- Filtering is done by the API. Changing a filter requests the list again; the page never filters rows itself.
- "From" and "To" are entered in the browser's local time and sent as an ISO 8601 timestamp with an offset
  (`new Date(value).toISOString()`). An empty field is left out of the query. If the API refuses a combination
  (for example From later than To), its 400 message is shown in the error alert.
- Table columns: title, room, organizer, start, end, attendees, status, actions.
  - Start and end are shown in local time (`toLocaleString`).
  - A reservation with `endedAt` shows "Ended early at <time>" under the planned end (the planned end is never
    replaced).
  - Status is a `Chip`; the colour is decoration only.
- Loading: progress indicator (`LoadingIndicator`). No rows: "No reservations match." Failed request:
  `ErrorAlert` plus a "Retry" button.

### Create and change dialog
- "New reservation" button above the table opens a dialog (`Dialog`, labelled, focus moves into it).
- Fields: room (select), organizer (select), title (text), start and end (`datetime-local`), attendees (number).
  All rooms are offered, inactive ones marked "(inactive)", because R7 is the back end's decision, not the page's.
- Before sending, the page only checks what it needs to build the request: both times filled, a room and an
  organizer chosen, and the attendee field holding a whole number. Otherwise it shows a field message and sends
  nothing. It does not check title, duration, capacity, overlap or any other rule.
- "Save" sends the request. While it is in flight both buttons are disabled and the label reads "Saving…".
  - Success: the dialog closes, the list is requested again, and a short confirmation (`Snackbar`) appears.
  - Failure: the dialog stays open, keeps every value the user typed, and shows `ErrorAlert` with all errors the API
    returned (several rules can fail together). Saving again sends a fresh request.
- Opening the dialog from a row's "Edit" button fills it with that reservation and uses `PUT`; the title reads
  "Edit reservation". "Edit" is offered only for a Booked reservation that has not started.
- Closing the dialog with Cancel, Escape or the backdrop discards the form and any error, except while a save is in flight.

### Cancel and end early
- Row actions depend on status. For a Booked reservation:
  - start in the future: "Edit" and "Cancel";
  - start in the past (running): "End now".
  The browser clock is used only to pick which button to show. The API decides if the action is allowed; if the
  clock difference makes it answer 409 (`RESERVATION_ALREADY_STARTED` or `RESERVATION_NOT_STARTED`), the error is
  shown and the list is requested again so the buttons match the server.
- Completed and Cancelled rows have no action buttons.
- "Cancel" and "End now" each open a confirm dialog naming the reservation (title, room, start). Confirming sends
  the call; the dialog shows any API error inside it and stays open. On success it closes, the list is requested
  again, and a confirmation appears.
- Only one write is in flight at a time; every action button is disabled meanwhile.

## Data changes
None. No backend change is needed.

## Files to change
- `src/App.tsx`: add the route `/reservations`.
- `src/components/Layout.tsx`: add the nav item `{ to: '/reservations', label: 'Reservations' }`.
- `src/api/types.ts`: add `ReservationStatus`, `ReservationDto`, `CreateReservationRequest`.
- `.claude/skills/frontend-design/SKILL.md`: "Built so far" list includes `reservations.ts` and `ReservationsPage`.

## Files to create
- `src/api/reservations.ts`: `listReservations(filters, signal?)`, `createReservation(body)`,
  `updateReservation(id, body)`, `cancelReservation(id)`, `endReservation(id)`, all through `request<T>`; the list
  uses `expect: 'array'`, the writes use `expect: 'object'`.
- `src/pages/ReservationsPage.tsx`.
- `src/components/ReservationFormDialog.tsx`: the create / edit dialog.
- `src/components/ConfirmActionDialog.tsx`: the shared confirm dialog for cancel and end.
- `src/lib/dateTime.ts`: `toIsoWithOffset(localValue)`, `toLocalInputValue(iso)`, `formatDateTime(iso)`. Formatting
  and conversion only, no rules.

## New dependencies
None. (`@mui/material` already provides `Dialog`, `Select`, `Chip`, `Snackbar`.)

## Rules for implementation
1. No booking rule, overlap check, capacity check or count is calculated in the front end. The only date logic is
   converting and formatting values and choosing which row button to show (see above).
2. All HTTP goes through `src/api/client.ts`; pages and dialogs never call `fetch`.
3. Follow the existing page pattern for loading: `AbortController` per load, `signal.aborted` guards,
   `setErrors([])` at the start of each load, a non-`ApiError` shown as `UNEXPECTED_ERROR`.
4. A write is never cancelled by an abort on filter change. Writes use their own state (`saving`), ignore results
   after the dialog or page unmounts, and never leave the page stuck in "Saving…".
5. Every API failure is shown through `ErrorAlert` (`CODE -- message`), never swallowed. Errors from a write appear
   in the dialog that started it; errors from the list or the lookups appear on the page.
6. Lookups (rooms, users) are loaded once per visit, with the same abort handling. If they fail, the page shows
   their error with Retry and disables "New reservation" and "Edit" (the drop-downs would be empty).
7. Double-submit guard: while `saving` is true, the Save and confirm buttons ignore clicks as well as being disabled.
8. Strict TypeScript, no `any`, no `@ts-ignore`. Ids are GUID strings. Status is a string union that matches the
   API names exactly.
9. Accessibility: dialogs have `aria-labelledby`, every field has a visible label, row action buttons carry an
   `aria-label` that names the reservation ("Cancel Sprint planning"), the status chip is text not colour only.
10. The table is wrapped in a `TableContainer` (horizontal scroll on small screens).
11. No link or page beyond Reservations (Calendar and Report belong to Specs 11 and 12).

## Definition of done
- [x] `npm run build` passes with no type errors.
- [x] `/reservations` lists the seeded reservations, newest start first, with the correct status chips.
- [x] Each filter (room, organizer, status, from, to) changes the request and the rows; with no filter all rows show.
- [x] Creating a valid reservation closes the dialog, shows the new row and a confirmation.
- [x] Each rule failure shown from the API: at least R2 (room overlap), R4 (past start), R5 (capacity) and R7
      (inactive room) are triggered in the browser and appear as `CODE -- message` inside the dialog, with the typed
      values kept. Two failing rules at once show both messages.
- [x] A blank title shows `TITLE_REQUIRED` from the API (the page does not check it itself).
- [x] Editing a future reservation updates the row; "Edit" and "Cancel" are absent on Completed and Cancelled rows.
- [x] Cancelling a future reservation (after confirming) changes its status to Cancelled; backing out of the confirm
      dialog changes nothing.
- [x] "End now" on a running reservation sets status Completed and shows "Ended early at <time>".
- [x] A stale action (for example cancelling a reservation that another browser tab already cancelled) shows the 409
      error and the list refreshes.
- [x] Stopping the backend shows a readable `NETWORK_ERROR` alert and Retry on the list, and inside the dialog on save.
- [x] An empty result shows "No reservations match." (checked with a filter that matches nothing).
- [x] Double-clicking Save creates one reservation only.
- [x] The nav shows "Rooms", "Users" and "Reservations", and the active link is highlighted.
- [x] The Users page shows the active count rising after a create and falling after a cancel or an early end (the
      count comes from the API on reload).
