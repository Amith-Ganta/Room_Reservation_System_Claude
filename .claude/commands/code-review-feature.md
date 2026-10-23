---
description: Local pre-PR review of the current diff by a security and a quality reviewer in parallel
argument-hint: <spec number>
allowed-tools: Read, Glob, Grep, Agent, Bash(git diff:*), Bash(git status:*), Bash(git log:*)
---

Review the work for spec: $ARGUMENTS (this runs before the GitHub Copilot review and should catch the obvious issues first).

1. **Pre-flight.** Run `git diff` and `git diff --staged`. If both are empty, say there is nothing to review and stop.
2. **Read the spec** `.claude/specs/$ARGUMENTS-*.md`.
3. **Parallel review.** In ONE message, launch `internship-security-reviewer` and `internship-quality-reviewer`, each with the diff and the spec.
4. **Unified report:**
   - Security findings (severity, file:line, fix)
   - Quality findings (severity, file:line, fix)
   - Spec compliance: each Definition-of-done item met / not met
   - Combined Action Plan (ordered, Blocker / Should-fix / Nice-to-have)
   - Verdict: Ready for PR / Needs changes
5. **Ask for approval** before implementing any fixes. Do not edit files in this command.
