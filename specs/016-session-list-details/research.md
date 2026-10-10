# Research: Session List Details

**Date**: 2026-10-10
**Status**: Complete; no unresolved design questions.

## Summary Data Ownership

**Decision**: Enrich the existing `BlogSessionSummary` with initialized `MinWords`, `MaxWords`, `DraftPreview`, and `IsDraftTruncated` properties. Keep its four positional constructor parameters and four-value deconstruction. Place a shared summary factory on this existing abstraction rather than introducing another service.

**Rationale**: Both stores already own list projection. The session service delegates `ListAsync` and the web workspace consumes its summaries. Enrichment at this boundary avoids a new UI-only list operation and per-entry `GetAsync` requests. Additional properties participate in record equality; test fixtures must account for their values.

**Alternatives considered**: Loading every full session after listing introduces up to 20 additional reads and disappearance races. A separate web-only store interface duplicates ownership and ordering rules. Adding positional parameters changes deconstruction and constructor contracts.

**Evidence**: `BlogSession.cs`, `IBlogSessionStore.cs`, `FileBlogSessionStore.ListAsync`, `CosmosBlogSessionStore.ListAsync`, and `BlogWriterSessionService.ListAsync`.

## Store Projection and Compatibility

**Decision**: File listing uses its already-deserialized sessions. Cosmos listing adds the saved word bounds and raw draft to its existing selected fields, deserializes through a private projection type, and constructs the public summary through the shared factory. Raw draft remains temporary server-side input, not a summary property.

**Rationale**: Preserve `TOP 20`, descending update order, parameterized owner predicate, owner partition request option, cancellation, and feed iteration. File listing retains its local-owner legacy allowance and existing ordering/limit. Persisted documents, writes, ETags, and SDK dependencies do not change.

**Alternatives considered**: Returning entire session documents transfers unrelated research/review data. A persisted preview requires migration and write synchronization. Database-side string splitting does not reliably match the specified whitespace-word rule.

**Tradeoff**: Cosmos now transfers draft text for up to 20 results to compute the excerpt. This is larger than the current metadata projection. Keep result count bounded and do not log draft text; do not introduce denormalized persistence without evidence that this cost requires a separate change.

**Guidance**: Cosmos best-practices skill (owner-scoped queries, required-field projections, parameterization, serialization) and Azure code-generation best practices were consulted. Reference: [Query items in Azure Cosmos DB using .NET](https://learn.microsoft.com/azure/cosmos-db/how-to-dotnet-query-items).

## Effective Word Limits

**Decision**: Missing individual bounds use their existing defaults, 1000 minimum and 2000 maximum. After deserialization, normalize the pair using the same rules as web restore: both positive and maximum at least minimum; otherwise use the complete default pair.

**Rationale**: `ResearchState` initializes the bounds and `BlogWorkspaceService.ResolveSessionWordRange` falls back through `WordRange.Default`. The list must agree with restoration, including partially populated legacy documents.

**Alternatives considered**: Independently clamping invalid bounds can produce a range different from restore. Throwing for invalid bounds makes legacy sessions unlistable. Treating all omitted projection properties as zero loses valid partially saved ranges.

## Draft Preview

**Decision**: The factory treats absent/null/whitespace drafts as empty, identifies nonempty .NET whitespace-delimited tokens, retains the first 50, and checks for a 51st. Join retained words with single spaces. Store truncation separately from the excerpt.

**Rationale**: Word boundaries, punctuation, markup, and exactly-50 behavior match FR-003 and FR-004. Stop token collection after detecting word 51 so long drafts do not allocate a full token array. This remains a small local operation, not a text-processing dependency.

**Alternatives considered**: Character truncation can cut words and misses the 50-word requirement. Rendering Markdown changes literal content and expands the security surface. A generated summary changes content and launches unwanted model work.

## Layout and Verification

**Decision**: Keep `SessionList.razor` native buttons, selection callback, disabled state, and accessible identifying name. Add noninteractive text spans for metadata and a separate preview line. Render ordinary Razor-escaped strings; never use `MarkupString`. Extend the existing row CSS with shrinkable grid tracks, `min-width: 0`, wrapping metadata, and long-word wrapping.

**Rationale**: The existing app uses flat, divider-separated list rows. It already has a keyboard focus treatment. Existing xUnit and bUnit projects cover stores and workspace selection; the Playwright package is available for real layout checks.

**Alternatives considered**: New decorative cards change the established design. bUnit alone cannot prove rendered geometry: `WorkspaceBrowserTests` currently inherits `BunitContext`, so viewport declarations there are not real browser coverage.

**Validation**: Extend existing test files for store normalization, preview boundaries, service delegation, rendering, encoding, refresh, and disabled/restore behavior. Use Chromium with actual 320- and 1280-pixel viewports for geometry, screenshots, and keyboard checks. Use existing fixture helpers; introduce a browser harness only if required for actual rendering.
