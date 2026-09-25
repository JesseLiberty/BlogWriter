# BlogWriter, an open source project is [documented here](https://jesseliberty.com)

This program, **Blog Writer**, is designed to research and write blog posts. It was written with *Microsoft Agent Framework* and the principal actors are the **BloggerAgent** which works as the orchestrator, the **ResearcherAgent** which goes out to the Web to research the requested topic, the **AuthorAgent** which then writes the blog post, and the **ReviewerAgent** which reviews the proposed blog post, sending it back to the AuthorAgent if it is not approved.

*Note: BlogWriter was written as a demonstration program and is not ready for production.*

## Blazor web interface

`BlogWriter.Web` provides an authenticated Interactive Server Blazor workspace over
the same workflow and Cosmos session store. It includes separate draft and reviewer
panes, prompt and revision inputs, numbered saved-session recall, bounded cancellation,
and responsive WCAG 2.2 AA-oriented controls. The compact `Min` and `Max` fields between
the prompts and content panes set the target word range for new drafts and revisions;
they default to 1000 and 2000 words. Workflow progress, validation, cancellation, and
failure messages appear as an ordered log beneath the New/List/Revise/Quit buttons.
Reviewer feedback is kept in Reviewer notes as it arrives and accumulates across
revisions for the active session; it is cleared when starting New or loading another
session. In List mode, enter the one-based session number beside List to restore the
saved MainTask and optional CurrentSubTask and launch it immediately. The `?` command
shows and copies the HTTPS launch command. The Revision request field is editable after
New, while Revise becomes available once a draft/session exists. Workflow status is
shown as one latest-message line; Reviewer notes remain separate.

After configuring Microsoft Entra, Foundry, and Cosmos values from
[docs/configuration.md](docs/configuration.md), start it with:

```powershell
dotnet run --project BlogWriter.Web/BlogWriter.Web.csproj
```

The original console remains available with `dotnet run --project BlogWriter.csproj`.

## Architecture: Azure AI Foundry Hosted Agents

The 4 agents are deployed as independent **Azure AI Foundry Hosted Agents**
(Foundry Agent Service), each with its own managed compute, dedicated
Microsoft Entra ID identity, and OpenAI-compatible `/responses` endpoint. The
console app (this project) no longer builds the agents in-process — it only
**orchestrates** them locally via the MAF Workflow in `BlogWorkflow.cs`,
calling each hosted agent as a remote `IChatClient`
using the Microsoft Agent Framework Foundry integration.

```text
BlogWriter/                (console app — orchestration only, calls hosted agents remotely)
HostedAgents/
  Blogger/                 (Foundry Hosted Agent — orchestration decisions)
  Researcher/               (Foundry Hosted Agent — owns hosted web search)
  Author/                  (Foundry Hosted Agent — drafts/revises the post)
  Reviewer/                (Foundry Hosted Agent — approves or requests revisions)
```

Each `HostedAgents/<Name>` project is deployed independently via `azd` (see
its own README) and is **pre-provisioned** — the console app only references
already-deployed hosted agents by name, it never creates or updates them at
runtime.

### Configuration (console app)

Set via `dotnet user-secrets` (preferred for local dev) or environment
variables — Entra ID (`AzureCliCredential`) is used for local Foundry/model
auth, no API keys:

| Key | Required | Default | Notes |
| --- | --- | --- | --- |
| `FOUNDRY_PROJECT_ENDPOINT` | yes | — | e.g. `https://<account>.services.ai.azure.com/api/projects/<project>` |
| `AZURE_TENANT_ID` | yes | — | Microsoft Entra tenant hosting the Foundry project |
| `BLOGGER_AGENT_NAME` | no | `Blogger` | Name of the deployed hosted agent |
| `RESEARCHER_AGENT_NAME` | no | `Researcher` | |
| `AUTHOR_AGENT_NAME` | no | `Author` | |
| `REVIEWER_AGENT_NAME` | no | `Reviewer` | |
| `MAX_TOTAL_TOKENS` | no | `40000` | Cumulative process-wide cap (`TokenCapChatClient`) |

## Documentation

* [docs/architecture.md](docs/architecture.md) — full architecture, workflow graph, auth, and token-budget details.
* [docs/changelog-v1-to-v2.md](docs/changelog-v1-to-v2.md) — what changed from the original in-process design to the current hosted-agent one.
* [docs/deployment.md](docs/deployment.md) — the `azd` flow for deploying/redeploying each hosted agent and running the console app locally.
* [docs/configuration.md](docs/configuration.md) — every environment variable/secret used by the console app and the four hosted agents.

## Miscellaneous Notes

* Web search runs **inside the hosted Researcher agent** through Foundry's hosted web-search tool.
* Foundry/model access uses Microsoft Entra ID exclusively; the console app authenticates with `AzureCliCredential`.
* The model deployment is chosen per hosted agent (via `AZURE_AI_MODEL_DEPLOYMENT_NAME` in each `HostedAgents/<Name>` project), not hardcoded in the console app.

## Additional Features

* Middleware is used to manage the tools.
* OpenTelemetry is used to manage logging and emits a GenAI span per model round-trip
* ChatOptions sets the temperature to 0 for maximum consistency

