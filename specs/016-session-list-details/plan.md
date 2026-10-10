# Implementation Plan: Session List Details

**Branch**: `016-session-list-details` (reported by Spec Kit setup) | **Date**: 2026-10-10 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/016-session-list-details/spec.md`.

## Summary

Show each saved session's effective minimum/maximum words and first 50 draft words when the web List button is pressed. Enrich the existing list summary in both stores through one shared factory, preserve current list semantics, and extend the existing native-button rows with labeled metadata and an escaped, read-only preview. No changes to saved document formats, agent behavior, console formatting, or restoration semantics.

## Technical Context

**Language/Version**: C# targeting .NET 10, nullable reference types enabled; Razor and CSS for presentation.

**Primary Dependencies**: Existing ASP.NET Core interactive server components, Microsoft.Azure.Cosmos 3.54.0, Newtonsoft.Json 13.0.3, and System.Text.Json. No package additions or upgrades.

**Storage**: Existing file-based JSON sessions and owner-partitioned Cosmos sessions. Read projections change; stored schemas and write paths do not.

**Testing**: xUnit 2.9.3, Microsoft.NET.Test.Sdk/VSTest, bUnit 2.11.3, and Microsoft.Playwright.Xunit 1.62.0. Verify runner configuration before executing test commands.

**Target Platform**: Existing authenticated web workspace on desktop and mobile browsers; Windows development environment.

**Project Type**: Existing console/core project plus Blazor web application; changes limited to shared list summaries, stores, component, CSS, and focused tests.

**Performance Goals**: Preserve the 20-result bound and existing Cosmos feed query; no per-session reads, writes, or model calls. Stop preview token collection after word 51. Measure payload/query cost on representative long drafts when validating Cosmos; no new latency SLA is introduced.

**Constraints**: Preserve four-argument summary construction/deconstruction, owner filtering, ordering, numbering, cancellation, error handling, disabled state, and selection. Show text safely at 320 and 1280 pixels; no raw full draft in the public summary. Legacy effective defaults must match restore.

**Scale/Scope**: At most 20 entries from the existing list flow; two storage adapters and one existing UI component. No infrastructure, authorization, migration, or workflow changes.

## Constitution Check

*GATE: Checked before research and re-evaluated after Phase 1 design.*

| Principle | Pre-Design | Post-Design Evidence |
| ----------- | ------------ | ---------------------- |
| Hosted-agent boundaries | Pass: list display is outside agent execution | Pass: no hosted-agent adapters, transport, prompts, or model logic change |
| MAF-native workflow composition | Pass: no orchestration changes proposed | Pass: existing List and restore paths remain intact; no new executors or routing |
| Identity, secrets, budget | Pass: reuse current identity and access | Pass: preserve owner predicate/partition and local-owner rules; no model calls, secrets, or new credentials |
| Testability and observability | Pass: reuse existing interfaces and test projects | Pass: factory, stores, service, component and browser checks specified; preserve cancellation and count-only logging |
| Simple compatible evolution | Pass: bounded additive display feature | Pass: constructor/deconstruction and documents unchanged; record equality effects explicitly tested |
| Technical constraints | Pass: .NET 10 and existing dependencies | Pass: no SDK upgrades, Python examples, deployment edits, or prompt changes |

No violations require justification. All design unknowns are resolved in [research.md](research.md). MAF migration/audit tools are not needed for this design because no MAF API or workflow change is proposed; implementation must apply repository MAF procedures if its scope changes.

## Project Structure

### Documentation (this feature)

```text
specs/016-session-list-details/
  spec.md
  plan.md
  research.md
  data-model.md
  quickstart.md
  contracts/
    session-list.md
  checklists/
    requirements.md
```

Task generation is the next phase; this command does not create `tasks.md`.

### Source Code (repository root)

```text
BlogSession.cs
FileBlogSessionStore.cs
CosmosBlogSessionStore.cs
BlogWriter.Web/
  Components/SessionList.razor
  wwwroot/app.css
BlogWriter.Tests/
  FileBlogSessionStoreTests.cs
  BlogSessionStoreOwnershipTests.cs
  BlogWriterSessionServiceTests.cs
  SessionListFormattingTests.cs
  SessionListSelectionTests.cs
  WordRangeTests.cs
BlogWriter.Web.Tests/
  BlogWorkspaceServiceTests.cs
  WorkspaceBrowserTests.cs
  ListLauncherTestHelpers.cs
```

**Structure Decision**: Keep normalization and preview creation with the existing summary abstraction. Use a private Cosmos projection type within the store. Reuse existing tests/helpers where suitable; a focused new test file is justified only where no current file owns summary/projection or real-browser coverage. No changes are expected to store/service method signatures, Home list wiring, or console output.

## Phase 0: Research Results

The storage research examined both projections, word-range restoration, and nearby tests. Decisions and rejected alternatives are captured in [research.md](research.md): additive summary properties, a shared factory, owner-scoped Cosmos projection, default-pair normalization, whitespace preview extraction, and native-button layout. No unresolved clarification remains.

## Phase 1: Design and Validation

1. Add initialized display properties to the four-positional-field summary and a shared factory that receives saved bounds and nullable draft text, normalizes limits, and produces the excerpt and truncation flag. Validate 0/1/49/50/51 words, mixed whitespace, punctuation, null draft, and default bounds.
2. Have file listing construct summaries from existing session reads. Extend Cosmos's selected fields and map a default-initialized projection type through the same factory. Validate ownership, order, cap, feed pages, default deserialization, cancellation, and absence of additional reads/writes.
3. Render metadata and literal preview text inside existing session buttons. Use a shrinkable content grid, wrapping metadata and `overflow-wrap` for long words, preserving row dividers, colors, focus and disabled treatments. Do not introduce nested interactive controls or decorative cards.
4. Extend workspace/component tests for refreshed saved details, empty drafts, markup escaping, pointer selection, and disabled state. Assert selecting a session still follows the current restoration-for-editing behavior and never starts work.
5. Validate actual browser geometry and keyboard activation at 320 and 1280 pixels. Existing bUnit tests are not sufficient for this check. Use controlled saved fixtures and a test-only host identity; do not weaken production authentication.

Entities and invariants are defined in [data-model.md](data-model.md), the internal/public display boundary in [contracts/session-list.md](contracts/session-list.md), and validation commands/scenarios in [quickstart.md](quickstart.md).

## Verification and Completion Gates

- Focused core and web tests pass, then affected projects build successfully. Broaden to both existing test projects because the summary is shared with console consumers.
- FR-001 through FR-009 and SC-001 through SC-004 have automated or browser checks. SC-005 requires the specified five-writer usability review and must not be represented as completed by automated tests.
- Current console list text, summary construction/deconstruction, owner isolation, and restoration behavior remain compatible. No preview or saved content appears in logs.
- Optional Cosmos emulator/live validation uses an existing authorized test setup; mock/projection tests remain the credential-free required gate. Record whether real Cosmos execution and its payload/RU measurements were performed.
- Post-design constitution gates pass; documentation contains no unresolved placeholders. Application implementation is intentionally deferred to tasks/implement.
