using System.Text.Json;

namespace BlogWriter;

/// <summary>Stores each session as a JSON document on the local machine.</summary>
public sealed class FileBlogSessionStore(string directoryPath, string ownerId = "local") : IBlogSessionStore
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };
    private readonly string _directoryPath = directoryPath;
    private readonly string _ownerId = ownerId;

    public async Task<BlogSession> CreateAsync(ResearchState state, CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var session = new BlogSession
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerId = _ownerId,
            CreatedAt = now,
            UpdatedAt = now,
            State = state,
        };

        await SaveAsync(session, cancellationToken);
        return session;
    }

    public async Task<BlogSession?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (!IsValidId(sessionId))
        {
            return null;
        }

        string path = GetPath(sessionId);
        if (!File.Exists(path))
        {
            return null;
        }

        await using FileStream stream = File.OpenRead(path);
        BlogSession? session = await JsonSerializer.DeserializeAsync<BlogSession>(stream, s_jsonOptions, cancellationToken);
        return session is not null && (session.OwnerId == _ownerId || (session.OwnerId.Length == 0 && _ownerId == "local")) ? session : null;
    }

    public async Task<IReadOnlyList<BlogSessionSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_directoryPath))
        {
            return [];
        }

        var sessions = new List<BlogSessionSummary>();
        foreach (string path in Directory.EnumerateFiles(_directoryPath, "*.json"))
        {
            await using FileStream stream = File.OpenRead(path);
            BlogSession? session = await JsonSerializer.DeserializeAsync<BlogSession>(stream, s_jsonOptions, cancellationToken);
            if (session is not null && (session.OwnerId == _ownerId || (session.OwnerId.Length == 0 && _ownerId == "local")))
            {
                sessions.Add(BlogSessionSummary.Create(
                    session.Id, session.State.MainTask, session.CreatedAt, session.UpdatedAt,
                    session.State.MinWords, session.State.MaxWords, session.State.Draft));
            }
        }

        return sessions.OrderByDescending(session => session.UpdatedAt).Take(20).ToList();
    }

    public async Task SaveAsync(BlogSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!IsValidId(session.Id))
        {
            throw new ArgumentException("Session ID must be a 32-character hexadecimal GUID.", nameof(session));
        }

        Directory.CreateDirectory(_directoryPath);
        session.UpdatedAt = DateTimeOffset.UtcNow;
        string path = GetPath(session.Id);
        string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";

        await using (FileStream stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, session, s_jsonOptions, cancellationToken);
        }

        File.Move(temporaryPath, path, overwrite: true);
    }

    public async Task DeleteOwnerSessionsAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (!Directory.Exists(_directoryPath))
        {
            return;
        }

        foreach (string path in Directory.EnumerateFiles(_directoryPath, "*.json"))
        {
            BlogSession? session;
            await using (FileStream stream = File.OpenRead(path))
            {
                session = await JsonSerializer.DeserializeAsync<BlogSession>(stream, s_jsonOptions, cancellationToken);
            }

            if (session?.OwnerId == ownerId)
            {
                File.Delete(path);
            }
        }
    }

    private string GetPath(string sessionId) => Path.Combine(_directoryPath, $"{sessionId}.json");

    private static bool IsValidId(string sessionId) =>
        Guid.TryParseExact(sessionId, "N", out _);
}