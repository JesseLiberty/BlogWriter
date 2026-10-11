# Implementation Plan: Workspace Visual Refresh

**Branch**: `017-workspace-visual-refresh` (reported by Spec Kit setup) | **Date**: 2026-10-11 | **Spec**: [spec.md](spec.md)

**Input**: `specs/017-workspace-visual-refresh/spec.md`, including the clarification removing user-initiated web revisions.

## Summary

Reorganize the existing web workspace around one widened New query input, a vertical Target Words stack on its right, and a Draft area spanning both columns. Remove rendered Revision request and Reviewer notes sections, update title and signed-in display copy, retain Sign out, remove the Draft index, and align commands to the query column. Remove revision submission at the web workspace service boundary; do not alter shared session services, console revision support, stored documents, or internal reviewer processing.

## Technical Context

**Language/Version**: Existing C#/.NET 10, nullable reference types, Razor and CSS.

**Primary Dependencies**: ASP.NET Core interactive server components and existing authentication, xUnit 2, bUnit 2.11.3, Microsoft.AspNetCore.Mvc.Testing 10.0.12, Microsoft.Playwright.Xunit 1.62.0. No additions or upgrades.

**Storage**: Existing persisted sessions unchanged. Historical revision/reviewer fields remain readable and are not migrated or deleted.

**Testing**: Focused workspace state/service, component/layout and live Chromium tests; current VSTest commands. Reuse feature 016's Kestrel fixture and recording service where suitable.

**Target Platform**: Existing authenticated Blazor web workspace; reference widths 320 and 1280 CSS pixels at default zoom, plus increased text-size checks. Windows development environment.

**Project Type**: Existing web UI and its workspace service. No new app, endpoint, deployment, or infrastructure.

**Performance Goals**: No new network requests, model calls, polling, or persistence writes caused by visual changes. Preserve existing loading and cancellation behavior and output scroll limits.

**Constraints**: Exactly 20% wider query at the desktop reference width within 2 pixels of the recorded baseline; Draft/command/word-input edge tolerances of 2 pixels; no horizontal page overflow. No hidden revision entry or alternative revision interaction. Preserve signed-in ownership and all non-revision behavior.

**Scale/Scope**: One workspace, one header layout, three shared display components, web service/state revision guards and existing web tests. Existing List still returns at most 20 summaries. Console and agent implementations remain out of scope.

## Constitution Check

*GATE: Evaluated before research and re-evaluated after Phase 1 design.*

| Principle | Before Research | Post-Design Result |
| --- | --- | --- |
| I. Hosted-Agent Boundaries | Pass: UI-only rendering and web command routing | Pass: no agent adapters, credentials, transport or prompt changes |
| II. MAF-Native Workflow Composition | Pass: no workflow topology changes | Pass: shared orchestration and bounded internal reviewer processing remain intact |
| III. Identity, Secrets, Budget | Pass: existing authentication retained | Pass: fixed name/role is display copy only; no ownership changes, new model work, or embedded secrets |
| IV. Testable and Observable Behavior | Pass: interfaces and test projects reused | Pass: service guard, state, rendering, live keyboard and geometry checks; logging/cancellation preserved |
| V. Simple, Compatible Evolution | Pass: explicit web revision-removal scope | Pass: preserve shared interfaces and persisted schema; retain a non-operational web revision method for caller compatibility and document its changed contract |

Technical constraints pass: .NET 10 remains, no SDK upgrades or deployment changes. The user explicitly authorized removal of user-initiated web revisions as the exception to aesthetic-only scope. This is not removal of automatic review or console functionality. No constitution violation requires justification.

## Project Structure

### Documentation (this feature)

```text
specs/017-workspace-visual-refresh/
  spec.md
  plan.md
  research.md
  data-model.md
  quickstart.md
  contracts/
    workspace.md
  checklists/
    requirements.md
```

Task generation is a subsequent phase; this planning command does not create tasks.md.

### Source Code (repository root)

```text
BlogWriter.Web/
  Components/Pages/Home.razor
  Components/Layout/MainLayout.razor
  Components/WordRangeInput.razor
  Components/CommandBar.razor
  Components/DraftPane.razor
  Services/BlogWorkspaceService.cs
  Services/BlogWorkspaceState.cs
  wwwroot/app.css
BlogWriter.Web.Tests/
  BlogWorkspaceServiceTests.cs
  HomePageTests.cs
  WorkspaceBrowserTests.cs
  SessionListBrowserTests.cs
  ListLauncherTestHelpers.cs
  WordRangeInputTests.cs
```

**Structure Decision**: Edit existing owning surfaces and test files. Remove Home's ReviewPane invocation, not the reusable ReviewPane source or persisted review data. Keep CommandBar's optional ChildContent parameter compatible, but render its range slot only when supplied; Home moves WordRangeInput out of that slot. Reuse the live host for whole-page validation, not static list markup for full workspace geometry. Locate existing state coverage during task generation rather than creating duplicate tests.

## Phase 0: Research Results

Research resolves the controlling command path, desktop sizing, mobile reflow, label semantics, identity display and browser fixture reuse. The current query width is calculable from the two equal tracks, container padding and gap; actual pre-change measurement remains a required implementation check, not an unresolved product decision.

## Phase 1: Design and Validation

1. Before changing markup or CSS, measure the current query input border box at 1280 pixels using the real workspace and stylesheet. Record actual query width, column boundaries, viewport and zoom in quickstart evidence. Use the existing live test fixture without running agents. Expected new width is the recorded value multiplied by 1.20, not a guessed pixel width.
2. Give the desktop prompt container `minmax(0, 1.2fr)` and `minmax(0, .8fr)` tracks while preserving the current gap and padding. New query occupies the left track; existing bound WordRangeInput occupies the right. On narrow screens use one column; override existing mobile rules that hide `.user-name` or position the old command-range slot.
3. Make word targets a single vertical stack: Target Words, Minimum label/input, Maximum label/input; remove the horizontal separator. Preserve min/max IDs, callbacks, validation and screen-reader descriptions. Match both field widths and left edges.
4. Make Draft a single full-span pane aligned to the query/target columns' combined edges, including the List branch. Inset the Draft frame and command row by the prompt container's border-plus-padding offset, or use an equivalent shared column alignment. Do not confuse the prompt strip's outer border with the query column boundary. Preserve empty states, scrolling and session summary content.
5. Update heading and header copy exactly; retain AuthorizeView and Sign out destination. Render name/role on separate lines even on mobile. Remove the Draft pane-index span and the workspace summary sentence. Retain existing color/type styling except minimal sizing/reflow needed to avoid overlap.
6. Remove revision routing in `BlogWorkspaceService.SubmitAsync`: restored pending sessions still submit an initial/new run; completed revision-latched state cannot submit a revision. Make direct `SubmitRevisionAsync` calls complete without invoking the shared service or mutating saved state. `IsRevisionInputEnabled` must remain false. Retain the draft latch/query lock and New/restored-pending unlock rules so the removal does not silently enable new post-draft editing or resubmission. Shared `IBlogWriterSessionService.ReviseAsync`, console consumers and reviewer logic stay unchanged.
7. Update web tests deliberately: old DOM expectations for revision/reviewer sections become absence checks; old web revision-operation assertions become zero-revision and source-immutability checks. Keep tests for initial runs, stop/cancel, command states, list/restore, prompts, word errors, authorization, and Sign out. Continue to assert restored historical values internally when relevant, but never require a removed input in rendered pages.
8. Test geometry and keyboard interaction through the live Kestrel host at 320 and 1280 pixels. Cover long content, validation errors and identity text; compare width against the captured baseline and ensure all edge differences stay within 2 pixels. A static SessionList-only fixture cannot establish the full workspace contract.

Supporting artifacts: [research.md](research.md), [data-model.md](data-model.md), [contracts/workspace.md](contracts/workspace.md), and [quickstart.md](quickstart.md).

## Verification and Completion Gates

- All FR-001 through FR-011 and SC-001 through SC-005 receive mapped component, service/state or real-browser checks.
- Web Go and direct web revision-method tests record zero shared revision calls for completed, restored and stale-revision states. New-query writing still invokes StartAsync with the current validated query and limits.
- Persisted session fields and shared revision interface/console tests remain unchanged; internal reviewer processing is still part of initial writing runs.
- Focused tests run after each small edit; then run the complete web suite and build the web/core dependency. Use Release while the existing Debug server locks files; do not stop user-owned servers.
- Capture/inspect baseline and final screenshots and actual element boxes; keyboard tests cover remaining controls and no focusable revision/reviewer fields. Production identity/access and Sign out behavior must not be weakened for tests.
- No speculative live Azure validation is required: no cloud resource or persistence contract changes. Planning creates documentation only, not application edits.
