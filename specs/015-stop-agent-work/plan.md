# Implementation Plan: Stop Active Agent Work

**Branch**: `015-stop-agent-work` | **Date**: 2026-10-09 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/015-stop-agent-work/spec.md`

## Summary

Add a Stop command for active Researcher, Author, or Reviewer work started by Go or Revise. Carry the active workflow stage to the web workspace, cancel through the existing operation token, disable every command while stopping, show `Cancellation failed` after 10 seconds if work remains active, and reset to the existing New state only after the operation ends. Preserve operation-version checks so stale updates cannot repopulate the reset workspace.

## Technical Context

**Language/Version**: C# / .NET 10

**Primary Dependencies**: Blazor Interactive Server, the existing Microsoft Agent Framework workflow, xUnit, and bUnit

**Storage**: Existing session store; no schema or store-contract change

**Testing**: `BlogWriter.Web.Tests` service tests and bUnit browser/component tests

**Target Platform**: Authenticated ASP.NET Core interactive-server web app

**Project Type**: Web application

**Performance Goals**: Show `stopping` immediately after Stop is accepted. If the operation remains active after 10 seconds, show `Cancellation failed`. Do not reset or re-enable controls until the operation ends.

**Constraints**: Reuse the existing cancellation-token path and operation-version guard. Preserve hosted-agent boundaries, workflow topology, shared token budgeting, New/List/Quit behavior, and session format. Stop is available only while Researcher, Author, or Reviewer is the active stage. Keep all command buttons disabled during stop handling.

**Scale/Scope**: One active Go or Revise operation per workspace. Changes are limited to workflow lifecycle reporting, workspace state/service, command-bar presentation, CSS, and focused web tests.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Status | Rationale |
| --- | --- | --- |
| Hosted-agent boundaries | PASS | No hosted-agent model logic, prompt, deployment, or transport changes are planned. |
| MAF-native workflow | PASS | Preserve the bounded workflow topology and existing cancellation-token propagation; surface the existing executor lifecycle without introducing another orchestration path. |
| Identity, secrets, and budget | PASS | No credentials or model calls are added. Cancellation remains on the existing operation path and does not bypass the shared token cap. |
| Testability and observability | PASS | Keep cancellation behavior in the testable workspace service and report status through the existing live workflow status line. Cover the state transitions with focused tests. |
| Simple, compatible evolution | PASS | No persistence schema, public session-store contract, or hosted-agent prompt changes; preserve existing New/List/Quit behavior. |

**Pre-design gate**: PASS. MAF Doctor reported grade B, with three existing warnings and six uncapped call sites, but no scanner errors or silent-starvation risks. These findings are outside this feature; the plan adds no model call sites.

**Post-design gate**: PASS. The design reports existing executor lifecycle at the workspace boundary, uses the established cancellation token and version guard, adds no hosted-agent or persistence contract changes, and keeps all new behavior covered by focused service and UI tests.

## Project Structure

### Documentation (this feature)

```text
specs/015-stop-agent-work/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/       # Not needed; no external interface is added
└── tasks.md         # Generated later by /speckit-tasks
```

### Source Code (repository root)

```text
WorkflowOutputUpdate.cs
WorkflowOutputPublisher.cs
BlogWorkflow.cs
BlogWriter.Web/
├── Components/
│   ├── CommandBar.razor
│   ├── Pages/Home.razor
│   └── WorkflowLog.razor
├── Services/
│   ├── BlogWorkspaceService.cs
│   └── BlogWorkspaceState.cs
└── wwwroot/app.css
BlogWriter.Web.Tests/
├── BlogWorkspaceServiceTests.cs
└── WorkspaceBrowserTests.cs
```

**Structure Decision**: Keep the feature in the existing Blazor workspace service and command-bar component. Carry typed stage lifecycle information through the existing output channel. No new project, public interface, persistence entity, or hosted-agent change is required.

## Complexity Tracking

No constitution violations or complexity exceptions are identified.
