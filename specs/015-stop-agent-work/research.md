# Research: Stop Active Agent Work

## Decision: Reuse the Existing Cancellation Path

The web workspace creates a `CancellationTokenSource` for each Go or Revise operation and passes its token through `IBlogWriterSessionService` to `IBlogWorkflow.RunAsync`. The workflow passes the token to streaming execution and stream enumeration.

**Rationale**: This is the existing, testable path for stopping workflow work. The feature does not need another hosted-agent call path or a new package.

**Alternatives considered**: Calling hosted agents directly from the web layer was rejected because it bypasses the existing workflow boundary.

## Decision: Give Stop Its Own Wait-and-Reset Behavior

New, List, and Quit currently share `CancelActiveOperationAsync`, which clears `IsProcessing` before waiting and returns after the configured 10-second timeout even if the operation is still running. Stop must not use that completion behavior. It will retain the active task and cancellation source, keep the workspace busy, and await task completion. At 10 seconds, if the task is still active, it changes the status to `Cancellation failed` but remains locked. It resets to New only after the task ends.

**Rationale**: This satisfies the clarified requirement that all buttons remain disabled until active work has ended, without changing existing New/List/Quit behavior.

**Alternatives considered**: Resetting after 10 seconds was rejected because the operation may still be active and could race a new task.

## Decision: Track the Active Workflow Stage Explicitly

`BlogWorkflow` already receives executor-invoked, completed, and failed events and publishes lifecycle updates. `WorkflowOutputUpdate` currently carries message text but no typed stage. Extend that internal update contract with a stage value mapped from the executor lifecycle, then maintain the active stage in workspace state. Enable Stop only for Researcher, Author, or Reviewer; keep it disabled during Blogger and between executor invocations. Do not infer stage by parsing display strings.

**Rationale**: The current `IsProcessing` flag covers the complete operation, including stages where Stop must be unavailable. A typed stage provides an unambiguous UI eligibility rule.

**Alternatives considered**: Reusing only `IsProcessing` was rejected because it cannot distinguish eligible agent stages. Parsing lifecycle message text was rejected as brittle.

## Decision: Preserve Operation-Version Suppression

Invalidate the current operation version when Stop is accepted so subsequent progress, reviewer feedback, and completed results from that run are ignored. Keep the operation task tracked until it settles; then clear workspace state through the existing New reset routine and release cancellation resources.

**Rationale**: The workspace already uses operation versions to suppress stale output after New/List/Quit transitions. Retaining this guard prevents cancelled output from appearing in a later task.

**Alternatives considered**: Allowing the canceled task's normal output handlers to update the workspace was rejected because its result must not overwrite the New state.

## Decision: Keep Persistence and Workflow Topology Unchanged

Do not add session-store methods, delete records, alter the saved-session schema, change hosted agents, or change workflow edges. Preserve the existing persistence lifecycle and the existing New behavior; this feature resets the active workspace, not saved-session history.

**Rationale**: The specification requires the user-visible workspace to match New and does not request session-history cleanup. Keeping the current lifecycle avoids a new persistence contract.

**Alternatives considered**: Deleting an abandoned run's session record was rejected as out of scope and would require a new store operation and ownership/concurrency rules.

## Verified Constraints

- One operation is allowed per workspace at a time; duplicate submissions are rejected.
- The cancellation timeout is injectable through the existing workspace-service constructor, allowing deterministic short-timeout tests.
- Initial session creation happens before workflow execution and final session save happens after it; storage behavior remains unchanged by this plan.
- MAF Doctor baseline is grade B: three existing warnings, six uncapped call sites, zero scanner errors, and zero silent-starvation risks. No new model call sites are planned.
