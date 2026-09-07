# Demo code associated with a [series of blog posts](https://jesseliberty.com)

This demonstration program, **Blog Writer**, is designed to research and write blog posts. It was written with *Microsoft Agent Framework* and the principal actors are the **BloggerAgent** which works as the orchestrator, the **ResearcherAgent** which goes out to the Web to research the requested topic, the **AuthorAgent** which then writes the blog post, and the **ReviewerAgent** which reviews the proposed blog post, sending it back to the AuthorAgent if it is not approved.

The system prompts for each agent is contained in Prompts.cs

BlogWorkflow is responsible for creating the nodes and edges for moving through the workflow and also contains the logic for managing a breach of the token-cap (the maximum number of tokens that can be used in a single request, as defined in TokenCapChatClient).

## Architecture: Azure AI Foundry Hosted Agents

The 4 agents are deployed as independent **Azure AI Foundry Hosted Agents**
(Foundry Agent Service), each with its own managed compute, dedicated
Microsoft Entra ID identity, and OpenAI-compatible `/responses` endpoint. The
console app (this project) no longer builds the agents in-process — it only
**orchestrates** them locally via the MAF Workflow in `BlogWorkflow.cs`,
calling each hosted agent as a remote `IChatClient`
using the Microsoft Agent Framework Foundry integration.

```
BlogWriter/                (console app — orchestration only, calls hosted agents remotely)
HostedAgents/
  Blogger/                 (Foundry Hosted Agent — orchestration decisions)
  Researcher/               (Foundry Hosted Agent — owns the Tavily web-search tool)
  Author/                  (Foundry Hosted Agent — drafts/revises the post)
  Reviewer/                (Foundry Hosted Agent — approves or requests revisions)
```

Each `HostedAgents/<Name>` project is deployed independently via `azd` (see
its own README) and is **pre-provisioned** — the console app only references
already-deployed hosted agents by name, it never creates or updates them at
runtime.

### Configuration (console app)

Set via `dotnet user-secrets` (preferred for local dev) or environment
variables — Entra ID (`DefaultAzureCredential`) is used for all Foundry/model
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

`TAVILY_API_KEY` is no longer configured here — it now lives in the
Researcher hosted agent's own configuration (see `HostedAgents/Researcher/README.md`).

## Miscellaneous Notes
* Tavily web search now runs **inside the hosted Researcher agent** — the tool call, HTTP client, and retry logic live in `HostedAgents/Researcher/Program.cs`.
* Foundry/model access uses Microsoft Entra ID exclusively; the console app authenticates with `DefaultAzureCredential`.
* The model deployment is chosen per hosted agent (via `AZURE_AI_MODEL_DEPLOYMENT_NAME` in each `HostedAgents/<Name>` project), not hardcoded in the console app.

## Additional Features
* Middleware is used to manage the tools. 
* OpenTelemetry is used to manage logging and emits a GenAI span per model round-trip
* ChatOptions sets the temperature to 0 for maximum consistency

## Known Issues
We are seeing a lot of calls to the LLM. Either there is a problem with the calls or with the telemetry.

## Next Steps
* The `Microsoft.Agents.AI.Foundry.Hosting` package used by `HostedAgents/*` is still prerelease — re-validate before production use.
* Decide whether the Researcher's hosted agent should also expose the Responses+Invocations combo, or add more Foundry Toolbox tools (Code Interpreter, Azure AI Search) now that it's hosted.
