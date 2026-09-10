using System.Text.Json;

namespace BlogWriter;

/// <summary>Stores each session as a JSON document on the local machine.</summary>
public sealed class FileBlogSessionStore(string directoryPath) : IBlogSessionStore
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };
    private readonly string _directoryPath = directoryPath;

    public async Task<BlogSession> CreateAsync(ResearchState state, CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var session = new BlogSession
        {
            Id = Guid.NewGuid().ToString("N"),
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
        return await JsonSerializer.DeserializeAsync<BlogSession>(stream, s_jsonOptions, cancellationToken);
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

    private string GetPath(string sessionId) => Path.Combine(_directoryPath, $"{sessionId}.json");

    private static bool IsValidId(string sessionId) =>
        Guid.TryParseExact(sessionId, "N", out _);
}