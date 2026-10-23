# Spec 06 -- Users API

## Overview
Users list with each user's number of active reservations. "Active" means status `Booked`, so elapsed
meetings are completed lazily before counting.

## Depends on
Spec 05 (lifecycle and reservation repository).

## Endpoints
| Method | Path | Description |
|--------|------|-------------|
| GET | /api/users | All users with `activeReservationCount` |

## Response
```json
{ "id": "guid", "firstName": "Anna", "lastName": "Kovalenko", "activeReservationCount": 2 }
```

## Files to create
`Dtos/UserDto.cs`, mapping addition, `Services/UserService.cs`, `Controllers/UsersController.cs`, tests.

## Rules for implementation
1. Count with one grouped query (`CountActiveByUserAsync`), not one query per user (no N+1).
2. Cancelled and completed reservations are not counted.
3. Users with zero reservations are still listed, with 0.

## Definition of done
- [ ] `dotnet build` and `dotnet test` pass.
- [ ] Tests: count ignores cancelled/completed, counts only Booked, elapsed booking stops counting after the clock advances, zero-count user listed.
