---
description: Commit, push, open a PR, request the Copilot review, and (after approval) squash-merge
argument-hint: <spec number or slug>
allowed-tools: Read, Grep, Glob, Bash(git status:*), Bash(git diff:*), Bash(git log:*), Bash(git branch:*), Bash(git add:*), Bash(git commit:*), Bash(git push:*), Bash(git checkout:*), Bash(git pull:*), Bash(gh pr:*), Bash(gh api:*), Bash(dotnet build:*), Bash(dotnet test:*), Bash(npm run build:*)
---

Ship the current feature branch for spec: $ARGUMENTS

## 1. Pre-flight
- `git branch --show-current`: refuse on `main`.
- Run the checks the spec's Definition of done requires: `dotnet build`, `dotnet test`, and `npm run build` inside `frontend/` for frontend specs. Stop if any fail.
- `git status` and `git diff --stat`: refuse to stage `bin/`, `obj/`, `node_modules/`, `.env*`, `*.user`, secrets. Stage files by name, never `git add -A` blindly.

## 2. Commit (ask first)
Write a Conventional Commit (`feat: ...`) summarising the spec. Show the message and the file list and ASK the user for approval before committing.
End the message with:
`Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`

## 3. Push and PR (ask first)
ASK the user for approval before `git push -u origin <branch>`. Then open the PR with `gh pr create --base main`:
- Title: the Conventional Commit subject.
- Body: Summary, spec file, what was tested, how to run. End with:
  `🤖 Generated with [Claude Code](https://claude.com/claude-code)`

## 4. Copilot review
- Request the review: `gh api -X POST repos/{owner}/{repo}/pulls/<n>/requested_reviewers -f 'reviewers[]=copilot-pull-request-reviewer[bot]'`
  (if that is rejected, tell the user to add Copilot as reviewer in the PR sidebar).
- Do NOT poll in a loop. Tell the user the PR link and stop; the user returns when the review is in, then run `/fix-copilot-review <PR number>`.

## 5. Merge (only on explicit approval, after the fix PR)
`gh pr merge <n> --squash --delete-branch`, then `git checkout main && git pull origin main`.
Report the merge commit and the next spec in line.

## Never
- Push, merge or force-push without explicit user approval in this conversation.
- Push to `main` directly.
