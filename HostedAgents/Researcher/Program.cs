using System.ComponentModel;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

// Foundry Hosted Agent for the "Researcher" role. Deployed independently
// (azd ai agent init / azd provision / azd deploy — see README) and referenced
// by name from the BlogWriter console app.
//
// Unlike the other three hosted agents, this one owns the Tavily web-search
// tool: the tool call happens inside this hosted process, not in the main
// console app, so its HTTP client, retry logic, and TAVILY_API_KEY secret all
// live here.

IConfiguration config = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets(typeof(Program).Assembly)
    .Build();

var projectEndpoint = new Uri(
    Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
        ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is not set."));
string modelDeployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME") ?? "gpt-5-mini";
string tavilyApiKey = config["TAVILY_API_KEY"]
    ?? throw new InvalidOperationException(
        "Missing configuration value 'TAVILY_API_KEY'. Set it with: dotnet user-secrets set \"TAVILY_API_KEY\" \"<value>\"");

var tavilyHttpClient = new HttpClient { BaseAddress = new Uri("https://api.tavily.com/"), Timeout = TimeSpan.FromSeconds(20) };
tavilyHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tavilyApiKey);

// Small manual retry: transient network errors/timeouts get up to 2 retries
// with exponential backoff before the failure surfaces to the calling agent.
// (Ported unchanged from the console app's pre-migration Program.cs.)
async Task<HttpResponseMessage> PostWithRetryAsync(string requestUri, object body, CancellationToken cancellationToken)
{
    const int maxAttempts = 3;
    for (int attempt = 1; ; attempt++)
    {
        try
        {
            HttpResponseMessage response = await tavilyHttpClient.PostAsJsonAsync(requestUri, body, cancellationToken);
            response.EnsureSuccessStatusCode();
            return response;
        }
        catch (Exception ex) when (attempt < maxAttempts && ex is HttpRequestException or TaskCanceledException)
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), cancellationToken);
        }
    }
}

// Foundry's function-tool contract consumes only the input schema. Excluding
// the generated string return schema keeps the tool definition compatible
// while MAF still returns the search payload to the model at invocation time.
[Description("Search the web for comprehensive, accurate, and trusted results.")]
async Task<string> SearchTavilyAsync(
    [Description("The research query to search for.")] string query,
    CancellationToken cancellationToken)
{
    var request = new
    {
        query,
        max_results = 5,
        topic = "general",
        include_answer = false,
        include_raw_content = false,
        search_depth = "basic"
    };

    using HttpResponseMessage response = await PostWithRetryAsync("search", request, cancellationToken);
    return await response.Content.ReadAsStringAsync(cancellationToken);
}

AIFunction tavilyTool = AIFunctionFactory.Create(
    SearchTavilyAsync,
    new AIFunctionFactoryOptions
    {
        Name = "tavily_search",
        ExcludeResultSchema = true,
    });

// Entra ID only — no API keys, per repository constraint (this applies to the
// Foundry/model auth; the Tavily key above is a third-party API key, not Foundry auth).
AIAgent agent = new AIProjectClient(projectEndpoint, new DefaultAzureCredential())
    .AsAIAgent(
        model: modelDeployment,
        instructions: BlogWriter.Prompts.ResearcherInstructions,
        name: "Researcher",
        tools: [tavilyTool]);

var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();

// Needed for AddUserSecrets(typeof(Program).Assembly) with top-level statements.
internal partial class Program;
