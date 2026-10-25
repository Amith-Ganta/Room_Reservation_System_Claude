# Spec 02 -- Domain, Data Layer and Seeder

## Overview
Define the entities (Room, User, Reservation), the EF Core InMemory `AppDbContext`, the repository layer
(generic `IRepository<T>` plus one repository per entity) and a seeder with realistic demo data. This is the
persistence foundation; no HTTP endpoints yet.

## Depends on
Spec 01.

## Domain
- `Room`: Id (Guid), Name, Capacity, Floor, Equipment (`[Flags] Equipment`: Projector, TvScreen, VideoConferencing, Whiteboard), IsActive.
- `User`: Id (Guid), FirstName, LastName.
- `Reservation`: Id, RoomId, UserId, Title, Start, End, AttendeeCount, Status (`Booked | Completed | Cancelled`), EndedAt (nullable, set by "end early").
- All times are `DateTimeOffset` (UTC).
- Filter records: `RoomFilter`, `ReservationFilter`, `UsageReportFilter`.

## Repositories
- `IRepository<T>`: GetByIdAsync, ListAsync, AddAsync, UpdateAsync.
- `IRoomRepository.ListAsync(RoomFilter)`: min capacity, equipment (all requested flags must be present), floor.
- `IReservationRepository`: filtered list (room, organizer, status, date range overlap), `GetWithDetailsAsync`,
  overlap queries for a room and for a user (ignore Cancelled, ignore a given reservation id, use `EndedAt ?? End`),
  `CountActiveByUserAsync`, `CompleteElapsedAsync(now)`, `ListForReportAsync`.
- `IUserRepository`: list ordered by last name, first name.

## Seeder
Seeds 8 rooms (varied capacity/floor/equipment, one inactive), 6 users and reservations relative to
`TimeProvider.GetUtcNow()`: past completed, one ended early, cancelled, today, future. Includes at least one room that is booked
often but rarely used (for Spec 07). Runs only when `Seed:Enabled` is true.

## Files to create
`Domain/*`, `Data/AppDbContext.cs`, `Data/DbSeeder.cs`, `Repositories/*`, DI registration in `Program.cs`,
and tests under `RoomReservation.Tests/Data` and `Repositories`.

## Rules for implementation
1. Repositories are thin: CRUD and queries only, no business rules.
2. Cancelled reservations never count as overlaps; a completed-early reservation frees the room from `EndedAt`.
3. Use the injected `TimeProvider`, never `DateTime.UtcNow`.
4. Test with a fresh InMemory database name per test.

## Definition of done
- [x] `dotnet build` and `dotnet test` pass.
- [x] Repository tests cover: each room filter, overlap edge (back-to-back is not an overlap), cancelled ignored, `EndedAt` respected, `CompleteElapsedAsync`.
- [x] Seeder test: expected counts, at least one inactive room, idempotent when run twice.
