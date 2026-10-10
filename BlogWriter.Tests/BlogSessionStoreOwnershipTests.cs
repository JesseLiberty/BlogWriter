using BlogWriter;
using Xunit;

namespace BlogWriter.Tests;

public class BlogSessionStoreOwnershipTests
{
    [Fact]
    public async Task GetAndListAsync_OnlyReturnTheCurrentOwnerSessions()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"BlogWriterTests-{Guid.NewGuid():N}");

        try
        {
            var firstOwnerStore = new FileBlogSessionStore(directory, "owner-one");
            var secondOwnerStore = new FileBlogSessionStore(directory, "owner-two");
            BlogSession firstOwnerSession = await firstOwnerStore.CreateAsync(new ResearchState
            {
                MainTask = "first", MinWords = 700, MaxWords = 1350, Draft = "first owner draft",
            });
            await secondOwnerStore.CreateAsync(new ResearchState
            {
                MainTask = "second", MinWords = 400, MaxWords = 400, Draft = "private second draft",
            });

            Assert.NotNull(await firstOwnerStore.GetAsync(firstOwnerSession.Id));
            Assert.Single(await firstOwnerStore.ListAsync());
            Assert.Equal("first", (await firstOwnerStore.ListAsync())[0].MainTask);
            BlogSessionSummary first = Assert.Single(await firstOwnerStore.ListAsync());
            BlogSessionSummary second = Assert.Single(await secondOwnerStore.ListAsync());
            Assert.Equal((700, 1350, "first owner draft"), (first.MinWords, first.MaxWords, first.DraftPreview));
            Assert.Equal((400, 400, "private second draft"), (second.MinWords, second.MaxWords, second.DraftPreview));
            Assert.Null(await secondOwnerStore.GetAsync(firstOwnerSession.Id));
            Assert.Equal("first owner draft", firstOwnerSession.State.Draft);
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
    public async Task ListAsync_OwnerlessLegacyDetailsAreVisibleOnlyToLocalOwner()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"BlogWriterTests-{Guid.NewGuid():N}");
        try
        {
            var local = new FileBlogSessionStore(directory);
            BlogSession legacy = await local.CreateAsync(new ResearchState
            {
                MainTask = "legacy", MinWords = 600, MaxWords = 900, Draft = "local legacy draft",
            });
            var ownerless = new BlogSession
            {
                Id = legacy.Id, OwnerId = "", CreatedAt = legacy.CreatedAt,
                UpdatedAt = legacy.UpdatedAt, State = legacy.State,
            };
            await local.SaveAsync(ownerless);
            string path = Path.Combine(directory, $"{legacy.Id}.json");
            string before = await File.ReadAllTextAsync(path);

            BlogSessionSummary summary = Assert.Single(await local.ListAsync());

            Assert.Equal((600, 900, "local legacy draft"), (summary.MinWords, summary.MaxWords, summary.DraftPreview));
            Assert.Empty(await new FileBlogSessionStore(directory, "other").ListAsync());
            Assert.Equal(before, await File.ReadAllTextAsync(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteOwnerSessionsAsync_RemovesOnlyRequestedOwnerSessions()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"BlogWriterTests-{Guid.NewGuid():N}");

        try
        {
            var firstOwnerStore = new FileBlogSessionStore(directory, "owner-one");
            var secondOwnerStore = new FileBlogSessionStore(directory, "owner-two");
            await firstOwnerStore.CreateAsync(new ResearchState { MainTask = "first" });
            await secondOwnerStore.CreateAsync(new ResearchState { MainTask = "second" });

            await firstOwnerStore.DeleteOwnerSessionsAsync("owner-one");

            Assert.Empty(await firstOwnerStore.ListAsync());
            Assert.Single(await secondOwnerStore.ListAsync());
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