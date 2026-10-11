# Research: Workspace Visual Refresh

**Date**: 2026-10-11
**Status**: Complete; no unresolved design decisions.

## Desktop Width and Alignment

**Decision**: Preserve prompt-strip padding, border and gap; change equal tracks to a 1.2-to-0.8 split. Align Draft and command-row edges to the actual query/target columns, not the outer prompt-strip border. Record actual current geometry before implementation.

**Rationale**: Current app.css sets page width to `min(1480px, 100%)`, horizontal padding to 4vw on each side, prompt-strip padding to 12px, border to 1px, and gap to 12px. Without scrollbar reduction at viewport width 1280, the content width is 1177.6px, inner strip width is 1151.6px, and each old track is `(1151.6 - 12) / 2 = 569.8px`. The proposed left track is 683.76px, or 120% of the old one. These are CSS-derived values, not measured evidence; intrinsic sizing and scrollbar allocation require browser checks.

**Alternatives considered**: A 20-percentage-point increase does not equal a 20% textarea increase. Fixed pixel width does not adapt to available space. Removing ReviewPane without changing work-grid leaves an unused second column.

**Evidence**: Home.razor and app.css `.app-main`, `.prompt-strip`, `.work-grid`, `.workspace-footer`.

## Targets and Commands

**Decision**: Move WordRangeInput from CommandBar.ChildContent to the query row. Retain its IDs, bindings and error semantics; render one vertical stack with full Minimum/Maximum labels and no `to`. Keep CommandBar's optional ChildContent parameter compatible and avoid an empty central range slot when it is absent.

**Rationale**: The existing component already owns label associations and errors. Reusing it prevents duplicate IDs or divergent bound values. Commands retain their order and callbacks below Draft.

**Alternatives considered**: A new range component duplicates semantics. Leaving an empty command slot distorts alignment. A command redesign is not needed beyond the clarified removal of revision submission.

## Web Revision Removal

**Decision**: Block revisions in both SubmitAsync routing and direct SubmitRevisionAsync calls in BlogWorkspaceService. Retain the direct method as a no-op for source compatibility. IsRevisionInputEnabled becomes always false. Keep the draft latch and existing query lock; New and restored-pending context remain the routes to initial query editing/submission.

**Rationale**: SubmitAsync currently routes to revision when IsRevisionRequested is latched, and Draft's setter latches it. Hiding the input leaves historical or stale revision content executable. A UI-only guard does not block direct method calls. Returning a completed task without shared ReviseAsync calls removes capability without automatically enabling unrelated post-draft new runs.

**Alternatives considered**: Reusing New query for revisions contradicts the clarification. Automatically rerunning StartAsync after a completed draft changes unrelated behavior. Deleting historical fields requires an unauthorized migration. Removing reviewer agents confuses user-initiated revision with internal review responsibilities.

**Compatibility**: Existing direct web revision callers now receive a no-op, intentionally changing that web contract. Shared IBlogWriterSessionService.ReviseAsync, console revision support, persisted session data and automatic reviewer processing remain unchanged.

## Identity and Removed Sections

**Decision**: Display Jesse Liberty above Senior AI Engineer within the existing authorized header, keep Sign out, replace the requested heading, remove the summary sentence and Draft index, and stop rendering the revision input and ReviewPane in Home. Retain the reusable ReviewPane source and internal review state.

**Rationale**: MainLayout currently displays Identity.Name; changing text need not modify claims, ownership or account records. Existing mobile CSS hides user-name and must be overridden for the new identity display. The layout should retain established typography/colors while reflowing at narrow widths.

**Alternatives considered**: Updating account profiles expands scope. CSS-hiding revision controls leaves them in the DOM. Deleting saved review feedback violates data preservation.

## Browser and Regression Validation

**Decision**: Reuse SessionListBrowserTests' live Kestrel fixture for whole-page geometry and interaction, with a captured pre-refresh baseline and final screenshots. Adapt removed revision DOM assertions while retaining loaded-ID, persisted-data and word-target checks. Use an existing writing stub for successful initial-Go checks; the list-only SavedListSessionService throws on StartAsync and ReviseAsync.

**Rationale**: The live fixture uses dynamic ports, Testing identity, early host configuration and a recording service. It waits for WebSocket connection and the rendered input's Blazor marker before clicking and honors discard confirmation after restore. Static SetContentAsync renders only session rows and cannot prove whole-workspace layout.

**Alternatives considered**: bUnit proves rendering/events but not geometry. Screenshots without measurements cannot prove exact 20% widening. Live agent or Cosmos access adds unrelated dependencies. Production authentication bypass is not permitted.

**Operational notes**: Use Release while user-owned Debug servers lock assemblies. Keep local-network-check exceptions in the Chromium test launch only. Dispose temporary hosts/browsers. Adapt web revision tests to zero-call expectations rather than skipping tests; core/console revision coverage remains unchanged.
