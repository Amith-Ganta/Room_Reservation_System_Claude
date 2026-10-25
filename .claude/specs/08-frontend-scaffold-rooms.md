# Spec 08 -- Frontend Scaffold and Rooms Page

## Overview
Create the React + Vite + TypeScript + MUI app, the typed API client, the app shell (top bar and navigation) and the
Rooms page with its three filters. This spec sets the patterns (API client, error handling, loading / empty states)
that Specs 09-12 reuse.

## Depends on
Spec 03 (rooms API). Specs 05-07 are needed by later frontend specs.

## Pages / routes
| Route | Page |
|-------|------|
| `/` | redirect to `/rooms` |
| `/rooms` | Rooms table + filters |
| anything else | redirect to `/rooms` |

The navigation shows only the pages that exist. This spec adds one link, "Rooms". Specs 09-12 each add their own link.

## Rooms page
Table columns: name, id, capacity, floor, equipment (one chip per item), status (Active / Inactive chip).
The API returns rooms ordered by floor, then name; the page keeps that order. Inactive rooms are listed too, with a
muted "Inactive" chip, so that the later R7 demonstration has a room to pick.

Filters (all optional, combined with AND by the API):
| Filter | Control | Query parameter |
|--------|---------|-----------------|
| Minimum capacity | number field (whole number >= 1) | `minCapacity` |
| Equipment | multi-select of the four values; every selected item must be present | `equipment` (comma-separated) |
| Floor | number field (whole number) | `floor` |

- Every filter change re-requests `GET /api/rooms`. The client does no filtering itself.
- Empty filters are left out of the query string.
- Number fields wait about 300 ms after the last keystroke before they request, and an older response never overwrites a
  newer one.
- A "Clear filters" button resets all three.
- Equipment wire values and labels: `Projector` = "Projector", `TvScreen` = "TV screen",
  `VideoConferencing` = "Video conferencing", `Whiteboard` = "Whiteboard". The labels live in one place (`src/lib/equipment.ts`).

## API client
- `request<T>(path, init?)` wraps `fetch`, sends and reads JSON, and returns the parsed body.
- A non-2xx response with the standard body `{ errors: [{ code, message }] }` throws `ApiError { status, errors }`.
- A non-2xx response without that body throws an `ApiError` with one error `HTTP_<status>` and a readable message.
- A network failure (backend stopped) throws an `ApiError` with status 0 and one error `NETWORK_ERROR`
  ("Cannot reach the server. Check that the API is running."). This code is raised by the front end only.
- `src/api/types.ts` mirrors the server DTOs (`RoomDto`, `EquipmentName`, `ApiErrorBody`); later specs extend it.
- The base path is relative (`/api/...`). In development Vite proxies `/api` to http://localhost:5080, so the browser
  makes no cross-origin call.

## Files to create
- `frontend/` Vite React-TS project: `package.json`, `index.html`, `vite.config.ts` (`/api` proxy to http://localhost:5080,
  dev port 5173 with `strictPort`), `tsconfig*.json` (strict), `.gitignore` (node_modules, dist)
- `src/api/client.ts` (fetch wrapper, `ApiError`), `src/api/types.ts`, `src/api/rooms.ts` (`listRooms(filters)`)
- `src/lib/equipment.ts` (wire value -> label, list of all values)
- `src/components/Layout.tsx` (AppBar + nav + page container), `src/components/ErrorAlert.tsx` (renders each error as
  `CODE -- message`)
- `src/pages/RoomsPage.tsx`, `src/App.tsx` (router), `src/main.tsx` (theme, router, `CssBaseline`)

## New dependencies
Runtime: react, react-dom, react-router-dom, @mui/material, @emotion/react, @emotion/styled.
Dev: typescript, vite, @vitejs/plugin-react, @types/react, @types/react-dom.
(Installing packages needs explicit approval.)

## Rules for implementation
1. Strict TypeScript, no `any`, no `@ts-ignore`.
2. Every API failure is shown through `ErrorAlert` using the server's code and message, never swallowed or logged only.
3. Loading and empty states are handled on every table (progress indicator while loading; "No rooms match these
   filters." when the list is empty). A failed request keeps the filters visible so the user can retry or change them.
4. No rule logic or filtering in the front end (see project CLAUDE.md warnings).
5. CORS: the backend already allows http://localhost:5173 (`Cors:AllowedOrigins`, Spec 01); no backend change is needed.
6. No authentication, no environment-specific URLs in code; the API base is the relative `/api`.
7. Do not add the Users, Reservations, Calendar or Report links or pages; they belong to Specs 09-12.

## Definition of done
- [x] `npm run build` passes with no type errors.
- [x] Rooms page lists the seeded rooms, including inactive ones, ordered by floor then name.
- [x] Each filter changes the request and the result; "Clear filters" restores the full list.
- [x] A filter combination with no match shows the empty message.
- [x] Stopping the backend shows a readable `NETWORK_ERROR` alert, not a blank page; restarting it and changing a filter recovers.
- [x] `/` and unknown routes land on `/rooms`.
