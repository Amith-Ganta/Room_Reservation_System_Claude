# Spec 11 -- Calendar / Timeline View

## Overview
A Calendar page that shows the reservations of one room on a week, day or month grid, so booked and free slots are
easy to see. The user can click an event to read its details and select an empty time range to start a new
reservation. Nice-to-have from the task. It adds no rule and no number of its own: the events are what the API
returns for the visible range, and a booking made from a slot goes through the same dialog and the same API as on
the Reservations page (Spec 10), so rule errors (R1-R7) look the same. This is the fourth page on the pattern from
Specs 08-10 (API client, `AbortController` loading, loading / empty / error states).

## Depends on
Specs 03 (rooms API), 05 (reservations API) and 10 (`ReservationFormDialog`, `lib/dateTime.ts`, `lib/errors.ts`).

## Endpoints / Pages
| Route | Page |
|-------|------|
| `/calendar` | Room selector + FullCalendar (week, day, month) |

API used (both already have front-end functions):
| Call | Purpose |
|------|---------|
| `GET /api/rooms` | fill the room selector |
| `GET /api/reservations?roomId=&from=&to=` | events of the visible range |
| `POST /api/reservations` | create, through `ReservationFormDialog` (Spec 10) |

`from` and `to` are ISO 8601 timestamps with an offset. The API treats them as the half-open window `[from, to)` and
returns every reservation that overlaps it (a reservation ended early counts until `endedAt`), so a reservation that
starts before the window and ends inside it is returned too. The page sends the visible range as FullCalendar reports
it (`datesSet`: `start` inclusive, `end` exclusive) converted with `toISOString()`. Cancelled reservations are
returned and are drawn, muted.

### Page layout
- Heading "Calendar", a **Room** drop-down (labelled), and a "New reservation" button.
- Rooms and users are loaded once per visit (users only fill the Organizer drop-down of the create dialog). If the
  users request fails, the calendar still works; the error is shown with Retry and the create actions stay disabled
  until the users are loaded. The first room returned is selected. Inactive rooms are listed and marked
  "(inactive)"; the page does not hide them or stop booking them, because R7 is the back end's decision.
- Below: a `LinearProgress` while events load, an `ErrorAlert` for failures, and the calendar.
- The calendar stays mounted while events load, fail or are empty, so the visible date and view are never lost. A
  failed load shows the error plus a "Retry" button above the calendar and no events. A room with no events in range
  simply shows an empty grid (no extra message).
- No rooms at all (empty list): the calendar is not shown; the page says "No rooms to show." and disables the button.
- Rooms request failed: `ErrorAlert` plus Retry, and the calendar and button are hidden or disabled (there is no
  room to ask about).

### Calendar behaviour
- Views: week (default, `timeGridWeek`), day (`timeGridDay`) and month (`dayGridMonth`), switched from the
  FullCalendar header together with previous / next / today. On a narrow screen (below the MUI `sm` breakpoint) the
  initial view is the day view.
- Events are loaded for the visible range and the selected room, again on every navigation or view change, and again
  after the room changes or a reservation is created. Only the visible range is requested, never the whole table.
- Time is the browser's local time (FullCalendar `timeZone: 'local'`); the first screen scrolls to the morning
  (display only). A "now" indicator is shown.
- One event per reservation: `id`, `title`, `start`, `end`, status and the rest of the DTO in `extendedProps`.
  - End shown: `endedAt ?? end`, the same interval the API uses for overlaps, so a reservation ended early shows the
    time the room was really used. The planned end appears in the detail dialog.
  - Colour by status, from the MUI theme (no hard-coded hex): Booked = `primary`, Completed = `success`, Cancelled =
    a muted grey with the title struck through. The status is also written in the event text for Completed and
    Cancelled ("Sprint planning (Cancelled)"), so the colour is not the only signal.
  - Events are focusable and open on Enter / Space as well as on click.
- **Click an event** (any view): opens a read-only detail dialog (labelled, closable with Escape): title, room,
  organizer, start and end in local time, "Ended early at <time>" when `endedAt` is set, attendees, status. No edit,
  cancel or end action here; those stay on the Reservations page, so the rules for who may do what live in one place.
- **Select an empty time range** in the week or day view (click or drag on the grid): opens the create dialog from
  Spec 10 pre-filled with the selected room, start and end. The page does not check whether the slot is free, in the
  future or within 90 days: the API answers, and its errors show inside the dialog with the typed values kept.
- **Month view:** clicking a day switches to the day view of that date (no selection of whole days, which would
  prefill a midnight-to-midnight booking).
- **"New reservation" button:** opens the same dialog with only the selected room pre-filled.
- After a successful save the dialog closes, the events are requested again, and a short confirmation (`Snackbar`)
  appears, as on the Reservations page.

## Data changes
None. No backend change is needed.

## Files to change
- `src/App.tsx`: add the route `/calendar`.
- `src/components/Layout.tsx`: add the nav item `{ to: '/calendar', label: 'Calendar' }`.
- `src/components/ReservationFormDialog.tsx`: new optional prop
  `prefill?: { roomId?: string; start?: string; end?: string }` (start and end are ISO strings, converted with
  `toLocalInputValue`). Used only when `reservation` is null; no change to the Reservations page or to any
  existing behaviour.
- `frontend/package.json` and lock file: the FullCalendar packages below.
- `.claude/skills/frontend-design/SKILL.md`: "Built so far" list includes `CalendarPage` and
  `ReservationDetailsDialog`.

## Files to create
- `src/pages/CalendarPage.tsx`.
- `src/components/ReservationDetailsDialog.tsx`: the read-only dialog (formatting only, no rules).

`api/reservations.ts` already has `listReservations` with the `roomId`, `from` and `to` filters; `api/rooms.ts` and
`api/users.ts` already have the room and user lists. No new API function is needed.

## New dependencies
Free (MIT) FullCalendar packages only, the latest 6.x release that supports React 19, one exact version for all of
them: `@fullcalendar/react`, `@fullcalendar/core`, `@fullcalendar/daygrid`, `@fullcalendar/timegrid`,
`@fullcalendar/interaction`. No premium or "scheduler" package (resource timeline is a paid plugin). Installing
them needs the user's approval at implementation time.

## Rules for implementation
1. No booking rule, overlap check, capacity check or count is calculated in the front end. The only date logic is
   converting the visible range and the selected slot to ISO timestamps and formatting values for display. Do not
   use FullCalendar options that encode a rule (`selectOverlap`, `selectAllow`, `eventOverlap`, `businessHours`
   limits, `validRange`).
2. All HTTP goes through `src/api/client.ts` (via `listReservations` and `listRooms`); the page never calls `fetch`.
3. Follow the page pattern: `AbortController` per load keyed on the room and the visible range, `signal.aborted`
   guards, `setErrors([])` at the start of each load, a non-`ApiError` shown as `UNEXPECTED_ERROR`, Retry with
   `aria-disabled` and "Retrying…" like the Reservations page.
4. Keep the visible range in state set from `datesSet`, and set it only when it really changed, so the effect does
   not loop. Events live in state; the calendar is not remounted to refresh it (no `key` change on reload).
5. An older response never replaces a newer one (abort on room change, navigation and unmount).
6. The create dialog is rendered only while open (as on the Reservations page). A write is never cancelled by an
   abort; ignore results after unmount.
7. Colours come from `useTheme()` palette tokens. FullCalendar's own styles are injected by the package; extra
   styling is limited to a few `sx` overrides on a wrapper so the calendar fits the MUI look. No new CSS file.
8. Strict TypeScript, no `any`, no `@ts-ignore`. Ids are GUID strings. Status is the string union from `types.ts`.
9. Accessibility: the room drop-down has a visible label, the dialogs have `aria-labelledby`, events are keyboard
   reachable, status is text as well as colour.
10. No link or page beyond Calendar (Report belongs to Spec 12).

## Definition of done
- [ ] `npm run build` passes with no type errors; `npm audit` shows no new high or critical finding from the added
      packages.
- [ ] `/calendar` shows the first room; changing the room reloads the events for that room only.
- [ ] A seeded Booked reservation appears in the right room, day, time slot and primary colour; a Completed one in
      the success colour with "(Completed)"; a Cancelled one muted and struck through with "(Cancelled)".
- [ ] A reservation ended early ends at `endedAt` on the grid and the detail dialog shows both times.
- [ ] Previous / next / today and the week / day / month buttons keep working and request only the visible range
      (checked in the network log: `from` and `to` match the view).
- [ ] Clicking an event (and pressing Enter on a focused event) opens the detail dialog with the right values.
- [ ] Selecting a future free slot in the week view opens the create dialog with room, start and end filled in (local
      time shown correctly); saving creates the reservation, closes the dialog, shows a confirmation and the event
      appears without a page reload.
- [ ] Selecting an occupied slot or a past slot shows `R2_ROOM_OVERLAP` / `R4_PAST_START` from the API inside the
      dialog, with the values kept.
- [ ] In month view, clicking a day switches to that day's view.
- [ ] "New reservation" opens the dialog with only the room pre-filled.
- [ ] Stopping the backend shows a readable `NETWORK_ERROR` alert with Retry; the visible date and view are kept,
      and Retry loads the events again once the backend is back.
- [ ] Switching room or navigating quickly never shows the events of an earlier request (no stale events).
- [ ] The nav shows "Rooms", "Users", "Reservations" and "Calendar", and the active link is highlighted.
- [ ] The Reservations page still works unchanged (create and edit through the dialog).
