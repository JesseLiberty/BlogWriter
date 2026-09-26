# Tasks: Workspace Control Availability

**Input**: Design documents from `specs/011-workspace-control-states/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/workspace-control-state.md`, `quickstart.md`

**Tests**: Focused tests are included because the specification requires acceptance coverage for the state transitions and the project constitution requires tests for state handling.

**Organization**: Tasks are grouped by the three P1 user stories. Shared control-state plumbing is foundational; List loading and populated-draft revision behavior are implemented in their respective story phases.

## Phase 1: Setup

**Purpose**: Reuse the existing web application and web test project; no project initialization or dependency changes are required.

No setup tasks are required.

---

## Phase 2: Foundational

**Purpose**: Establish the shared transient availability policy and bind it to the existing controls before implementing story-specific transitions.

- [X] T001 [P] Add transient list-loading state and derived per-control availability properties to `BlogWriter.Web/Services/BlogWorkspaceState.cs` for New, processing, List, draft, revision, and ended states.
- [X] T002 Pass the availability properties from `BlogWriter.Web/Components/Pages/Home.razor` into `BlogWriter.Web/Components/WordRangeInput.razor` and `BlogWriter.Web/Components/CommandBar.razor`; add native `disabled` attributes to the owned inputs and buttons while preserving `PromptInput`'s existing `Disabled` parameter.
- [X] T003 [P] Update disabled-button selectors in `BlogWriter.Web/wwwroot/app.css` so light-gray background and text remain visible for every disabled command variant, including hover and active states.

**Checkpoint**: Workspace state drives native disabled semantics and disabled commands share the light-gray treatment.

---

## Phase 3: User Story 1 - Start a fresh writing task (Priority: P1)

**Goal**: New restores the fresh-query state, leaves revision disabled, and makes the other commands and word-range inputs available; an empty new query keeps revision disabled.

**Independent Test**: Activate New from a populated workspace and verify the new-query input and all command buttons are enabled, revision is disabled, word-range inputs are enabled, and clearing an empty new query cannot enable revision.

### Tests for User Story 1

- [X] T004 [US1] Add bUnit assertions in `BlogWriter.Web.Tests/HomePageTests.cs` for New reset, enabled commands and word-range inputs, disabled revision input, and revision remaining disabled when the new query is empty.
- [X] T005 [US1] Update the New reset path in `BlogWriter.Web/Services/BlogWorkspaceService.cs` to clear transient list-loading state and restore the fresh workspace mode, prompts, revision latch, and default word range; require a non-empty query in `BlogWriter.Web/Services/BlogWorkspaceState.cs` before enabling Revision.

**Checkpoint**: Fresh New state and empty-query revision gating are covered and pass independently.

---

## Phase 4: User Story 2 - Prevent conflicting actions while work is underway (Priority: P1)

**Goal**: Go disables its specified inputs and commands, including Go itself, until draft publication; List disables its specified controls while loading and in List mode, keeps New available, and ignores stale list results after a newer New transition.

**Independent Test**: Hold Go and List requests pending in tests, inspect disabled attributes and state before completion, then complete, fail, cancel, or supersede each operation and verify controls recover correctly.

### Tests for User Story 2

- [X] T006 [P] [US2] Add service tests in `BlogWriter.Web.Tests/BlogWorkspaceServiceTests.cs` proving List marks loading before awaiting session retrieval, clears loading on success and failure, and does not overwrite New when an older List request completes late.
- [X] T007 [P] [US2] Add bUnit tests in `BlogWriter.Web.Tests/HomePageTests.cs` proving Go disables the specified controls while pending and List disables its specified controls while leaving New and available session selection usable.

### Implementation for User Story 2

- [X] T008 [US2] Set and clear the List-loading state around the asynchronous session fetch and guard against stale completion after New in `BlogWriter.Web/Services/BlogWorkspaceService.cs`.

**Checkpoint**: Go and List lockouts begin at operation start, and success, failure, cancellation, and superseding New transitions leave the workspace usable.

---

## Phase 5: User Story 3 - Continue working with a populated draft (Priority: P1)

**Goal**: With a populated draft, keep New Query disabled, enable the remaining applicable controls, enable Revision only while its field is empty, and reapply those rules after a revision completes.

**Independent Test**: Load or create a draft, verify New Query is disabled and Revision is enabled while empty, enter revision text and verify Revision disables, then complete a revision and verify the populated-draft rules again.

### Tests for User Story 3

- [X] T009 [US3] Add bUnit coverage in `BlogWriter.Web.Tests/HomePageTests.cs` for populated-draft availability, empty versus populated revision input, and availability after a revised draft is published.

### Implementation for User Story 3

- [X] T010 [US3] Update revision availability in `BlogWriter.Web/Services/BlogWorkspaceState.cs` so the field is enabled only in an eligible populated-draft state when no revision text is present and no operation is busy.

**Checkpoint**: Draft and revision control states pass independently, including after revision completion.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Validate visual feedback and the complete focused regression slice.

- [X] T011 Verify every disabled command variant appears light gray and remains non-actionable in the browser by following `specs/011-workspace-control-states/quickstart.md`.
- [X] T012 Run the focused test commands in `specs/011-workspace-control-states/quickstart.md` and resolve any regressions in the affected `BlogWriter.Web` and `BlogWriter.Web.Tests` files.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No work; existing projects and dependencies are reused.
- **Foundational (Phase 2)**: T001 and T003 can run in parallel; T002 depends on T001. All user-story work waits for T001-T003.
- **User Stories (Phases 3-5)**: Complete in priority order. US2 follows US1 because both extend the same Home page test suite; US3 follows US2 because its controls depend on the populated-draft lifecycle.
- **Polish (Phase 6)**: Depends on all three user stories.

### User Story Dependencies

- **US1 (P1)**: Depends on the foundational state policy and component bindings; otherwise independent.
- **US2 (P1)**: Depends on the foundational state policy and component bindings and follows US1's reset behavior. Its service and Home page tests can be authored in parallel; the implementation follows those tests.
- **US3 (P1)**: Depends on foundational bindings and the draft lifecycle from US2; test before tightening the revision availability rule.

### Parallel Opportunities

- **Foundational**: T001 and T003 touch different files and can run in parallel; T002 follows T001.
- **US2**: T006 and T007 touch different test files and can run in parallel; T008 follows both.
- **US1**: No parallel tasks; one focused component test task validates the shared foundation.
- **US3**: No parallel tasks; the state rule follows its component-level regression test.

## Parallel Example: User Story 2

```text
Task: T006 Add List lifecycle and stale-completion tests in BlogWriter.Web.Tests/BlogWorkspaceServiceTests.cs
Task: T007 Add Go/List rendered-control tests in BlogWriter.Web.Tests/HomePageTests.cs
```

## Implementation Strategy

### MVP First (User Story 1)

1. Complete foundational state policy and component bindings (T001-T003).
2. Complete US1 assertions (T004).
3. Validate New reset and empty-query revision gating before proceeding.

### Incremental Delivery

1. Add US2 tests, then List loading lifecycle and Go/List disabled-state behavior.
2. Add US3 tests, then revision-text gating for populated drafts.
3. Run visual and focused regression checks from `quickstart.md`.

### Task Format Validation

All implementation and validation tasks use `- [ ] T###`, include a `[US#]` label only in user-story phases, mark `[P]` only for independent work, and name the affected repository file path.

## Phase 7: Convergence

- [X] T013 Verify the current `BlogWriter.Web/wwwroot/app.css` in a browser fixture, confirming disabled New, List, Go, Quit, and Help buttons remain light gray and non-actionable at rest, hover, and active states per SC-002 / T011 (partial).