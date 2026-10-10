# Data Model: Session List Details

## Saved Writing Session (Unchanged)

`BlogSession` retains `Id`, `OwnerId`, `CreatedAt`, `UpdatedAt`, and `State`. `ResearchState` remains the source of `MainTask`, `MinWords`, `MaxWords`, and `Draft`. No persisted preview, schema migration, new index, or write is introduced.

Identity is the existing session ID within the owner's access scope. Listing must never infer identity from task text: duplicate tasks remain distinct sessions.

## Public List Summary (Additive)

`BlogSessionSummary` retains its four positional fields and constructor/deconstruction contract.

| Field | Type | Rule |
| ------- | ------ | ------ |
| Id | string | Existing session identity; selection continues to use this value |
| MainTask | string | Existing saved task, unchanged |
| CreatedAt | DateTimeOffset | Existing creation time, unchanged |
| UpdatedAt | DateTimeOffset | Existing update time, sorting and display unchanged |
| MinWords | int | Initialized to existing minimum default; factory supplies effective positive value |
| MaxWords | int | Initialized to existing maximum default; factory supplies effective value at least MinWords |
| DraftPreview | string | Initialized empty; at most 50 nonempty whitespace-delimited saved draft words, single-space joined |
| IsDraftTruncated | bool | Initialized false; true only if a 51st saved draft word exists |

New fields are init-only display properties, not positional arguments. Existing four-argument fixtures retain usable default values. Additional properties affect record equality; callers comparing records must intentionally include or ignore the new content. No full `Draft` property is exposed.

The shared summary factory belongs to this existing abstraction. It performs no storage I/O, logging, or mutation.

## Cosmos List Projection (Private)

The store's private projection carries existing summary fields plus saved `MinWords`, `MaxWords`, and nullable `Draft`. Missing bounds initialize individually to `ResearchState` defaults before pairwise validation. Draft text is discarded after factory projection to the public summary.

The selected query fields expand, but owner filtering, owner partition key, descending `UpdatedAt`, `TOP 20`, feed paging, and cancellation remain unchanged. The file store uses the same factory with already-read state.

## Validation Rules

1. Valid word-limit pairs have minimum greater than zero and maximum at least minimum. Invalid pairs use `WordRange.Default` as a complete pair, matching restore; do not repair each bound independently.
2. Missing bounds inherit the existing individual defaults. Equal positive bounds are valid. This applies equally to both serializers' omitted-property behavior.
3. Null or whitespace-only draft yields empty preview and false truncation. The UI, not the model, provides "No draft yet".
4. Words are nonempty segments separated by .NET whitespace. Punctuation, hyphens, and markup symbols remain part of their segment. Collect at most 50 segments and detect one additional segment.
5. Ellipsis is presentation only, outside preview text and word count. Exactly 50 words never trigger it.
6. Summary generation must not change task, saved draft, limits, timestamps, ETags, or ownership. Literal text is escaped at the UI boundary.

## Lifecycle and Relationships

- One returned summary represents one owner-visible saved session. Returned lists remain newest-first and capped at 20.
- Pressing List replaces displayed summaries using the current saved-session response; no summary cache or live subscription is added.
- Selecting a summary passes it through the existing callback and loads by ID for restoration-for-editing. The preview is not a replacement for the source draft or restored session.
- List loading/failure, pending-transition confirmation, and disabled-selection behavior remain controlled by the existing workspace service. No new state transition is introduced.
