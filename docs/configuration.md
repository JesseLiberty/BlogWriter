# Configuration reference

All values below are read from environment variables, with `dotnet user-secrets`
recommended for local development of the console app (secrets win over environment
variables on key collisions). None of the four hosted agents or the console app use API
keys — every credential is Microsoft Entra ID (`AzureCliCredential` locally,
`DefaultAzureCredential` in hosted agents).

## Console app (`BlogWriter.csproj`, root `Program.cs`)

| Key | Required | Default | Notes |
| --- | --- | --- | --- |
| `FOUNDRY_PROJECT_ENDPOINT` | yes | — | e.g. `https://<account>.services.ai.azure.com/api/projects/<project>` |
| `AZURE_TENANT_ID` | yes | — | Microsoft Entra tenant hosting the Foundry project |
| `BLOGGER_AGENT_NAME` | no | `Blogger` | Name of the deployed hosted agent to call |
| `RESEARCHER_AGENT_NAME` | no | `Researcher` | |
| `AUTHOR_AGENT_NAME` | no | `Author` | |
| `REVIEWER_AGENT_NAME` | no | `Reviewer` | |
| `MAX_TOTAL_TOKENS` | no | `40000` | Cumulative cross-agent token cap (`TokenCapChatClient`); parse failures fall back to the default |
| `BLOG_SESSION_STORE_PATH` | no | `%LOCALAPPDATA%\BlogWriter\sessions` | Local directory where completed conversations are stored as JSON files |

Set with, e.g.:

```powershell
dotnet user-secrets set "FOUNDRY_PROJECT_ENDPOINT" "https://<account>.services.ai.azure.com/api/projects/<project>"
dotnet user-secrets set "AZURE_TENANT_ID" "<tenant-id>"
```

## Each hosted agent (`HostedAgents/Blogger`, `Researcher`, `Author`, `Reviewer`)

| Key | Required | Default | Notes |
| --- | --- | --- | --- |
| `FOUNDRY_PROJECT_ENDPOINT` | yes | — | Same Foundry project the console app points at |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | no | `gpt-5-mini` | Model deployment used by that specific agent; set per-project in its own `azure.yaml` |

These are set as `environmentVariables` in each project's `azure.yaml` and provisioned by
`azd` — see [deployment.md](deployment.md). They're not read from `dotnet user-secrets`
since hosted agents run in Azure, not locally, once deployed.

## Keeping prompts in sync

Each hosted agent's `AgentPrompt.cs` must be kept in sync with the corresponding section
of the console app's `Prompts.cs`. There's no automated check for this today — when
changing one, update the other.
