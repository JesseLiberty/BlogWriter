using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace BlogWriter;

/// <summary>
/// Generates and revises blog drafts using a <see cref="ChatClientAgent"/>.
///
/// The agent receives the main task, research findings, current draft, and
/// review notes, then returns draft content for the author stage.
/// </summary>
public class AuthorAgent : IAuthorAgent
{
    private readonly AIAgent _agent;

    // Emits a span per draft creation/revision. Activated by the ActivityListener
    // registered in Program.cs (or an OpenTelemetry TracerProvider).
    private static readonly ActivitySource s_activitySource = new("BlogWriter.AuthorAgent");

    private readonly ILogger<AuthorAgent> _logger;

    public AuthorAgent(AIAgent agent, ILogger<AuthorAgent> logger)
    {
        _logger = logger;

        _agent = agent;
        _logger.LogInformation("AuthorAgent initialized.");
    }

    public async Task<string?> InvokeAsync(ResearchState state, CancellationToken cancellationToken = default)
    {
        using Activity? activity = s_activitySource.StartActivity("Author.Invoke");
        activity?.SetTag("blog.revision", state.RevisionNumber);

        List<string> research = state.ResearchFindings;
        string researchText = research.Count > 0 ? string.Join("\n\n", research) : "No research available.";

        // Per-turn input only — the role/instructions are already on the agent.
        string message = $"""
            Main Task: {state.MainTask}

            Research Findings:
            {researchText}

            Current Draft: {(string.IsNullOrEmpty(state.Draft) ? "(none — write the first draft)" : state.Draft)}

            Review Notes: {(string.IsNullOrEmpty(state.ReviewNotes) ? "(none)" : state.ReviewNotes)}

            Target Word Count: {state.MinWords} to {state.MaxWords} words
            """;

        try
        {
            AgentResponse response = await _agent.RunAsync(message, cancellationToken: cancellationToken);
            string content = response.Text;
            if (!string.IsNullOrEmpty(content))
            {
                return content;
            }

            _logger.LogWarning("Author agent returned no content for revision {Revision}.", state.RevisionNumber);
            return null;
        }
        catch (TokenCapExceededException)
        {
            // Budget breach is fatal — let it propagate so the app can shut down.
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Author agent failed to generate content.");
            return null;
        }
    }

    /// <summary>Author node that creates or revises draft.</summary>
    public async Task<ResearchState> AuthorNodeAsync(ResearchState state, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Author stage started.");

        string? draft = await InvokeAsync(state, cancellationToken);

        if (string.IsNullOrEmpty(draft))
        {
            // Keep whatever draft already exists rather than clobbering it with a
            // placeholder — an empty/failed generation shouldn't erase real content.
            _logger.LogWarning("Author agent produced no draft; keeping the previous draft (if any).");
        }
        else
        {
            state.Draft = draft;
            _logger.LogInformation("Draft created: {Length} characters", draft.Length);
        }

        state.RevisionNumber += 1;
        return state;
    }
}
