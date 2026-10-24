# Spec 14 -- List Paging

## Overview
The three list endpoints (`GET /api/rooms`, `GET /api/users`, `GET /api/reservations`) return every row today. This
spec adds server-side paging to all three, so a list never grows without a limit. A list response becomes an
envelope with the items and the paging numbers. The default page size and the largest allowed page size are
configuration, not code. The three list pages show a table pager. The calendar, the booking form drop-downs and the
usage report still need every row, so they keep working through a helper that reads all pages.

## Depends on
Specs 03, 05, 06 (list endpoints), 07 (usage report, must keep seeing every room) and 08 to 12 (front end).

## Endpoints / Pages
All three list endpoints accept two new optional query parameters:

| Parameter | Meaning | Rule |
|-----------|---------|------|
| `page` | 1-based page number | integer from 1 to 100000, default 1. Anything else gives HTTP 400 `INVALID_REQUEST`. |
| `pageSize` | rows per page | integer 1 or more, default `Paging:DefaultPageSize`. A value above `Paging:MaxPageSize` is **clamped** to the maximum (no error). 0, negative or not a number gives HTTP 400. |

Response (HTTP 200) for the three endpoints:
```json
{ "items": [ ], "page": 1, "pageSize": 20, "totalCount": 45, "totalPages": 3 }
```
- `items` is exactly what the endpoint returned before (same shape, same order, same filters).
- `pageSize` in the response is the size that was actually used (after clamping).
- `totalCount` is the number of rows that match the filters, over all pages. `totalPages` is `ceil(totalCount /
  pageSize)`, and 0 when there is no row.
- A page past the end gives HTTP 200 with an empty `items` list and the real `totalCount`.
- Order is unchanged and stable, so pages never repeat or skip a row: rooms by floor then name, users by last name
  then first name, reservations newest start first (ties broken by id).
- Filters are applied before paging. `GET /api/users` has no filters.

Front end:

| Route | Change |
|-------|--------|
| `/rooms`, `/users`, `/reservations` | Table pager (MUI `TablePagination`) under the table: page, rows per page, total. Changing a filter returns to page 1. |
| `/calendar`, booking form | No pager. They read all pages through `fetchAllPages`. |
| `/report` | No change (the report endpoint is not a list and is not paged). |

## Data changes
None. No entity, seed or migration change.

## Files to change
Back end:
- `Dtos/RoomListQuery.cs`, `Dtos/ReservationListQuery.cs`: derive from the new `PageQuery`.
- `Repositories/RoomRepository.cs`, `UserRepository.cs`, `ReservationRepository.cs`: add `ListPagedAsync`.
  `RoomRepository.ListAsync(filter)` (unpaged) stays because the usage report needs every room.
  `ReservationRepository.ListAsync(filter)` is replaced by `ListPagedAsync` (nothing needs it unpaged).
- `Services/RoomService.cs`, `UserService.cs`, `ReservationService.cs`, `IReservationService.cs`: take
  `IOptions<PagingOptions>`, return `Result<PagedResult<T>>`.
- `Controllers/RoomsController.cs`, `UsersController.cs`, `ReservationsController.cs`: bind the query, document the
  new response type.
- `Program.cs`: register `PagingOptions` (same pattern as the other options). `appsettings.json`: `Paging` section.

Front end: `src/api/types.ts`, `rooms.ts`, `users.ts`, `reservations.ts`, the load logic of `RoomsPage`, `UsersPage`,
`ReservationsPage`, `CalendarPage`.

Docs: `README.md` (endpoint tables, configuration table), `DEVELOPMENT_GUIDE.md` (spec row), `CLAUDE.md` (spec row,
`Configuration/` list).

## Files to create
- `Configuration/PagingOptions.cs`: `SectionName = "Paging"`, `DefaultPageSize`, `MaxPageSize` (no defaults in code,
  `[Range]` on both, and a check that the default is not larger than the maximum).
- `Dtos/PageQuery.cs`: base class with `Page` and `PageSize`, and `ToRequest(PagingOptions)`.
- `Dtos/UserListQuery.cs`: only the paging parameters.
- `Dtos/PagedResult.cs`: the envelope record.
- `Domain/PageRequest.cs`: `PageRequest` and `PagedItems<T>`.
- `Repositories/PagingExtensions.cs`: `ToPagedAsync` (count, skip, take).
- `src/api/paging.ts`: `fetchAllPages`.
- Tests under `backend/RoomReservation.Tests/` (see Definition of done).

## New dependencies
None.

## Rules for implementation
1. Page size defaults and the maximum come from `PagingOptions`. No number such as 20 or 100 appears in code.
2. Paging happens in the database query (`Skip`/`Take` after the filters and the order), never by loading all rows
   and slicing them in memory.
3. The order must be total, so pages are stable. Add `Id` as the last sort key where it is missing.
4. Expected failures stay `Result<T>`. Bad `page` / `pageSize` values are rejected by model validation (HTTP 400,
   the existing `INVALID_REQUEST` body). Too large a `pageSize` is clamped, not an error.
5. The usage report keeps reading every room through the unpaged repository method.
6. Controllers stay thin: bind, call the service, map the `Result`.
7. The front end shows the numbers the API returns; it never computes totals or pages itself.
8. `fetchAllPages` follows `totalPages` from the response, passes the `AbortSignal` to every request, stops at the
   first error and shows it through `ErrorAlert`.
9. Strict TypeScript, no `any`. All HTTP goes through `src/api/client.ts`.
10. No new package. No `[Authorize]`, no migration.

## Definition of done
- [ ] `dotnet build` has no new warning and `dotnet test` passes.
- [ ] Tests cover: envelope shape on all three endpoints; default page size from configuration; `pageSize` above the
      maximum is clamped; `page=0`, `pageSize=0`, negative and non-numeric values give 400; a page past the end is an
      empty 200 with the right `totalCount`; filters change `totalCount`; pages do not overlap and together contain
      every row; the usage report still lists every room even when rooms outnumber one page.
- [ ] `npm run build` passes.
- [ ] `/rooms`, `/users` and `/reservations` show a pager and change page without a full reload; a filter change
      returns to page 1.
- [ ] The calendar and the booking form still show every room and user.
- [ ] README, DEVELOPMENT_GUIDE and CLAUDE.md are updated.
