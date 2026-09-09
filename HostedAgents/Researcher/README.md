# Researcher — Foundry Hosted Agent

Hosts the Researcher role as an Azure AI Foundry Hosted Agent. See
`../README.md` for the shared `azd` deploy flow.

Unlike the other 3 hosted agents, this one owns a Foundry-hosted web-search
tool. Search runs inside this hosted process, not in the console app.

## Configuration

Set before `azd ai agent run` / `azd deploy`:

| Key | Required | Default | Notes |
| --- | --- | --- | --- |
| `FOUNDRY_PROJECT_ENDPOINT` | yes | — | Entra ID auth (`DefaultAzureCredential`) |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | no | `gpt-5-mini` | |
Instructions are compiled from the deployment-local `AgentPrompt.cs`. Keep it
aligned with `../../Prompts.cs` when changing the Researcher prompt.
