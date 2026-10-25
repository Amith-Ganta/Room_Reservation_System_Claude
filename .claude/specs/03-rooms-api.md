# Spec 03 -- Rooms API

## Overview
Read-only rooms endpoint with the three filters from the task: minimum capacity, equipment and floor.
Introduces the DTO + AutoMapper + service + thin controller pattern that every later API spec follows.

## Depends on
Spec 02.

## Endpoints
| Method | Path | Description |
|--------|------|-------------|
| GET | /api/rooms | List rooms, optionally filtered |

Query: `minCapacity` (int), `equipment` (comma-separated, e.g. `Projector,Whiteboard`; all must be present), `floor` (int).

## Response (`RoomDto`)
```json
{ "id": "guid", "name": "Atlas", "capacity": 12, "floor": 2,
  "equipment": ["Projector", "Whiteboard"], "isActive": true }
```
Equipment is a list of names, never a flags integer. Rooms are ordered by floor, then name.

## Files to create
`Dtos/RoomDto.cs`, `Mapping/MappingProfile.cs` (Room map), `Services/RoomService.cs`,
`Controllers/ApiControllerBase.cs` (Result -> HTTP: Invalid 422, NotFound 404, Conflict 409), `Controllers/RoomsController.cs`,
tests: service, mapping (`AssertConfigurationIsValid`), endpoint tests via `WebApplicationFactory`.

## New dependencies
AutoMapper 14.0.0.

## Rules for implementation
1. Controller has no logic: bind filter, call service, return.
2. Unknown equipment name (numbers and `None` included), a negative `minCapacity` or a non-numeric value -> 400 `INVALID_REQUEST`,
   raised by model validation on the query class, before the service runs. `floor` may be any integer (0 = ground, negative = basement).
   Equipment names are case-insensitive; repeated names are allowed.
3. Inactive rooms are returned (the UI shows them); booking is blocked later by R7.

## Definition of done
- [x] `dotnet build` and `dotnet test` pass.
- [x] Endpoint tests for every filter and for the combination of filters.
- [x] Mapping profile configuration test passes.
- [x] Swagger lists `GET /api/rooms` with the query parameters.
