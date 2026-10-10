using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BlogWriter.Tests;

public sealed class BlogWorkflowTests
{
    [Fact]
    public async Task RunAsync_EmitsLifecycleAndReviewerUpdatesWithoutChangingFinalState()
    {
        var author = new TestAuthor("draft");
        var reviewer = new TestReviewer("APPROVED");
        var workflow = new BlogWorkflow(
            new TestBlogger(),
            new TestResearcher(),
            author,
            reviewer,
            NullLogger<BlogWorkflow>.Instance);
        var output = new WorkflowOutputCollector();

        var service = new BlogWriterSessionService(workflow, new RecordingStore());
        BlogSession session = await service.StartAsync("topic", output: output);
        ResearchState result = session.State;

        Assert.Equal("draft", result.Draft);
        Assert.Equal("APPROVED", result.ReviewNotes);
        Assert.Contains(output.Updates, update =>
            update.Kind == WorkflowOutputKind.Lifecycle &&
            update.Outcome == WorkflowOutputOutcome.Progress);
        Assert.Contains(output.Updates, update =>
            update.Kind == WorkflowOutputKind.ReviewerFeedback &&
            update.Message == "APPROVED");
        Assert.Contains(output.Updates, update =>
            update.Kind == WorkflowOutputKind.Lifecycle &&
            update.Outcome == WorkflowOutputOutcome.Success);
        Assert.Contains(output.Updates, update => update.AgentStage == WorkflowAgentStage.Blogger);
        Assert.Contains(output.Updates, update => update.AgentStage == WorkflowAgentStage.Researcher);
        Assert.Contains(output.Updates, update => update.AgentStage == WorkflowAgentStage.Author);
        Assert.Contains(output.Updates, update => update.AgentStage == WorkflowAgentStage.Reviewer);
        Assert.All(
            output.Updates.Where(update => update.Kind == WorkflowOutputKind.Lifecycle && update.Message.EndsWith("completed.", StringComparison.Ordinal)),
            update => Assert.Equal(WorkflowAgentStage.None, update.AgentStage));
        Assert.Equal(output.Updates.Count, output.Updates.Select(update => update.Sequence).Distinct().Count());
        Assert.Equal(1, author.Calls);
        Assert.Equal(1, reviewer.Calls);
    }

    [Fact]
    public async Task RunAsync_CancellationDuringResearcherDoesNotInvokeDownstreamAgents()
    {
        var researcher = new BlockingResearcher();
        var author = new TestAuthor("draft");
        var reviewer = new TestReviewer("APPROVED");
        var workflow = new BlogWorkflow(
            new TestBlogger(),
            researcher,
            author,
            reviewer,
            NullLogger<BlogWorkflow>.Instance);
        using var cancellation = new CancellationTokenSource();

        Task<ResearchState> run = workflow.RunAsync(new ResearchState { MainTask = "topic" }, cancellation.Token);
        await researcher.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        try
        {
            await run;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Equal(0, author.Calls);
        Assert.Equal(0, reviewer.Calls);
    }

    [Fact]
    public async Task RunAsync_FailedExecutorClearsActiveAgentStage()
    {
        var author = new TestAuthor("draft");
        var workflow = new BlogWorkflow(
            new TestBlogger(),
            new FailingResearcher(),
            author,
            new TestReviewer("APPROVED"),
            NullLogger<BlogWorkflow>.Instance);
        var output = new WorkflowOutputCollector();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.RunAsync(new ResearchState { MainTask = "topic" }, output: output));

        Assert.Contains(output.Updates, update => update.AgentStage == WorkflowAgentStage.Researcher);
        WorkflowOutputUpdate failure = Assert.Single(output.Updates.Where(update => update.Outcome == WorkflowOutputOutcome.Failure));
        Assert.Equal(WorkflowAgentStage.None, failure.AgentStage);
        Assert.Equal(0, author.Calls);
    }

    [Fact]
    public async Task RunAsync_RejectedInitialDraftGetsOneRevisionAndNoSecondReview()
    {
        var author = new TestAuthor("draft-1", "draft-2");
        var reviewer = new TestReviewer("Please revise the introduction.");
        var workflow = new BlogWorkflow(
            new TestBlogger(),
            new TestResearcher(),
            author,
            reviewer,
            NullLogger<BlogWorkflow>.Instance);
        var output = new WorkflowOutputCollector();

        var service = new BlogWriterSessionService(workflow, new RecordingStore());
        BlogSession session = await service.StartAsync("topic", output: output);

        Assert.Equal("draft-2", session.State.Draft);
        Assert.Equal("Please revise the introduction.", session.State.ReviewNotes);
        Assert.Equal(2, author.Calls);
        Assert.Equal(2, author.ReviewNotesSeen.Count);
        Assert.Equal("", author.ReviewNotesSeen[0]);
        Assert.Equal("Please revise the introduction.", author.ReviewNotesSeen[1]);
        Assert.Equal(1, reviewer.Calls);
        Assert.Contains(output.Updates, update =>
            update.Kind == WorkflowOutputKind.Lifecycle &&
            update.Outcome == WorkflowOutputOutcome.Success);
    }

    [Fact]
    public async Task RunAsync_RejectedRevisionWithoutReplacementKeepsLatestDraftAndNoSecondReview()
    {
        var author = new TestAuthor("draft-1", null);
        var reviewer = new TestReviewer("Please revise the introduction.");
        var workflow = new BlogWorkflow(
            new TestBlogger(),
            new TestResearcher(),
            author,
            reviewer,
            NullLogger<BlogWorkflow>.Instance);
        var output = new WorkflowOutputCollector();

        var service = new BlogWriterSessionService(workflow, new RecordingStore());
        BlogSession session = await service.StartAsync("topic", output: output);

        Assert.Equal("draft-1", session.State.Draft);
        Assert.Equal(2, author.Calls);
        Assert.Equal(1, reviewer.Calls);
        Assert.Contains(output.Updates, update =>
            update.Kind == WorkflowOutputKind.Lifecycle &&
            update.Outcome == WorkflowOutputOutcome.Success);
    }

    private sealed class TestBlogger : IBloggerAgent
    {
        public Task<BloggerDecision> InvokeAsync(ResearchState state, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BloggerDecision("research", state.MainTask));

        public Task<ResearchState> BloggerNodeAsync(ResearchState state, CancellationToken cancellationToken = default)
        {
            state.NextStep = "research";
            state.CurrentSubTask = state.MainTask;
            return Task.FromResult(state);
        }
    }

    private sealed class TestResearcher : IResearcherAgent
    {
        public Task<string> InvokeAsync(string query, CancellationToken cancellationToken = default) =>
            Task.FromResult("finding");

        public Task<ResearchState> ResearchNodeAsync(ResearchState state, CancellationToken cancellationToken = default)
        {
            state.ResearchFindings.Add("finding");
            return Task.FromResult(state);
        }
    }

    private sealed class BlockingResearcher : IResearcherAgent
    {
        public TaskCompletionSource Started { get; } = new();

        public Task<string> InvokeAsync(string query, CancellationToken cancellationToken = default) =>
            Task.FromResult("finding");

        public async Task<ResearchState> ResearchNodeAsync(ResearchState state, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return state;
        }
    }

    private sealed class FailingResearcher : IResearcherAgent
    {
        public Task<string> InvokeAsync(string query, CancellationToken cancellationToken = default) =>
            Task.FromResult("finding");

        public Task<ResearchState> ResearchNodeAsync(ResearchState state, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("research failed");
    }

    private sealed class TestAuthor(params string?[] drafts) : IAuthorAgent
    {
        private readonly IReadOnlyList<string?> _drafts = drafts;

        public int Calls { get; private set; }
        public List<string> ReviewNotesSeen { get; } = [];

        public Task<string?> InvokeAsync(ResearchState state, CancellationToken cancellationToken = default) =>
            Task.FromResult(_drafts[Math.Min(Calls, _drafts.Count - 1)]);

        public Task<ResearchState> AuthorNodeAsync(ResearchState state, CancellationToken cancellationToken = default)
        {
            string? draft = _drafts[Math.Min(Calls, _drafts.Count - 1)];
            ReviewNotesSeen.Add(state.ReviewNotes);
            Calls++;
            state.RevisionNumber++;
            if (!string.IsNullOrEmpty(draft))
            {
                state.Draft = draft;
            }

            return Task.FromResult(state);
        }
    }

    private sealed class TestReviewer(string reviewNotes) : IReviewerAgent
    {
        public int Calls { get; private set; }

        public Task<string> InvokeAsync(ResearchState state, CancellationToken cancellationToken = default) =>
            Task.FromResult(reviewNotes);

        public Task<ResearchState> ReviewerNodeAsync(ResearchState state, CancellationToken cancellationToken = default)
        {
            Calls++;
            state.ReviewNotes = reviewNotes;
            return Task.FromResult(state);
        }
    }

    private sealed class RecordingStore : IBlogSessionStore
    {
        public Task<BlogSession> CreateAsync(ResearchState state, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BlogSession
            {
                Id = Guid.NewGuid().ToString("N"),
                OwnerId = "owner",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                State = state,
            });

        public Task<BlogSession?> GetAsync(string sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<BlogSession?>(null);

        public Task<IReadOnlyList<BlogSessionSummary>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BlogSessionSummary>>([]);

        public Task SaveAsync(BlogSession session, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteOwnerSessionsAsync(string ownerId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
