# Researcher — Foundry Hosted Agent

Hosts the Researcher role as an Azure AI Foundry Hosted Agent. See
`../README.md` for the shared `azd` deploy flow.

Unlike the other 3 hosted agents, this one owns the **Tavily web-search
tool**: the tool call, its `HttpClient`, and retry logic run inside this
hosted process (see `Program.cs`), not in the console app.

## Configuration

Set before `azd ai agent run` / `azd deploy`:

| Key | Required | Default | Notes |
| --- | --- | --- | --- |
| `FOUNDRY_PROJECT_ENDPOINT` | yes | — | Entra ID auth (`DefaultAzureCredential`) |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | no | `gpt-5-mini` | |
| `TAVILY_API_KEY` | yes | — | Set via `dotnet user-secrets set "TAVILY_API_KEY" "<value>"` for local runs; use the hosted environment's secret store for deployed runs |

Instructions are sourced from `BlogWriter.Prompts.ResearcherInstructions`
(shared with the console app via a `ProjectReference` to
`../../BlogWriter.csproj`) — no local prompt duplication.
