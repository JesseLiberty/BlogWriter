# Quickstart Validation: Session List Details

This guide validates the implementation after tasks are completed. Planning alone does not provide the new UI or tests.

## Prerequisites

- Use the repository root and .NET 10 SDK. Existing tests use xUnit 2 and VSTest; verify there is no overriding test runner configuration before using these commands.
- Restore existing projects. Unit/component checks require no live agents or Cosmos credentials.
- For browser checks, use the existing Playwright package and installed Chromium with a controlled fixture. Do not run real agents to manufacture test drafts.
- For a hosted workspace, use existing authenticated development configuration. The existing test-only identity switch must remain confined to the `Testing` environment.

## Focused Checks

```powershell
dotnet restore BlogWriter.Web.Tests/BlogWriter.Web.Tests.csproj
dotnet test BlogWriter.Tests/BlogWriter.Tests.csproj --filter "FullyQualifiedName~FileBlogSessionStoreTests|FullyQualifiedName~BlogSessionStoreOwnershipTests|FullyQualifiedName~BlogWriterSessionServiceTests|FullyQualifiedName~SessionList|FullyQualifiedName~WordRangeTests"
dotnet test BlogWriter.Web.Tests/BlogWriter.Web.Tests.csproj --filter "FullyQualifiedName~BlogWorkspaceServiceTests|FullyQualifiedName~WorkspaceBrowserTests|FullyQualifiedName~SessionList"
dotnet build BlogWriter.Web/BlogWriter.Web.csproj
```

Include any added summary/projection test class in the focused filter. Expected: exact previews and effective limits for both adapters, preserved owner scope/order/cap, escaped rendered content, current saved values after refresh, and unchanged selection-for-editing behavior. Mock the existing Cosmos container/feed boundary to prove projection and paging without a live database.

Because `BlogSessionSummary` is shared, finish with both test projects:

```powershell
dotnet test BlogWriter.Tests/BlogWriter.Tests.csproj
dotnet test BlogWriter.Web.Tests/BlogWriter.Web.Tests.csproj
```

Expected: no console formatting, workflow, ownership, or restore regressions. Tests run at implementation time, not as evidence that planning has implemented the feature.

## Browser Preparation

After building web tests, install the package-matched browser if missing:

```powershell
& ./BlogWriter.Web.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
```

Use a Playwright fixture that renders the actual component with the real stylesheet and controlled saved-session data, or an isolated web host wired to a recording session service. Reuse existing list-launcher helpers and keep fake services/identity test-only. This browser harness is an implementation deliverable; current `WorkspaceBrowserTests` are bUnit tests and do not prove geometry.

To inspect the existing authenticated development host:

```powershell
dotnet run --project BlogWriter.Web/BlogWriter.Web.csproj --no-launch-profile -- --urls http://localhost:5160 --environment Development
```

Use another free port if occupied. This host still needs its existing development authentication/service configuration; it is not a credential-free substitute for the controlled fixture. Stop it when validation is complete.

## End-to-End Scenarios

1. Prepare at least ten owner-visible saved sessions with distinct ranges and drafts, including empty/whitespace drafts, 1/49/50/51 words, mixed whitespace, markup, duplicate tasks, and a long unbroken word. Include valid equal bounds and legacy omitted/invalid bounds.
2. Press List. Check the labels, number, task, update time, limits and excerpt against [contracts/session-list.md](contracts/session-list.md) and [data-model.md](data-model.md). Exactly 50 words have no ellipsis; 51 words show the first 50 plus an ellipsis. Markup is literal and cannot execute.
3. Save a changed draft and range through the controlled fixture, press List again, and confirm fresh values for that session without borrowing unsaved input or another entry's values.
4. At viewport widths of 320 and 1280 pixels, take screenshots and inspect row bounds. Assert each field stays within its row and the page/list has no horizontal overflow; scroll vertically to inspect all entries. Check long tasks and words, not just short fixtures.
5. Tab to a session entry, verify visible focus, and activate it with Enter and Space. Repeat with pointer selection. Verify the intended saved inputs restore for editing, no start/revision calls occur, and saved content remains unchanged. Disable selection and confirm pointer/keyboard do not restore.
6. Display zero sessions and verify the existing empty-list message. Exercise the existing failed-list and cancellation paths to ensure added details do not bypass their current handling.

## Acceptance Evidence

- Record results for SC-001 through SC-004, including browser screenshots, word-boundary assertions, preserved console formatting, and owner-isolation checks.
- Conduct the SC-005 usability review with five writers and ten distinguishable sessions: at least four select correctly on the first attempt within 30 seconds. This manual outcome is not implied by test success.
- If an existing authorized Cosmos emulator/test account is available, repeat list validation there and record query payload/RU observations for long drafts. Do not provision resources, change consistency, or request secrets for this optional check. Explicitly report when real Cosmos execution was not performed.

## Implementation Evidence (2026-10-10)

- Setup: SDK 10.0.400-preview.0.26312.103; xUnit 2/VSTest; no ancestor runner override found. Both test project restores passed. Existing .NET ignore patterns are sufficient; no additional build-tool ignore files are applicable.
- Foundation: new summary tests first failed on missing display properties/factory, then all 16 passed after implementation. Existing workflow test warning xUnit2031 is unrelated and unchanged.
- Checklists remain read-only; implementation progress is tracked separately in tasks.md.
- US1: file-store checks first failed on missing saved data, then 9 passed; Cosmos projection checks first failed on missing preview/normalization, then 7 passed. Core integration passed 75 tests; workspace/component integration passed 65. Both stores preserve owner scope, newest-first order, the 20-result bound and read-only behavior.
- Web validation uses `--configuration Release`: the existing running Debug web process locks its output DLL. It was not stopped or altered. Release avoids that lock; Debug builds must wait until that existing process is stopped by its owner.
- US2: Chromium 151 (Playwright build 1234) was installed using the existing package script. The four actual-browser checks initially exposed overflow/hierarchy defects, then all passed after scoped row CSS changes. Screenshots at 320 and 1280 pixels were inspected; long unbroken words and large bounds wrap within rows. External font requests receive empty test responses, so geometry checks use font fallbacks with the actual application stylesheet.
- Original browser evidence combines native pointer/Enter/Space/focus/disabled checks on static rendered component markup with bUnit callback checks. T024 now supplements these with an isolated live Kestrel/Blazor circuit, verifying loaded IDs, restored inputs, pointer and Tab/Enter/Space activation, and disabled concurrent-selection prevention. Saved data remains unchanged and no writing/revision work starts during list selection.
- Final regressions: `dotnet test BlogWriter.Tests/BlogWriter.Tests.csproj --no-restore` passed 126/126; `dotnet test BlogWriter.Web.Tests/BlogWriter.Web.Tests.csproj --no-restore --configuration Release` passed 102/102, including four Chromium cases. Final Release web build, including its core dependency, passed. Screenshots are generated under `BlogWriter.Web.Tests/bin/Release/net10.0/TestResults/` (ignored build output).
- T021 / SC-005 remains pending: five-writer usability participants were not available in this coding session. No timing or participant results are claimed.
- T022: no live Cosmos/emulator query or payload/RU measurement was performed. The required credential-free SDK query/feed tests passed; no resources were provisioned or credentials requested.
- Scope review: no persisted schema, dependency version, authorization, agent implementation or console output changes. Existing user edits were preserved, including unrelated workflow/control-state work. Quality checklist markers were not modified.

## Convergence Validation

- T024: `dotnet test BlogWriter.Web.Tests/BlogWriter.Web.Tests.csproj --no-restore --configuration Release --filter FullyQualifiedName~LiveWorkspace_Restores` passed both viewport cases. The test-only host uses existing Testing authentication and a recording session service, dynamically assigned loopback ports, and is disposed after each test. Non-secret startup settings are supplied through host configuration before minimal-app startup reads them.
- The live fixture initially failed because Chromium's local-network checks blocked the loopback WebSocket, commands could run before interactive rendering, and restored prompts require discard confirmation before another List invocation. The test browser disables only local-network-check features, waits for a connected WebSocket and rendered Blazor element-reference marker, verifies New focuses the input through the real circuit, and accepts the existing discard dialog. Production browser settings and app behavior are unchanged.
- T025: `dotnet test BlogWriter.Web.Tests/BlogWriter.Web.Tests.csproj --no-restore --configuration Release --filter FullyQualifiedName~ListButton_ShowsTenSavedSessions` passed. Actual Home/List invocation displays ten sessions; every number, task, time, bound and preview is checked against saved content independently of the summary factory, including empty/49/50/51-word drafts. Repeated List reflects one edited saved session without changing the other nine or starting/loading work.
- Final convergence regression: the complete Release web suite passed 105/105, including six Chromium cases; Release application/core build passed. No application source or dependency changes were needed for T024/T025. T021 remains the only unchecked task (manual five-writer usability review); live Cosmos/RU measurement remains unperformed as previously disclosed.
