# Spec 13 -- README and Development Guide

## Overview
Finish the repository for the reviewers. A README lets them run the app in a few minutes, shows where every task
requirement lives, and explains the architecture decisions, tools and design patterns. A DEVELOPMENT_GUIDE.md
describes the spec-driven workflow. Documentation only: no source code changes.

## Depends on
All previous specs (01-12, all merged).

## Endpoints / Pages
None. The README documents the existing API (Rooms, Users, Reservations, Reports) and the five front-end pages
(Rooms, Users, Reservations, Calendar, Report). Nothing is added or changed.

## Data changes
None.

## Files to change
- `README.md`: replace the current short version with the full README below.
- `CLAUDE.md`: only the status row (`13 | docs-readme`), and only after the PR is merged, in the separate "mark merged" commit.

## Files to create
- `DEVELOPMENT_GUIDE.md` (repo root).

## New dependencies
None.

## README content
1. What it does, in a few lines.
2. Requirement coverage table: every task requirement -> where it lives (endpoint, rule code, page, test class). No empty rows.
3. Prerequisites: .NET 8 SDK, Node.js (version taken from what was used to build), npm.
4. Run the backend: `dotnet run --project RoomReservation.Api --launch-profile http` from `backend/`,
   http://localhost:5080, Swagger at http://localhost:5080/swagger (Development only). Say that the launch profile
   runs in Development, which seeds demo data on every start (InMemory, new GUIDs each time).
5. Run the frontend: `npm install` then `npm run dev` from `frontend/`, http://localhost:5173 (Vite proxies `/api` to 5080).
   Say that port 5173 is fixed (`strictPort`) and the CORS origin in `appsettings.json` must match it.
6. Run the tests: `dotnet test` from `backend/`. Front-end build check: `npm run build`.
7. Configuration table (`appsettings.json` keys and defaults, as today) plus `Seed:Enabled` and `Database:Name`.
8. Project structure (the tree from CLAUDE.md, checked against the real folders).
9. API summary: the real routes and methods, the error body `{ "errors": [ { "code", "message" } ] }`, the HTTP status mapping,
   the booking rules table (R1-R7), and the usage report query (`from`, `to`, flag rule).
10. Architecture: controller -> service -> validator/repository; `Result<T>` instead of exceptions; why a dedicated validator;
    why lazy completion instead of a background job; why a static write lock (`SemaphoreSlim`); why InMemory;
    why `TimeProvider`; string enums; configurable limits; rules and report numbers calculated only in the backend.
11. Design patterns: Repository, Dependency Injection, DTO + Mapper, Options, Result, Strategy-like validator rules, Facade (service).
12. Tools used: ASP.NET Core 8, EF Core InMemory, AutoMapper, xUnit, FluentAssertions, Swagger, React, Vite, TypeScript, MUI,
    FullCalendar, jsPDF + jspdf-autotable, Claude Code, GitHub Copilot review. Versions come from the project files.
13. Notes for reviewers (each one checked against the code before it is written):
    - **AutoMapper licence:** AutoMapper 15.1.1 is kept on purpose. It is a demo project for an internship. A paid licence
      is only needed for commercial use in production; if that ever applies, replace it with hand-written mapping (no cost).
    - **Empty and error states:** a list page shows "No users to show." (and the same wording for rooms and reservations)
      when the load failed, and "No users yet." / "No ... match" when the load worked and the list is empty.
      The error alert and a Retry button appear above the table.
    - **AllowedHosts:** `appsettings.json` sets `AllowedHosts` to `localhost;127.0.0.1;[::1]`. A real deployment needs its own host value.
    - **PDF fonts:** the usage report PDF uses the jsPDF built-in font, which covers Latin text only. Room names with other
      characters may print wrongly in the PDF. The on-screen table is not affected.
14. Known limitations and next steps: no authentication, InMemory data lost on restart, single-instance write lock,
    no paging on lists (planned), no front-end tests yet (planned).

## DEVELOPMENT_GUIDE.md content
- The loop: spec -> branch -> implement -> test -> review -> PR -> Copilot review -> fix PR -> merge -> mark merged.
- The slash commands in `.claude/commands/` and the subagent files in `.claude/agents/`, with what each one does and that
  reviewers and the test runner are read-only.
- The spec list (01-13) and where the spec files live.
- The rules from CLAUDE.md that keep the project safe (no push, merge or commit without approval; the NEVER list).

## Rules for implementation
1. Every claim in the README is true of the code at merge time. Run each command in the README (build, test, run, Swagger,
   front-end build) and check each file path, route, config key and message text with a search before writing it.
2. No screenshots of unfinished screens; no invented numbers (for example test counts) that will go stale. Link to the command instead.
3. Cambridge B2 professional English, natural tone, no em dashes in new text.
4. Do not edit source code, tests or the parent `Internship/` folder. If a README claim cannot be made true, change the
   README, not the code.

## Definition of done
- [x] A fresh clone follows the README and gets a running API and front end.
- [x] Requirement coverage table has no empty rows.
- [x] The four reviewer notes (AutoMapper licence, empty/error state, `AllowedHosts`, PDF font) are in the README and match the code.
- [x] `DEVELOPMENT_GUIDE.md` exists and its command and agent names match `.claude/`.
- [x] Every command in the README was run successfully; `dotnet build`, `dotnet test` and `npm run build` still pass.
