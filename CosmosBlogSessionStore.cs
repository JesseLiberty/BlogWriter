using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace BlogWriter;

public sealed class CosmosBlogSessionStore(
    Container container,
    ISessionOwnerProvider ownerProvider,
    ILogger<CosmosBlogSessionStore> logger) : IBlogSessionStore
{
    private readonly Container _container = container;
    private readonly ISessionOwnerProvider _ownerProvider = ownerProvider;
    private readonly ILogger<CosmosBlogSessionStore> _logger = logger;

    public async Task<BlogSession> CreateAsync(ResearchState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        var session = new BlogSession
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerId = await _ownerProvider.GetOwnerIdAsync(cancellationToken),
            CreatedAt = now,
            UpdatedAt = now,
            State = state,
        };

        ItemResponse<BlogSession> response = await _container.CreateItemAsync(
            session,
            new PartitionKey(session.OwnerId),
            cancellationToken: cancellationToken);
        session.ETag = response.ETag;
        _logger.LogInformation("Created session {SessionId}.", session.Id);
        return session;
    }

    public async Task<BlogSession?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (!IsValidId(sessionId))
        {
            return null;
        }

        string ownerId = await _ownerProvider.GetOwnerIdAsync(cancellationToken);
        try
        {
            ItemResponse<BlogSession> response = await _container.ReadItemAsync<BlogSession>(
                sessionId,
                new PartitionKey(ownerId),
                cancellationToken: cancellationToken);
            BlogSession session = response.Resource;
            session.ETag = response.ETag;
            _logger.LogInformation("Loaded session {SessionId}.", sessionId);
            return session.OwnerId == ownerId ? session : null;
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInformation("Session {SessionId} was not found.", sessionId);
            return null;
        }
    }

    public async Task<IReadOnlyList<BlogSessionSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        string ownerId = await _ownerProvider.GetOwnerIdAsync(cancellationToken);
        var query = new QueryDefinition(
            "SELECT TOP 20 c.id AS Id, c.State.MainTask AS MainTask, c.CreatedAt AS CreatedAt, c.UpdatedAt AS UpdatedAt, " +
            "c.State.MinWords AS MinWords, c.State.MaxWords AS MaxWords, c.State.Draft AS Draft " +
            "FROM c WHERE c.OwnerId = @ownerId ORDER BY c.UpdatedAt DESC")
            .WithParameter("@ownerId", ownerId);
        using FeedIterator<SessionListProjection> iterator = _container.GetItemQueryIterator<SessionListProjection>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(ownerId) });
        var sessions = new List<BlogSessionSummary>();
        while (iterator.HasMoreResults)
        {
            FeedResponse<SessionListProjection> page = await iterator.ReadNextAsync(cancellationToken);
            sessions.AddRange(page.Select(session => BlogSessionSummary.Create(
                session.Id, session.MainTask, session.CreatedAt, session.UpdatedAt,
                session.MinWords, session.MaxWords, session.Draft)));
        }

        _logger.LogInformation("Listed {SessionCount} saved sessions.", sessions.Count);
        return sessions;
    }

    public async Task SaveAsync(BlogSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!IsValidId(session.Id) || string.IsNullOrWhiteSpace(session.OwnerId))
        {
            throw new ArgumentException("A session must have a valid ID and owner.", nameof(session));
        }

        string ownerId = await _ownerProvider.GetOwnerIdAsync(cancellationToken);
        if (!string.Equals(session.OwnerId, ownerId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A session can only be saved by its owner.");
        }

        session.UpdatedAt = DateTimeOffset.UtcNow;
        try
        {
            ItemResponse<BlogSession> response = await _container.ReplaceItemAsync(
                session,
                session.Id,
                new PartitionKey(ownerId),
                new ItemRequestOptions { IfMatchEtag = session.ETag },
                cancellationToken);
            session.ETag = response.ETag;
            _logger.LogInformation("Saved session {SessionId}.", session.Id);
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            _logger.LogWarning("Rejected a stale save for session {SessionId}.", session.Id);
            throw new SessionConflictException(session.Id);
        }
    }

    public async Task DeleteOwnerSessionsAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        var query = new QueryDefinition("SELECT VALUE c.id FROM c WHERE c.OwnerId = @ownerId")
            .WithParameter("@ownerId", ownerId);
        using FeedIterator<string> iterator = _container.GetItemQueryIterator<string>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(ownerId) });

        while (iterator.HasMoreResults)
        {
            FeedResponse<string> page = await iterator.ReadNextAsync(cancellationToken);
            foreach (string sessionId in page)
            {
                await _container.DeleteItemAsync<BlogSession>(
                    sessionId,
                    new PartitionKey(ownerId),
                    cancellationToken: cancellationToken);
            }
        }

        _logger.LogInformation("Deleted sessions for an account lifecycle event.");
    }

    private sealed class SessionListProjection
    {
        public string Id { get; set; } = "";
        public string MainTask { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public int MinWords { get; set; } = ResearchState.DefaultMinWords;
        public int MaxWords { get; set; } = ResearchState.DefaultMaxWords;
        public string? Draft { get; set; }
    }

    private static bool IsValidId(string sessionId) => Guid.TryParseExact(sessionId, "N", out _);
}