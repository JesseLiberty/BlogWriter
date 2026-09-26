# Feature Specification: Workspace Control Availability

**Feature Branch**: `011-workspace-control-states`

**Created**: 2026-09-26

**Status**: Draft

**Input**: User description: "When New is pressed, enable the new query, disable the revision request, and enable all buttons. When Go is pressed, disable the new query, revision, min, max, List, New, Quit, and Help until Draft is populated. When the new query is empty, disable revision. When List is pressed, disable query, revision, min, max, List, Go, Quit, and Help. When Draft is populated, enable all buttons and windows except New Query, which should be disabled, and disable Revision if it is populated. When a button is disabled, turn it light gray."

## Clarifications

### Session 2026-09-26

- Q: While a Go request is processing, should the Go button itself be disabled to prevent another submission? → A: Disable Go until the draft is populated.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Start a fresh writing task (Priority: P1)

As a user beginning or resetting a writing task, I want the query controls and commands to reflect that state so I can enter a topic without accidentally editing a revision request.

**Why this priority**: This is the entry point for creating a draft and establishes a clear, usable control state.

**Independent Test**: Start in the workspace and activate New; verify the new-query field is enabled, the revision field is disabled, other commands are enabled, and disabled buttons appear light gray.

**Acceptance Scenarios**:

1. **Given** the workspace is showing a populated draft, **When** the user activates New, **Then** the new-query field is enabled, the revision field is disabled, and all command buttons are enabled.
2. **Given** the new-query field contains no text, **When** the workspace renders or the text is cleared, **Then** the revision field is disabled.
3. **Given** any command button is disabled, **When** the user views it, **Then** it has a light-gray appearance that distinguishes it from enabled buttons.

---

### User Story 2 - Prevent conflicting actions while work is underway (Priority: P1)

As a user who has submitted a query or opened the saved-session list, I want controls that could conflict with that operation disabled until the workspace is ready for another action.

**Why this priority**: Preventing edits and navigation during an in-flight operation or list transition avoids inconsistent workspace state.

**Independent Test**: Trigger Go and List separately; verify each specified field and command becomes disabled, then verify controls return to the correct state when the operation reaches its next stable state.

**Acceptance Scenarios**:

1. **Given** a new query is ready to submit, **When** the user activates Go, **Then** the new-query, revision, minimum-word, and maximum-word fields and the List, New, Quit, and Help buttons are disabled until a draft is populated.
2. **Given** Go is processing and no draft has been populated, **When** the user views the Go control, **Then** Go is disabled and cannot start a duplicate operation.
3. **Given** the user activates List, **When** the session list is being displayed, **Then** the new-query, revision, minimum-word, maximum-word fields and the List, Go, Quit, and Help buttons are disabled.
4. **Given** List mode is active, **When** the user chooses a saved session, **Then** the session-selection control remains usable and, once the selected session's draft is populated, the workspace follows the populated-draft control rules.
5. **Given** a Go or List operation ends without reaching its expected populated-draft state, **When** the operation reports failure or cancellation, **Then** the workspace controls recover to an enabled/disabled state appropriate to the available draft and mode rather than remaining locked.

---

### User Story 3 - Continue working with a populated draft (Priority: P1)

As a user with a draft, I want to revise it while preventing changes to the original query and avoiding edits to a revision request that has already been entered.

**Why this priority**: Draft review and revision are the main follow-on workflow after the first generation.

**Independent Test**: Populate a draft, verify the new-query field is disabled and other commands and applicable inputs are enabled; enter a revision request and verify that field becomes disabled.

**Acceptance Scenarios**:

1. **Given** a draft is populated and no revision request is entered, **When** the workspace settles, **Then** all command buttons and applicable fields are enabled except the new-query field, which is disabled.
2. **Given** a draft is populated and the revision field is empty, **When** the user enters a revision request, **Then** the revision field becomes disabled while the other applicable controls remain enabled.
3. **Given** a revision request has been submitted, **When** a revised draft is populated, **Then** the workspace returns to the populated-draft control rules.

### Edge Cases

- If the query is cleared after text was entered, the revision field becomes disabled immediately.
- If no saved sessions are available, List mode still leaves New available so the user can leave the list state.
- If a selected saved session contains no draft, the workspace must not be treated as being in the populated-draft state.
- If processing is canceled or fails before a draft is populated, controls must not remain disabled indefinitely.
- A disabled button's light-gray visual treatment must not remove its disabled semantics or make enabled and disabled actions indistinguishable.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Activating New MUST enable the new-query field, disable the revision field, and enable every command button.
- **FR-002**: The revision field MUST be disabled whenever the new-query field is empty.
- **FR-003**: Activating Go MUST disable the new-query, revision, minimum-word, and maximum-word fields and the List, New, Quit, and Help buttons until a draft is populated.
- **FR-004**: The Go button MUST be disabled while its operation is processing and remain disabled until a draft is populated.
- **FR-005**: Activating List MUST disable the new-query, revision, minimum-word, and maximum-word fields and the List, Go, Quit, and Help buttons.
- **FR-006**: In List mode, New MUST remain enabled and the saved-session selection control MUST remain usable.
- **FR-007**: When a draft is populated, all command buttons and applicable fields MUST be enabled except the new-query field, which MUST be disabled.
- **FR-008**: When a draft is populated, the revision field MUST be enabled while empty and MUST become disabled once it contains a revision request.
- **FR-009**: After a revision request produces a populated revised draft, the workspace MUST apply the populated-draft control rules again.
- **FR-010**: Any disabled command button MUST use a light-gray appearance; its disabled state MUST remain programmatically available to assistive technology.
- **FR-011**: When Go or List ends by failure or cancellation, the workspace MUST restore controls according to the current mode and whether a draft is populated.

### Key Entities *(include if feature involves data)*

- **Workspace control state**: The enabled or disabled status of the query fields, revision field, word-count fields, command buttons, and saved-session selection control as the writing workflow changes.
- **Draft**: The generated or revised writing content whose presence determines when the workspace leaves the Go-in-progress state.
- **Revision request**: User-provided instructions for changing a populated draft; an entered request is not editable until the workspace returns to an eligible state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All five requested workspace transitions produce the specified control availability in 100% of acceptance tests.
- **SC-002**: Every disabled command button is visibly light gray in all workspace states where it is disabled.
- **SC-003**: Users can identify which workspace actions are available without attempting disabled actions in at least 90% of moderated usability checks.
- **SC-004**: No duplicate Go operation can be started while a submitted operation is still waiting for a populated draft.
- **SC-005**: Failed or canceled Go and List operations leave the workspace usable, with no controls unintentionally remaining disabled.

## Assumptions

- The initial workspace uses the same control rules as a fresh workspace after New: new query enabled, revision disabled, and command buttons enabled.
- “Windows” means the new-query, revision, and minimum/maximum word-count input fields.
- New remains enabled in List mode because the List rule did not request disabling New; the saved-session selection control also remains enabled so a session can be chosen.
- A populated draft means non-empty draft content. If processing fails or is canceled before that point, controls return to the state appropriate to the current mode and draft.
- Light gray applies to disabled buttons; disabled input fields retain their existing disabled-state treatment.