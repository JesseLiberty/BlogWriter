using Newtonsoft.Json;
using System.Text;

namespace BlogWriter;

/// <summary>Persisted state for one user's blog-writing conversation.</summary>
public sealed class BlogSession
{
    [JsonProperty("id")]
    public required string Id { get; init; }
    public string OwnerId { get; init; } = "";
    [JsonIgnore]
    public string? ETag { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
    public required ResearchState State { get; set; }
}

public sealed record BlogSessionSummary(
    string Id,
    string MainTask,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public int MinWords { get; init; } = ResearchState.DefaultMinWords;
    public int MaxWords { get; init; } = ResearchState.DefaultMaxWords;
    public string DraftPreview { get; init; } = "";
    public bool IsDraftTruncated { get; init; }

    public static BlogSessionSummary Create(
        string id,
        string mainTask,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        int minWords,
        int maxWords,
        string? draft)
    {
        WordRange range = minWords > 0 && maxWords >= minWords
            ? new WordRange(minWords, maxWords)
            : WordRange.Default;
        var preview = new StringBuilder();
        int position = 0;
        int wordCount = 0;
        bool truncated = false;

        while (draft is not null && position < draft.Length)
        {
            if (char.IsWhiteSpace(draft[position]))
            {
                position++;
                continue;
            }

            if (wordCount == 50)
            {
                truncated = true;
                break;
            }

            int start = position;
            while (position < draft.Length && !char.IsWhiteSpace(draft[position]))
            {
                position++;
            }

            if (wordCount > 0)
            {
                preview.Append(' ');
            }

            preview.Append(draft.AsSpan(start, position - start));
            wordCount++;
        }

        return new BlogSessionSummary(id, mainTask, createdAt, updatedAt)
        {
            MinWords = range.Min,
            MaxWords = range.Max,
            DraftPreview = preview.ToString(),
            IsDraftTruncated = truncated,
        };
    }
}