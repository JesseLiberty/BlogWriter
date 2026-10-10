namespace BlogWriter.Web.Tests;

internal static class ListLauncherTestHelpers
{
    public static BlogSessionSummary Summary(string task) =>
        new(Guid.NewGuid().ToString("N"), task, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    public static BlogSession Session(
        string mainTask,
        string currentSubTask = "",
        string draft = "draft",
        string review = "review") => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerId = "owner",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            State = new ResearchState
            {
                MainTask = mainTask,
                CurrentSubTask = currentSubTask,
                Draft = draft,
                ReviewNotes = review,
            },
        };
}

internal sealed class SavedListSessionService : IBlogWriterSessionService
{
    public List<BlogSession> Sessions { get; } = [];
    public System.Collections.Concurrent.ConcurrentQueue<string> LoadedIds { get; } = new();
    public TaskCompletionSource<BlogSession?>? PendingLoad { get; set; }
    public int StartCalls { get; private set; }
    public int RevisionCalls { get; private set; }
    public int ListCalls { get; private set; }

    public Task<IReadOnlyList<BlogSessionSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        ListCalls++;
        return Task.FromResult<IReadOnlyList<BlogSessionSummary>>(Sessions.Select(session => BlogSessionSummary.Create(
            session.Id, session.State.MainTask, session.CreatedAt, session.UpdatedAt,
            session.State.MinWords, session.State.MaxWords, session.State.Draft)).ToArray());
    }

    public Task<BlogSession?> LoadAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        LoadedIds.Enqueue(sessionId);
        return PendingLoad?.Task ?? Task.FromResult(Sessions.FirstOrDefault(session => session.Id == sessionId));
    }

    public Task<BlogSession> StartAsync(string prompt, int minWords = ResearchState.DefaultMinWords,
        int maxWords = ResearchState.DefaultMaxWords, CancellationToken cancellationToken = default,
        IProgress<WorkflowOutputUpdate>? output = null)
    {
        StartCalls++;
        throw new InvalidOperationException("List tests must not start writing work.");
    }

    public Task<BlogSession> ReviseAsync(BlogSession session, string revision, int minWords, int maxWords,
        CancellationToken cancellationToken = default, IProgress<WorkflowOutputUpdate>? output = null)
    {
        RevisionCalls++;
        throw new InvalidOperationException("List tests must not start revision work.");
    }
}
