# Data Model: Workspace Control Availability

This feature changes only transient presentation state. It does not add fields to `BlogSession` or `ResearchState`, and it does not change the persisted Cosmos DB document shape.

## Workspace Control State

**Owner**: `BlogWorkspaceState`

**Purpose**: Represent the active workspace lifecycle and provide derived availability for inputs, command buttons, and saved-session selection.

| Attribute | Type | Meaning | Validation / transition |
|---|---|---|---|
| `Mode` | `WorkspaceMode` | New, Draft, List, or Ended view state | Existing state; transition operations set the mode. |
| `IsProcessing` | Boolean | A Go-triggered writing operation is running | Set before invoking the session service; cleared in operation cleanup. |
| `IsListing` (or equivalent) | Boolean | Saved sessions are being fetched | Set before the List request; cleared on success/failure or superseding transition. |
| `HasDraft` | Derived Boolean | Draft contains non-whitespace content | Existing derived property. |
| `IsRevisionRequested` | Boolean | Workspace has entered the draft/revision lifecycle | Set when a draft or selected session is loaded; cleared by New. |
| `InitialPrompt` | String | New query text | Editable only in a fresh New state and not while an operation is busy. |
| `RevisionPrompt` | String | User's revision request or selected session follow-up | Editable only in an eligible draft state and while empty; populated text disables its field. |
| Word-range inputs | Strings | Minimum and maximum requested word counts | Editable in New and Draft states; disabled while writing or listing. |
| Selection state | Existing fields | Saved-session number, validation, and availability | Usable in stable List mode; does not make the other locked controls available. |

## Derived Availability

Component parameters should receive derived booleans rather than compute policy independently:

- New query enabled: fresh New state, not processing/listing, and no latched draft/revision state.
- Revision request enabled: eligible populated-draft state, not processing/listing, and revision text is empty.
- Word range enabled: not processing/listing and workspace is in New or Draft mode.
- Go enabled: not processing/listing and workspace is not Ended; Go's handler still validates required prompt/session inputs.
- List enabled: not processing/listing and workspace is not Ended or already in List mode.
- New enabled: not processing and workspace is not Ended; remains enabled in List mode as specified.
- Quit and Help enabled: not processing/listing and workspace is not Ended.
- Session selection enabled: stable List mode and not processing/listing.

## State Transitions

| Event | State change | Control consequence |
|---|---|---|
| Initial render / New completes | `Mode=New`, prompts cleared, processing/listing false, revision latch cleared | New query and word range enabled; revision disabled; commands enabled. |
| New query empty | Query remains empty | Revision remains disabled. |
| Go starts | `IsProcessing=true`; draft cleared by existing operation path | Disable prompts, word range, all commands including Go; preserve entered text. |
| Go completes with draft | Draft published; processing false; mode Draft | New query disabled; revision enabled only when empty; word range and commands enabled. |
| Go fails/cancels before draft | Processing false; preserve current mode/data as provided by existing recovery path | Recompute availability; do not leave controls locked. |
| List starts | `IsListing=true` before awaiting session retrieval | Disable query/revision/range and List/Go/Quit/Help; leave New available. |
| List completes | `IsListing=false`, `Mode=List`, summaries available | Session selector usable; the same specified controls stay disabled; New remains enabled. |
| List fails | `IsListing=false`, existing recovery mode/error path retained | Controls recompute from recovered mode; New remains available. |
| New supersedes List | Advance/validate transition generation and clear listing state | A late response cannot change the workspace back to List. |
| Saved session selected and draft loaded | Existing selection/submission flow publishes the session draft | Apply populated-draft rules; selection control is no longer shown. |

## Persistence Boundary

Only the existing session data continues to be persisted. Busy flags and derived enabled/disabled values are per-circuit UI state and must not be serialized.