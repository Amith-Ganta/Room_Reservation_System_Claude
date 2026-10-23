# Spec 08 -- Frontend Scaffold and Rooms Page

## Overview
Create the React + Vite + TypeScript + MUI app, the typed API client, the app shell (navigation) and the
Rooms page with its three filters.

## Depends on
Spec 03 (rooms API). Specs 05-07 are needed by later frontend specs.

## Pages / routes
| Route | Page |
|-------|------|
| `/` | redirect to `/rooms` |
| `/rooms` | Rooms table + filters |

Rooms table columns: name, id, capacity, floor, equipment (chips), status (Active / Inactive chip).
Filters: minimum capacity (number), equipment (multi-select of the four values), floor (number). Filters call the API; the
client does no filtering itself.

## Files to create
- `frontend/` Vite React-TS project (`package.json`, `vite.config.ts` with `/api` proxy to http://localhost:5080, `tsconfig*.json`)
- `src/api/client.ts` (fetch wrapper that throws `ApiError { status, errors[] }`), `src/api/types.ts`, `src/api/rooms.ts`
- `src/components/Layout.tsx` (AppBar + nav), `ErrorAlert.tsx` (renders `code: message` list)
- `src/pages/RoomsPage.tsx`, `src/App.tsx`, `src/main.tsx`

## New dependencies
react, react-dom, react-router-dom, @mui/material, @emotion/react, @emotion/styled, typescript, vite.

## Rules for implementation
1. Strict TypeScript, no `any`.
2. Every API failure is shown through `ErrorAlert` using the server's code and message.
3. Loading and empty states are handled on every table.
4. Backend CORS already allows http://localhost:5173 (Spec 01).

## Definition of done
- [ ] `npm run build` passes with no type errors.
- [ ] Rooms page lists seeded rooms; each filter changes the request and the result.
- [ ] Stopping the backend shows a readable error, not a blank page.
