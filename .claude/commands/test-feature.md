---
description: Write tests from a spec (not from the implementation), then run them
argument-hint: <spec number, e.g. 04>
allowed-tools: Read, Glob, Grep, Agent, Bash(dotnet test:*), Bash(npm run build:*)
---

Test feature for spec: $ARGUMENTS

1. Find `.claude/specs/$ARGUMENTS-*.md` and read it fully.
2. Launch the `internship-test-writer` agent. Give it the spec text and the file paths it names. It writes xUnit tests from the
   spec's Definition of done and Rules, NOT by copying what the implementation happens to do.
3. Launch the `internship-test-runner` agent to run only the new test class (`dotnet test --filter "FullyQualifiedName~<Class>"`).
4. Report: tests written, passed / failed, and for each failure whether the test or the implementation looks wrong.
   Do NOT change production code in this command; if the implementation is wrong, say so and stop.

For frontend specs there is no unit-test project: run `npm run build` and list the manual checks from the spec's Definition of done.
