# Contract: Refreshed Writing Workspace

## Rendered UI

| Region | Required Contract |
| --- | --- |
| Intro | Research. Write. Revise.; no former heading or descriptive sentence |
| Signed-in header | Jesse Liberty above Senior AI Engineer; unchanged Sign out destination; authorization based on claims |
| Query row | New query at left; vertical Target Words stack at right on desktop |
| Target stack | Target Words, Minimum, minimum input, Maximum, maximum input; equal widths and left alignment; no `to` separator |
| Draft | One pane spanning combined column edges; existing draft/list/empty content; no 01 marker |
| Commands | New, List, Go, Stop, Quit, Help in existing order; below Draft and left-aligned to query |
| Removed sections | No revision label/input or Reviewer notes heading/pane; no hidden focusable replacements |
| Log and dialogs | Existing feedback/log, discard, Help and ended-state behavior |

At 1280 CSS pixels, capture the current query width before editing and verify refreshed width is 120% within 2 pixels. Draft/command/target-field edge alignment uses the same tolerance. At 320 pixels, query and targets may stack; controls, identity and Sign out stay reachable without overlapping text or horizontal page scrolling.

Keep `initial-prompt`, `min-words` and `max-words` IDs, labels, bindings, errors and callbacks. The `revision-prompt` control is intentionally absent. No hidden review/revision interactive panel replaces it.

## Web Submission Boundary

- Initial/restored-pending SubmitAsync uses the existing new-run flow and captured WordRange. New query is never interpreted as revision input.
- Completed/revision-latched SubmitAsync completes without ReviseAsync calls, regardless of historical RevisionPrompt content. Existing query lock and command availability remain unchanged.
- SubmitRevisionAsync remains callable for source compatibility but completes without starting work or mutating state. This web contract change is intentional.
- IsRevisionInputEnabled is always false; no DOM control, restored text, callback or direct web method may enable revision submission.
- Shared IBlogWriterSessionService.ReviseAsync and console consumers are unchanged; internal research/author/reviewer processing is unchanged.

## Saved Data and Authentication

Display changes and revision suppression perform no writes to saved sessions. Preserve owner access, identity, newest-first order, 20-entry cap and existing fresh-run-from-restored-input behavior. Historical revision/review fields remain in persisted documents.

Fixed header name/role is display-only. Keep AuthorizeView and the current Sign out link/action. No claims, owner provider, permissions, account profile or identity-store change is authorized.

## Verification Mapping

| Intent | Evidence |
| --- | --- |
| FR-001, FR-007, FR-008 | Copy/absence checks, signed-in and unauthorized states, preserved Sign out |
| FR-002 | No removed sections in DOM/tab traversal; persisted data unchanged |
| FR-003 through FR-006 | Baseline/final browser measurements and screenshots; vertical targets and aligned Draft/commands |
| FR-009 | Existing validation plus relocated label/description/error/enabled-state checks |
| FR-010 | Zero shared revision calls via Go/direct/stale/restored scenarios; successful initial runs and unchanged saved data |
| FR-011 | Real browser long-text/error geometry and keyboard traversal at 320/1280 pixels |
| SC-001 through SC-005 | Full copy, geometry, responsive, non-revision regression and keyboard evidence |
