# HostedAgents

Each subfolder here is an independent **Azure AI Foundry Hosted Agent**
(Foundry Agent Service) — a small `AgentHost` app built with
`Microsoft.Agents.AI.Foundry.Hosting` that wraps one of BlogWriter's 4 MAF
agents and exposes it over the OpenAI-compatible Responses protocol
(`/responses`). They are **deployed independently** from the main console
app (`BlogWriter/`), which only calls them by name over the network — it
never builds or provisions them at runtime.

| Project | Role | Notes |
| --- | --- | --- |
| `Blogger/` | Orchestration decisions (next step routing) | |
| `Researcher/` | Web research | Owns the Tavily search tool + its API key |
| `Author/` | Drafts/revises the post | |
| `Reviewer/` | Approves or requests revisions | |

## Prerequisites (once per machine)

```powershell
azd ext install microsoft.foundry
azd auth login
```

## Deploying a hosted agent

From inside each `HostedAgents/<Name>` folder:

```powershell
# First time only: scaffold azd wiring for this folder (or hand-author azure.yaml — see below)
azd ai agent init --deploy-mode code

# Provision Foundry project/model/ACR resources (skip if reusing an existing project)
azd provision

# Test locally before shipping
azd ai agent run
azd ai agent invoke "Hello!"

# Deploy the source to Foundry Agent Service
azd deploy

# Invoke / monitor the deployed agent
azd ai agent invoke "Hello!"
azd ai agent monitor --follow
```

Set `FOUNDRY_PROJECT_ENDPOINT` and `AZURE_AI_MODEL_DEPLOYMENT_NAME` (and, for
Researcher, the `TAVILY_API_KEY` user-secret) before `azd ai agent run` /
`azd deploy` — see each project's own README.

> **Note:** `azure.yaml` in each folder is a starting-point manifest, not a
> generated one — regenerate/replace it via `azd ai agent init` against your
> real Foundry project before deploying for real. `Microsoft.Agents.AI.Foundry.Hosting`
> is still a **prerelease** package; re-validate versions before production use.
