# Data Model: Revision Window Availability

This feature changes no persisted data. It adjusts a derived, transient UI availability value in the existing workspace state.

## Revision Window Availability

**Owner**: `BlogWorkspaceState`

**Inputs**:

| Input | Meaning |
|---|---|
| `InitialPrompt` | Current query-window text; whitespace-only counts as empty. |
| `Mode` | Workspace mode; List and Ended continue to lock the revision window. |
| `IsProcessing` | Active Go operation; locks the revision window. |
| `IsListing` | Active saved-session fetch; locks the revision window. |

**Availability rule**:

- If processing or listing is active, disable the revision window.
- If the workspace is List or Ended, disable the revision window.
- Otherwise, enable it exactly when `InitialPrompt` contains non-whitespace text.
- Draft presence, revision-latch state, and existing revision text do not affect the availability result.

## State Matrix

| Workspace condition | Query | Draft | Revision text | Revision window |
|---|---|---|---|---|
| Editable New | Empty/whitespace | Absent | Any | Disabled |
| Editable New | Non-empty | Absent | Empty or populated | Enabled |
| Editable Draft | Empty/whitespace | Absent or present | Any | Disabled |
| Editable Draft | Non-empty | Absent or present | Empty or populated | Enabled |
| Go processing | Any | Any | Any | Disabled |
| List loading/List mode | Any | Any | Any | Disabled |
| Ended | Any | Any | Any | Disabled/not rendered |

The current component binding continues to render this derived value as the native disabled state of the revision input. The separate Go action continues using existing session and workflow eligibility checks.