# Spec 05 -- Reservations API

## Overview
Create, list, read, edit, cancel and end-early for reservations. The rules come from Spec 04 (`IReservationValidator`);
this spec adds the service that orchestrates them, the static write lock that makes check-then-insert safe under
concurrency, and the HTTP layer.

## Depends on
Specs 03 and 04.

## Endpoints
| Method | Path | Success | Failure |
|--------|------|---------|---------|
| GET | /api/reservations | 200 list | 400 `INVALID_REQUEST` (bad filter) |
| GET | /api/reservations/{id} | 200 | 404 `RESERVATION_NOT_FOUND`, 400 (id is not a GUID) |
| POST | /api/reservations | 201 + `Location` | 422 basic errors or R1-R7, 404 `ROOM_NOT_FOUND` / `USER_NOT_FOUND`, 400 |
| PUT | /api/reservations/{id} | 200 | 404, 409 (state), 422 (rules), 400 |
| POST | /api/reservations/{id}/cancel | 200 | 404, 409 |
| POST | /api/reservations/{id}/end | 200 | 404, 409 |

`{id}` is declared as a `Guid` parameter without a route constraint, so a malformed id gives the standard 400 body
instead of an empty 404.

### List query
`roomId`, `userId`, `status`, `from`, `to`. All optional and AND-combined. The reservation must overlap the half-open
window `[from, to)` (this is already implemented by `IReservationRepository.ListAsync`; the service only passes the filter).
- `status`: `Booked`, `Completed` or `Cancelled`, case-insensitive. A number (`status=1`) or any other text is 400
  `INVALID_REQUEST`. Do not rely on default enum binding: it accepts numbers.
- `from` later than `to` -> 400 `INVALID_REQUEST` (equal values are allowed and give an empty window).
- An unknown `roomId` / `userId` is not an error: the list is simply empty.
- Order: newest start first (as the repository returns it).
- No paging in this project (the rooms list has none either). The data set is small and in memory.

## Request and response
Create and edit use the same body (the `CreateReservationRequest` record from Spec 04):
```json
{ "roomId": "guid", "userId": "guid", "title": "Sprint planning",
  "start": "2026-11-02T09:00:00Z", "end": "2026-11-02T10:00:00Z", "attendeeCount": 6 }
```
Response (`ReservationDto`):
```json
{ "id": "guid", "roomId": "guid", "roomName": "Atlas", "userId": "guid", "organizerName": "Ada Lovelace",
  "title": "Sprint planning", "start": "...", "end": "...", "endedAt": null,
  "attendeeCount": 6, "status": "Booked" }
```
- `organizerName` is `"{FirstName} {LastName}"`. `status` is a string, never a number (the global enum converter does this).
- `endedAt` is null until the meeting is ended early. `end` is the planned end and is never rewritten.
- Errors: `{ "errors": [ { "code", "message" } ] }`. The front end shows `message` as plain text.
- Timestamps must carry an offset (`Z` or `+hh:mm`), in the body and in the query. A value without an offset must give
  400 `INVALID_REQUEST`, not be read as server-local time. A test must prove the actual behaviour; if the framework
  accepts offset-less values silently, add a small converter/validation to reject them.
- A missing `roomId` or `userId` arrives as `Guid.Empty` and gives 404 `ROOM_NOT_FOUND` / `USER_NOT_FOUND`.
  A missing `title` is `null` and gives 422 `TITLE_REQUIRED` from the validator. An empty or unreadable body is 400.

## Service flow
All decisions live in `ReservationService`; the controller only binds and maps the `Result` to HTTP.

**Create** (inside the write lock):
1. Complete elapsed reservations.
2. Load the room: missing -> `NotFound` `ROOM_NOT_FOUND`. Then load the organizer: missing -> `NotFound` `USER_NOT_FOUND`.
   (Room is checked first; only one not-found error is returned.)
3. Run `IReservationValidator.ValidateAsync(request, room)`. Any errors -> `Invalid(errors)`, nothing is written.
4. Insert with status `Booked`, `EndedAt` null; return the `ReservationDto` (201).

**Edit** (inside the write lock):
1. Complete elapsed; load the reservation with details: missing -> 404 `RESERVATION_NOT_FOUND`.
2. State check first (409): not `Booked` -> `RESERVATION_NOT_BOOKED`; `Booked` but `Start <= now` -> `RESERVATION_ALREADY_STARTED`.
3. Load the (possibly new) room and organizer -> 404 as in create.
4. Validate with `excludeReservationId = id` so the reservation does not block itself. Errors -> 422.
5. Update room, organizer, title, start, end, attendees. Status stays `Booked`. The room and the organizer may change.
   Update the tracked entity and its navigation properties so the response shows the new room and organizer names.

**Cancel** (inside the write lock): 404 if missing; not `Booked` -> 409 `RESERVATION_NOT_BOOKED`; `Booked` with
`Start <= now` -> 409 `RESERVATION_ALREADY_STARTED` (a running meeting is ended, not cancelled). Otherwise status
`Cancelled`; `EndedAt` stays null.

**End early** (inside the write lock): 404 if missing; not `Booked` -> 409 `RESERVATION_NOT_BOOKED`; `Booked` with
`Start > now` -> 409 `RESERVATION_NOT_STARTED`. Otherwise `EndedAt = now`, status `Completed`; planned `End` unchanged.
After the elapsed pass a `Booked` reservation with `Start <= now` is always still running, so no other case remains.

**Get / list** (no lock): complete elapsed, then read. Completion is idempotent, so concurrent reads are safe.

"Started" means `Start <= now`, which matches R4 (a start equal to now is not in the future).
Second call of cancel or end returns 409, never 500.

## State rules at a glance
| Status | Start vs. now | Edit | Cancel | End early |
|--------|---------------|------|--------|-----------|
| Booked | in the future | yes | yes | 409 `NOT_STARTED` |
| Booked | started, not elapsed | 409 `ALREADY_STARTED` | 409 `ALREADY_STARTED` | yes |
| Completed (elapsed or ended early) | any | 409 `NOT_BOOKED` | 409 `NOT_BOOKED` | 409 `NOT_BOOKED` |
| Cancelled | any | 409 `NOT_BOOKED` | 409 `NOT_BOOKED` | 409 `NOT_BOOKED` |

## Files to create or change
- `Services/IReservationService.cs` + `Services/ReservationService.cs` (interface and class in the same style as `RoomService`).
- `Controllers/ReservationsController.cs`.
- `Controllers/ApiControllerBase.cs`: add a created-result helper for the 201 + `Location` response; keep one place for Result -> HTTP.
- `Dtos/ReservationDtos.cs`: add `ReservationDto`. `Dtos/ReservationListQuery.cs`: query class with the `status` and
  `from <= to` checks (data annotations / `IValidatableObject`, same pattern as `RoomListQuery`).
- `Mapping/MappingProfile.cs`: `Reservation -> ReservationDto`, `ReservationListQuery -> ReservationFilter`.
- `Program.cs`: register `IReservationService` as scoped.
- Tests: `Services/ReservationServiceTests.cs`, `Controllers/ReservationsEndpointTests.cs`, mapping tests for the new maps
  (`AssertConfigurationIsValid` must still pass).
- `CLAUDE.md`: spec 05 shown as in progress while the PR is open.

`ReservationLifecycle` stays a concrete scoped class (as shipped in Spec 04); there is no `IReservationLifecycle`.

## New dependencies
None.

## Rules for implementation
1. One `private static readonly SemaphoreSlim(1,1)` in `ReservationService`, shared by every instance. Every write
   (create, edit, cancel, end early) takes it with `await WaitAsync(ct)` and releases it in `finally`. Validation and
   save happen inside it, so two parallel bookings cannot both pass R2/R3.
2. Controllers are thin: bind, call the service, return. No `if` on business state in the controller.
3. Services return `Result<T>` for every expected failure and never throw for them.
4. "Now" comes from the injected `TimeProvider`; the limits come from the options. No literals (7, 90).
5. The room passed to the validator must be the room named by `request.RoomId` (`room.Id == request.RoomId`).
6. Mutate only entities loaded with `GetByIdAsync` / `GetWithDetailsAsync`; never pass an `AsNoTracking` list item to `UpdateAsync`.
7. The front end must render `errors[].message` as plain text (note for Spec 10; never `dangerouslySetInnerHTML`).
8. Swagger: `ProducesResponseType` for every status the endpoint can return, using `ApiErrorResponse` for failures.

## Definition of done
- [x] `dotnet build` and `dotnet test` pass.
- [x] Endpoint test per rule R1-R7 asserting HTTP status (422), code and message; a request that breaks several rules returns all codes together in the documented order.
- [x] Basic input errors (end <= start, blank title, title too long, attendees < 1) return 422 alone.
- [x] Create: 201, `Location` header, response shape and string status; unknown room and unknown user -> 404 with the right code; nothing is stored on failure.
- [x] Concurrency test: two parallel identical bookings -> exactly one 201 and one 422 `R2_ROOM_OVERLAP`; also two parallel bookings for the same organizer in different rooms -> one 201 and one 422 `R3_USER_OVERLAP`.
- [x] Edit: success (including moving to a free slot and keeping the same slot), rules re-checked with the reservation excluded from its own overlap, room/organizer change reflected in the response, 409 for started / completed / cancelled, 404 for unknown id.
- [x] Cancel and end early with `FakeTimeProvider`: every row of the state table above (success and 409), second call -> 409, a cancelled slot can be booked again, an ended-early slot is free from `EndedAt`, `End` is unchanged after end early.
- [x] Lazy completion: an elapsed `Booked` reservation shows `Completed` in the list and in get-by-id without any other call.
- [x] Each list filter tested alone and combined (room, user, status, from, to), including the half-open window edges, case-insensitive `status`, and the 400 cases (`status=1`, `status=abc`, `from > to`, bad GUID, offset-less timestamp).
- [x] Malformed JSON, an empty body and a malformed `{id}` give the standard 400 body without framework internals.
- [x] Mapping tests pass; Swagger lists all six endpoints.
