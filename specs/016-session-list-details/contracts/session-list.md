# Contract: Saved Session List Details

## Existing Service Boundary

`IBlogSessionStore.ListAsync(CancellationToken)` and `IBlogWriterSessionService.ListAsync(CancellationToken)` keep their existing signatures and return `IReadOnlyList<BlogSessionSummary>`. No new endpoint or command is introduced.

Both storage adapters return enriched summaries described in [data-model.md](../data-model.md). The existing session service remains a delegate, not a second hydration layer.

## Read Contract

- Results retain existing owner visibility, descending update order, and 20-entry cap. No additional `GetAsync` calls occur during list hydration.
- Each result's bounds and preview originate from that result's saved state. Unsaved workspace state must not override them.
- Every new List invocation reads through the existing store flow; "latest saved" means values visible to that invocation under current store consistency, not a new global consistency guarantee.
- Existing cancellation, errors, count logging, and loading-state handling remain unchanged. Do not log draft text or expose another owner's content.
- Legacy omitted bounds receive defaults and invalid pairs fall back exactly as restore does. No session write is performed to normalize list display.

## User Interface Contract

Each native `button.session-entry` retains its identifying accessible name, session number, task, update time, disabled state, and `Selected` callback. Additional content is noninteractive:

1. Identifying number and task.
2. Supporting update time and distinctly labeled "Min words" and "Max words" values.
3. A labeled "Draft preview" below the metadata, containing the excerpt or "No draft yet". Display `...` after the excerpt only when `IsDraftTruncated` is true.

Ordinary Razor text encoding is mandatory; previews must not render Markdown/HTML, activate links, or create nested buttons. Labels and values remain available in rendered text without changing which session the button identifies.

At widths of 320 and 1280 pixels, metadata can wrap and unbroken preview words can break within the row. No field may overlap, be visually clipped, or force horizontal scrolling. Preserve existing flat row styling, focus-visible indication, and disabled behavior.

## Compatibility and Acceptance

| Contract Area | Acceptance Evidence |
| --------------- | --------------------- |
| Four-argument constructor and deconstruction | Existing list/selection fixtures still compile; compatibility check exercises deconstruction |
| Both stores supply matching details | Core tests cover bounds, previews, order, cap, owners, and legacy omissions |
| Exact preview boundaries | Null/empty, 1/49/50/51 words, whitespace, punctuation, hyphens, and markup checks |
| Freshness and source isolation | Save changed details, press List again, verify only that session's details change |
| Safe rendering and layout | bUnit encoding checks; real browser screenshots and geometry at both widths |
| Selection and disabled behavior | Pointer and keyboard restore the intended ID; disabled entries do not restore |
| No side effects | No start/revision/model calls and no writes; original saved state remains identical |

Console list formatting remains unchanged despite the shared summary enrichment. No guarantee is made that record equality ignores the additional properties.
