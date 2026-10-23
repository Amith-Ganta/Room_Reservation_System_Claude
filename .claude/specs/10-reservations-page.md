# Spec 10 -- Reservations Page

## Overview
Reservations table with filters and the create / edit / cancel / end-early actions. Rule violations from the
backend (R1-R7 and the state errors) are shown to the user with the code and the human-readable message.

## Depends on
Specs 05, 08 and 09.

## Pages / routes
| Route | Page |
|-------|------|
| `/reservations` | Table + filters + dialogs |

Table columns: room, organizer, title, start, end, attendees, status chip (Booked / Completed / Cancelled).
Filters: room, organizer, status, date range (from / to). Row actions:
- Edit and Cancel: enabled only for Booked reservations that have not started.
- End early: enabled only for Booked reservations that have started.
(The UI hides or disables actions for convenience; the backend remains the authority and its errors are shown.)

## Create / edit dialog
Fields: room (select), organizer (select), title, start, end (datetime-local), attendees. On submit the API result is shown:
success closes the dialog and refreshes; failure keeps the dialog open and renders every returned error as `CODE -- message`.

## Files to create / change
`src/api/reservations.ts`, `src/pages/ReservationsPage.tsx`, `src/components/ReservationDialog.tsx`,
`src/components/ConfirmDialog.tsx`, `src/lib/datetime.ts` (local input <-> ISO UTC), route and nav link.

## Rules for implementation
1. No rule logic in the front end: do not pre-validate R1-R7 client-side, always show the server's answer.
2. Times are sent as ISO UTC and displayed in local time.
3. Disabled rooms are still selectable so that R7 can be demonstrated.

## Definition of done
- [ ] `npm run build` passes.
- [ ] Manual check against the running API: one failing booking per rule R1-R7 shows its code and message; successful create, edit, cancel and end-early refresh the table.
- [ ] All four filters work.
