# Spec 17 -- CI Workflow

## Overview
Today the tests run only on the author's machine. This spec adds a GitHub Actions workflow that builds and tests the
back end and the front end on every pull request and on every push to `main`, on a clean Linux runner. The Windows
Defender problem described in the README does not apply there, so this is also the first place where plain
`dotnet test` runs for the xunit.v3 project. No production code, endpoint, rule or test changes.

## Depends on
Spec 15 (front-end tests) and spec 16 (xunit.v3 test project).

## Endpoints / Pages
None.

## Data changes
None.

## Files to create
- `global.json`: pins the .NET SDK to 8.0.x (`rollForward: latestFeature`). The first CI run showed that the Linux runner also has the .NET 10 SDK, which
  refuses `dotnet test` for an xunit.v3 project (the Microsoft.Testing.Platform VSTest error). Without the pin, CI and any machine with a newer SDK use the wrong one.
- `.github/workflows/ci.yml`: two independent jobs on `ubuntu-latest`, triggered by `push` to `main` and by `pull_request`.
  - `backend`: .NET SDK 8.0.x; `dotnet restore`, `dotnet build --configuration Release`, `dotnet test --no-build` on `backend/RoomReservation.sln`.
  - `frontend`: Node 22 with the npm cache; in `frontend`: `npm ci`, `npm run build` (strict type check and production build), `npm test`.
  - Workflow `permissions: contents: read` only, and a `concurrency` group that cancels an older run of the same ref.

## Files to change
- `README.md`: a CI status badge under the title, and the Testing section says that CI runs both suites.
- `DEVELOPMENT_GUIDE.md`: a row for this spec in the specs table, and a short "Continuous integration" note.
- `CLAUDE.md`: spec table row (after the merge, in a separate small commit) and the project overview line.

## New dependencies
None in the project. The workflow uses three GitHub actions, each pinned to a major version:
`actions/checkout@v4`, `actions/setup-dotnet@v4`, `actions/setup-node@v4`.

## Rules for implementation
- No secrets, no deployment, no publishing step. CI only builds and tests.
- Do not skip, retry or loosen a test to get a green run. A test that fails on Linux and passes on Windows is a finding
  (for example a file-path case or line-ending difference): report it and fix its cause.
- Keep the commands equal to the ones in the README (`dotnet test`, `npm run build`, `npm test`), so CI proves the
  documented steps.
- Do not add a code-coverage gate, a linter or a matrix of versions in this spec.
- Merge only after the workflow run on the pull request is green.

## Definition of done
- [x] `global.json` pins the SDK to 8.0.x and `dotnet test` still works with SDK 8.0.426.
- [x] `.github/workflows/ci.yml` exists and runs on `pull_request` and on `push` to `main`.
- [x] The workflow run on the pull request is green: the back-end job passes all 318 tests and the front-end job passes build and all 98 tests.
- [x] `README.md` shows the CI badge and `DEVELOPMENT_GUIDE.md` lists the spec.
- [x] No file under `backend/` or `frontend/` changed.
