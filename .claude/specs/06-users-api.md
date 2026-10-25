# Spec 06 -- Users API

## Overview
Read-only users endpoint for the Users page: each user's name and the number of their active reservations.
"Active" means status `Booked`. There is no background job, so elapsed meetings are completed first
(`ReservationLifecycle.CompleteElapsedAsync`), and only then are the Booked reservations counted. The count is
calculated in the back end; the front end only shows it.

## Depends on
Spec 05 (reservation repository and lifecycle).

## Endpoints
| Method | Path | Description |
|--------|------|-------------|
| GET | /api/users | All users with `activeReservationCount` |

No query parameters. The list is not paged (the task has no user filters).

## Response (`UserDto`)
```json
{ "id": "guid", "firstName": "Anna", "lastName": "Kovalenko", "activeReservationCount": 2 }
```
Users are ordered by last name, then first name (the order `UserRepository.ListAsync` already gives).
A user with no reservations is listed with `activeReservationCount: 0`.

## Files to create
`Dtos/UserDto.cs`, `Services/UserService.cs` (with `IUserService`), `Controllers/UsersController.cs`,
`Mapping/MappingProfile.cs` (User map), `Program.cs` (register `IUserService`),
tests: service, mapping (`AssertConfigurationIsValid` still passes), endpoint tests via `WebApplicationFactory`.

## New dependencies
None.

## Rules for implementation
1. Controller has no logic: call the service, return `ToActionResult`.
2. The service calls `ReservationLifecycle.CompleteElapsedAsync` first, then reads the users and the counts.
   It takes no write lock: completing elapsed reservations is idempotent, as in `ReservationService.ListAsync`.
3. Count with one grouped query (`IReservationRepository.CountActiveByUserAsync`), not one query per user (no N+1).
   The service looks each user up in the returned dictionary and uses 0 when the user is absent.
4. Cancelled and completed reservations are not counted. A Booked reservation whose end has passed stops
   counting once the clock moves past its end (the lifecycle pass completes it).
5. Map with AutoMapper. The count is not a property of `User`, so the service supplies it when mapping
   (for example through `ForCtorParam` with a resolution context item, or by mapping the user and then
   building the DTO with the count). Keep the mapping profile test green.
6. No hard-coded values; no `DateTime.Now`/`UtcNow` (the lifecycle already uses the injected `TimeProvider`).
   No `[Authorize]`, no migrations.
7. Return `Result<IReadOnlyList<UserDto>>.Ok(...)`. The service does not throw for an expected failure
   (there is none for this endpoint).

## Definition of done
- [x] `dotnet build` and `dotnet test` pass (baseline 210 tests).
- [x] Tests: count ignores Cancelled and Completed reservations, counts only Booked, an elapsed Booked
      reservation stops counting after the clock advances, a user with no reservations is listed with 0.
- [x] Tests: count is per organizer (two users with different numbers), ordering is last name then first name,
      an empty user table gives an empty list with 200.
- [x] Mapping profile configuration test passes.
- [x] Swagger lists `GET /api/users`.
