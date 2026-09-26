# Feature Specification: Revision Window Availability

**Feature Branch**: `012-revision-window-availability`

**Created**: 2026-09-26

**Status**: Draft

**Input**: User description: "Fix revision window. Should be disabled when query window is empty, otherwise it should be enabled."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Keep revision available for a supplied query (Priority: P1)

As a user entering a writing query, I want the revision window enabled whenever the query window contains text so I can provide revision instructions without waiting for a draft or clearing an earlier request.

**Why this priority**: The revision window is currently gated by additional conditions beyond query presence, preventing users from entering or editing revision instructions when the query is present.

**Independent Test**: Exercise an empty and non-empty query in the normal New and Draft workspace states, with both empty and populated revision text and with or without draft content; verify Revision is disabled only for an empty query, subject to existing operation/mode lockouts.

**Acceptance Scenarios**:

1. **Given** the workspace is in an editable New or Draft state and the query window is empty, **When** the workspace renders or the query is cleared, **Then** the revision window is disabled.
2. **Given** the workspace is in an editable New or Draft state and the query window contains non-whitespace text, **When** the workspace renders, **Then** the revision window is enabled even when no draft exists.
3. **Given** the workspace is in an editable New or Draft state and both the query window and revision window contain text, **When** the user edits either window, **Then** the revision window remains enabled while the query remains non-empty.
4. **Given** a non-empty query is shortened or cleared, **When** the query becomes empty, **Then** the revision window becomes disabled immediately.
5. **Given** Go or List is active, or the workspace has ended, **When** the user views the revision window, **Then** the existing busy, list, or ended-state lockout continues to apply.

### Edge Cases

- A query containing only whitespace is treated as empty.
- A query changed from non-empty to empty disables Revision without requiring a page reload.
- Existing Go/List busy-state and Ended-mode lockouts take precedence over query-based availability.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: In an editable New or Draft workspace state, the revision window MUST be disabled when the query window is empty or contains only whitespace.
- **FR-002**: In an editable New or Draft workspace state, the revision window MUST be enabled whenever the query window contains non-whitespace text, regardless of whether a draft exists.
- **FR-003**: In an editable New or Draft workspace state, the presence of text in the revision window MUST NOT by itself disable that window while the query remains non-empty.
- **FR-004**: The revision window's enabled state MUST update immediately when the query changes between empty and non-empty.
- **FR-005**: Existing Go-processing, List-mode/loading, and Ended-mode lockouts MUST continue to disable the revision window.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All defined combinations of empty/non-empty query, empty/non-empty revision text, and draft absent/present produce the specified revision availability in 100% of acceptance checks while the workspace is editable.
- **SC-002**: Query clearing disables the revision window on the next workspace render, with no page reload or additional user action.
- **SC-003**: Users can enter or edit revision instructions whenever a non-empty query is present and the workspace is not busy, in at least 95% of usability-check attempts.
- **SC-004**: Go, List, and Ended lockouts remain effective in 100% of their applicable acceptance checks.

## Assumptions

- “Empty” includes whitespace-only query text.
- “Otherwise enabled” applies to normal editable New and Draft states; existing busy, List, and Ended lockouts remain higher-priority protections.
- Revision text being present is not a reason to disable its own input while the query remains non-empty.
- This feature changes only revision-window availability; it does not change query submission, draft generation, saved-session behavior, or persistence.