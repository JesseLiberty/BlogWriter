using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace BlogWriter;

/// <summary>
/// Reviews draft content with a <see cref="ChatClientAgent"/> and returns either
/// approval or revision feedback.
///
/// The review result drives whether the workflow ends or routes back to the
/// author for another iteration.
/// </summary>
public class ReviewerAgent : IReviewerAgent
{
    private readonly AIAgent _agent;

    // Emits a span per review. Activated by the ActivityListener registered in
    // Program.cs (or an OpenTelemetry TracerProvider).
    private static readonly ActivitySource s_activitySource = new("BlogWriter.ReviewerAgent");

    private readonly ILogger<ReviewerAgent> _logger;

    public ReviewerAgent(AIAgent agent, ILogger<ReviewerAgent> logger)
    {
        _logger = logger;

        _agent = agent;
        _logger.LogInformation("ReviewerAgent initialized.");
    }

    public ReviewerAgent(IChatClient llm, ChatOptions chatOptions, ILogger<ReviewerAgent> logger)
        : this(new ChatClientAgent(llm.AsBuilder()
            .UseOpenTelemetry(sourceName: "BlogWriter.ChatClient")
            .Build(), new ChatClientAgentOptions
        {
            Name = "Reviewer",
            ChatOptions = chatOptions,
        }), logger)
    {
    }

    public async Task<string> InvokeAsync(ResearchState state, CancellationToken cancellationToken = default)
    {
        using Activity? activity = s_activitySource.StartActivity("Reviewer.Invoke");
        activity?.SetTag("blog.revision", state.RevisionNumber);

        string draft = state.Draft;

        // Per-turn input only — the evaluation criteria are on the agent.
        string message = $"""
            Main Task: {state.MainTask}

            Target Word Count: {state.MinWords} to {state.MaxWords} words

            Draft to Review:
            {draft}
            """;

        try
        {
            AgentResponse response = await _agent.RunAsync(message, cancellationToken: cancellationToken);
            string content = response.Text;
            return !string.IsNullOrWhiteSpace(content)
                ? content
                : throw new InvalidOperationException("The reviewer returned no content.");
        }
        catch (TokenCapExceededException)
        {
            // Budget breach is fatal — let it propagate so the app can shut down.
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Reviewer agent failed.");
            throw;
        }
    }

    /// <summary>Node that reviews the draft.</summary>
    public async Task<ResearchState> ReviewerNodeAsync(ResearchState state, CancellationToken cancellationToken = default)
    {
        string review = await InvokeAsync(state, cancellationToken);
        string preview = review.Length > 100 ? review[..100] : review;
        _logger.LogInformation("Review: {Preview}...", preview);

        bool isApproved = ResearchState.IsApproved(review);

        if (isApproved)
        {
            _logger.LogInformation("Draft APPROVED");
            state.ReviewNotes = ResearchState.ApprovedMarker;
            state.NextStep = "END";
        }
        else
        {
            _logger.LogInformation("Revisions needed");
            state.ReviewNotes = review;
            state.NextStep = "author";
        }

        return state;
    }
}
