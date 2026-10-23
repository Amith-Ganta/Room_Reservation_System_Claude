# Spec 05 -- Reservations API

## Overview
Create, list, edit, cancel and end-early for reservations. Validation comes from Spec 04; this spec adds the
service that orchestrates it, the write lock that makes validate-then-insert safe under concurrency, and the HTTP layer.

## Depends on
Specs 03 and 04.

## Endpoints
| Method | Path | Success | Failure |
|--------|------|---------|---------|
| GET | /api/reservations | 200 list | |
| GET | /api/reservations/{id} | 200 | 404 `RESERVATION_NOT_FOUND` |
| POST | /api/reservations | 201 | 422 rule codes, 404 room/user |
| PUT | /api/reservations/{id} | 200 | 422, 404, 409 |
| POST | /api/reservations/{id}/cancel | 200 | 404, 409 |
| POST | /api/reservations/{id}/end | 200 | 404, 409 |

Query (list): `roomId`, `userId`, `status`, `from`, `to` (a reservation matches when it overlaps the range).

## State rules
- Edit and cancel: only `Booked` and not yet started (`NOT_BOOKED`, `ALREADY_STARTED`).
- End early: only `Booked` and already started (`NOT_STARTED`); sets `EndedAt = now`, status `Completed`, planned `End` unchanged.
- Elapsed reservations are completed lazily before every read/write (`IReservationLifecycle`), no background job.

## Request / response
```json
{ "roomId": "guid", "userId": "guid", "title": "Sprint planning",
  "start": "2026-11-02T09:00:00Z", "end": "2026-11-02T10:00:00Z", "attendeeCount": 6 }
```
Response adds `id`, `roomName`, `organizerName`, `status`, `endedAt`. Errors: `{ "errors": [ { "code", "message" } ] }`.

## Files to create
`Services/ReservationService.cs`, `Controllers/ReservationsController.cs`, DTO and mapping additions, tests (service + endpoint).

## Rules for implementation
1. A single static `SemaphoreSlim(1,1)` serialises validate+insert (create) and load+validate+update (edit).
2. Controllers are thin; all decisions live in the service.
3. Status serialised as a string.
4. Cancel/end are idempotency-safe: a second call returns 409, not 500.

## Definition of done
- [ ] `dotnet build` and `dotnet test` pass.
- [ ] Endpoint test per rule R1-R7 asserting HTTP status, code and message.
- [ ] Concurrency test: two parallel identical bookings -> exactly one 201 and one 422 `R2_ROOM_OVERLAP`.
- [ ] Cancel, end-early, edit and lazy-completion tests with `FakeTimeProvider`.
- [ ] Each list filter tested.
