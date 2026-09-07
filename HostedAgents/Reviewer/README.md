# Reviewer — Foundry Hosted Agent

Hosts the Reviewer role (approves or requests revisions) as an Azure AI
Foundry Hosted Agent. See `../README.md` for the shared `azd` deploy flow.

## Configuration

Set before `azd ai agent run` / `azd deploy` (env vars, or via `azd env set`):

| Key | Required | Default |
| --- | --- | --- |
| `FOUNDRY_PROJECT_ENDPOINT` | yes | — |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | no | `gpt-5-mini` |

Instructions are compiled from the deployment-local `AgentPrompt.cs`. Keep it
aligned with `../../Prompts.cs` when changing the Reviewer prompt.
