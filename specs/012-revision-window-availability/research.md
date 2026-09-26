# Research: Revision Window Availability

## Decision 1: Make query content the availability condition

**Decision**: During editable New and Draft states, enable the revision window when `InitialPrompt` contains non-whitespace text; disable it when empty or whitespace-only. Keep processing, listing, List-mode, and Ended-mode lockouts.

**Rationale**: The feature request explicitly makes query presence the criterion. The current `IsRevisionInputEnabled` also requires a draft, a revision latch, and an empty revision field, which is stricter than the requested rule.

**Alternatives considered**:
- Retain draft/latch gating: rejected because it keeps the revision window disabled for a non-empty query before a draft exists.
- Disable the input after revision text is entered: rejected because the request says it should remain enabled whenever the query is non-empty.
- Remove processing/List/Ended protections: rejected because the specification explicitly preserves these existing workspace lockouts.

## Decision 2: Keep submission eligibility separate

**Decision**: Change revision input availability only; do not change `SubmitAsync` routing or enable a separate revision action.

**Rationale**: The user requested a window availability correction. Existing service routing and session eligibility remain responsible for determining whether Go starts an initial write or revision.

**Alternatives considered**:
- Change submission routing to submit a revision before a draft/session exists: rejected as out of scope and incompatible with existing session requirements.
- Add a new UI component or workspace mode: rejected because the existing `IsRevisionInputEnabled` property already owns the bound state.

## Local Evidence

- `BlogWriter.Web/Services/BlogWorkspaceState.cs` currently derives revision availability using busy/mode guards plus `HasDraft`, `IsRevisionRequested`, non-empty `InitialPrompt`, and empty `RevisionPrompt` checks.
- `BlogWriter.Web/Components/Pages/Home.razor` passes `!IsRevisionInputEnabled` to the existing revision `PromptInput.Disabled` parameter.
- Existing coverage is in `BlogWriter.Web.Tests/BlogWorkspaceServiceTests.cs` and `BlogWriter.Web.Tests/HomePageTests.cs`.
- Older features captured different revision availability rules; this feature's specification is the current intent for the revision window, while submission behavior remains outside its scope.

No external dependency research is needed.