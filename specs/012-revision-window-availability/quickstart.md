# Quickstart: Revision Window Availability

## Prerequisites

- .NET 10 SDK and restored `BlogWriter.Web.Tests` dependencies.

## Automated Validation

From the repository root, run the focused workspace tests:

```powershell
dotnet test BlogWriter.Web.Tests/BlogWriter.Web.Tests.csproj --filter "FullyQualifiedName~HomePageTests|FullyQualifiedName~BlogWorkspaceServiceTests"
```

Expected outcomes:

- Empty and whitespace-only query text disables Revision in editable states.
- Non-empty query text enables Revision with no draft, with a draft, and whether revision text is empty or populated.
- Clearing a query disables Revision immediately in the rendered workspace.
- Go processing, List loading/List mode, and Ended mode continue to lock the field.
- Go submission eligibility and routing remain unchanged.

## Browser Check

In the configured authenticated workspace, enter a non-whitespace query and verify Revision becomes enabled before a draft exists. Clear the query and verify it disables immediately. Repeat with an existing draft and with revision text already present. Confirm Go/List busy states continue to disable the field.

The expected state combinations are summarized in [revision-window-availability.md](contracts/revision-window-availability.md).