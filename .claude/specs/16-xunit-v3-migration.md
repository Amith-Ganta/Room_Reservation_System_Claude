# Spec 16 -- xunit.v3 Migration

## Overview
The back-end tests use xUnit 2 (`xunit` 2.9.2). xUnit 2 is in maintenance mode and its successor is `xunit.v3`. This
spec moves the test project to `xunit.v3` and changes nothing else: the same tests, the same assertions, the same
result. No production code, endpoint, rule or front-end file changes.

## Depends on
Specs 01 to 07 (the back-end tests) and spec 14 (the paging tests, the largest group).

## Endpoints / Pages
None. No route and no API call changes.

## Data changes
None.

## Files to change
- `backend/RoomReservation.Tests/RoomReservation.Tests.csproj`: replace the `xunit` package with `xunit.v3`, update
  `xunit.runner.visualstudio` to a version that supports xunit.v3, and set `<OutputType>Exe</OutputType>` (a xunit.v3
  test project is an executable). Keep `Microsoft.NET.Test.Sdk`, FluentAssertions, Mvc.Testing and TimeProvider.Testing
  as they are unless the build forces a change.
- Test files: only if the build or a test fails because an xunit.v3 API differs (for example an `Xunit.Abstractions`
  namespace, which moved to `Xunit`). Today the tests use only `[Fact]`, `[Theory]`, `[InlineData]` and
  `IClassFixture<WebApplicationFactory<Program>>`, so no source change is expected.
- `backend/RoomReservation.Tests/AssemblyInfo.cs`: xunit.v3 turns `[assembly: CollectionBehavior(DisableTestParallelization = true)]`
  into a compile error (CS0619). It becomes `[assembly: Parallelization(Mode = ParallelMode.None)]` (namespaces `Xunit.Sdk`
  and `Xunit.v3`). Same effect, because the `WebApplicationFactory` hosts share static startup state.
- `RoomReservation.Tests.csproj` also gets `<NoWarn>$(NoWarn);xUnit1051</NoWarn>`. The new xunit.v3 analyzer raises
  about 144 advisory warnings ("pass `TestContext.Current.CancellationToken`") across the test files. Fixing them would
  mean editing every one of those call sites, which this spec does not do. Adopting the token can be a later cleanup.
- Windows note: an xunit.v3 test project builds an app host (`RoomReservation.Tests.exe`). On a machine where Defender
  blocks new unsigned executables (ASR rule "Block executable files from running unless they meet a prevalence, age,
  or trusted list criterion"), `dotnet test` fails with "Access is denied". Run the test DLL instead:
  `dotnet backend/RoomReservation.Tests/bin/Debug/net8.0/RoomReservation.Tests.dll`.
- `README.md`: the technology table row `Tests | xUnit | 2.9.2` gets the new version, and the xUnit wording stays.
- `CLAUDE.md` (spec table row) is updated only after the merge, in a separate small commit.

## Files to create
None, apart from this spec.

## New dependencies
| Package | From | To | Why |
|---------|------|----|-----|
| `xunit.v3` | (new, replaces `xunit` 2.9.2) | 4.0.2 (pinned) | the xUnit 3 framework |
| `xunit.runner.visualstudio` | 2.8.2 | 4.0.1 (pinned; restore confirmed it) | lets `dotnet test` discover and run xunit.v3 tests |

Both are test-only packages. Nothing is added to `RoomReservation.Api`. Installing them needs the user's approval.

## Rules for implementation
- Tests only. No change under `backend/RoomReservation.Api/` and none under `frontend/`.
- Do not rewrite, delete or skip a test to make the migration pass. A test that fails after the move is a finding, not
  something to hide. If a real behaviour difference appears, stop and report it.
- Keep the test count: 318 tests passed before this spec, and 318 must pass after it.
- Do not change the assertion library (FluentAssertions stays at 6.12.2) and do not add other packages.
- `dotnet test` must keep working from the solution folder and from the test project folder, with no extra arguments.
  This was not verified on the author's Windows machine, where the Defender rule in the Windows note blocks the app
  host. There the tests were run through the test DLL. It is expected to work on a machine without that rule.
- Stop the API before `dotnet build` and `dotnet test` (it locks the DLLs).

## Definition of done
- [x] `RoomReservation.Tests.csproj` references `xunit.v3` and no longer references `xunit` 2.x.
- [x] No test was removed, skipped or weakened; the test count is the same (318).
- [x] The no-parallelization setting still applies (the test run is stable, with no host start-up failures).
- [x] `README.md` shows the new xUnit version.
- [x] `dotnet build` passes with no new warnings (0 warnings, 0 errors; `xUnit1051` is suppressed, see above).
- [x] The 318 tests pass under xunit.v3 (run through the test DLL, see the Windows note above). `dotnet test` itself
  was not run successfully on the author's machine.
- [x] `npm run build` and `npm test` in `frontend` are unaffected (no file under `frontend/` changed).
