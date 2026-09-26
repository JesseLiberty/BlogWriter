# Implementation Plan: Workspace Control Availability

**Branch**: `011-workspace-control-states` | **Date**: 2026-09-26 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/011-workspace-control-states/spec.md`

## Summary

Make control availability a consistent consequence of workspace state across New, Go processing, List loading/list mode, and populated-draft/revision states. Keep the policy in `BlogWorkspaceState`, expose it to the existing Blazor input and command components, and use native disabled semantics with the stylesheet's light-gray disabled treatment. Track List loading explicitly so the lockout begins before the asynchronous session query completes and stale completion cannot override a subsequent New action.

## Technical Context

**Language/Version**: C# / .NET 10

**Primary Dependencies**: ASP.NET Core Blazor Interactive Server, Microsoft.Extensions.DependencyInjection, bUnit, xUnit

**Storage**: No storage changes; control availability is transient UI state and is not persisted in Cosmos DB or session files.

**Testing**: Focused bUnit component tests and BlogWorkspaceService unit tests in `BlogWriter.Web.Tests`.

**Target Platform**: Authenticated browser workspace hosted by ASP.NET Core Blazor Server.

**Project Type**: Existing web application with a shared application service and component library.

**Performance Goals**: Availability changes render on the existing Blazor event/update cycle; no additional network request is introduced for control-state decisions.

**Constraints**: Preserve current command labels, layout, prompt values, session persistence, operation cancellation, and accessible native disabled state. New remains available in List mode. Go is disabled while its operation is in flight. Disabled command buttons use the existing light-gray styling.

**Scale/Scope**: One workspace state model, the Home page, prompt/word-range/command components, relevant CSS, and focused web tests. No hosted-agent, workflow topology, API, or persistence changes.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Design Constitution Check

- **I. Hosted-Agent Boundaries**: PASS. The feature only controls the web workspace and does not alter hosted-agent boundaries or communication.
- **II. MAF-Native Workflow Composition**: PASS. No workflow topology or agent behavior changes; control state remains explicit in the existing workspace state model.
- **III. Identity, Secrets, and Budget Control**: PASS. Authentication, secrets, and model-call budget behavior are untouched.
- **IV. Testable and Observable Behavior**: PASS. State transitions and rendered disabled attributes can be exercised with existing service and bUnit tests.
- **V. Simple, Compatible Evolution**: PASS. No public service contract, persisted session shape, prompt contract, or deployment configuration changes are planned.

No gate violations or unresolved technical clarifications.

## Design

1. Derive control availability from the existing workspace mode, draft/revision latch, prompt contents, and writing-operation flag. Add a distinct transient List-loading state because `ListAsync` currently awaits session retrieval before switching to `WorkspaceMode.List`.
2. Keep component markup declarative: `Home.razor` passes the derived availability into `PromptInput`, `WordRangeInput`, and `CommandBar`; components apply native `disabled` attributes to their owned controls.
3. During writing, disable every control specified by the contract, including Go itself, and re-enable according to the resulting draft state or the existing failure/cancellation recovery path.
4. During List loading and stable List mode, lock the controls named in the contract while leaving New available. Keep session selection usable once sessions are displayed. Ensure a late List response cannot replace a newer workspace state after New.
5. Preserve and verify the existing disabled-button color rule in `BlogWriter.Web/wwwroot/app.css`; add or adjust only selectors needed for consistent light-gray rendering across command variants.
6. Add focused service and component tests for each state transition, list-loading behavior, revision-text gating, and disabled-button attributes. Verify CSS rendering in the browser as a manual visual check.

## Project Structure

### Documentation (this feature)

```text
specs/011-workspace-control-states/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── workspace-control-state.md
└── tasks.md
```

### Source Code (repository root)

```text
BlogWriter.Web/
├── Components/
│   ├── Pages/Home.razor
│   ├── PromptInput.razor
│   ├── WordRangeInput.razor
│   └── CommandBar.razor
├── Services/
│   ├── BlogWorkspaceState.cs
│   └── BlogWorkspaceService.cs
└── wwwroot/app.css

BlogWriter.Web.Tests/
├── HomePageTests.cs
├── CommandBarTests.cs
└── BlogWorkspaceServiceTests.cs
```

**Structure Decision**: Keep implementation within the existing Blazor web project and web test project. The workspace state/service own transition facts, components render those facts, and app.css owns disabled appearance.

## Phase 0: Research

See [research.md](research.md). Existing workspace state and components provide the required integration points; no external technology or API research is needed.

## Phase 1: Design & Contracts

- [data-model.md](data-model.md) defines transient workspace control state and derived availability.
- [contracts/workspace-control-state.md](contracts/workspace-control-state.md) defines the user-visible control matrix.
- [quickstart.md](quickstart.md) lists focused automated and visual validation steps.

### Post-Design Constitution Check

- **I. Hosted-Agent Boundaries**: PASS. No hosted-agent code or transport changes.
- **II. MAF-Native Workflow Composition**: PASS. Existing workflow remains unchanged; only UI state and the web-service boundary are affected.
- **III. Identity, Secrets, and Budget Control**: PASS. No effect.
- **IV. Testable and Observable Behavior**: PASS. Each state transition has a focused automated check; existing operation logs and cancellation remain intact.
- **V. Simple, Compatible Evolution**: PASS. No persistence or public application-service contract changes; the List-loading marker remains transient.

No post-design gate violations. No complexity exception is required.

## Complexity Tracking

No constitution violations or additional project boundaries are introduced.
