# Spec 04 -- Booking Rules R1-R7

## Overview
The heart of the task: a dedicated `IReservationValidator` that checks rules R1-R7 and returns every
violation with an error code and a readable message. Also adds the lazy lifecycle that completes elapsed
reservations. Still no HTTP endpoint; Spec 05 wires it into the API.

## Depends on
Specs 02 and 03.

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

Basic input errors (`INVALID_TIME_RANGE` when End <= Start, `TITLE_REQUIRED`, `INVALID_ATTENDEES`) are returned
before the rules. Back-to-back bookings (one ends when the next starts) are not overlaps. When editing, the
reservation being edited is excluded from R2/R3 via `excludeReservationId`.

## Files to create
`Services/IReservationValidator.cs`, `Services/ReservationValidator.cs`, `Services/ReservationLifecycle.cs`,
`Dtos/ReservationDtos.cs` (request only), `Common/ErrorCodes.cs` additions,
tests with `FakeTimeProvider`.

## Rules for implementation
1. Limits come from `IOptions<BookingRulesOptions>`; never literals.
2. The validator returns all violations at once (a list), so the UI can show them together.
3. The validator never throws and never writes to the database.
4. Messages quote the actual numbers, e.g. "A reservation can last at most 7 days."

## Definition of done
- [ ] `dotnet build` and `dotnet test` pass.
- [ ] Boundary tests: exactly 7 days allowed, 7 days + 1 s rejected, exactly 90 days allowed, 90 days + 1 s rejected, start == now rejected.
- [ ] Overlap tests: back-to-back allowed, partial overlap rejected, cancelled ignored, edit excludes itself, ended-early frees the room.
- [ ] Capacity and inactive-room tests; multiple violations returned together.
- [ ] Limits changed through options change the outcome (proves nothing is hard-coded).
