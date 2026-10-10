# Quickstart: Stop Active Agent Work

## Prerequisites

- .NET 10 SDK
- Repository dependencies restored

The focused service and browser tests use a stub session service; they do not require live Foundry agents or Cosmos DB credentials.

## Automated Validation

From the repository root, run the Stop-focused web tests:

```powershell
dotnet test BlogWriter.Web.Tests/BlogWriter.Web.Tests.csproj --filter "FullyQualifiedName~Stop"
```

Expected result: Stop eligibility, Go/Revise cancellation, immediate status, all-button lockout, 10-second status transition, delayed completion, New-state reset, and late-result suppression tests pass.

Build the affected web project:

```powershell
dotnet build BlogWriter.Web/BlogWriter.Web.csproj
```

Expected result: build succeeds without warnings or errors.

## Workspace Scenarios

1. Open the authenticated workspace in its initial/New state. Verify Stop is disabled.
2. Start Go and observe the Blogger stage, then Researcher, Author, and Reviewer stages. Verify Stop is enabled only during Researcher, Author, or Reviewer activity, and displays a red background with yellow `Stop` text.
3. Press Stop during each eligible stage. Verify the status line immediately displays `stopping`, all command buttons are disabled, no later stage starts, and the workspace resets to New only after the active operation ends.
4. Repeat during Revise while the prior draft remains visible. Verify the same cancellation and reset behavior.
5. Hold the operation active beyond 10 seconds. Verify the status changes to `Cancellation failed`, all buttons stay disabled, and no reset occurs. Release the operation and verify the workspace then resets to New.
6. After reset, deliver a late progress or result update from the canceled operation. Verify it does not alter the new workspace.
