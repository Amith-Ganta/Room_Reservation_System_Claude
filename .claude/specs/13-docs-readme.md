# Spec 13 -- README and Development Guide

## Overview
Finish the repository for the reviewers: a README that lets them run the app in minutes and explains the
architecture decisions, tools and design patterns, plus a DEVELOPMENT_GUIDE.md describing the spec-driven workflow.

## Depends on
All previous specs.

## Files to create / change
- `README.md`: what it does, requirement coverage table (every task requirement -> where it lives), prerequisites,
  run backend (`dotnet run`, http://localhost:5080/swagger), run frontend (`npm run dev`, http://localhost:5173), run tests,
  configuration (`appsettings.json` keys), project structure, API summary.
- Architecture section: layers (controller -> service -> validator/repository), `Result<T>` instead of exceptions, why
  a dedicated validator, why lazy completion instead of a background job, why a write lock, why InMemory,
  why `TimeProvider`, string enums, configurable limits.
- Design patterns used: Repository, Dependency Injection, DTO + Mapper, Options, Result, Strategy-like validator rules, Facade (service).
- Tools used: ASP.NET Core 8, EF Core InMemory, AutoMapper, xUnit, FluentAssertions, Swagger, React, Vite, TypeScript, MUI,
  FullCalendar, jsPDF, Claude Code, GitHub Copilot review.
- Known limitations and next steps (no auth, InMemory data lost on restart, single-instance lock).
- `DEVELOPMENT_GUIDE.md`: the spec -> branch -> implement -> PR -> Copilot review -> fix PR -> merge loop, with the list of specs.

## Rules for implementation
1. Every claim in the README is true of the code at merge time (verify commands by running them).
2. No screenshots of unfinished screens.

## Definition of done
- [ ] A fresh clone follows the README and gets a running app.
- [ ] Requirement coverage table has no empty rows.
