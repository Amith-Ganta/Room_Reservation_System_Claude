# Spec 09 -- Users Page

## Overview
A Users page with a table of every user: first name, last name, id and the number of active (Booked) reservations.
The count comes from the API (Spec 06); the page only shows it. The table can be sorted by last name and by active
count. This is the second page that uses the pattern set in Spec 08 (API client, `AbortController` loading,
loading / empty / error states).

## Depends on
Specs 06 (users API) and 08 (frontend scaffold, `ErrorAlert`, `Layout`).

## Endpoints / Pages
| Route | Page |
|-------|------|
| `/users` | Users table |

API used: `GET /api/users` (no query parameters). Response, ordered by last name then first name:
```json
[{ "id": "guid", "firstName": "Anna", "lastName": "Kovalenko", "activeReservationCount": 2 }]
```

Table columns: last name, first name, id, active reservations.
- Default order is the one the API returns (last name, then first name).
- The "Last name" and "Active reservations" headers are sortable (`TableSortLabel`): the first click sorts ascending,
  the second descending. Only one column is the sort key at a time. Sorting is done on the returned list in the
  browser; it is display order only and changes no data and no request.
- Ties keep the API order (the sort is stable), so users with the same count stay in last name, first name order.
- Loading: progress indicator. Empty list: "No users yet." Failed request: `ErrorAlert` with the server's code and
  message, and a "Retry" button, because this page has no filters the user could change to trigger a new request.

## Data changes
None. No backend change is needed.

## Files to change
- `src/App.tsx`: add the route `/users`.
- `src/components/Layout.tsx`: add the nav item `{ to: '/users', label: 'Users' }`.
- `src/api/types.ts`: add `UserDto`.
- `.claude/skills/frontend-design/SKILL.md`: "Built so far" list includes `users.ts` and `UsersPage`.

## Files to create
- `src/api/users.ts`: `listUsers(signal?)`, built on `request<UserDto[]>('/api/users', { signal })`.
- `src/pages/UsersPage.tsx`.

## New dependencies
None.

## Rules for implementation
1. The active count is read from `activeReservationCount`; the page never counts or filters reservations itself.
2. All HTTP goes through `src/api/client.ts`; the page does not call `fetch`.
3. Follow the Rooms page pattern: `AbortController` per load, `signal.aborted` guards, `setErrors([])` at the start of
   each load, and a non-`ApiError` shown as `UNEXPECTED_ERROR`. Aborting on unmount means a late response never
   updates an unmounted page.
4. Every API failure is shown through `ErrorAlert` (`CODE -- message`), never swallowed. A failed request keeps the
   page header and the Retry button on screen. Retry re-requests the list.
5. Strict TypeScript, no `any`, no `@ts-ignore`. Ids are GUID strings.
6. Sorting is a pure display concern. Copy the array before sorting (never mutate state), and use a stable sort.
7. The table is wrapped in a `TableContainer` (horizontal scroll on small screens). Sort headers are real buttons
   (keyboard reachable) and expose the sort direction through `aria-sort`.
8. No new link or page beyond Users (Reservations, Calendar and Report belong to Specs 10-12).

## Definition of done
- [x] `npm run build` passes with no type errors.
- [x] `/users` lists the seeded users with their counts, in last name, first name order by default.
- [x] Sorting by last name and by active count works in both directions; a third kind of order is not offered.
- [x] The counts match the API response (`GET /api/users`); the page shows no number it computed.
- [x] Stopping the backend shows a readable `NETWORK_ERROR` alert and a Retry button; restarting the API and clicking
      Retry recovers.
- [x] An empty user list shows "No users yet." (checked with the API started without `Seed__Enabled`).
- [x] The nav shows "Rooms" and "Users", and the active link is highlighted.
- [x] After Spec 10 exists: creating or cancelling a reservation changes the count on reload.
