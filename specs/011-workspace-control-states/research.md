# Research: Workspace Control Availability

## Decision 1: Keep control policy with workspace state

**Decision**: Derive input and command availability from `BlogWorkspaceState`; have `Home.razor` pass those values to the owning components.

**Rationale**: The state object already owns `Mode`, `IsProcessing`, `HasDraft`, prompt values, and the revision latch, and exposes prompt-enabled properties. Centralizing policy there prevents the page and child components from independently interpreting workflow state.

**Alternatives considered**:
- Compute availability separately in `Home.razor`: rejected because it duplicates business-state conditions at the presentation boundary.
- Let each component inspect the entire workspace state: rejected because it couples reusable components to the workspace service and spreads policy.

## Decision 2: Represent List loading separately from stable List mode

**Decision**: Track the asynchronous List request as a transient busy state or equivalent, beginning before session retrieval and ending on success or failure.

**Rationale**: `BlogWorkspaceService.ListAsync` currently sets a loading status and notifies before awaiting the session service, but switches `Mode` to `List` only after the request completes. `IsProcessing` is used for writing and cannot alone model the List policy because New must remain available in List mode. A separate signal lets controls lock at the required time without conflating writing and listing.

**Alternatives considered**:
- Reuse `IsProcessing` for both writing and listing: rejected because command exceptions differ, and New must remain available in List mode.
- Use `StatusMessage` text to infer loading: rejected because user-facing text is not a stable state contract.

Because New remains available during List, the List completion must be ignored if a later workspace transition has superseded it. Reuse or extend the existing operation-version/transition guard rather than introducing a new service abstraction.

## Decision 3: Use native disabled state and existing visual styling

**Decision**: Render the HTML disabled state on unavailable buttons and inputs, and retain the existing light-gray CSS treatment for disabled buttons.

**Rationale**: Native disabled semantics prevent activation and expose unavailability to assistive technology. The current stylesheet already has a general `.command-bar button:disabled` light-gray rule and a dedicated Go disabled rule; implementation should verify selector precedence for command-specific color rules.

**Alternatives considered**:
- Apply a gray class without disabling controls: rejected because it would only imitate disabled appearance and leave controls actionable.
- Add separate bespoke color rules per button: rejected unless browser validation shows existing disabled rules are overridden.

## Local Evidence

- `BlogWriter.Web/Services/BlogWorkspaceState.cs` already derives prompt enablement from processing and revision state.
- `BlogWriter.Web/Services/BlogWorkspaceService.cs` owns New, List, submission, cancellation, and draft publication transitions.
- `BlogWriter.Web/Components/Pages/Home.razor` binds state values to `PromptInput`, `WordRangeInput`, and `CommandBar`.
- `WordRangeInput.razor` and `CommandBar.razor` currently render their fields/buttons without shared disabled parameters.
- `BlogWriter.Web/wwwroot/app.css` includes disabled styling for command buttons and the Go control.
- Existing coverage is in `BlogWriter.Web.Tests/HomePageTests.cs`, `CommandBarTests.cs`, and `BlogWorkspaceServiceTests.cs`.

No external dependencies, APIs, or technical unknowns remain.