# Deployment guide

BlogWriter has two independently deployable parts: the four Azure AI Foundry Hosted
Agents, and the console app that orchestrates them. See
[architecture.md](architecture.md) for how they fit together and
[configuration.md](configuration.md) for the full environment variable reference.

## 1. One-time setup

```powershell
azd ext install microsoft.foundry
azd auth login
az login
```

## 2. Deploy (or redeploy) a hosted agent

From inside each `HostedAgents/<Name>` folder (`Blogger`, `Researcher`, `Author`,
`Reviewer`):

```powershell
# First time only per project — scaffold/replace azure.yaml against your real Foundry project
azd ai agent init --deploy-mode code
azd ai agent init --infra=bicep

# Provision Foundry project/model/ACR resources (skip if reusing an existing project)
azd provision

# Test locally before shipping
azd ai agent run
azd ai agent invoke "Hello!"

# Deploy the source to Foundry Agent Service (direct code deploy, no container build)
azd deploy

# Test the deployed version and stream its logs
azd ai agent invoke --new-session "Hello!"
azd ai agent monitor --tail 100
azd ai agent monitor --tail 100 --type system
```

Repeat `azd deploy` for each of the four agents whenever their code changes — they
deploy independently of each other and of the console app.

> `azure.yaml` in each folder is a starting-point manifest, not a generated one —
> regenerate it via `azd ai agent init` against your real Foundry project before
> deploying for real. `Microsoft.Agents.AI.Foundry.Hosting` is still a **prerelease**
> package; re-validate versions before production use.

## 3. Run the console app locally

The console app is never deployed to Azure — it runs locally and calls the four
already-deployed hosted agents by name over the network.

```powershell
dotnet user-secrets set "FOUNDRY_PROJECT_ENDPOINT" "https://<account>.services.ai.azure.com/api/projects/<project>"
dotnet user-secrets set "AZURE_TENANT_ID" "<tenant-id>"
dotnet run --project .
```

It will prompt for a topic and a min/max word count, then stream workflow progress
(`[trace] → ...` / `[trace] ← ...` lines) before printing the final approved draft.
After a run, enter a follow-up request to revise the same draft; the console app
persists the session locally under `%LOCALAPPDATA%\BlogWriter\sessions` by default.
The result prints the session ID; use `resume <session-id>` at the next topic prompt
to continue it after restarting the console app.

## 4. Verifying a deployment

After `azd deploy` for a given agent, confirm it's healthy before wiring the console app
to it:

1. `azd ai agent invoke --new-session "<test prompt>"` — should return real assistant
   text, not an error.
2. `azd ai agent monitor --tail 100` — look for `HTTP 200` on the `/responses` request
   and no unhandled exceptions. Startup warnings about Kestrel address binding or a 404
   on the very first task-storage lookup (before the task exists) are expected noise, not
   failures.
3. Run the console app end-to-end once against the redeployed agent and confirm the
   reviewer reaches `APPROVED` (or a clear revision-cap message) with no exceptions.

## Never do this

Do not call an agent's `/responses` endpoint directly with `HttpClient` or similar, in
either the console app or a hosted agent. All agent-to-agent and app-to-agent
communication must go through Microsoft Agent Framework (`AsAIAgent` /
`AIAgent.RunAsync`) — see [architecture.md](architecture.md).
