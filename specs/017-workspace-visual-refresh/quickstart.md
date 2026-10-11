# Quickstart Validation: Workspace Visual Refresh

This guide validates the later implementation. Planning artifacts do not implement the refresh.

## Prerequisites

- .NET 10 and existing xUnit 2/VSTest projects; verify runner configuration before using commands.
- Chromium matching the existing Playwright package. Reuse live Kestrel tests with controlled sessions and Testing identity; no agents or Cosmos credentials needed.
- Use Release when a user-owned Debug server locks output assemblies. Do not stop that server or change production authorization for tests.

## Capture the Baseline

Before editing Home or app.css, render the current live workspace at 1280 CSS pixels/default zoom. Record actual query textarea border-box width, query-column left edge, prompt container bounds, border/padding/gap and screenshot as validation evidence.

The current CSS-derived query width is approximately 569.8px only without scrollbar reduction; it is not measured evidence. The acceptance target is the actual baseline multiplied by 1.20. Preserve a baseline snapshot/fixture or recorded numeric result so final tests do not compare the refreshed page to itself.

## Focused Checks

```powershell
dotnet restore BlogWriter.Web.Tests/BlogWriter.Web.Tests.csproj
dotnet test BlogWriter.Web.Tests/BlogWriter.Web.Tests.csproj --configuration Release --filter "FullyQualifiedName~BlogWorkspaceServiceTests|FullyQualifiedName~HomePageTests|FullyQualifiedName~WordRangeInputTests"
dotnet test BlogWriter.Web.Tests/BlogWriter.Web.Tests.csproj --configuration Release --filter "FullyQualifiedName~SessionListBrowserTests|FullyQualifiedName~WorkspaceBrowserTests"
dotnet build BlogWriter.Web/BlogWriter.Web.csproj --configuration Release
```

Include any new state/layout test class in the filters. Replace obsolete web revision execution and removed-DOM assertions with the clarified contract; do not skip entire suites or weaken non-revision checks.

If Chromium is missing after building tests:

```powershell
& ./BlogWriter.Web.Tests/bin/Release/net10.0/playwright.ps1 install chromium
```

## Live Fixture

Use WebApplicationFactory with Kestrel dynamic ports before creating its client. Supply non-secret startup values through host configuration and replace the session service. Await connected WebSocket and an interactive-render marker before clicking prerendered commands; keep loopback network exceptions scoped to test Chromium. Honor discard confirmation after restoring saved inputs. Dispose temporary browsers and hosts.

Replace the existing live fixture's removed `#revision-prompt` assertion with DOM absence and saved-data checks. Static session-row tests cannot validate the whole workspace. Successful initial-Go checks need a writing stub that supports StartAsync; the existing list-only SavedListSessionService deliberately throws on StartAsync and ReviseAsync.

## Acceptance Scenarios

1. At 1280 pixels, verify exact title/name/role, absent description/revision/reviewer sections, retained Sign out and absent Draft index. Compare measured query width and edges to the baseline and [workspace contract](contracts/workspace.md).
2. Verify targets sit right of query in the required vertical order; both input widths and all label/input left edges differ by at most 2 pixels. Draft spans combined columns; commands begin at query's left edge.
3. At 320 pixels and enlarged text, check reflow, vertical target order, name/role/Sign out and no page overflow. Test long query/draft words and validation errors; inspect actual boxes and screenshots.
4. Enter query/valid targets, invoke Go and observe one StartAsync with exact values. Draft and internal reviewer/log output still arrive, with no review pane. Invalid bounds still prevent submission through current validation/error associations.
5. After a completed draft, populate stale revision text through a fixture and call Go and direct SubmitRevisionAsync. Both record zero revisions, no saved mutation/write and no replacement revision interpretation of query. Existing query lock remains until New or restored-pending context.
6. Seed a saved session with historical revision/reviewer content. List appears in widened Draft; restoration fills query/targets, with no revision input and no source mutation. Go uses the existing new initial run, not revision processing.
7. Traverse remaining enabled controls with Tab, verify visible focus/full labels and native activation. Removed sections have no focusable controls. Retain stop/cancel/disabled/ended/Help/discard and Sign out checks, independent of fixed display name.

## Final Regression Gates

```powershell
dotnet test BlogWriter.Web.Tests/BlogWriter.Web.Tests.csproj --configuration Release
dotnet test BlogWriter.Tests/BlogWriter.Tests.csproj
dotnet build BlogWriter.Web/BlogWriter.Web.csproj --configuration Release
```

Expected: web tests pass with intentional revision-removal expectations; core/console revision and persistence tests remain unchanged. Record counts, build outcome, measured width/alignment, screenshots and keyboard results against all FR/SC items. Report any unexercised Sign out or layout outcomes rather than claiming them from unit checks.

Only for authenticated manual inspection, run the existing app with its current configuration on a free port:

```powershell
dotnet run --project BlogWriter.Web/BlogWriter.Web.csproj --configuration Release --no-build --no-launch-profile -- --urls http://localhost:5161 --environment Development
```

Use another port if occupied. This is not a credential-free substitute for controlled fixtures; no deployment or cloud-resource changes are needed.

## Implementation Evidence (2026-10-11)

- Setup: .NET 10.0.400-preview.0.26312.103, existing xUnit/VSTest packages restored. Pre-change web suite passed 106/106 including the temporary baseline capture. Existing .NET ignore patterns are sufficient; no new build/deployment technologies or package changes.
- A running Release server locks its DLLs, so this implementation uses `--configuration Spec017` for isolated build/test output rather than stopping it. Substitute Spec017 for Release in the commands above while that server is running.
- Pre-change live Chromium baseline at 1280 CSS pixels and devicePixelRatio 1: query width 569.8125px, query left 64.1875px; prompt strip left 51.1875px, width 1177.625px, padding 12px, border 1px, gap 12px. The required query target is 683.775px. Baseline JSON/screenshot remain under ignored `BlogWriter.Web.Tests/bin/Spec017/net10.0/TestResults/workspace-before.*`.
- US1: labels/location/removal and geometry checks first failed against old presentation, then passed after component/CSS edits. Range checks 3/3, Home wiring 2/2, actual-browser geometry/error checks 2/2. New desktop query width 683.765625px, within 0.01px of the target. Command left 64.1875px; Draft frame and target outer edges align within 2px. Current final measurements/screenshots use `workspace-after-320.*` and `workspace-after-1280.*` in the same ignored result directory.
- US3: six new no-revision checks first failed on shared revision calls/state mutation, then passed after suppressing Go's revision route and making direct SubmitRevisionAsync inert. IsRevisionInputEnabled is always false; query-lock, New and restored-initial-run rules remain. All 62 service/state tests, 21 Home tests and 12 browser/restoration checks passed after intentional legacy expectations were reconciled. Cancellation timeout coverage now uses an initial writing operation, not a removed revision operation. Persisted historical data and shared/console revision code were not changed.
- US2: exact heading/description/Draft-index tests passed. The header renders Jesse Liberty above Senior AI Engineer within existing authorization, with the original Sign out destination. Signed-in/signed-out fixture tests verify presentation gating and unchanged identity claims. No external Entra sign-out transaction was executed; the preserved link destination/action was verified without changing authentication configuration.
- Final live acceptance covers default and 125% text at 320/1280 pixels, long query text, validation-error placement, word-stack ordering/alignment, actual baseline-relative width, full Draft/command edges, both identity lines and zero hidden removed controls. Four geometry cases passed; screenshots were inspected. At widths up to 400px, commands use two rows to avoid enlarged-label overflow. Final files are `workspace-after-<width>-<textScale>.json/png`, e.g. `workspace-after-320-1.25.png`, in ignored TestResults output.
- Live keyboard/command tests traverse all remaining enabled inputs/commands and Sign out with visible focus, exercise Help open/close, List/restore, an initial Go via Enter, repeated post-draft Go with no revision, long draft rendering, and Quit. The test-only writing result factory permits controlled initial runs while still failing unexpected revisions. No live agent or database calls are used; all temporary Kestrel hosts/browsers dispose after their tests.
- Final gates: `dotnet test BlogWriter.Web.Tests/BlogWriter.Web.Tests.csproj --no-restore --configuration Spec017` passed 120/120; `dotnet test BlogWriter.Tests/BlogWriter.Tests.csproj --configuration Spec017` passed 126/126; `dotnet build BlogWriter.Web/BlogWriter.Web.csproj --no-restore --configuration Spec017` passed. Existing core xUnit2031 warning is unrelated and unchanged. No source schema, package, cloud deployment, profile/claims or agent implementation changes.
- Quality checklist markers remain read-only. No measurements or external authentication effects are claimed beyond the checks described here.
