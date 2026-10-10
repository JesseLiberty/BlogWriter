# Tasks: Stop Active Agent Work

**Input**: Design documents from `specs/015-stop-agent-work/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `quickstart.md`

**Tests**: Focused service, workflow-lifecycle, and bUnit tests are included because the specification and constitution require verification of the cancellation and state transitions.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel with another marked task because it changes a different file and has no dependency on incomplete work.
- **[Story]**: User story label for story-specific tasks.
- Every task names the file or project it changes or validates.

## Phase 1: Setup

**Purpose**: Restore the existing web and test projects; no new project or package is required.

- [X] T001 Restore dependencies for `BlogWriter.Web/BlogWriter.Web.csproj` and `BlogWriter.Web.Tests/BlogWriter.Web.Tests.csproj`.

## Phase 2: Foundational

**Purpose**: Make the active workflow stage available to workspace state before implementing Stop behavior.

- [X] T002 Add a typed workflow-agent-stage value to `WorkflowOutputUpdate.cs`, including a value for no active stage and the Blogger, Researcher, Author, and Reviewer stages.
- [X] T003 [P] Publish the stage on executor-invoked lifecycle updates and clear it on executor-completed or executor-failed updates in `WorkflowOutputPublisher.cs` and `BlogWorkflow.cs`.
- [X] T004 [P] Add `ActiveAgentStage`, `IsStopping`, and a derived Stop-eligibility property to `BlogWriter.Web/Services/BlogWorkspaceState.cs`; keep operation-busy state true until the active task ends.

**Checkpoint**: Lifecycle updates identify eligible stages; workspace state can distinguish an eligible agent from Blogger, idle, and stopping states.

## Phase 3: User Story 1 - Stop Active Writing Work (Priority: P1)

**Goal**: Let users cancel Go or Revise work, lock the workspace while cancellation completes, and restore the New state without accepting stale output.

**Independent Test**: Use a controlled session service for both Go and Revise. Request Stop, verify the operation token is canceled and all buttons remain disabled, then release the operation and verify the workspace matches New and ignores late output.

### Tests for User Story 1

- [X] T005 [US1] Add workspace-service tests for Stop during Go and Revise, immediate stopping status, button lockout, completion before and after the 10-second timeout, New-state reset, and suppressed late output in `BlogWriter.Web.Tests/BlogWorkspaceServiceTests.cs`.

### Implementation for User Story 1

- [X] T006 [US1] Implement a dedicated Stop operation in `BlogWriter.Web/Services/BlogWorkspaceService.cs` that invalidates the active operation version, cancels the existing token, retains the task and cancellation source, reports `Cancellation failed` after 10 seconds if still active, waits for task completion, then clears to New and disposes cancellation resources without changing New/List/Quit timeout behavior.
- [X] T007 [US1] Add the Stop callback and enabled-state parameters to `BlogWriter.Web/Components/CommandBar.razor`, wire them to `BlogWorkspaceService.StopAsync` and workspace state in `BlogWriter.Web/Components/Pages/Home.razor`, and ensure every command button is disabled throughout stopping.

**Checkpoint**: Service and command-bar tests demonstrate cancellation, lockout, timeout status, New-state reset, and stale-result suppression.

## Phase 4: User Story 2 - See When Stopping Is Available and Complete (Priority: P1)

**Goal**: Enable Stop only for Researcher, Author, or Reviewer activity and make the button and status presentation unambiguous.

**Independent Test**: Exercise lifecycle updates for Blogger, Researcher, Author, and Reviewer during both Go and Revise. Verify Stop availability, red/yellow enabled styling, disabled-state styling, stopping status, and timeout status.

### Tests for User Story 2

- [X] T008 [P] [US2] Add workflow tests that verify typed stage information is published for executor invocation and cleared on completion or failure in `BlogWriter.Tests/BlogWorkflowTests.cs`.
- [X] T009 [P] [US2] Add bUnit tests for initial, Blogger, eligible-agent, idle, completed, stopping, and cancellation-failed button/status states during Go and Revise in `BlogWriter.Web.Tests/WorkspaceBrowserTests.cs`.

### Implementation for User Story 2

- [X] T010 [US2] Style the enabled Stop button with a red background and yellow `Stop` text while preserving the existing disabled-button treatment in `BlogWriter.Web/wwwroot/app.css`.

**Checkpoint**: UI tests prove Stop is enabled only for the three named agents and that status and button styling match the specification.

## Phase 5: Polish and Cross-Cutting Validation

**Purpose**: Run the documented focused validation and confirm no unrelated behavior regressed.

- [X] T011 Run the Stop-focused tests and web-project build from `specs/015-stop-agent-work/quickstart.md`; confirm New/List/Quit cancellation behavior remains unchanged in `BlogWriter.Web.Tests/BlogWorkspaceServiceTests.cs`.

## Dependencies and Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies; restore the existing web and test projects.
- **Foundational (Phase 2)**: Starts after setup. T002 precedes T003 and T004; T003 and T004 can proceed in parallel after T002.
- **User Story 1 (Phase 3)**: Starts after the foundational stage contract and workspace state are available. T005 precedes T006; T006 precedes T007.
- **User Story 2 (Phase 4)**: Starts after the Stop operation and command-bar integration from User Story 1. T008 and T009 can proceed in parallel; T010 completes the visual behavior.
- **Polish (Phase 5)**: Starts after both user stories.

### User Story Dependencies

- **User Story 1 (P1)**: Depends on foundational stage and workspace state tasks; delivers the cancellation action and New-state reset.
- **User Story 2 (P1)**: Depends on User Story 1's Stop action; verifies exact stage eligibility, timeout/status presentation, and enabled button colors.

### Parallel Opportunities

- After T002, T003 (`WorkflowOutputPublisher.cs` and `BlogWorkflow.cs`) and T004 (`BlogWorkspaceState.cs`) touch separate files and can proceed in parallel.
- After User Story 1 is integrated, T008 (`BlogWriter.Tests/BlogWorkflowTests.cs`) and T009 (`BlogWriter.Web.Tests/WorkspaceBrowserTests.cs`) can proceed in parallel.

## Parallel Example: Foundational Work

```text
T003: Publish typed executor lifecycle stages in WorkflowOutputPublisher.cs and BlogWorkflow.cs
T004: Add stage and stopping state to BlogWorkspaceState.cs
```

## Parallel Example: User Story 2 Tests

```text
T008: Verify lifecycle-stage updates in BlogWriter.Tests/BlogWorkflowTests.cs
T009: Verify button states and status in BlogWriter.Web.Tests/WorkspaceBrowserTests.cs
```

## Implementation Strategy

### MVP First

1. Complete Setup and Foundational phases.
2. Complete User Story 1 to prove cancellation, lockout, deferred reset, and stale-output suppression.
3. Validate the User Story 1 service behavior independently.

User Story 2 is also P1 and is required before release to enforce agent-specific Stop eligibility and the requested red/yellow presentation.

### Incremental Delivery

1. Establish typed stage reporting and workspace state.
2. Deliver and validate Stop cancellation/reset behavior.
3. Add and validate exact stage availability, timeout/status behavior, and visual styling.
4. Run the focused tests and web build in `quickstart.md`.

## Phase 6: Convergence

- [X] T012 Add a cancellation regression test to `BlogWriter.Tests/BlogWorkflowTests.cs` that cancels a gated Researcher, Author, or Reviewer executor and proves its successor agent is never invoked per FR-006 and US1/AC1 (missing)
