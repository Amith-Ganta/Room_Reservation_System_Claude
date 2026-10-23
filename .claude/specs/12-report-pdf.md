# Spec 12 -- Usage Report Page and PDF Export

## Overview
Report page that shows the per-room usage numbers from Spec 07 and highlights rooms that are "often booked
but rarely used". Includes a PDF export of the report (nice-to-have).

## Depends on
Specs 07 and 08.

## Pages / routes
| Route | Page |
|-------|------|
| `/report` | Usage report |

## Behaviour
- Optional date range (from / to) passed to `GET /api/reports/usage`.
- Table: room, floor, total bookings, completed, cancelled, upcoming, planned hours, used hours, utilization %, flag chip
  "Often booked, rarely used".
- Flagged rooms are visually highlighted and listed first (the order comes from the API).
- A short line explains the two thresholds returned by the API.
- "Export PDF" builds a PDF from the response already shown (title, generated-at, range, thresholds, table).

## Files to create / change
`src/api/reports.ts`, `src/pages/ReportPage.tsx`, `src/lib/exportReportPdf.ts`, route and nav link.

## New dependencies
jspdf, jspdf-autotable.

## Rules for implementation
1. No calculation in the front end: show and export exactly what the API returns.
2. The PDF contains the same rows and flags as the screen.
3. Null utilization is shown as "n/a".

## Definition of done
- [ ] `npm run build` passes.
- [ ] Seeded data shows at least one flagged room; the exported PDF opens and matches the screen.
