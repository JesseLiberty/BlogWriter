# Implementation Plan: Revision Window Availability

**Branch**: `012-revision-window-availability` | **Date**: 2026-09-26 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/012-revision-window-availability/spec.md`

## Summary

Make revision-window availability depend on query content in editable New and Draft states: disable it for empty/whitespace-only query text and enable it for non-whitespace query text regardless of draft presence or existing revision text. Preserve higher-priority processing, List, and Ended lockouts. Implement the predicate in the existing `BlogWorkspaceState` and keep Home's existing component binding; do not change Go submission routing or persisted data.

## Technical Context

**Language/Version**: C# / .NET 10

**Primary Dependencies**: ASP.NET Core Blazor Interactive Server, bUnit, xUnit

**Storage**: None; revision-window availability is transient UI state.

**Testing**: Focused `BlogWorkspaceServiceTests` and `HomePageTests` in `BlogWriter.Web.Tests`.

**Target Platform**: Authenticated browser workspace hosted by ASP.NET Core Blazor Server.

**Project Type**: Existing web application.

**Performance Goals**: Availability updates on the existing Blazor render cycle without extra network requests.

**Constraints**: Preserve the existing processing/List/Ended lockouts, initial-query submission rules, visual design, accessible native disabled state, and persisted session shape. This feature changes only the revision input's availability.

**Scale/Scope**: One derived state property, its Home binding, and focused tests. No component API, workflow routing, agent, database, or deployment changes are expected.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Design Constitution Check

- **I. Hosted-Agent Boundaries**: PASS. No hosted-agent code or communication changes.
- **II. MAF-Native Workflow Composition**: PASS. No workflow topology or `ResearchState` changes; submission routing remains unchanged.
- **III. Identity, Secrets, and Budget Control**: PASS. No identity, secret, or model-call changes.
- **IV. Testable and Observable Behavior**: PASS. The derived predicate and rendered disabled state can be tested without live services.
- **V. Simple, Compatible Evolution**: PASS. Reuse the existing state property and binding; no public contracts or persisted formats change.

No gate violations or unresolved technical clarifications.

## Design

1. Update `BlogWorkspaceState.IsRevisionInputEnabled` so the query's non-whitespace status determines availability during editable New and Draft states.
2. Continue to apply `IsProcessing`, `IsListing`, List mode, and Ended mode as lockouts. Do not gate the field on `HasDraft`, `IsRevisionRequested`, or `RevisionPrompt` contents.
3. Keep `Home.razor` bound to the existing state property; no new component parameter or input event is needed.
4. Add state-level tests covering empty, whitespace-only, and non-empty query text across draft/revision-content combinations and lockout states. Add a rendered Home test to verify immediate DOM disabled-state changes after query edits.
5. Leave `BlogWorkspaceService.SubmitAsync` and its routing behavior unchanged. Enabled input availability does not imply a revision can be submitted without an eligible session.

## Project Structure

### Documentation (this feature)

```text
specs/012-revision-window-availability/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/
    └── revision-window-availability.md
```

### Source Code (repository root)

```text
BlogWriter.Web/
├── Services/BlogWorkspaceState.cs
└── Components/Pages/Home.razor

BlogWriter.Web.Tests/
├── BlogWorkspaceServiceTests.cs
└── HomePageTests.cs
```

**Structure Decision**: Keep the implementation in the existing workspace state property and verify both the property and its current Home-page binding through focused tests.

## Phase 0: Research

See [research.md](research.md). The existing state and binding are sufficient; there are no external dependencies or technical unknowns.

## Phase 1: Design & Contracts

- [data-model.md](data-model.md) describes the query-derived availability and preserved higher-priority lockouts.
- [contracts/revision-window-availability.md](contracts/revision-window-availability.md) defines the user-visible state matrix.
- [quickstart.md](quickstart.md) provides focused automated validation steps.

### Post-Design Constitution Check

- **I. Hosted-Agent Boundaries**: PASS. Agent boundaries are untouched.
- **II. MAF-Native Workflow Composition**: PASS. No MAF workflow behavior changes; input availability does not alter submission routing.
- **III. Identity, Secrets, and Budget Control**: PASS. No effect.
- **IV. Testable and Observable Behavior**: PASS. State combinations and rendered changes receive focused tests.
- **V. Simple, Compatible Evolution**: PASS. Existing state property, component binding, and session format are retained.

No post-design gate violations. No complexity exception is required.

## Complexity Tracking

No constitution violations or additional project boundaries are introduced.
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
