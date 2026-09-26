# Quickstart: Workspace Control Availability

## Prerequisites

- .NET 10 SDK available.
- Repository restored and the `BlogWriter.Web.Tests` project dependencies available.

## Automated Validation

From the repository root, run the focused UI and state tests:

```powershell
dotnet test BlogWriter.Web.Tests/BlogWriter.Web.Tests.csproj --filter "FullyQualifiedName~HomePageTests|FullyQualifiedName~CommandBarTests|FullyQualifiedName~BlogWorkspaceServiceTests"
```

Expected result: all selected tests pass. Coverage should include New reset, empty-query revision gating, Go lockout and recovery, List loading and stable List lockout, session selection, populated-draft revision gating, and native disabled attributes.

If the combined filter is unsupported by the installed test runner, run each class separately:

```powershell
dotnet test BlogWriter.Web.Tests/BlogWriter.Web.Tests.csproj --filter "FullyQualifiedName~HomePageTests"
dotnet test BlogWriter.Web.Tests/BlogWriter.Web.Tests.csproj --filter "FullyQualifiedName~CommandBarTests"
dotnet test BlogWriter.Web.Tests/BlogWriter.Web.Tests.csproj --filter "FullyQualifiedName~BlogWorkspaceServiceTests"
```

## Browser Visual Check

1. Start the web application with its configured development authentication and service settings.
2. Confirm the fresh workspace has an enabled new-query input, a disabled revision input, enabled commands, and editable word-range inputs.
3. Start Go and verify all specified controls, including Go, become disabled while the operation runs; verify disabled command buttons are light gray.
4. Open List and verify lockout appears while sessions load, New remains available, and session selection becomes usable after loading.
5. Load a session with a draft. Verify New Query stays disabled; verify Revision is enabled while empty and disables when populated.
6. Exercise failure/cancellation and confirm controls recover instead of remaining disabled.

## Expected Outcomes

- Every state matches the matrix in [workspace-control-state.md](contracts/workspace-control-state.md).
- Disabled controls are non-actionable through both pointer and keyboard input.
- New remains usable in List mode, and a late List completion cannot replace a newer workspace state.
- No session document or hosted-agent behavior changes are introduced.