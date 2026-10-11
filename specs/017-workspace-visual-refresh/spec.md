# Feature Specification: Workspace Visual Refresh

**Feature Branch**: Not created (no branch hook configured)

**Created**: 2026-10-11

**Status**: Draft

**Input**: User description: "The following changes are all aesthetic. No behavior changes. Change the title from Write. Review. Refine to Research. Write. Revise. Remove the Revision Request prompt and window. Widen the New Query window by 20%. Remove the Reviewer Notes prompt and window. Put the target number of Words to the right of the New Query Window. Stack the Target Words as follows: Text: Target Words. Below that Minimum. Below that the Minimum entry window, Below that Maximum, below that the maximum window. For the minimum and maximum Have the prompts and windows align. Widen the draft window so that it aligns with the Query and Minimum/Maximum stack. Align the buttons with the Query window. Remove the text A researcher and author for blog pages. Change text that now says `jesseliberty@gmail.com` and replace it with Jesse Liberty and below that Senior AI Engineer. Keep the sign out link. Remove the 01 on the Draft window."

## Clarifications

### Session 2026-10-11

- Q: After removing the Revision request input, should users lose the ability to enter a separate revision request on this screen? -> A: Remove revision capability, including user-initiated revision submission from the web workspace; preserve existing saved data.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Focused Writing Workspace (Priority: P1)

As a writer, I want a wider New query input, adjacent word targets, and a full-width Draft area so the visible workspace concentrates on the query and its output.

**Why this priority**: This is the primary visual restructuring requested for the everyday writing screen.

**Independent Test**: Open the workspace at a desktop width, compare its query width with the pre-refresh layout at the same width, and inspect the visible inputs, output area, and command alignment without submitting work.

**Acceptance Scenarios**:

1. **Given** the writing workspace, **When** it is displayed, **Then** the Revision request label and input and the Reviewer notes heading and output area are absent from the visible layout, leaving New query and Draft visible.
2. **Given** a desktop viewport of 1280 pixels and the same page width and zoom before and after the refresh, **When** the New query input is measured, **Then** its width is 1.20 times its pre-refresh width, within a 2-pixel measurement tolerance.
3. **Given** the desktop layout, **When** the writer inspects Target Words, **Then** the controls appear to the right of New query in this vertical order: Target Words, Minimum, minimum input, Maximum, maximum input; both labels and both inputs share a common left edge, and the two inputs have equal widths.
4. **Given** the desktop layout, **When** its major edges are compared, **Then** Draft spans the combined query and word-target area, with its outer left edge aligned to the New query column and its outer right edge aligned to the word-target column; the command row is placed below Draft with its left edge aligned to the New query column.

---

### User Story 2 - Updated Workspace Identity (Priority: P2)

As a writer, I want the revised title and author identity to appear without redundant description or numbering, so the workspace presents the requested professional identity clearly.

**Why this priority**: These changes establish the requested visible wording independently of the layout restructuring.

**Independent Test**: Inspect the title, signed-in header, and Draft heading; verify the replacement wording and activate the unchanged Sign out link in an isolated test session.

**Acceptance Scenarios**:

1. **Given** the workspace, **When** the heading is displayed, **Then** it reads "Research. Write. Revise." and neither "Write. Review. Refine." nor "A researcher and author for blog pages." appears.
2. **Given** the signed-in header that currently displays the user's email, **When** the refreshed header is displayed, **Then** it shows "Jesse Liberty" with "Senior AI Engineer" directly below it, retains the Sign out link, and does not show `jesseliberty@gmail.com` as the identity text.
3. **Given** the Draft area, **When** its heading is displayed, **Then** it retains "Draft" but no longer shows the decorative "01" marker.

---

### User Story 3 - Preserve Non-Revision Operations (Priority: P1)

As a writer, I want revision capability removed while new-query writing, word-limit validation, saved sessions, and accessibility continue to work as before.

**Why this priority**: Removing user-initiated revision capability is the clarified exception to the original aesthetic-only request; all other behavior must remain unchanged.

**Independent Test**: Run the existing command, word-limit, saved-session, and restoration checks against the refreshed workspace, then inspect and use the remaining controls on narrow and wide screens.

**Acceptance Scenarios**:

1. **Given** a query and valid word limits, **When** the writer edits the remaining controls and invokes a non-revision command, **Then** the same input values, operation, enabled/disabled rules, and resulting state transitions apply as before the visual refresh.
2. **Given** invalid minimum or maximum values, **When** the writer edits a word target, **Then** existing validation and error messages remain associated with the corresponding visible input and do not overlap the neighboring controls or Draft.
3. **Given** a saved session containing draft, revision, and reviewer data, **When** List and restoration are used, **Then** persisted data remains unchanged, the session list still appears in the Draft area, and restored revision text cannot cause a user-initiated revision submission; the query and word limits remain available for the existing new-run workflow.
4. **Given** a 320-pixel viewport, **When** the workspace is displayed, **Then** the query, word-target stack, Draft, buttons, identity text, and Sign out link remain visible or reachable by vertical scrolling without overlapping or requiring horizontal page scrolling; the target stack may reflow beneath New query when side-by-side placement would not fit.
5. **Given** the refreshed workspace, **When** the writer navigates by keyboard, **Then** all remaining interactive controls retain visible focus, correct labels, and existing activation behavior, and removed sections contain no hidden focusable controls.
6. **Given** a completed draft or restored session containing a prior revision request, **When** the writer uses Go or another workspace command, **Then** no user-initiated revision operation can be submitted, and New query is not repurposed as a revision input.

### Edge Cases

- Long query text and long unbroken draft words: wrap or scroll within their existing content controls without expanding the page horizontally.
- Word-limit error messages: fit beneath the associated input without disrupting the minimum/maximum stack or covering adjacent content.
- Busy, stopped, failed, list, restored, and ended states: retain non-revision transitions and command availability; no state may re-enable user-initiated revision submission. The ended screen need not display editing controls that were previously unavailable there.
- No draft or no saved sessions: preserve the existing empty-state wording and placement within the widened Draft area.
- Restored revision and reviewer content: preserve persisted values, but never use restored revision text to trigger user-initiated revision processing. Reviewer processing within a new-query writing run remains unchanged.
- Signed-out or unauthorized view: preserve existing access rules; the fixed display name and role do not grant access or replace the authenticated session owner.
- Narrow screens or increased text size: reflow identity and controls while keeping text legible and the Sign out link accessible.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The workspace heading MUST read "Research. Write. Revise." and the previous heading and descriptive sentence MUST no longer be visible. Acceptance: US2 scenario 1.
- **FR-002**: The workspace MUST remove the visible Revision request label/input and Reviewer notes heading/output area, not merely collapse them into another visible panel or leave hidden focusable controls. Acceptance: US1 scenario 1 and US3 scenario 5.
- **FR-003**: At a 1280-pixel viewport, the New query input MUST be 20% wider than the pre-refresh input at the same page width, zoom, and styling context, within 2 pixels; on smaller screens it MUST adapt to available space instead of overflowing. Acceptance: US1 scenario 2 and US3 scenario 4.
- **FR-004**: The Target Words group MUST sit to the right of New query on desktop and show, in vertical order, "Target Words", "Minimum", its input, "Maximum", and its input. Minimum and maximum labels and inputs MUST have matching left alignment; both inputs MUST have equal width. The former horizontal "to" separator MUST not appear in this stack. Acceptance: US1 scenario 3.
- **FR-005**: The Draft area MUST span the combined query and Target Words columns, with its outer edges aligned to their combined outer edges within 2 pixels on desktop. The session list and empty Draft state MUST use this same area. Acceptance: US1 scenario 4 and US3 scenario 3.
- **FR-006**: The existing command buttons MUST remain below Draft with the command row's left edge aligned to the New query column within 2 pixels on desktop. Keep their relative order, focus behavior, and non-revision actions and availability; no button may submit a user-initiated revision. Acceptance: US1 scenario 4 and US3 scenarios 1, 5, and 6.
- **FR-007**: The signed-in header MUST replace its current email identity display with "Jesse Liberty" and "Senior AI Engineer" on the next line, while retaining the Sign out link and its current destination and action. This is presentation only and MUST NOT alter authentication, session ownership, authorization, or account records. Acceptance: US2 scenario 2 and the unauthorized-view edge case.
- **FR-008**: The Draft heading MUST retain "Draft" and remove its decorative "01" marker. Acceptance: US2 scenario 3.
- **FR-009**: Relocated word inputs MUST retain their values, existing validation, accessible label associations, errors, and enabled/disabled rules. Their labels MUST use the full words "Minimum" and "Maximum". Acceptance: US1 scenario 3 and US3 scenarios 1, 2, and 5.
- **FR-010**: The web workspace MUST remove user-initiated revision capability, not only its visible input. No command, hidden control, or restored revision text may submit a revision operation, and New query MUST NOT act as a replacement revision input. Existing persisted revision/review data MUST remain intact. New-query writing and its internal reviewer processing, cancellation, saved-session retrieval, and non-revision restoration/persistence behavior MUST remain unchanged. No alternative revision or review interaction is introduced. Acceptance: US3 scenarios 1, 3, and 6 and the restored-content edge case.
- **FR-011**: At widths of 320 and 1280 pixels, all remaining labels, inputs, output, buttons, identity text, and links MUST fit their layout without text overlap or horizontal page scrolling. On narrow screens, the Target Words stack MAY move below New query while preserving its internal order and alignment. Acceptance: US3 scenarios 2, 4, and 5.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A visual content check passes for 100% of requested replacements and removals: the revised heading, absent description, absent revision/reviewer sections, name and role on separate lines, retained Sign out, and absent Draft number.
- **SC-002**: At 1280 pixels, query width is 120% of the recorded pre-refresh width within 2 pixels, and each specified Draft, word-target, and command alignment differs by no more than 2 pixels.
- **SC-003**: At 320 and 1280 pixels, all layout checks pass with zero overlapping fields and zero horizontal page overflow, including long content and word-limit errors.
- **SC-004**: All behavior checks for non-revision commands, word-limit editing/validation, saved-session listing/restoration, and sign-out pass with unchanged non-revision outcomes; saved revision/review data remains unchanged. Checks across completed and restored sessions record zero user-initiated revision submissions through any workspace command.
- **SC-005**: Keyboard checks reach and activate every remaining enabled control, reach zero controls in removed sections, and find associated visible labels for both word-limit inputs.

## Assumptions

- "Window" means an existing visible input or output region, not a separate operating-system window.
- The clarification to remove revision capability supersedes the original no-behavior-change constraint only for user-initiated revisions in the web workspace. Existing saved revision/review data is retained; no replacement revision interaction is added. Console revision functionality and internal reviewer processing within a new-query run are outside this removal's scope.
- The new name and role are fixed display copy for this requested workspace, not a change to account profile information or a new identity-management feature.
- Record the current New query input width at the reference desktop viewport before implementation. The 20% increase is relative to that input, not a 20-percentage-point increase in page coverage.
- "Align the buttons with the Query window" means matching the command row's starting edge to the query column, while retaining command order and behavior; it does not require each individual button to span the query width.
- Desktop uses the requested side-by-side query/word-target arrangement. Narrow screens may stack those regions to preserve legibility and accessibility.
- Existing typography, colors, output scrolling, workflow log, session-list details, and Sign out behavior remain unless a minimal layout adjustment is needed for the requested alignment. No new features, infrastructure, or agent implementation changes are in scope; workspace revision submission is removed as explicitly clarified.
