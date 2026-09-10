using System.Diagnostics;
using Azure.AI.Projects;
using Azure.Identity;
using BlogWriter;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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

// Cumulative process-wide budget shared by all four MAF-hosted agent clients.
long maxTotalTokens = long.TryParse(config["MAX_TOTAL_TOKENS"], out long configuredMaxTotalTokens) ? configuredMaxTotalTokens : 40000;

// Entra ID only — no API keys, per repository constraint. Agent Framework owns
// the Foundry transport and Responses protocol details.
var azureCredential = new AzureCliCredential(new AzureCliCredentialOptions
{
    TenantId = tenantId,
});
AIProjectClient projectClient = new(foundryProjectEndpoint, azureCredential);
Func<IChatClient, IChatClient> tokenCapFactory = TokenCapChatClient.CreateSharedFactory(maxTotalTokens);
AIAgent BuildFoundryAgent(string hostedAgentName)
{
    Uri agentEndpoint = new($"{foundryProjectEndpoint.AbsoluteUri.TrimEnd('/')}/agents/{hostedAgentName}/endpoint/protocols/openai");
    return projectClient.AsAIAgent(
        agentEndpoint,
        tools: null,
        clientFactory: tokenCapFactory,
        services: null);
}

AIAgent bloggerLlm = BuildFoundryAgent(bloggerAgentName);
AIAgent researcherLlm = BuildFoundryAgent(researcherAgentName);
AIAgent authorLlm = BuildFoundryAgent(authorAgentName);
AIAgent reviewerLlm = BuildFoundryAgent(reviewerAgentName);

// Creating a callable object
using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

var bloggerAgent = new BloggerAgent(bloggerLlm, loggerFactory.CreateLogger<BloggerAgent>());
var researcherAgent = new ResearcherAgent(researcherLlm, loggerFactory.CreateLogger<ResearcherAgent>());
var authorAgent = new AuthorAgent(authorLlm, loggerFactory.CreateLogger<AuthorAgent>());
var reviewerAgent = new ReviewerAgent(reviewerLlm, loggerFactory.CreateLogger<ReviewerAgent>());
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

string sessionDirectory = config["BLOG_SESSION_STORE_PATH"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BlogWriter", "sessions");
IBlogSessionStore sessionStore = new FileBlogSessionStore(sessionDirectory);

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

// Ctrl+C requests a graceful cancellation of the in-flight run instead of an
// abrupt process kill.
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

while (!cts.IsCancellationRequested)
{
    Console.Write("Enter a topic, 'resume <session-id>', or press Enter to exit: ");
    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input))
    {
        break;
    }

    BlogSession? session = null;
    const string resumePrefix = "resume ";
    if (input.StartsWith(resumePrefix, StringComparison.OrdinalIgnoreCase))
    {
        string sessionId = input[resumePrefix.Length..].Trim();
        session = await sessionStore.GetAsync(sessionId, cts.Token);
        if (session is null)
        {
            Console.Error.WriteLine("Session not found. Check the session ID and configured session store path.");
            continue;
        }
    }
    else
    {
        int minWords = ReadWordCount(
            $"Enter minimum word count [{ResearchState.DefaultMinWords}]: ",
            ResearchState.DefaultMinWords);
        int maxWords = ReadWordCount(
            $"Enter maximum word count [{ResearchState.DefaultMaxWords}]: ",
            ResearchState.DefaultMaxWords,
            minimum: minWords);

        session = await sessionStore.CreateAsync(new ResearchState
        {
            MainTask = input,
            MinWords = minWords,
            MaxWords = maxWords
        }, cts.Token);
    }

    while (!cts.IsCancellationRequested)
    {
        try
        {
            using Activity? runActivity = appActivitySource.StartActivity("BlogWriter.Run");
            runActivity?.SetTag("blog.topic", session.State.MainTask);
            session.State = await app.RunAsync(session.State, cts.Token);
            await sessionStore.SaveAsync(session, cts.Token);
        }
        catch (TokenCapExceededException ex)
        {
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

        PrintResults(session);
        Console.Write("Follow-up request, or press Enter for a new topic: ");
        string? followUp = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(followUp))
        {
            break;
        }

        session!.State.StartFollowUp(followUp);
        await sessionStore.SaveAsync(session, cts.Token);
    }
}

void PrintResults(BlogSession session)
{
    ResearchState result = session.State;
    Console.WriteLine("\n========== RESULTS ==========");
    Console.WriteLine($"Session: {session.Id}");
    Console.WriteLine($"Task: {result.MainTask}");
    Console.WriteLine($"\nResearch Findings ({result.ResearchFindings.Count}):");
    foreach (string finding in result.ResearchFindings)
    {
        Console.WriteLine($"- {finding}");
    }

    Console.WriteLine($"\nDraft:\n{result.Draft}");
    Console.WriteLine($"\nReview Notes: {result.ReviewNotes}");
    Console.WriteLine($"Revision Number: {result.RevisionNumber}");
    if (result.RevisionLimitReached)
    {
        Console.WriteLine("Note: Maximum revision limit reached; draft above printed as-is.");
    }
    Console.WriteLine("=============================");
}


