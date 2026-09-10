namespace BlogWriter;

/// <summary>Persisted state for one user's blog-writing conversation.</summary>
public sealed class BlogSession
{
    public required string Id { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
    public required ResearchState State { get; set; }
}