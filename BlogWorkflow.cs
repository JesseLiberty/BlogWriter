using System.Diagnostics;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace BlogWriter;

/// <summary>
/// Orchestrates the blog-writing pipeline with workflow stages for planning,
/// research, drafting, and review.
///
/// Execution flows Blogger → Researcher → Author → Reviewer, with a bounded
/// reviewer-to-author revision loop controlled by
/// <see cref="ResearchState.MaxRevisions"/>.
/// </summary>
public class BlogWorkflow(
    IBloggerAgent blogger,
    IResearcherAgent researcher,
    IAuthorAgent author,
    IReviewerAgent reviewer,
    ILogger<BlogWorkflow> logger) : IBlogWorkflow
{
    // Emits the root span for a workflow run. Activated by the ActivityListener
    // registered in Program.cs (or an OpenTelemetry TracerProvider).
    private static readonly ActivitySource s_activitySource = new("BlogWriter.Workflow");
    private static long s_operationVersion;

    public async Task<ResearchState> RunAsync(
        ResearchState state,
        CancellationToken cancellationToken = default,
        IProgress<WorkflowOutputUpdate>? output = null)
    {
        long operationVersion = Interlocked.Increment(ref s_operationVersion);
        var publisher = new WorkflowOutputPublisher(output, operationVersion);
        publisher.PublishLifecycle(WorkflowOutputOutcome.Progress, "Writing workflow started.");

        using Activity? activity = s_activitySource.StartActivity("Workflow.Run");
        activity?.SetTag("blog.topic", state.MainTask);

        var bloggerExecutor = new BloggerExecutor(blogger);
        var researcherExecutor = new ResearcherExecutor(researcher);
        var authorExecutor = new AuthorExecutor(author);
        var reviewerExecutor = new ReviewerExecutor(reviewer, publisher);

        Workflow workflow = new WorkflowBuilder(bloggerExecutor)
            .AddEdge(bloggerExecutor, researcherExecutor)
            .AddEdge(researcherExecutor, authorExecutor)
            // Review only the initial author pass. A rejected review can route back
            // to the author once, but the capped revision is terminal output.
            .AddEdge<ResearchState>(authorExecutor, reviewerExecutor,
                condition: s => s is not null && s.RevisionNumber < ResearchState.MaxRevisions)
            // Bounded revision loop: route back to the author only while the draft
            // still needs work and the revision cap has not been reached.
            .AddEdge<ResearchState>(reviewerExecutor, authorExecutor, condition: s => s?.NeedsRevision == true)
            .WithOutputFrom(reviewerExecutor, authorExecutor)
            .Build();

        // Stream execution instead of running to completion in one shot. The
        // topology is proven terminating; streaming simply surfaces each
        // executor's lifecycle as it happens, giving live progress. The final
        // ResearchState is captured from the WorkflowOutputEvent emitted by the
        // reviewer after approval or the author after the single revision.
        StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, state, cancellationToken: cancellationToken);

        ResearchState? result = null;

        await foreach (WorkflowEvent evt in run.WatchStreamAsync().WithCancellation(cancellationToken))
        {
            switch (evt)
            {
                case ExecutorInvokedEvent invoked:
                    logger.LogInformation("[workflow] -> {ExecutorId} started", invoked.ExecutorId);
                    publisher.PublishLifecycle(
                        WorkflowOutputOutcome.Progress,
                        $"{invoked.ExecutorId} started.",
                        GetAgentStage(invoked.ExecutorId));
                    break;

                case ExecutorCompletedEvent completed:
                    logger.LogInformation("[workflow] {ExecutorId} completed", completed.ExecutorId);
                    publisher.PublishLifecycle(WorkflowOutputOutcome.Progress, $"{completed.ExecutorId} completed.");
                    break;

                case ExecutorFailedEvent failed:
                    logger.LogError(failed.Data as Exception, "[workflow] {ExecutorId} failed", failed.ExecutorId);
                    publisher.PublishLifecycle(WorkflowOutputOutcome.Failure, $"{failed.ExecutorId} failed.");

                    // A token-cap breach must abort the whole run, not just the
                    // node. Re-throw it so it unwinds to the application entry point.
                    if (failed.Data is Exception ex && FindTokenCap(ex) is { } capEx)
                    {
                        throw capEx;
                    }

                    throw failed.Data as Exception ??
                        new InvalidOperationException($"Workflow executor '{failed.ExecutorId}' failed.");

                case WorkflowOutputEvent { Data: ResearchState finalState }:
                    // Reviewer approval or the capped author revision yields the final state.
                    result = finalState;
                    publisher.PublishLifecycle(WorkflowOutputOutcome.Success, "Writing workflow completed.");
                    break;
            }
        }

        // Fall back to the input state only if no output event was ever produced.
        return result ?? state;
    }

    private static WorkflowAgentStage GetAgentStage(string executorId) => executorId switch
    {
        "Blogger" => WorkflowAgentStage.Blogger,
        "Researcher" => WorkflowAgentStage.Researcher,
        "Author" => WorkflowAgentStage.Author,
        "Reviewer" => WorkflowAgentStage.Reviewer,
        _ => WorkflowAgentStage.None,
    };

    // Walks the exception chain (including AggregateException children) looking
    // for a token-cap breach, which the workflow runtime may have wrapped.
    private static TokenCapExceededException? FindTokenCap(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is TokenCapExceededException capEx)
            {
                return capEx;
            }

            if (exception is AggregateException aggregate)
            {
                foreach (Exception inner in aggregate.InnerExceptions)
                {
                    if (FindTokenCap(inner) is { } found)
                    {
                        return found;
                    }
                }

                return null;
            }

            exception = exception.InnerException;
        }

        return null;
    }
}
