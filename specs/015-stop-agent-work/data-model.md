# Data Model: Stop Active Agent Work

## Workspace Activity

Represents the current Go or Revise operation in the browser workspace.

| Attribute | Type | Meaning |
| --- | --- | --- |
| `IsProcessing` | Boolean | An operation task is active. It remains true while cancellation is pending and through the 10-second status transition. |
| `ActiveAgentStage` | Nullable stage | The current workflow executor stage, derived from lifecycle events. Stop eligibility is limited to Researcher, Author, and Reviewer. |
| `IsStopping` | Boolean | Stop was accepted and cancellation is being awaited. It disables Stop and all other command buttons. |
| `CurrentStatus` | Nullable text | Existing live status line. It shows `stopping`, then `Cancellation failed` if the operation remains active after 10 seconds. |
| `OperationVersion` | Integer | Existing monotonically increasing generation used to ignore output from superseded work. |
| `CancellationTokenSource` | Operation resource | Existing cancellation resource retained until the operation task ends. |
| `ActiveOperation` | Task | Existing operation task retained so reset cannot happen before completion. |

The stage values correspond to workflow executors. Blogger is represented so eligibility can explicitly exclude it; idle periods have no active stage.

## Stop Transition

| From | Event | State and visible result |
| --- | --- | --- |
| Idle | Go or Revise starts | Operation is processing; Stop remains disabled until an eligible agent stage is active. |
| Processing, eligible stage | User activates Stop | Mark stopping, invalidate the operation version, request cancellation, display `stopping`, and disable every command button. |
| Stopping | Operation ends before 10 seconds | Discard this operation's output and reset the workspace to the existing New state. |
| Stopping | 10 seconds elapse while operation remains active | Display `Cancellation failed`; keep every command button disabled and continue awaiting the same task. |
| Cancellation failed | Operation ends | Discard this operation's output and reset the workspace to the existing New state. |
| Processing, Blogger stage or no active stage | Any time before an eligible stage | Stop remains disabled. |

## Fresh Workspace State

The existing `ClearWorkspace(WorkspaceMode.New)` behavior is the source of truth. It clears prompt inputs, word-range edits, draft, review, workflow output, active session, current status, and transient mode flags, and restores default word counts. The feature adds no persisted fields or session-store contract changes.
