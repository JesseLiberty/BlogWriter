using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;

// Foundry Hosted Agent for the "Researcher" role. Deployed independently
// (azd ai agent init / azd provision / azd deploy — see README) and referenced
// by name from the BlogWriter console app.
//
// Unlike the other three hosted agents, this one owns a Foundry-hosted web
// search tool. Search executes inside the hosted process, not in the console.

var projectEndpoint = new Uri(
    Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
        ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is not set."));
string modelDeployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME") ?? "gpt-5-mini";

// Entra ID only — no API keys, per repository constraint (this applies to the
// Foundry model and hosted web-search authentication).
AIAgent agent = new AIProjectClient(projectEndpoint, new DefaultAzureCredential())
    .AsAIAgent(
        model: modelDeployment,
        instructions: BlogWriter.Prompts.ResearcherInstructions,
        name: "Researcher",
        tools: [new HostedWebSearchTool()]);

var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();
