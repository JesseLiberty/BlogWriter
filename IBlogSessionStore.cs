namespace BlogWriter;

/// <summary>Persists completed workflow state so a user can continue a draft.</summary>
public interface IBlogSessionStore
{
    Task<BlogSession> CreateAsync(ResearchState state, CancellationToken cancellationToken = default);
    Task<BlogSession?> GetAsync(string sessionId, CancellationToken cancellationToken = default);
    Task SaveAsync(BlogSession session, CancellationToken cancellationToken = default);
}