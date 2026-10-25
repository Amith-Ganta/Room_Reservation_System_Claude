# Spec 04 -- Booking Rules R1-R7

## Overview
The heart of the task: a dedicated `IReservationValidator` that checks rules R1-R7 and returns every
violation with an error code and a readable message. Also adds the lazy lifecycle that completes elapsed
reservations. Still no HTTP endpoint; Spec 05 wires it into the API.

## Depends on
Specs 02 and 03 (repository overlap queries, `BookingRulesOptions`, `ErrorCodes`, `Result<T>`, `TimeProvider`).

## Rules and error codes
| Rule | Code | Check |
|------|------|-------|
| R1 | R1_MAX_DURATION | `End - Start <= MaxDurationDays` (exactly the limit is allowed) |
| R2 | R2_ROOM_OVERLAP | no overlapping non-cancelled reservation in the same room |
| R3 | R3_USER_OVERLAP | organizer has no overlapping non-cancelled reservation |
| R4 | R4_PAST_START | `Start > now` (start equal to now is rejected) |
| R5 | R5_CAPACITY_EXCEEDED | `AttendeeCount <= room.Capacity` |
| R6 | R6_BEYOND_HORIZON | `Start <= now + MaxHorizonDays` (exactly the limit is allowed) |
| R7 | R7_ROOM_INACTIVE | room must be active |

Basic input errors are returned instead of the rules, never together with them:

| Code | When |
|------|------|
| INVALID_TIME_RANGE | `End <= Start` |
| TITLE_REQUIRED | title is null, empty or whitespace |
| TITLE_TOO_LONG | title is longer than 200 characters (the stored maximum) |
| INVALID_ATTENDEES | `AttendeeCount < 1` |

If any basic error is found, the validator returns only the basic errors (all of them) and skips R1-R7, because
the rules are meaningless for a broken range. Otherwise it runs R1-R7 and returns every violation.

## Contract
```csharp
public interface IReservationValidator
{
    // Pure check: no writes, no throws. Empty list = valid.
    Task<IReadOnlyList<ApiError>> ValidateAsync(
        CreateReservationRequest request, Room room, Guid? excludeReservationId = null, CancellationToken ct = default);
}
```
- The caller (Spec 05 service) loads the room and the organizer first. A missing room or user is `ROOM_NOT_FOUND` /
  `USER_NOT_FOUND` (404) in the service, so the validator always receives a real `Room`.
- Errors come back in a fixed order: basic errors, or R1, R2, R3, R4, R5, R6, R7. Tests rely on this order.
- `excludeReservationId` removes one reservation from R2/R3 (used when a reservation is changed).
- `CreateReservationRequest(Guid RoomId, Guid UserId, string Title, DateTimeOffset Start, DateTimeOffset End, int AttendeeCount)`
  is a request DTO only. Times are compared as instants, so any offset is accepted.

## Interval semantics
- An interval is half-open `[start, end)`. Back-to-back bookings (one ends when the next starts) do not overlap.
- Cancelled reservations never block (R2/R3).
- A reservation ended early frees the room and the organizer from `EndedAt` (the repository query already does this).
- "Days" in R1/R6 are 24-hour spans (`TimeSpan.FromDays`); all values are UTC instants.

## Lifecycle
`ReservationLifecycle.CompleteElapsedAsync(ct)` calls `IReservationRepository.CompleteElapsedAsync(timeProvider.GetUtcNow())`.
It marks Booked reservations whose planned `End` has passed as Completed. It is idempotent. Spec 05+ calls it at the
start of every read that shows status or active counts (reservation list, users list, report). No background service.
Cancel and end-early transitions belong to Spec 05.

## Files to create
`Services/IReservationValidator.cs`, `Services/ReservationValidator.cs`, `Services/ReservationLifecycle.cs`,
`Dtos/ReservationDtos.cs` (request only), register both services as scoped in `Program.cs`,
tests with `FakeTimeProvider` (Services/ReservationValidatorTests.cs, Services/ReservationLifecycleTests.cs).
Error codes already exist in `Common/ErrorCodes.cs`; the review added `TITLE_TOO_LONG`, backed by `Reservation.TitleMaxLength` (200, shared with the EF mapping).

## Rules for implementation
1. Limits come from `IOptions<BookingRulesOptions>`; never literals (7, 90).
2. The validator returns all violations at once (a list), so the UI can show them together.
3. The validator never throws for expected failures and never writes to the database.
4. Messages quote the actual numbers, e.g. "A reservation can last at most 7 days." The number comes from the options.
5. "Now" comes from the injected `TimeProvider`; never `DateTime.Now` / `UtcNow`.
6. Overlap checks go through the repository methods that already exist; do not duplicate the interval logic.
7. The check-then-insert race (two requests passing R2/R3 at the same time) is closed by the static write lock in Spec 05, not here.

## Definition of done
- [x] `dotnet build` and `dotnet test` pass.
- [x] Boundary tests: exactly 7 days allowed, 7 days + 1 s rejected, exactly 90 days allowed, 90 days + 1 s rejected, start == now rejected, start 1 s after now allowed.
- [x] Overlap tests: back-to-back allowed, partial overlap rejected, cancelled ignored, edit excludes itself, ended-early frees the room, R3 mirrors R2 for the organizer.
- [x] Capacity (attendees == capacity allowed, +1 rejected) and inactive-room tests; multiple violations returned together in the documented order.
- [x] Basic input errors tested (end before start, end == start, blank title, 0 and negative attendees) and returned without R1-R7.
- [x] Limits changed through options change the outcome (proves nothing is hard-coded).
- [x] Lifecycle tests: elapsed Booked becomes Completed, future Booked and Cancelled are untouched, second run changes nothing.
