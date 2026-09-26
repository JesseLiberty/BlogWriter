# Workspace Control-State Contract

This contract describes user-visible control availability. Button labels and existing layout remain unchanged. Disabled buttons use native disabled semantics and appear light gray.

| Workspace state | New query | Revision | Min/Max | New | List | Go | Quit | Help | Session selection |
|---|---|---|---|---|---|---|---|---|---|
| Fresh New, empty query | Enabled | Disabled | Enabled | Enabled | Enabled | Enabled | Enabled | Enabled | Hidden |
| Go processing, before draft | Disabled | Disabled | Disabled | Disabled | Disabled | Disabled | Disabled | Disabled | Disabled/hidden |
| Stable List mode | Disabled | Disabled | Disabled | Enabled | Disabled | Disabled | Disabled | Disabled | Enabled when sessions are available |
| Populated Draft, revision empty | Disabled | Enabled | Enabled | Enabled | Enabled | Enabled | Enabled | Enabled | Hidden |
| Populated Draft, revision populated | Disabled | Disabled | Enabled | Enabled | Enabled | Enabled | Enabled | Enabled | Hidden |
| Ended | Workspace controls are not rendered |  |  |  |  |  |  |  |  |

## Transition Rules

- New resets the workspace to Fresh New: enable the new query, disable revision, restore range and command availability.
- An empty new query always leaves the revision field disabled.
- Go begins a busy state immediately; all controls listed in the Go processing row are disabled until draft content is published. Go itself is disabled to prevent duplicate submissions.
- List begins a distinct loading state before the session fetch; controls follow the List lockout rules during loading and in stable List mode. New remains enabled, and a late List response must not override a newer New transition.
- Selecting a session with a populated draft enters the populated Draft rules. Empty-draft selection does not count as a populated-draft state.
- Failure or cancellation clears the relevant busy state and restores controls from the current mode and draft state.
- Every disabled command button remains semantically disabled and visibly light gray, including command buttons with distinct enabled-state colors.