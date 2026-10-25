# Room Reservation System (Claude SDK build)

## Project Overview

IDT internship task: a room reservation system. Built spec by spec (see `.claude/specs/`), one PR per spec,
each PR reviewed by GitHub Copilot and fixed in a follow-up PR before the next spec starts.

- Backend: ASP.NET Core 8 Web API (port 5080), EF Core InMemory, AutoMapper, xUnit + FluentAssertions.
- Frontend: React + Vite + TypeScript + MUI, FullCalendar, jsPDF (port 5173, `/api` proxied to 5080).
- GitHub: https://github.com/Amith-Ganta/Room_Reservation_System_Claude
- The parent `Internship/` folder holds an older build of the same task. It is reference only: never edit it.

## Architecture

```
backend/
  RoomReservation.sln
  RoomReservation.Api/
    Common/          Result<T>, ErrorCodes, ApiErrorResponse
    Configuration/   BookingRulesOptions, UsageReportOptions, PagingOptions
    Controllers/     thin: bind, call service, map Result to HTTP
    Data/            AppDbContext, DbSeeder
    Domain/          Room, User, Reservation, enums, filters
    Dtos/            request/response shapes
    Mapping/         AutoMapper MappingProfile
    Repositories/    IRepository<T> + per-entity repositories
    Services/        business logic (+ IReservationValidator, ReservationLifecycle)
  RoomReservation.Tests/
frontend/            React + TS app (src/api, src/pages, src/components, src/lib)
.claude/             specs, commands, agents, settings
```

## Spec-driven workflow

1. `/create-spec <slug>`: writes `.claude/specs/NN-slug.md`, branch `feature/<slug>` from updated `main`.
2. Implement the slice, `/test-feature NN`, `/code-review-feature NN`.
3. `/ship-feature NN`: commit, push, PR, request Copilot review (each step asks for approval).
4. `/fix-copilot-review <PR>`: triage Copilot comments, fix on `fix/copilot-pr<N>-review`, PR, merge.
5. Next spec.

## Spec status

| Spec | Name | Status |
|------|------|--------|
| 01 | project-setup | merged |
| 02 | domain-data-seeder | merged |
| 03 | rooms-api | merged |
| 04 | booking-rules | merged |
| 05 | reservations-api | merged |
| 06 | users-api | merged |
| 07 | usage-report | merged |
| 08 | frontend-scaffold-rooms | merged |
| 09 | users-page | merged |
| 10 | reservations-page | merged |
| 11 | calendar-view | merged |
| 12 | report-pdf | merged |
| 13 | docs-readme | merged |
| 14 | list-paging | merged |
| 15 | frontend-tests | merged |
| 16 | xunit-v3-migration | merged |

Do not implement a stub unless the active task targets that spec. Update this table when a spec is merged.

## Booking rules (all validated in the backend)

| Rule | Code | Detail |
|------|------|--------|
| R1 | R1_MAX_DURATION | duration <= `BookingRules:MaxDurationDays` (7); exactly 7 days allowed |
| R2 | R2_ROOM_OVERLAP | no overlap with a non-cancelled reservation in the same room |
| R3 | R3_USER_OVERLAP | organizer has no overlapping non-cancelled reservation |
| R4 | R4_PAST_START | start must be in the future (start == now rejected) |
| R5 | R5_CAPACITY_EXCEEDED | attendees <= room capacity |
| R6 | R6_BEYOND_HORIZON | start <= now + `BookingRules:MaxHorizonDays` (90) |
| R7 | R7_ROOM_INACTIVE | room must be active |

Cancelled reservations never block. Ended-early reservations free the room from `EndedAt`.
Error body: `{ "errors": [ { "code", "message" } ] }`. HTTP: Invalid 422, NotFound 404, Conflict 409, bad model 400 `INVALID_REQUEST`.

## Subagent Policy

Subagents MUST NOT: push, merge, commit, run `dotnet publish`, install packages without confirmation, or edit files outside their remit.
Subagents MAY: read any file, run `dotnet build` / `dotnet test` / `npm test` / `npm run build`, write tests under `backend/RoomReservation.Tests/`.
Reviewers and the test runner are read-only.

## Warnings

- NEVER hard-code 7 or 90 (or the report thresholds): use the options classes.
- NEVER throw from a service for an expected failure: return `Result<T>`.
- NEVER use `DateTime.Now` / `UtcNow`: inject `TimeProvider`; use `DateTimeOffset`.
- NEVER add `[Authorize]`: no auth in this project. NEVER add EF migrations.
- NEVER calculate rules or report numbers in the front end.
- The write `SemaphoreSlim` must stay static (one lock across all service instances).
- NEVER run `git push`, merge a PR or commit without explicit user approval.
