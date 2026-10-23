---
description: Create a new numbered spec file and the feature branch for it
argument-hint: <slug> (e.g. rooms-api)
allowed-tools: Read, Write, Glob, Grep, Bash(git status:*), Bash(git branch:*), Bash(git checkout:*), Bash(git switch:*), Bash(git pull:*), Bash(git fetch:*)
---

You are creating the next spec for the Room Reservation System (ASP.NET Core 8 + React/TypeScript).

User input: $ARGUMENTS

## Steps
1. **Clean tree check.** Run `git status --short`. If there are uncommitted changes, STOP and tell the user. Do not stash or discard.
2. **Next number.** Glob `.claude/specs/*.md`, take the highest `NN`, add 1, zero-padded to 2 digits. Slug = `$ARGUMENTS` in kebab-case.
3. **Update main.** `git checkout main` then `git pull origin main`. Then create `feature/<slug>` from it.
4. **Research.** Read `CLAUDE.md`, the specs this one depends on, and the existing code it will touch. Do not invent files or APIs that do not exist.
5. **Write** `.claude/specs/NN-<slug>.md` with these sections, in this order:
   Overview, Depends on, Endpoints / Pages, Data changes (if any), Files to change, Files to create,
   New dependencies, Rules for implementation, Definition of done (checklist ending with `dotnet build` / `dotnet test`, or `npm run build` for frontend specs).
6. **Report** the spec path, the branch name and a 5-line summary. Do NOT implement anything and do NOT commit.

## Rules
- Business rules R1-R7 live only in the backend; never specify them in the front end.
- Limits (7 days, 90 days, report thresholds) come from configuration, never literals.
- Each spec must be small enough for one PR.
