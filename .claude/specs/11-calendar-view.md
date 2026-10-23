# Spec 11 -- Calendar / Timeline View

## Overview
A calendar showing reservations visually, one room at a time (week and day time grids), so double-bookings
and free slots are easy to see. Nice-to-have from the task.

## Depends on
Spec 10.

## Pages / routes
| Route | Page |
|-------|------|
| `/calendar` | Room selector + FullCalendar |

## Behaviour
- Room selector (default: first room). Views: week (default), day, month.
- Events come from `GET /api/reservations?roomId=...&from=...&to=...` for the visible range, refetched on navigation.
- Colour by status: Booked, Completed, Cancelled (cancelled shown muted).
- Clicking an event shows title, organizer, time range and attendee count.
- Selecting an empty slot opens the create dialog from Spec 10 pre-filled with room, start and end.

## Files to create / change
`src/pages/CalendarPage.tsx`, route and nav link; reuse `ReservationDialog`.

## New dependencies
@fullcalendar/react, @fullcalendar/core, @fullcalendar/daygrid, @fullcalendar/timegrid, @fullcalendar/interaction (free plugins only).

## Rules for implementation
1. Fetch only the visible range, never the whole table.
2. A booking made from the calendar goes through the same API and shows the same rule errors.
3. Keep calendar styling inside MUI theme colours.

## Definition of done
- [ ] `npm run build` passes.
- [ ] A seeded reservation appears in the right room, slot and colour; creating from a slot refreshes the calendar.
