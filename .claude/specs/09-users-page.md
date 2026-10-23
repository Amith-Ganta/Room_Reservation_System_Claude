# Spec 09 -- Users Page

## Overview
Users table showing first name, last name, id and active reservation count.

## Depends on
Specs 06 and 08.

## Pages / routes
| Route | Page |
|-------|------|
| `/users` | Users table |

## Files to create / change
- Create `src/api/users.ts`, `src/pages/UsersPage.tsx`
- Change `src/App.tsx` (route) and `src/components/Layout.tsx` (nav link)

## Rules for implementation
1. The count comes from the API; the page never counts reservations itself.
2. Sortable by last name and by active count (client-side sort of the returned list is fine).
3. Same loading / empty / error handling as the Rooms page.

## Definition of done
- [ ] `npm run build` passes.
- [ ] Seeded users show their counts; creating or cancelling a reservation (after Spec 10) changes the count on reload.
