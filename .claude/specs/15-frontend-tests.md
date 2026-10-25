# Spec 15 -- Front-end Tests

## Overview
The front end has no automated tests today; only `npm run build` checks it. This spec adds a test runner (vitest with
Testing Library) and a first set of tests for the code that carries behaviour: the API client, the paging helper, the
small helper modules and the shared components. No screen or API behaviour changes.

## Depends on
Specs 08 to 12 (front end) and 14 (paging helper and `ListPager`).

## Endpoints / Pages
None. No route and no API call changes.

## Data changes
None.

## Files to change
- `frontend/package.json`: dev dependencies `vitest`, `jsdom`, `@testing-library/react`, `@testing-library/dom`,
  `@testing-library/jest-dom`, `@testing-library/user-event`; scripts `test` (single run) and `test:watch`.
- `frontend/vite.config.ts`: a `test` block (jsdom environment, setup file, globals off).
- `frontend/src/test/setup.ts`: loads the jest-dom matchers and cleans up after each test.
- Tests, next to the code they cover (`*.test.ts` / `*.test.tsx`):
  - `api/client.test.ts`: success, `expect` shape check, API error body, gateway statuses, network failure, abort.
  - `api/paging.test.ts`: `fetchAllPages` (several pages, empty page, bad `totalPages`, page cap, signal) and `setPageParams`.
  - `lib/dateTime.test.ts`, `lib/errors.test.ts`, `lib/reportFormat.test.ts`, `lib/equipment.test.ts`.
  - `components/ErrorAlert.test.tsx`, `ConfirmActionDialog.test.tsx`, `ErrorBoundary.test.tsx`, `ListPager.test.tsx`.
- `README.md` and `DEVELOPMENT_GUIDE.md`: replace the "no front-end tests" limitation with how to run them, and name
  the four tested components.
- `frontend/package.json`: an `engines` field (`node >=22.22.2`), because the test tools need a current Node.
- `CLAUDE.md`, `DEVELOPMENT_GUIDE.md` and `.claude/settings.json`: let subagents run `npm test` (Subagent Policy and
  allow list).

## Rules
- Tests only. No production code changes, except what a test cannot reach without it (none expected).
- Tests never call the real API: `fetch` is replaced in the test.
- Tests do not depend on the time zone or the locale of the machine (compare against values built with the same
  `Date` calls, never against a hard-coded local string).
- No booking rule or report number is calculated in a test helper; expected values are written out.

## Acceptance
- `npm test` in `frontend` runs once and passes.
- `npm run build` still passes.
- `dotnet test` is unaffected.
