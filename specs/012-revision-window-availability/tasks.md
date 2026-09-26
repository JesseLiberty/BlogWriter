# Tasks: Revision Window Availability

**Input**: Design documents from `specs/012-revision-window-availability/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/revision-window-availability.md`, `quickstart.md`

**Tests**: Focused state and rendered UI tests are included to cover each acceptance scenario and the constitution's state-change testing requirement.

**Organization**: All feature work belongs to the single P1 user story. The two test files can be updated independently before the shared state predicate is changed.

## Phase 1: Setup

**Purpose**: Reuse the existing web and web-test projects; no project or dependency setup is required.

No setup tasks are required.

---

## Phase 2: Foundational

**Purpose**: No cross-story foundation is needed for this single-state-property change.

No foundational tasks are required.

---

## Phase 3: User Story 1 - Keep revision available for a supplied query (Priority: P1) 🎯 MVP

**Goal**: In editable New and Draft states, Revision is enabled exactly when the query contains non-whitespace text; processing, List, and Ended lockouts remain effective.

**Independent Test**: Cover empty, whitespace-only, and non-empty query values with draft absent/present and revision text empty/populated; render Home to verify query edits immediately update the revision field's native disabled state and busy/mode lockouts remain.

### Tests for User Story 1

- [X] T001 [P] [US1] Add state tests in `BlogWriter.Web.Tests/BlogWorkspaceServiceTests.cs` proving query emptiness alone controls revision availability during editable New/Draft states, independent of draft and revision contents, while processing/List/Ended states stay disabled.
- [X] T002 [P] [US1] Add rendered Home tests in `BlogWriter.Web.Tests/HomePageTests.cs` proving Revision enables for non-empty query before draft creation, remains enabled with existing revision text, disables immediately when query is cleared, and stays disabled during Go/List lockouts.

### Implementation for User Story 1

- [X] T003 [US1] Update `IsRevisionInputEnabled` in `BlogWriter.Web/Services/BlogWorkspaceState.cs` to retain processing/List/Ended guards and use only non-whitespace `InitialPrompt` as the availability condition for editable New/Draft states; remove draft, revision-latch, and revision-text gates.

**Checkpoint**: Query-derived revision availability and preserved lockouts pass both state-level and rendered component tests.

---

## Phase 4: Polish & Cross-Cutting Validation

**Purpose**: Run the feature's focused automated validation and affected-project build.

- [X] T004 Run the focused tests in `specs/012-revision-window-availability/quickstart.md` and build `BlogWriter.Web/BlogWriter.Web.csproj` in Release configuration.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No work; existing projects and dependencies are reused.
- **Foundational (Phase 2)**: No shared prerequisites are needed.
- **User Story 1 (Phase 3)**: Tests T001 and T002 can be written in parallel; T003 follows both tests.
- **Polish (Phase 4)**: T004 depends on T001-T003.

### User Story Dependencies

- **US1 (P1)**: No dependency on another feature story; the existing `PromptInput` binding is retained.

### Parallel Opportunities

- T001 and T002 touch separate test files and can be implemented in parallel before T003.
- No other tasks are parallelizable because the implementation and final validation depend on those tests.

## Parallel Example: User Story 1

```text
Task: T001 Add state predicate coverage in BlogWriter.Web.Tests/BlogWorkspaceServiceTests.cs
Task: T002 Add rendered workspace coverage in BlogWriter.Web.Tests/HomePageTests.cs
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Write T001 and T002 and confirm they expose the current extra draft/revision-text gates.
2. Implement T003 in the existing workspace state property.
3. Run T004 and confirm the full focused acceptance slice and affected web project build pass.

### Incremental Delivery

This feature has one story; deliver the revised availability rule as a single independently testable increment.

### Task Format Validation

All tasks use `- [ ] T###`, include `[US1]` only within the user-story phase, use `[P]` only for independent work, and include the exact affected file path.