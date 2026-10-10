# Feature Specification: Stop Active Agent Work

**Feature Branch**: `015-stop-agent-work`

**Created**: 2026-10-09

**Status**: Draft

**Input**: User description: "Add a stop button on the button bar. Make it red with yellow text that says Stop. It should be disabled until the user presses Go and it should become disabled again when the document window is filled. That is, it should only be enabled while the Researcher, Author or Reviewer is active. Pressing Stop should cleanly stop whatever work is being done by any of those three agents and should return everything to the state they would be in if the user pressed New. Between the time the user presses stop and the agents are stopped put the word stopping where it usually displays Reviewer Started, etc. (below the button bar). Pressing the stop button disables all the buttons (including stop) until everything is stopped."

## Clarifications

### Session 2026-10-09

- Q: Should Stop also cancel agent work started by Revise, not only work started by Go? → A: Yes. Stop cancels agent work started by Go or Revise.
- Q: If an agent is still running 10 seconds after Stop is pressed, what should the workspace do? → A: Display `Cancellation failed`, keep all buttons disabled, and reset only after the active work ends.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Stop active writing work (Priority: P1)

As a user whose research or writing is still in progress, I want to stop the active work and return to a fresh workspace so I can abandon the current task without waiting for its result or having late output alter the workspace.

**Why this priority**: Users need a predictable way to cancel work at any of the three agent stages and immediately prepare for a different task once cancellation has completed.

**Independent Test**: Start work that reaches each of the Researcher, Author, and Reviewer stages in turn. Stop during each stage and verify cancellation, status, button lockout, and the final New-equivalent workspace state.

**Acceptance Scenarios**:

1. **Given** the Researcher, Author, or Reviewer is actively processing work started by Go or Revise, **When** the user presses Stop, **Then** the status line below the button bar displays `stopping`, every button including Stop is disabled, and no subsequent agent stage starts.
2. **Given** cancellation is in progress, **When** the active agent work has ended, **Then** the workspace returns to the same fresh state as activating New, including cleared task and refinement inputs, document content, reviewer notes, and transient workflow messages.
3. **Given** Stop has completed and the workspace is in its New-equivalent state, **When** the user starts a new task, **Then** no output from the cancelled work appears in or changes the new task.

---

### User Story 2 - See when stopping is available and complete (Priority: P1)

As a user, I want Stop to be available only while a supported agent is working and to see a clear stopping status while cancellation completes, so I can distinguish active work from a completed or idle workspace.

**Why this priority**: Accurate availability prevents ineffective stop requests, while the status and button lockout make the asynchronous transition clear and prevent conflicting actions.

**Independent Test**: Check Stop in the initial/New state, during each supported agent stage for both Go and Revise, during cancellation, and after the current writing operation is complete; verify its label, colors, and enabled state in each case.

**Acceptance Scenarios**:

1. **Given** no Researcher, Author, or Reviewer work is active, **When** the button bar is displayed, **Then** Stop is disabled.
2. **Given** one of those agents is actively processing, **When** the button bar is displayed, **Then** Stop is enabled and has a red background with yellow text labeled `Stop`.
3. **Given** the document is populated and agent work is complete, **When** the workspace settles, **Then** Stop is disabled.
4. **Given** the user has pressed Stop and agent work has not yet ended, **When** fewer than 10 seconds have elapsed, **Then** the status line below the button bar says `stopping` and all buttons remain disabled, including Stop.
5. **Given** all work has ended after a stop request, **When** the workspace resets, **Then** Stop is disabled and the other buttons reflect the New-equivalent state.
6. **Given** a revision operation is active while the previous draft remains visible, **When** the Author or Reviewer is processing, **Then** Stop is enabled.
7. **Given** agent work is still active 10 seconds after Stop is pressed, **When** the cancellation wait reaches 10 seconds, **Then** the status line displays `Cancellation failed`, all buttons remain disabled, and the workspace does not reset until the work ends.

### Edge Cases

- If an agent completes at the same time that Stop is pressed, the workspace still resets to the New-equivalent state and late output from that run is discarded.
- A stop request MUST NOT start another agent stage while the active stage is winding down.
- Stop MUST remain unavailable during idle periods between agent calls and after the document is populated, because it is available only while one of the three named agents is active.
- Repeated activation during stopping is prevented because Stop and all other buttons are disabled.
- The `stopping` status remains visible until work ends or 10 seconds elapse; if work is still active after that, `Cancellation failed` remains visible until work ends, and the workspace MUST NOT reset early while an agent can still update it.
- If active work has not ended within 10 seconds of a stop request, the status changes to `Cancellation failed`; the button lockout remains until that work ends.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The button bar MUST include a button labeled `Stop`, with a red background and yellow text.
- **FR-002**: Stop MUST be disabled when no Researcher, Author, or Reviewer work is active, including the initial/New state, idle gaps between agent calls, and after the current writing operation is complete.
- **FR-003**: Stop MUST be enabled only while the Researcher, Author, or Reviewer is actively processing work initiated by Go or Revise.
- **FR-004**: When Stop is activated, the system MUST request cancellation of the currently active work performed by any active Researcher, Author, or Reviewer.
- **FR-005**: From acceptance of a stop request, the status line below the button bar MUST display `stopping` and every button MUST be disabled, including Stop. If active work has not ended after 10 seconds, the status MUST change to `Cancellation failed`; all buttons MUST remain disabled until the work ends.
- **FR-006**: While stopping, the workflow MUST NOT start another agent stage or allow another button action.
- **FR-007**: The system MUST wait until all active agent work has ended before resetting the workspace.
- **FR-008**: After all active work has ended, the system MUST discard the cancelled run's visible and transient state and restore the same workspace state as activating New.
- **FR-009**: Results arriving from a cancelled run after the reset MUST NOT repopulate or otherwise change the fresh workspace.
- **FR-010**: When cancellation completes, Stop MUST be disabled and the other buttons MUST follow the existing New-state availability rules.

### Key Entities *(include if feature involves data)*

- **Agent activity**: The active processing state of the Researcher, Author, or Reviewer for the current writing task.
- **Stop transition**: The period after the user requests cancellation and before all active agent work has ended; it includes the `stopping` status and disabled buttons.
- **Fresh workspace state**: The user-visible state reached by activating New, including its inputs, document area, notes, transient messages, and control availability.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In 100% of tested idle, initial/New, and completed-work states, Stop is disabled; in 100% of tested active Researcher, Author, and Reviewer states, Stop is enabled.
- **SC-002**: In 100% of cancellation tests, the status line displays `stopping` initially, changes to `Cancellation failed` if work remains active after 10 seconds, and every button stays disabled until all active work has ended.
- **SC-003**: In 100% of cancellation tests, no further agent stage starts and no late result changes the workspace after reset.
- **SC-004**: In 100% of cancellation tests, the final user-visible workspace matches the state produced by activating New.
- **SC-005**: Users can identify that work is stopping and when the workspace is ready for a new task in at least 90% of usability checks.

## Assumptions

- The existing New action defines the canonical fresh workspace state; Stop restores that state after active work has fully ended.
- The existing status line below the button bar is the place to display `stopping` until cancellation completes.
- Stop applies to work initiated by Go or Revise and is available only during active processing by the Researcher, Author, or Reviewer; it is not an always-available workflow reset command.
- Cancelled output is discarded from the active workspace, including output that arrives after the stop transition has completed.
