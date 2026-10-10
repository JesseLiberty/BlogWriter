# Feature Specification: Session List Details

**Feature Branch**: Not created (no branch hook configured)

**Created**: 2026-10-10

**Status**: Draft

**Input**: User description: "Update the contents of the list when the list button is pressed so that each entry also shows the MinWords and MaxWords and the first 50 words of the Draft. Lay this out nicely with the information already provided."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Compare Saved Writing Sessions (Priority: P1)

As a writer, I want the List button to show each saved session's word limits and opening draft text alongside its existing details, so I can identify the session I want without opening multiple drafts.

**Why this priority**: These details provide the requested value directly in the existing session-selection workflow.

**Independent Test**: Save two sessions with different word limits and drafts, press List, and compare each entry with its saved content without restoring either session.

**Acceptance Scenarios**:

1. **Given** saved sessions with different word limits and drafts longer than 50 words, **When** the writer presses List, **Then** every entry retains its session number, task, and update time and also shows its own minimum words, maximum words, and exactly the first 50 draft words in their original order.
2. **Given** a saved draft containing between 1 and 50 words, **When** the writer presses List, **Then** its entry shows the entire draft without a truncation indicator.
3. **Given** a saved session with an absent, empty, or whitespace-only draft, **When** the writer presses List, **Then** its entry shows "No draft yet" and still shows its word limits and existing details.
4. **Given** a displayed list and a subsequently saved change to a session's word limits or draft, **When** the writer presses List again, **Then** the entry reflects the latest saved values rather than the previous list contents or another session's current values.

---

### User Story 2 - Scan and Select Readable Entries (Priority: P2)

As a writer, I want a clearly organized list that remains readable on narrow and wide screens, so I can scan the added information and restore the intended session using the existing controls.

**Why this priority**: The added content must remain easy to compare without obscuring existing information or interfering with selection.

**Independent Test**: Display multiple entries with long tasks and draft previews at narrow and wide screen widths, then restore an entry using a pointer and the keyboard.

**Acceptance Scenarios**:

1. **Given** entries containing all requested details, **When** the list is displayed, **Then** each entry groups its number and task as identifying information, its update time and labeled word limits as supporting information, and its labeled draft preview below that information.
2. **Given** long tasks, long draft words, and large word-limit values, **When** the list is viewed at widths of 320 and 1280 pixels, **Then** all entry content wraps within its bounds without overlapping, hiding other fields, or requiring horizontal scrolling.
3. **Given** an entry that can currently be selected, **When** the writer activates it by pointer or keyboard, **Then** the corresponding saved session is restored exactly as before and the preview does not change the saved draft.
4. **Given** a state in which session selection is disabled, **When** the expanded list is displayed, **Then** entries remain unselectable under the existing rules.

### Edge Cases

- No saved sessions: preserve the existing empty-list message and do not show placeholder entries.
- A draft of exactly 50 words: display all 50 words with no truncation indicator.
- A draft of 51 or more words: display only the first 50 words followed by an ellipsis, outside the preview's word count.
- Repeated spaces, tabs, and line breaks: treat each nonempty whitespace-delimited segment as one word and present the preview with single spaces between words.
- Punctuation and hyphenated text: preserve them within their original word; do not split on punctuation or hyphens.
- Draft text containing formatting or markup: display it as literal text, not interactive or interpreted content; the stored draft is not changed.
- Older sessions without saved word limits: show the same effective default limits that restoring that session would use.
- Multiple sessions with the same task: retain their numbers and update times so the added preview can help distinguish them.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Pressing the existing web List button MUST display the expanded information for every saved session returned by the existing list workflow, preserving its current visibility rules, order, numbering, task, and update time. Acceptance: User Story 1 scenario 1 and the no-sessions edge case.
- **FR-002**: Each entry MUST display that session's minimum and maximum word limits with distinct labels "Min words" and "Max words". Older sessions MUST display the effective defaults used when restoring them. Acceptance: User Story 1 scenarios 1 and 3 and the older-sessions edge case.
- **FR-003**: Each entry MUST display a "Draft preview" containing the first 50 nonempty whitespace-delimited words of that session's saved Draft, or all words when fewer than 50 exist. Word order and punctuation MUST be preserved, with single spaces between words. Acceptance: User Story 1 scenarios 1 and 2 and the whitespace and punctuation edge cases.
- **FR-004**: Entries MUST show an ellipsis only when additional draft words are omitted and MUST show "No draft yet" for absent, empty, or whitespace-only drafts. Acceptance: User Story 1 scenarios 2 and 3 and the 50-word and 51-word edge cases.
- **FR-005**: Each List invocation MUST reflect the latest saved word limits and draft for each returned session, without substituting unsaved workspace content or values from another session. Acceptance: User Story 1 scenario 4.
- **FR-006**: Entries MUST use a consistent hierarchy: identifying information first, supporting update time and word limits next, and the labeled draft preview below, while remaining consistent with the surrounding workspace appearance. Acceptance: User Story 2 scenario 1.
- **FR-007**: Entry content MUST remain readable at narrow and wide screen widths, wrap long text within its bounds, and avoid overlapping fields or horizontal scrolling. Acceptance: User Story 2 scenario 2.
- **FR-008**: The expanded entries MUST preserve existing pointer and keyboard selection, disabled-selection rules, and session restoration behavior. Displaying a preview MUST NOT modify the stored draft or word limits. Acceptance: User Story 2 scenarios 3 and 4.
- **FR-009**: Preview content MUST be presented as literal text without executing or interpreting embedded markup. Acceptance: the formatting-or-markup edge case.

### Key Entities *(include if feature involves data)*

- **Saved writing session**: An existing writing session with an identity, task, update time, saved minimum and maximum word limits, and saved draft. Its content is the source of each entry's details.
- **Session list entry**: A selectable representation of one saved session, combining its existing number, task, and update time with labeled word limits and a read-only draft preview.
- **Draft preview**: A display-only excerpt of up to 50 words, associated with exactly one saved session and indicating whether additional words were omitted.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a representative set of at least 10 saved sessions with distinct word limits and drafts, 100% of entries display the correct limits and preview alongside their existing details after pressing List.
- **SC-002**: Preview checks covering missing drafts, whitespace-only drafts, 1, 49, 50, and 51 words, longer drafts, mixed whitespace, punctuation, and markup all produce the specified result, with zero words after the 50th included.
- **SC-003**: At widths of 320 and 1280 pixels, all tested entry fields remain readable with zero overlap or horizontal scrolling, including long tasks and unbroken draft words.
- **SC-004**: In every pointer, keyboard, and disabled-selection acceptance check, the list preserves the correct restoration or prevention behavior and leaves saved content unchanged.
- **SC-005**: In a usability review with five writers and ten distinguishable sessions, at least four writers identify and restore the requested session on their first selection within 30 seconds using only the list details.

## Assumptions

- "List button" refers to the existing saved-session list in the web writing workspace; console list output is outside this feature's scope.
- The preview uses the saved Draft as literal text, not a generated summary or a rendered document. Formatting symbols attached to words remain visible and count as part of those words.
- Existing session access, ordering, empty-list behavior, and effective word-limit defaults remain authoritative; this feature does not introduce new filtering, sorting, permissions, or defaults.
- The existing saved-session workflow can supply each session's word limits and draft. No new content needs to be generated to populate the list.
- List display does not start writing or reviewing work. Changes to agent behavior, draft editing, persistence policy, and restoration semantics are outside scope.
