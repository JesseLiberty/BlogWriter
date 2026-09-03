# Blogger — Foundry Hosted Agent

Hosts the Blogger role (workflow routing decisions) as an Azure AI Foundry
Hosted Agent. See `../README.md` for the shared `azd` deploy flow.

## Configuration

Set before `azd ai agent run` / `azd deploy` (env vars, or via `azd env set`):

| Key | Required | Default |
| --- | --- | --- |
| `FOUNDRY_PROJECT_ENDPOINT` | yes | — |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | no | `gpt-5-mini` |

Instructions are sourced from `BlogWriter.Prompts.BloggerInstructions` (shared
with the console app via a `ProjectReference` to `../../BlogWriter.csproj`) —
no local prompt duplication.
