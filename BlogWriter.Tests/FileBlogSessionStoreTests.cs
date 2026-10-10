using BlogWriter;
using System.Text.Json.Nodes;
using Xunit;

namespace BlogWriter.Tests;

public class FileBlogSessionStoreTests
{
    [Fact]
    public async Task ListAsync_ReflectsSavedDetailsWithoutChangingDocuments()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"BlogWriterTests-{Guid.NewGuid():N}");
        try
        {
            var store = new FileBlogSessionStore(directory);
            BlogSession first = await store.CreateAsync(new ResearchState
            {
                MainTask = "same topic",
                MinWords = 700,
                MaxWords = 1350,
                Draft = string.Join(" ", Enumerable.Range(1, 51).Select(number => $"word{number}")),
            });
            BlogSession second = await store.CreateAsync(new ResearchState
            {
                MainTask = "same topic",
                MinWords = 400,
                MaxWords = 400,
                Draft = "short draft",
            });
            string path = Path.Combine(directory, $"{first.Id}.json");
            string saved = await File.ReadAllTextAsync(path);

            IReadOnlyList<BlogSessionSummary> listed = await store.ListAsync();

            Assert.Equal(saved, await File.ReadAllTextAsync(path));
            Assert.Equal(BlogSessionSummary.Create(first.Id, first.State.MainTask, first.CreatedAt,
                first.UpdatedAt, 700, 1350, first.State.Draft), listed.Single(summary => summary.Id == first.Id));
            Assert.Equal("short draft", listed.Single(summary => summary.Id == second.Id).DraftPreview);

            first.State.MinWords = 800;
            first.State.MaxWords = 1400;
            first.State.Draft = "new saved draft";
            await store.SaveAsync(first);
            listed = await store.ListAsync();
            BlogSessionSummary refreshed = listed.Single(summary => summary.Id == first.Id);
            Assert.Equal((800, 1400, "new saved draft"), (refreshed.MinWords, refreshed.MaxWords, refreshed.DraftPreview));
            Assert.False(refreshed.IsDraftTruncated);
            Assert.Equal("short draft", listed.Single(summary => summary.Id == second.Id).DraftPreview);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(null, null, 1000, 2000)]
    [InlineData(700, null, 700, 2000)]
    [InlineData(null, 1500, 1000, 1500)]
    [InlineData(0, 1500, 1000, 2000)]
    [InlineData(1800, 1200, 1000, 2000)]
    public async Task ListAsync_LegacyAndInvalidBoundsMatchRestore(int? min, int? max, int expectedMin, int expectedMax)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"BlogWriterTests-{Guid.NewGuid():N}");
        try
        {
            var store = new FileBlogSessionStore(directory);
            BlogSession session = await store.CreateAsync(new ResearchState { MainTask = "legacy" });
            string path = Path.Combine(directory, $"{session.Id}.json");
            JsonObject document = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
            JsonObject state = document["State"]!.AsObject();
            state.Remove("MinWords");
            state.Remove("MaxWords");
            if (min.HasValue) state["MinWords"] = min.Value;
            if (max.HasValue) state["MaxWords"] = max.Value;
            state["Draft"] = null;
            string saved = document.ToJsonString();
            await File.WriteAllTextAsync(path, saved);

            BlogSessionSummary summary = Assert.Single(await store.ListAsync());

            Assert.Equal((expectedMin, expectedMax), (summary.MinWords, summary.MaxWords));
            Assert.Empty(summary.DraftPreview);
            Assert.False(summary.IsDraftTruncated);
            Assert.Equal(saved, await File.ReadAllTextAsync(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CreateAndGetAsync_RoundTripsCompletedWorkflowState()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"BlogWriterTests-{Guid.NewGuid():N}");

        try
        {
            var store = new FileBlogSessionStore(directory);
            var state = new ResearchState
            {
                MainTask = "session memory",
                ResearchFindings = ["finding"],
                Draft = "draft",
                ReviewNotes = ResearchState.ApprovedMarker,
            };

            BlogSession created = await store.CreateAsync(state);
            BlogSession? loaded = await store.GetAsync(created.Id);

            Assert.NotNull(loaded);
            Assert.Equal(created.Id, loaded.Id);
            Assert.Equal("session memory", loaded.State.MainTask);
            Assert.Equal(["finding"], loaded.State.ResearchFindings);
            Assert.Equal("draft", loaded.State.Draft);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetAsync_ReturnsNullForUnknownOrInvalidSessionId()
    {
        var store = new FileBlogSessionStore(Path.Combine(Path.GetTempPath(), $"BlogWriterTests-{Guid.NewGuid():N}"));

        Assert.Null(await store.GetAsync("not-a-session-id"));
        Assert.Null(await store.GetAsync(Guid.NewGuid().ToString("N")));
    }

    [Fact]
    public async Task ListAsync_ReturnsTheTwentyMostRecentlyUpdatedSessionsFirst()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"BlogWriterTests-{Guid.NewGuid():N}");

        try
        {
            var store = new FileBlogSessionStore(directory);
            for (int index = 0; index < 21; index++)
            {
                await store.CreateAsync(new ResearchState { MainTask = $"topic {index}" });
            }

            IReadOnlyList<BlogSessionSummary> sessions = await store.ListAsync();

            Assert.Equal(20, sessions.Count);
            Assert.Equal("topic 20", sessions[0].MainTask);
            Assert.DoesNotContain(sessions, session => session.MainTask == "topic 0");
            Assert.True(sessions.Zip(sessions.Skip(1), (first, second) => first.UpdatedAt >= second.UpdatedAt).All(result => result));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}