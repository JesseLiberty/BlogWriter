# Data Model: Workspace Visual Refresh

## Persisted Sessions (Unchanged)

BlogSession/ResearchState fields, including task, draft, word bounds, revision context and reviewer feedback, retain their schema and meaning. No field deletion, migration, display-driven write or storage adapter change is planned. BlogSessionSummary and the 50-word List preview remain unchanged.

Authenticated ownership still governs access. Fixed header copy is not identity data and must not be used for ownership, authorization or an account update.

## Web Workspace State

| Field or Property | Contract |
| --- | --- |
| InitialPrompt | Only visible writing input; retains initial/new-run meaning and existing lock rules |
| MinWordsInput / MaxWordsInput | Existing values, validation, defaults, errors and enabled rules; location changes only |
| AcceptedRange | Existing validated pair used in submission/restoration |
| Draft | Existing output/list area; setting content retains the latch that locks the initial prompt |
| RevisionPrompt | Historical/transient context retained for compatibility; no visible input and never submitted by web commands |
| Review | Existing internal feedback accumulation remains; no rendered Reviewer notes pane |
| IsRevisionRequested | Retained post-draft latch for query lock; cannot authorize revision submission |
| IsRevisionInputEnabled | Always false in the web workspace |
| IsRestoredPending | Existing initial-run routing after restoration, independent of historical revision text |
| ActiveSession / DisplayedSessions | Existing identity, list/restore mechanics and result limit |
| Busy/state flags | Existing processing, stopping, listing, selecting, ended and command availability rules |

## Transitions

1. New clears transient workspace state as before, restores default targets and unlocks the query.
2. Go in initial or restored-pending context invokes the existing new-run flow with captured query and validated targets.
3. A completed draft retains its query lock. Go in revision-latched context is a no-op even if historical revision text exists; New or restored-pending context permits initial query entry again.
4. Direct SubmitRevisionAsync is non-operational: no shared revision invocation, model calls, persistence writes or source mutation.
5. List displays existing summaries in widened Draft. Restore fills query and targets for a fresh initial run; stored historical revision text cannot change submission routing.
6. Internal reviewer/stage updates during an initial run retain existing feedback/logging behavior. Removing the pane does not remove review processing.
7. Stop, cancellation, errors, Quit/ended mode, Help and discard confirmation retain existing behavior.

## Presentation and Validation

- Heading: Research. Write. Revise.
- Signed-in display: Jesse Liberty, then Senior AI Engineer on a separate line; existing Sign out retained.
- Targets: Target Words, Minimum label/input, Maximum label/input, vertically ordered with equal input widths/common left edges.
- Draft label remains, without decorative 01. Revision request, Reviewer notes and summary sentence are absent.
- Word targets retain current positive/ordered validation, default values, input IDs and accessible descriptions; no new numeric constraints.
- Desktop query border-box width equals captured baseline times 1.20 within 2 pixels. Draft spans both columns; command left edge aligns to query. Layout measurements are not session data.
- At narrow widths, the stack may move below query; no overlap, page overflow or hidden focusable removed controls.
