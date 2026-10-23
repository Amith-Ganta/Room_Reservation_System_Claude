# Spec 01 -- Project Setup

## Overview
Create the .NET solution skeleton so every later spec lands on a building, tested, configurable base:
an ASP.NET Core 8 Web API, an xUnit test project, typed configuration for the two booking limits and the
usage-report thresholds, the shared `Result<T>` / error-code types, CORS for the Vite dev server, and Swagger.
No domain logic yet.

## Depends on
Nothing. First spec.

## Endpoints
None yet. The app must start and serve `/swagger`.

## Configuration (appsettings.json)
```json
{
  "BookingRules": { "MaxDurationDays": 7, "MaxHorizonDays": 90 },
  "UsageReport":  { "MinBookingsToFlag": 4, "UnderusedThresholdPercent": 50 },
  "Database":     { "Name": "RoomReservation" },
  "Seed":         { "Enabled": true },
  "Cors":         { "AllowedOrigins": ["http://localhost:5173"] }
}
```

## Files to create
- `backend/RoomReservation.sln`
- `backend/RoomReservation.Api/RoomReservation.Api.csproj`
- `backend/RoomReservation.Api/Program.cs` (controllers, Swagger, CORS, options, JSON string enums, `public partial class Program`)
- `backend/RoomReservation.Api/appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json` (port 5080)
- `backend/RoomReservation.Api/Configuration/BookingRulesOptions.cs`, `UsageReportOptions.cs`
- `backend/RoomReservation.Api/Common/Result.cs`, `ErrorCodes.cs`, `ApiErrorResponse.cs`
- `backend/RoomReservation.Tests/RoomReservation.Tests.csproj` and a smoke test (app starts, swagger JSON served, options bind)
- `.gitignore`, `README.md` (stub: how to run)

## New dependencies
Swashbuckle.AspNetCore 6.9.0. Test project: xUnit, FluentAssertions 6.12.2,
Microsoft.AspNetCore.Mvc.Testing 8.0.11, Microsoft.Extensions.TimeProvider.Testing.

## Rules for implementation
1. Options are validated with DataAnnotations and `ValidateOnStart()`; `MaxDurationDays` and `MaxHorizonDays` must be >= 1.
2. Never hard-code 7 or 90 anywhere except `appsettings.json`.
3. `Result<T>` has `Ok`, `Invalid`, `NotFound`, `Conflict`; services never throw for expected failures.
4. Error responses always use `{ "errors": [ { "code": "...", "message": "..." } ] }`.
5. Invalid model state returns 400 with code `INVALID_REQUEST` in the same body shape.
6. No authentication, no `[Authorize]`, no EF migrations.

## Definition of done
- [ ] `dotnet build` is clean (no warnings treated as errors).
- [ ] `dotnet test` passes the smoke tests.
- [ ] `dotnet run` serves Swagger on http://localhost:5080/swagger.
- [ ] Startup fails with a clear error if `MaxDurationDays` is 0 (tested).
