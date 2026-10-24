# Spec 12 -- Usage Report Page and PDF Export

## Overview
A Report page that shows the per-room usage figures from Spec 07, highlights the rooms that are "often booked but
rarely used", and lets the user export the same report as a PDF (nice-to-have from the task). It adds no rule and no
number of its own: every figure, every flag and the order of the rooms come from `GET /api/reports/usage`, and the PDF
is built from the same response that is on the screen. This is the fifth page on the pattern from Specs 08-11 (API
client, `AbortController` loading, loading / empty / error states).

## Depends on
Specs 07 (usage report API) and 08 (front-end scaffold, `api/client.ts`, `Layout`, `ErrorAlert`,
`LoadingIndicator`).

## Endpoints / Pages
| Route | Page |
|-------|------|
| `/report` | Date-range filter + usage table + "Export PDF" |

API used:
| Call | Purpose |
|------|---------|
| `GET /api/reports/usage?from=&to=` | the report (both parameters optional) |

`from` and `to` are ISO 8601 timestamps with an offset. The API counts a reservation in the period in which it
**starts** (`from` and `to` both inclusive) and rejects `from > to` with HTTP 400 `INVALID_REQUEST`. The report is not
paged and lists every room, including inactive rooms and rooms with no reservations in the window.

Response (`UsageReportDto`), as typed in `api/types.ts`:
```
generatedAt: string                         // ISO 8601
from: string | null, to: string | null      // echo of the query
minBookingsToFlag: number
underusedThresholdPercent: number
rooms: RoomUsageDto[]                       // flagged first, then totalBookings desc, then name
RoomUsageDto: roomId, roomName, floor, totalBookings, completedBookings, cancelledBookings, upcomingBookings,
              plannedHours, usedHours, utilizationPercent (number | null), isOftenBookedRarelyUsed
```

### Page layout
- Heading "Usage report", then a short intro line: "Rooms booked often but used rarely." (no number in it).
- **Period** filter: two labelled date inputs, "From" and "To" (`type="date"`, local calendar days), and a "Clear"
  button. Both empty = all time (the default on first load). A change reloads the report.
  - The "From" day is sent as the start of that local day and the "To" day as the end of that local day
    (23:59:59.999), both converted with `toISOString()`, so a meeting that starts on the chosen "To" day is included.
    This is the only date logic on the page, and it is a conversion, not a rule.
  - The page does not compare the two days itself. If "From" is after "To", the API answers 400 `INVALID_REQUEST` and
    the error is shown with the other errors; the inputs keep their values.
- A **Thresholds** line built from the response, never from constants:
  "A room is flagged when it has at least {minBookingsToFlag} completed or cancelled bookings and used less than
  {underusedThresholdPercent}% of its planned hours." Below it, "Generated {generatedAt, local time}" and the period
  the API echoes back ("All dates", "From {date}", "To {date}" or "{date} to {date}").
- **Table** (one row per room, in the order the API sent; no client-side sorting): Room, Floor, Total, Completed,
  Cancelled, Upcoming, Planned hours, Used hours, Utilization, and a last column with the flag.
  - Numbers are right-aligned. Hours are shown with 2 decimals and utilization with 1 decimal plus "%"
    (`toFixed`, display only: the API already rounded them). A `null` utilization is shown as "n/a".
  - Flagged rooms (`isOftenBookedRarelyUsed`) get a `Chip` "Often booked, rarely used" in the warning colour and a
    light warning-tinted row background from the theme. The chip is text, so the colour is not the only signal.
  - Inactive rooms are not marked: the report DTO does not carry `isActive`, and the page does not look it up
    elsewhere.
- **Export PDF** button next to the filter. It is disabled while loading, after an error, and when the report has no
  rooms. It exports the report currently shown (see PDF content).
- States:
  - Loading: `LoadingIndicator` row inside the table; Export disabled.
  - Error: `ErrorAlert` (`CODE -- message`) plus a "Retry" button (`aria-disabled`, "Retrying…", as on the other
    pages); the table shows "No report to show."; Export disabled.
  - Empty (no rooms at all): the table shows "No rooms yet."; Export disabled.
- The filter stays usable in every state, so a user can fix a bad range without a reload.

### PDF content
Built in the browser with `jspdf` and `jspdf-autotable` from the `UsageReportDto` held in page state (not by calling
the API again, so the file always matches the screen):
- A4 landscape. Title "Room usage report"; "Generated {generatedAt, local time}"; the period line; the thresholds
  line (same text as the page).
- A table with the same columns, the same rows, in the same order, with the same display rules ("n/a" for null
  utilization). The flag column reads "Often booked, rarely used" for flagged rooms and is empty otherwise; flagged
  rows get a light fill. The table header repeats on every page and the page footer shows "Page X of Y".
- File name: `room-usage-report-{yyyy-MM-dd}.pdf`, the date taken from `generatedAt` in local time (no `Date.now()` or
  clock of its own).
- jsPDF's built-in font covers Latin text only. Room names with characters outside it may print wrongly; accepted for
  this demo and noted in the README (Spec 13).
- The two libraries are loaded with a dynamic `import()` when the user clicks "Export PDF", so the PDF code is not in
  the main bundle (the build already warns about a large chunk). While it loads the button shows "Preparing…" and is
  `aria-disabled`. A failure to build the PDF shows an error `Snackbar` ("The PDF could not be created.") and does not
  touch the screen.

## Data changes
None. No backend change is needed.

## Files to change
- `src/App.tsx`: add the route `/report`.
- `src/components/Layout.tsx`: add the nav item `{ to: '/report', label: 'Report' }`, and let the toolbar and the nav
  wrap on narrow screens (see Rules, item 10).
- `src/api/types.ts`: add `RoomUsageDto` and `UsageReportDto`, exactly as the API sends them.
- `frontend/package.json` and lock file: the two PDF packages below.
- `.claude/skills/frontend-design/SKILL.md`: "Built so far" list includes `ReportPage`, `api/reports.ts` and
  `lib/exportReportPdf.ts`.

## Files to create
- `src/api/reports.ts`: `getUsageReport(range, signal)`, with `from` and `to` optional, left out of the query string
  when empty; uses `request` with `expect: 'object'`.
- `src/pages/ReportPage.tsx`.
- `src/lib/exportReportPdf.ts`: `exportReportPdf(report: UsageReportDto): Promise<void>`. Formatting and layout only.
- `src/lib/reportFormat.ts`: the display helpers shared by the page and the PDF (hours, percent / "n/a", period line,
  thresholds line), so both show identical text.

## New dependencies
`jspdf` and `jspdf-autotable` (MIT, free), exact versions: the latest releases that work together and with
TypeScript strict mode, with `jspdf-autotable` compatible with the chosen `jspdf`. No paid package. Installing them
needs the user's approval at implementation time.

## Rules for implementation
1. No rule, count, total, percentage, threshold, flag or sort is calculated in the front end. The only logic is
   formatting for display and converting the chosen days to ISO timestamps. Do not add a "total" row, do not re-sort
   the rooms and do not hard-code the thresholds or the flag text's numbers.
2. All HTTP goes through `src/api/client.ts` (via `getUsageReport`); the page never calls `fetch`. The PDF code makes
   no HTTP call.
3. Follow the page pattern: `AbortController` per load keyed on the applied range, `signal.aborted` guards,
   `setErrors([])` at the start of each load, a non-`ApiError` shown as `UNEXPECTED_ERROR`, Retry with
   `aria-disabled` and "Retrying…".
4. An older response never replaces a newer one (abort on range change, retry and unmount). On an error the previous
   report is cleared, so the screen never shows a report that does not match the filter.
5. The PDF is built from the report state, and the page and the PDF use the same helpers from `reportFormat.ts`, so
   the text is identical by construction.
6. Colours come from `useTheme()` palette tokens (warning colour for flags). No hard-coded hex on the page. The PDF
   fill for flagged rows is the one place a literal RGB value is allowed, because jsPDF does not read the theme.
7. Strict TypeScript, no `any`, no `@ts-ignore`. Ids are GUID strings.
8. Accessibility: the filter inputs have visible labels, the table has an `aria-label`, numeric columns are
   right-aligned, the flag is text as well as colour, the Export button states its busy state with `aria-disabled`.
9. Dates are ISO 8601 with an offset on the wire and local time on screen and in the PDF.
10. Navigation fix (small, flagged for the user's decision): at 375 px the nav bar clips its links, and a fifth link
    makes it worse. Let the toolbar and the nav wrap onto a second line (`flexWrap: 'wrap'`) so every link stays
    reachable. No change at desktop width. If the user prefers this as a separate change, drop this item.
11. No link or page beyond Report. The docs belong to Spec 13.

## Definition of done
- [ ] `npm run build` passes with no type errors; `npm audit` shows no new high or critical finding from the added
      packages; the PDF libraries appear in their own chunk, not in the main one.
- [ ] `/report` loads with no filter and shows every room, with the thresholds and "generated" lines taken from the
      response.
- [ ] Seeded data: Boardroom is first, highlighted, with the chip "Often booked, rarely used"; no other room is
      flagged; its figures match `GET /api/reports/usage` in the network log.
- [ ] A room with no completed or cancelled bookings shows utilization "n/a".
- [ ] Choosing a period requests `/api/reports/usage?from=…&to=…` with the local day start and day end as ISO
      timestamps (checked in the network log) and the table, the period line and the "generated" line update.
- [ ] "From" later than "To" shows the API's `INVALID_REQUEST` error, keeps the inputs, and "Clear" restores the
      all-time report.
- [ ] Switching the period quickly never shows the report of an earlier request.
- [ ] Stopping the backend shows a readable `NETWORK_ERROR` alert with Retry, no stale table and a disabled Export
      button; Retry works once the backend is back.
- [ ] "Export PDF" downloads `room-usage-report-{date}.pdf`; opened, it shows the title, generated time, period,
      thresholds and the same rooms, order, numbers, "n/a" values and flags as the screen, in landscape A4.
- [ ] A filtered report exports with its own period line and rows (not the all-time ones).
- [ ] The nav shows "Rooms", "Users", "Reservations", "Calendar" and "Report", the active link is highlighted, and at
      375 px all five links are reachable.
- [ ] The other pages (Rooms, Users, Reservations, Calendar) still work unchanged.
