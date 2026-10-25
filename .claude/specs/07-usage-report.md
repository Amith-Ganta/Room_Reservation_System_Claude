# Spec 07 -- Usage report

## Overview
The task asks: which rooms are booked often but rarely used? This endpoint answers it by comparing, per room,
the hours that were **planned** with the hours that were **actually used**. All numbers are calculated in the
back end. The front end (Spec 12) only shows them and exports them to PDF.

## Depends on
Spec 05 (reservation repository, lifecycle, `ListForReportAsync`) and Spec 03 (room repository).

## Endpoints
| Method | Path | Description |
|--------|------|-------------|
| GET | /api/reports/usage | Usage figures for every room, with the "often booked, rarely used" flag |

Query parameters (both optional, ISO 8601 timestamps with an offset, bound with `OffsetDateTimeBinder`):

| Name | Meaning |
|------|---------|
| `from` | Count only reservations that **start** on or after this instant |
| `to` | Count only reservations that **start** on or before this instant |

Each reservation belongs to the period in which it starts, so a long meeting is never counted in two reports.
`from == to` is allowed (a single instant). `from > to` is rejected by model validation with HTTP 400
`INVALID_REQUEST`, the same as `GET /api/reservations`. The report is not paged.

## Calculation (back end only)
For each room, over the reservations in the window:

| Figure | Definition |
|--------|-----------|
| `totalBookings` | All reservations, any status |
| `completedBookings` | Status `Completed` |
| `cancelledBookings` | Status `Cancelled` |
| `upcomingBookings` | Status `Booked` (not finished yet: not started, or in progress) |
| `plannedHours` | Sum of `End - Start` over completed and cancelled reservations |
| `usedHours` | Sum over completed reservations of `(EndedAt ?? End) - Start`, clamped to `0 .. End - Start`. Cancelled reservations add 0 |
| `utilizationPercent` | `usedHours / plannedHours * 100`, or `null` when `plannedHours` is 0 |
| `isOftenBookedRarelyUsed` | `completedBookings + cancelledBookings >= MinBookingsToFlag` **and** `utilizationPercent < UnderusedThresholdPercent` |

Only **resolved** reservations (Completed or Cancelled) are judged. A Booked reservation is counted in
`totalBookings` and `upcomingBookings` but not in the hours, because its outcome is not known yet.

Rounding: `plannedHours` and `usedHours` to 2 decimals, `utilizationPercent` to 1 decimal, all with
`MidpointRounding.AwayFromZero`. The percentage and the flag are computed from the **unrounded** hours,
so rounding never moves a room across the threshold. The comparison is strict: a room at exactly
`UnderusedThresholdPercent` is not flagged; a room with exactly `MinBookingsToFlag` resolved bookings can be.

Thresholds come from `UsageReportOptions` (`MinBookingsToFlag` 4, `UnderusedThresholdPercent` 50 in `appsettings.json`).

## Response (`UsageReportDto`)
```json
{
  "generatedAt": "2026-06-01T10:00:00+00:00",
  "from": null,
  "to": null,
  "minBookingsToFlag": 4,
  "underusedThresholdPercent": 50,
  "rooms": [
    {
      "roomId": "guid", "roomName": "Boardroom", "floor": 3,
      "totalBookings": 7, "completedBookings": 2, "cancelledBookings": 4, "upcomingBookings": 1,
      "plannedHours": 12.0, "usedHours": 0.42, "utilizationPercent": 3.5,
      "isOftenBookedRarelyUsed": true
    }
  ]
}
```
`from` and `to` echo the query (null when not supplied). `generatedAt` is the injected `TimeProvider` clock.

Every room is listed, including inactive rooms and rooms with no reservations in the window (all counts 0,
hours 0, `utilizationPercent: null`, not flagged). Order: flagged rooms first, then `totalBookings` descending,
then room name.

## Files to create
`Dtos/UsageReportDtos.cs` (`UsageReportDto`, `RoomUsageDto`, `UsageReportQuery`),
`Services/UsageReportService.cs` (with `IUsageReportService`), `Controllers/ReportsController.cs`,
`Mapping/MappingProfile.cs` (query to `UsageReportFilter`), `Program.cs` (register `IUsageReportService`),
tests: service, mapping (`AssertConfigurationIsValid` still passes), endpoint tests via `WebApplicationFactory`.

## New dependencies
None. `IReservationRepository.ListForReportAsync` and `IRoomRepository.ListAsync` already exist.

## Rules for implementation
1. Controller has no logic: bind the query, call the service, return `ToActionResult`.
2. The service calls `ReservationLifecycle.CompleteElapsedAsync` first, so elapsed meetings are Completed before
   they are judged. It takes no write lock (the pass is idempotent, as in `UserService`).
3. Two queries, no N+1: all rooms (`IRoomRepository.ListAsync(new RoomFilter())`) and the windowed reservations
   (`ListForReportAsync`). Group the reservations by `RoomId` in memory.
4. Thresholds come from `IOptions<UsageReportOptions>`. Never hard-code 4, 50 or any other threshold.
5. Use `decimal` or `TimeSpan` arithmetic for hours; round only when building the DTO (see Calculation).
6. Use `TimeProvider` for `generatedAt`. No `DateTime.Now`/`UtcNow`. No `[Authorize]`, no migrations.
7. Map the query to `UsageReportFilter` with AutoMapper. The per-room figures are built in the service,
   because they are calculated values and not a property mapping.
8. Return `Result<UsageReportDto>.Ok(...)`. The service does not throw for an expected failure
   (there is none: bad input is already rejected by model validation).

## Definition of done
- [x] `dotnet build` and `dotnet test` pass (baseline 222 tests).
- [x] Tests: a room is flagged when it has enough resolved bookings and utilization is below the threshold;
      not flagged just at the threshold (exactly 50 %), with too few resolved bookings (3 of 4), or with
      exactly `MinBookingsToFlag` bookings and low use (flagged).
- [x] Tests: a cancelled booking counts as planned but unused; an ended-early meeting lowers utilization;
      a completed meeting without `EndedAt` counts fully; `EndedAt` before `Start` is clamped to 0.
- [x] Tests: Booked reservations are counted in `upcomingBookings` and `totalBookings` but not in the hours;
      an elapsed Booked reservation becomes Completed after the clock advances.
- [x] Tests: `from`/`to` filter by start (inclusive on both ends); `from > to` returns 400 `INVALID_REQUEST`.
- [x] Tests: a room without bookings is listed with zeros and `null` utilization; an inactive room is listed;
      ordering is flagged first, then total bookings descending, then name.
- [x] Tests: thresholds are read from options (a test with different option values changes the flag).
- [x] Endpoint test checks the JSON shape (field names, `generatedAt`, echoed `from`/`to`).
- [x] Seeded data (`DbSeeder`) gives exactly one flagged room: Boardroom (6 resolved, about 3.5 % used).
- [x] Mapping profile configuration test passes.
- [x] Swagger lists `GET /api/reports/usage`.
