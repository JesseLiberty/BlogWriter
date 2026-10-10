using BlogWriter;
using Xunit;

namespace BlogWriter.Tests;

public sealed class BlogSessionSummaryTests
{
    private static readonly DateTimeOffset s_created = DateTimeOffset.Parse("2026-10-01T12:00:00Z");
    private static readonly DateTimeOffset s_updated = s_created.AddDays(1);

    [Fact]
    public void Constructor_PreservesFourFieldContractAndDisplayDefaults()
    {
        var summary = new BlogSessionSummary("id", "topic", s_created, s_updated);
        (string id, string task, DateTimeOffset created, DateTimeOffset updated) = summary;

        Assert.Equal(("id", "topic", s_created, s_updated), (id, task, created, updated));
        Assert.Equal(ResearchState.DefaultMinWords, summary.MinWords);
        Assert.Equal(ResearchState.DefaultMaxWords, summary.MaxWords);
        Assert.Empty(summary.DraftPreview);
        Assert.False(summary.IsDraftTruncated);
        Assert.NotEqual(summary, summary with { DraftPreview = "changed" });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n\u2003 ")]
    public void Create_AbsentDraft_HasEmptyUntruncatedPreview(string? draft)
    {
        BlogSessionSummary summary = Create(draft);

        Assert.Empty(summary.DraftPreview);
        Assert.False(summary.IsDraftTruncated);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(49)]
    [InlineData(50)]
    [InlineData(51)]
    [InlineData(10000)]
    public void Create_PreviewsOnlyFirstFiftyWords(int wordCount)
    {
        string[] words = Enumerable.Range(1, wordCount).Select(number => $"word{number}").ToArray();
        string draft = string.Join(" \t\r\n\u2003", words);

        BlogSessionSummary summary = Create(draft);

        Assert.Equal(string.Join(" ", words.Take(50)), summary.DraftPreview);
        Assert.Equal(wordCount > 50, summary.IsDraftTruncated);
        Assert.DoesNotContain("...", summary.DraftPreview);
        Assert.Equal("id", summary.Id);
        Assert.Equal("topic", summary.MainTask);
        Assert.Equal(s_created, summary.CreatedAt);
        Assert.Equal(s_updated, summary.UpdatedAt);
    }

    [Fact]
    public void Create_PreservesPunctuationHyphensAndLiteralMarkup()
    {
        Assert.Equal("<b>hello</b> well-known, world!", Create(" \t<b>hello</b>\nwell-known,   world! ").DraftPreview);
    }

    [Theory]
    [InlineData(700, 1350, 700, 1350)]
    [InlineData(500, 500, 500, 500)]
    [InlineData(0, 100, 1000, 2000)]
    [InlineData(-1, 2000, 1000, 2000)]
    [InlineData(700, 0, 1000, 2000)]
    [InlineData(2000, 1000, 1000, 2000)]
    public void Create_NormalizesRangeAsCompletePair(int min, int max, int expectedMin, int expectedMax)
    {
        BlogSessionSummary summary = Create("draft", min, max);

        Assert.Equal(expectedMin, summary.MinWords);
        Assert.Equal(expectedMax, summary.MaxWords);
    }

    private static BlogSessionSummary Create(string? draft, int min = 1000, int max = 2000) =>
        BlogSessionSummary.Create("id", "topic", s_created, s_updated, min, max, draft);
}
