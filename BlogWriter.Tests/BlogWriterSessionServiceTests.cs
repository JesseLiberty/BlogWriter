using BlogWriter;
using Xunit;

namespace BlogWriter.Tests;

public sealed class BlogWriterSessionServiceTests
{
    [Fact]
    public async Task StartAsync_CreatesRunsAndSavesSession()
    {
        var store = new RecordingStore();
        var workflow = new StubWorkflow(state =>
        {
            state.Draft = "draft";
            state.ReviewNotes = "review";
            return state;
        });
        var service = new BlogWriterSessionService(workflow, store);

        BlogSession session = await service.StartAsync("topic", 500, 900);

        Assert.Equal("topic", session.State.MainTask);
        Assert.Equal("draft", session.State.Draft);
        Assert.Equal("review", session.State.ReviewNotes);
        Assert.Equal(500, session.State.MinWords);
        Assert.Equal(900, session.State.MaxWords);
        Assert.Equal(1, store.CreateCalls);
        Assert.Equal(1, store.SaveCalls);
    }

    [Fact]
    public async Task StartAsync_CreatesDistinctHistoryEntryFromRestoredSource()
    {
        var store = new RecordingStore();
        BlogSession source = CreateSession("source draft", "source review");
        source.State.MainTask = "source topic";
        store.Session = source;
        var service = new BlogWriterSessionService(
            new StubWorkflow(state => state),
            store);

        BlogSession created = await service.StartAsync("edited topic", 700, 1100);

        Assert.NotEqual(source.Id, created.Id);
        Assert.Same(created, store.Session);
        Assert.Equal(1, store.CreateCalls);
        Assert.Equal(1, store.SaveCalls);
        Assert.Equal("source topic", source.State.MainTask);
        Assert.Equal("source draft", source.State.Draft);
        Assert.Equal("source review", source.State.ReviewNotes);
    }

    [Fact]
    public async Task ReviseAsync_DoesNotMutateStableSessionWhenWorkflowFails()
    {
        var store = new RecordingStore();
        var service = new BlogWriterSessionService(
            new StubWorkflow(_ => throw new InvalidOperationException("failed")),
            store);
        BlogSession original = CreateSession("original draft", "original review");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReviseAsync(original, "change it", 500, 900));

        Assert.Equal("original draft", original.State.Draft);
        Assert.Equal("original review", original.State.ReviewNotes);
        Assert.Equal(0, store.SaveCalls);
    }

    [Fact]
    public async Task ReviseAsync_PublishesAndSavesCompletedCopy()
    {
        var store = new RecordingStore();
        var service = new BlogWriterSessionService(
            new StubWorkflow(state =>
            {
                state.Draft = "revised";
                state.ReviewNotes = "approved";
                return state;
            }),
            store);

        BlogSession revised = await service.ReviseAsync(
            CreateSession("old", "old review"),
            "change it",
            600,
            800);

        Assert.Equal("revised", revised.State.Draft);
        Assert.Equal("approved", revised.State.ReviewNotes);
        Assert.Equal(600, revised.State.MinWords);
        Assert.Equal(800, revised.State.MaxWords);
        Assert.Equal(1, store.SaveCalls);
    }

    [Fact]
    public async Task InvalidRange_DoesNotRunWorkflowOrPersist()
    {
        var store = new RecordingStore();
        var workflow = new StubWorkflow(state => state);
        var service = new BlogWriterSessionService(workflow, store);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.StartAsync("topic", 900, 500));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.ReviseAsync(CreateSession("draft", "review"), "change it", 0, 500));

        Assert.Equal(0, workflow.CallCount);
        Assert.Equal(0, store.CreateCalls);
        Assert.Equal(0, store.SaveCalls);
    }

    [Fact]
    public async Task ListAndLoadAsync_DelegateToOwnerScopedStore()
    {
        var store = new RecordingStore();
        BlogSession existing = CreateSession("draft", "review");
        store.Session = existing;
        var service = new BlogWriterSessionService(new StubWorkflow(state => state), store);

        IReadOnlyList<BlogSessionSummary> summaries = await service.ListAsync();
        BlogSession? loaded = await service.LoadAsync(existing.Id);

        Assert.Single(summaries);
        Assert.Same(existing, loaded);
    }

    [Fact]
    public async Task ListAsync_ForwardsEnrichedSummaryAndCancellationWithoutOtherWork()
    {
        BlogSession session = CreateSession("saved draft", "review");
        session.State.MinWords = 700;
        session.State.MaxWords = 1350;
        var store = new RecordingStore { Session = session };
        var workflow = new StubWorkflow(state => state);
        var service = new BlogWriterSessionService(workflow, store);
        using var cancellation = new CancellationTokenSource();

        BlogSessionSummary summary = Assert.Single(await service.ListAsync(cancellation.Token));

        Assert.Equal((700, 1350, "saved draft"), (summary.MinWords, summary.MaxWords, summary.DraftPreview));
        Assert.Equal(cancellation.Token, store.ListCancellationToken);
        Assert.Equal(0, store.GetCalls);
        Assert.Equal(0, store.SaveCalls);
        Assert.Equal(0, store.CreateCalls);
        Assert.Equal(0, workflow.CallCount);
        Assert.Equal("saved draft", session.State.Draft);
    }

    [Fact]
    public async Task StartAndReviseAsync_ForwardOutputObserverToWorkflow()
    {
        var workflow = new StubWorkflow(state => state);
        var service = new BlogWriterSessionService(workflow, new RecordingStore());
        var output = new Progress<WorkflowOutputUpdate>();

        await service.StartAsync("topic", output: output);
        Assert.Same(output, workflow.LastOutput);

        BlogSession session = CreateSession("draft", "review");
        await service.ReviseAsync(session, "change it", 500, 900, output: output);
        Assert.Same(output, workflow.LastOutput);
    }

    private static BlogSession CreateSession(string draft, string review) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        OwnerId = "owner",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        ETag = "etag",
        State = new ResearchState { MainTask = "topic", Draft = draft, ReviewNotes = review },
    };

    private sealed class StubWorkflow(Func<ResearchState, ResearchState> run) : IBlogWorkflow
    {
        public int CallCount { get; private set; }
        public IProgress<WorkflowOutputUpdate>? LastOutput { get; private set; }

        public Task<ResearchState> RunAsync(
            ResearchState state,
            CancellationToken cancellationToken = default,
            IProgress<WorkflowOutputUpdate>? output = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastOutput = output;
            return Task.FromResult(run(state));
        }
    }

    private sealed class RecordingStore : IBlogSessionStore
    {
        public int CreateCalls { get; private set; }
        public int SaveCalls { get; private set; }
        public int GetCalls { get; private set; }
        public CancellationToken ListCancellationToken { get; private set; }
        public BlogSession? Session { get; set; }

        public Task<BlogSession> CreateAsync(ResearchState state, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            Session = new BlogSession
            {
                Id = Guid.NewGuid().ToString("N"),
                OwnerId = "owner",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                State = state,
            };
            return Task.FromResult(Session);
        }

        public Task<BlogSession?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            GetCalls++;
            return Task.FromResult(Session?.Id == sessionId ? Session : null);
        }

        public Task<IReadOnlyList<BlogSessionSummary>> ListAsync(CancellationToken cancellationToken = default)
        {
            ListCancellationToken = cancellationToken;
            return Task.FromResult<IReadOnlyList<BlogSessionSummary>>(Session is null
                ? []
                : [BlogSessionSummary.Create(Session.Id, Session.State.MainTask, Session.CreatedAt,
                    Session.UpdatedAt, Session.State.MinWords, Session.State.MaxWords, Session.State.Draft)]);
        }

        public Task SaveAsync(BlogSession session, CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            Session = session;
            return Task.CompletedTask;
        }

        public Task DeleteOwnerSessionsAsync(string ownerId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
