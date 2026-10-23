# Spec 07 -- Usage Report ("often booked, rarely used")

## Overview
Answers the task's key question: which rooms are booked often but rarely used? Compares planned hours with
hours actually used per room, and flags rooms with many bookings and low utilization.

## Depends on
Spec 05.

## Endpoints
| Method | Path | Description |
|--------|------|-------------|
| GET | /api/reports/usage | Per-room usage; optional `from`, `to` |

## Calculation (backend only)
Only resolved reservations (Completed or Cancelled) are judged; upcoming ones are counted but not judged.
- `plannedHours` = sum of `End - Start` over completed + cancelled.
- `usedHours` = sum over completed of `max(0, (EndedAt ?? End) - Start)`; cancelled count as 0.
- `utilizationPercent` = `used / planned * 100`, 1 decimal, null when nothing is planned.
- `isOftenBookedRarelyUsed` = resolved count >= `MinBookingsToFlag` and utilization < `UnderusedThresholdPercent`.
- Sorted: flagged first, then total bookings descending, then name.

## Response
`generatedAt`, `from`, `to`, the two thresholds, and `rooms[]` with roomId, roomName, floor, totalBookings,
completedBookings, cancelledBookings, upcomingBookings, plannedHours, usedHours, utilizationPercent, isOftenBookedRarelyUsed.

## Files to create
`Dtos/UsageReportDtos.cs`, `Services/UsageReportService.cs`, `Controllers/ReportsController.cs`, tests.

## Rules for implementation
1. Thresholds come from `IOptions<UsageReportOptions>`.
2. No calculation in the controller or the front end; the front end only displays and exports.
3. Rooms with no bookings still appear (utilization null, not flagged).

## Definition of done
- [ ] `dotnet build` and `dotnet test` pass.
- [ ] Tests: flagged vs not flagged around the threshold, cancelled counts as unused, ended-early lowers utilization, date range filter, no-bookings room, ordering.
- [ ] Seeded data produces at least one flagged room.
