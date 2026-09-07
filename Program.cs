using System.Diagnostics;
using Azure.AI.Projects;
using Azure.Identity;
using BlogWriter;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
<<<<<<< HEAD
using ModelContextProtocol.Client;
using OpenAI;
=======
>>>>>>> c05a6c3 (Add hosted agent implementations)

// Secrets come from the .NET user-secrets store and from
// environment variables (secrets win on key collisions).
IConfiguration config = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>()
    .Build();

string GetRequired(string key) =>
    config[key] ?? throw new InvalidOperationException(
        $"Missing configuration value '{key}'. Set it with: dotnet user-secrets set \"{key}\" \"<value>\"");

// Foundry project + hosted agent names — the 4 agents are pre-provisioned and
// deployed independently (see HostedAgents/*/README and azd scaffolding);
// this app only references them by name, it never creates/updates them.
var foundryProjectEndpoint = new Uri(GetRequired("FOUNDRY_PROJECT_ENDPOINT"));
string tenantId = GetRequired("AZURE_TENANT_ID");
string bloggerAgentName = config["BLOGGER_AGENT_NAME"] ?? "Blogger";
string researcherAgentName = config["RESEARCHER_AGENT_NAME"] ?? "Researcher";
string authorAgentName = config["AUTHOR_AGENT_NAME"] ?? "Author";
string reviewerAgentName = config["REVIEWER_AGENT_NAME"] ?? "Reviewer";

// Overridable via user-secrets/env vars; these defaults match the original behaviour.
int maxOutputTokens = int.TryParse(config["MAX_OUTPUT_TOKENS"], out int configuredMaxOutputTokens) ? configuredMaxOutputTokens : 4096;
long maxTotalTokens = long.TryParse(config["MAX_TOTAL_TOKENS"], out long configuredMaxTotalTokens) ? configuredMaxTotalTokens : 40000;

// Entra ID only — no API keys, per repository constraint. Agent Framework owns
// the Foundry transport and Responses protocol details.
var azureCredential = new AzureCliCredential(new AzureCliCredentialOptions
{
    TenantId = tenantId,
});
AIProjectClient projectClient = new(foundryProjectEndpoint, azureCredential);
AIAgent BuildFoundryAgent(string hostedAgentName)
{
    Uri agentEndpoint = new($"{foundryProjectEndpoint.AbsoluteUri.TrimEnd('/')}/agents/{hostedAgentName}/endpoint/protocols/openai");
    return projectClient.AsAIAgent(agentEndpoint);
}

AIAgent bloggerLlm = BuildFoundryAgent(bloggerAgentName);
AIAgent researcherLlm = BuildFoundryAgent(researcherAgentName);
AIAgent authorLlm = BuildFoundryAgent(authorAgentName);
AIAgent reviewerLlm = BuildFoundryAgent(reviewerAgentName);

var chatOptions = new ChatOptions
{
    Temperature = 1,
    MaxOutputTokens = maxOutputTokens
};

// Creating a callable object
using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
ILogger startupLogger = loggerFactory.CreateLogger("BlogWriter.Startup");

// Microsoft Learn's remote MCP server exposes docs search/fetch tools the
// Researcher can call alongside Tavily for authoritative Microsoft/Azure content.
// If the remote endpoint is unreachable/slow/erroring at startup, don't let it
// take down the whole app — fall back to Tavily-only tools.
List<AIFunction> researcherTools = [tavilyTool];
try
{
    McpClient microsoftLearnMcp = await McpClient.CreateAsync(
        new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri("https://learn.microsoft.com/api/mcp"),
            Name = "microsoft-learn",
        }));
    IList<McpClientTool> microsoftLearnTools = await microsoftLearnMcp.ListToolsAsync();
    researcherTools.AddRange(microsoftLearnTools);
}
catch (Exception ex)
{
    startupLogger.LogWarning(ex, "Microsoft Learn MCP server unavailable; continuing with Tavily-only research tools.");
}

<<<<<<< HEAD
var bloggerAgent = new BloggerAgent(llm, chatOptions, loggerFactory.CreateLogger<BloggerAgent>());
var researcherAgent = new ResearcherAgent(llm, chatOptions, researcherTools, loggerFactory.CreateLogger<ResearcherAgent>());
var authorAgent = new AuthorAgent(llm, chatOptions, loggerFactory.CreateLogger<AuthorAgent>());
var reviewerAgent = new ReviewerAgent(llm, chatOptions, loggerFactory.CreateLogger<ReviewerAgent>());
=======
var bloggerAgent = new BloggerAgent(bloggerLlm, chatOptions, loggerFactory.CreateLogger<BloggerAgent>());
var researcherAgent = new ResearcherAgent(researcherLlm, chatOptions, loggerFactory.CreateLogger<ResearcherAgent>());
var authorAgent = new AuthorAgent(authorLlm, chatOptions, loggerFactory.CreateLogger<AuthorAgent>());
var reviewerAgent = new ReviewerAgent(reviewerLlm, chatOptions, loggerFactory.CreateLogger<ReviewerAgent>());
>>>>>>> c05a6c3 (Add hosted agent implementations)
var app = new BlogWorkflow(bloggerAgent, researcherAgent, authorAgent, reviewerAgent, loggerFactory.CreateLogger<BlogWorkflow>());

// Distributed tracing: an ActivityListener activates every "BlogWriter.*"
// ActivitySource in the app (agents, workflow, and the IChatClient's
// "BlogWriter.ChatClient" GenAI spans) and writes span start/stop to the
// console. Swap this listener for OpenTelemetry's TracerProvider to export the
// same spans to a backend instead.
var appActivitySource = new ActivitySource("BlogWriter.Program");

ActivitySource.AddActivityListener(new ActivityListener
{
    ShouldListenTo = source => source.Name.StartsWith("BlogWriter", StringComparison.Ordinal),
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    ActivityStarted = activity => Console.WriteLine($"[trace] \u2192 {activity.DisplayName}"),
    ActivityStopped = activity =>
        Console.WriteLine($"[trace] \u2190 {activity.DisplayName} ({activity.Duration.TotalMilliseconds:F0} ms)")
});

Console.Write("Enter your topic: ");
string topic = Console.ReadLine() ?? string.Empty;

int minWords = ReadWordCount(
    $"Enter minimum word count [{ResearchState.DefaultMinWords}]: ",
    ResearchState.DefaultMinWords);
int maxWords = ReadWordCount(
    $"Enter maximum word count [{ResearchState.DefaultMaxWords}]: ",
    ResearchState.DefaultMaxWords,
    minimum: minWords);

// Prompts for a positive word count, re-asking until a valid value (or blank
// for the default) is entered. `minimum`, when set, enforces max >= min.
int ReadWordCount(string prompt, int defaultValue, int? minimum = null)
{
    while (true)
    {
        Console.Write(prompt);
        string? input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue;
        }

        if (int.TryParse(input, out int value) && value > 0 && (minimum is null || value >= minimum))
        {
            return value;
        }

        Console.WriteLine(minimum is null
            ? "Please enter a positive whole number."
            : $"Please enter a whole number greater than or equal to {minimum}.");
    }
}

// Run the workflow for the entered topic
var initialState = new ResearchState
{
    MainTask = topic,
    MinWords = minWords,
    MaxWords = maxWords
};

// Ctrl+C requests a graceful cancellation of the in-flight run instead of an
// abrupt process kill.
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

ResearchState result;
try
{
    using Activity? runActivity = appActivitySource.StartActivity("BlogWriter.Run");
    runActivity?.SetTag("blog.topic", topic);
    result = await app.RunAsync(initialState, cts.Token);
}
catch (TokenCapExceededException ex)
{
    // Graceful shutdown: the exception unwinds the call stack so every `using`
    // (logger factory, HTTP clients, etc.) is disposed before we exit.
    Console.Error.WriteLine($"{ex.Message} Exiting application.");
    Environment.ExitCode = 1;
    return;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Run cancelled. Exiting application.");
    Environment.ExitCode = 1;
    return;
}

Console.WriteLine("\n========== RESULTS ==========");
Console.WriteLine($"Task: {result.MainTask}");

Console.WriteLine($"\nResearch Findings ({result.ResearchFindings.Count}):");
foreach (string finding in result.ResearchFindings)
{
    Console.WriteLine($"- {finding}");
}

Console.WriteLine($"\n\n========== Draft ==========\n\n{result.Draft}");
Console.WriteLine($"\n========== Review Notes ==========\n{result.ReviewNotes}");
Console.WriteLine($"\n========== Revision Notes ==========\n{result.RevisionNumber}");
if (result.RevisionNumber >= ResearchState.MaxRevisions)
{
    // The revision cap terminates the loop even if the reviewer never approved —
    // call that out so the draft above isn't mistaken for a reviewer-approved one.
    Console.WriteLine("Note: Maximum revision limit reached; draft above printed as-is.");
}
Console.WriteLine("\n=============================\n");


