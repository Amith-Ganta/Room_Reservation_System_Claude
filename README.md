# Room Reservation System

Internship technical task: a room reservation system with an ASP.NET Core 8 API and a React + TypeScript front end.

The project is built spec by spec. Each spec in [`.claude/specs/`](.claude/specs) ships as its own pull request,
is reviewed by GitHub Copilot, and is fixed in a follow-up PR before the next spec starts.

## Run the API

Requires the .NET 8 SDK.

```bash
cd backend
dotnet run --project RoomReservation.Api --launch-profile http
```

The API listens on http://localhost:5080 and Swagger UI is at http://localhost:5080/swagger (Development only).

## Run the tests

```bash
cd backend
dotnet test
```

## Configuration

All limits live in `backend/RoomReservation.Api/appsettings.json` and are validated on startup
(the app refuses to start if a value is out of range).

| Section | Key | Default |
|---------|-----|---------|
| `BookingRules` | `MaxDurationDays` | 7 |
| `BookingRules` | `MaxHorizonDays` | 90 |
| `UsageReport` | `MinBookingsToFlag` | 4 |
| `UsageReport` | `UnderusedThresholdPercent` | 50 |
| `Cors` | `AllowedOrigins` | `http://localhost:5173` |

The full README (requirements, architecture, front end) arrives with spec 13.
