using Bunit;
using BlogWriter.Web.Components.Pages;
using BlogWriter.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace BlogWriter.Web.Tests;

public sealed class HomePageTests : BunitContext
{
    public HomePageTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Fact]
    public void Home_RendersWritingWorkspaceAndFiveCommands()
    {
        BlogWorkspaceService workspace = RegisterWorkspace();

        IRenderedComponent<Home> cut = Render<Home>();

        Assert.NotNull(cut.Find("#initial-prompt"));
        Assert.NotNull(cut.Find("#revision-prompt"));
        Assert.NotNull(cut.Find("[aria-labelledby='draft-heading']"));
        Assert.NotNull(cut.Find("[aria-labelledby='review-heading']"));
        Assert.Equal(["New", "List", "Go", "Quit", "?"],
            cut.FindAll(".command-bar button").Select(button => button.TextContent.Trim()).ToArray());
        Assert.True(cut.Find("#revision-prompt").HasAttribute("disabled"));
        Assert.False(workspace.State.IsSelectionVisible);
    }

    [Fact]
    public void Home_PlacesDefaultWordRangeBetweenPromptsAndContentPanes()
    {
        RegisterWorkspace();
        IRenderedComponent<Home> cut = Render<Home>();
        string markup = cut.Markup;

        Assert.True(markup.IndexOf("prompt-strip", StringComparison.Ordinal) <
            markup.IndexOf("word-range-row", StringComparison.Ordinal));
        Assert.True(markup.IndexOf("word-range-row", StringComparison.Ordinal) <
            markup.IndexOf("work-grid", StringComparison.Ordinal));
        Assert.Equal("1000", cut.Find("#min-words").GetAttribute("value"));
        Assert.Equal("2000", cut.Find("#max-words").GetAttribute("value"));
        Assert.NotNull(cut.Find("button[data-command='go']"));
    }

    [Fact]
    public void Home_ShowsNumberInputAndKeepsRevisionDisabledForUnselectedList()
    {
        BlogWorkspaceService workspace = RegisterWorkspace([
            new BlogSessionSummary(Guid.NewGuid().ToString("N"), "first topic", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
        ]);
        IRenderedComponent<Home> cut = Render<Home>();

        cut.Find("button[data-command='list']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("#command-session-number"));
            Assert.True(cut.Find("#revision-prompt").HasAttribute("disabled"));
            Assert.Contains("[1]", cut.Find(".session-list").TextContent);
        });
    }

    [Fact]
    public void Home_NewClearsWorkspace()
    {
        BlogWorkspaceService workspace = RegisterWorkspace();
        workspace.State.InitialPrompt = "unsaved";
        workspace.State.Draft = "draft";
        IRenderedComponent<Home> cut = Render<Home>();

        cut.Find("button[data-command='new']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[role='dialog']")));
        cut.Find("button[data-confirm='discard']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(workspace.State.InitialPrompt);
            Assert.Empty(workspace.State.Draft);
        });
    }

    [Fact]
    public void Home_NewRestoresFreshControlAvailability()
    {
        BlogWorkspaceService workspace = RegisterWorkspace();
        IRenderedComponent<Home> cut = Render<Home>();

        cut.Find("#initial-prompt").Input("topic");
        cut.Find("button[data-command='go']").Click();
        cut.WaitForAssertion(() => Assert.Equal(WorkspaceMode.Draft, workspace.State.Mode));

        cut.Find("button[data-command='new']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(WorkspaceMode.New, workspace.State.Mode);
            Assert.False(cut.Find("#initial-prompt").HasAttribute("disabled"));
            Assert.True(cut.Find("#revision-prompt").HasAttribute("disabled"));
            Assert.False(cut.Find("#min-words").HasAttribute("disabled"));
            Assert.False(cut.Find("#max-words").HasAttribute("disabled"));
            Assert.All(cut.FindAll(".command-bar button"), button => Assert.False(button.HasAttribute("disabled")));
        });
    }

    [Fact]
    public async Task Home_RevisionIsDisabledWhenNewQueryIsEmpty()
    {
        BlogWorkspaceService workspace = RegisterWorkspace();
        workspace.State.InitialPrompt = "topic";

        await workspace.SubmitInitialAsync();
        workspace.State.InitialPrompt = "";
        IRenderedComponent<Home> cut = Render<Home>();

        Assert.True(workspace.State.HasDraft);
        Assert.True(cut.Find("#initial-prompt").HasAttribute("disabled"));
        Assert.True(cut.Find("#revision-prompt").HasAttribute("disabled"));
    }

    [Fact]
    public void Home_RevisionWindowTracksQueryNotDraftOrRevisionText()
    {
        BlogWorkspaceService workspace = RegisterWorkspace();
        IRenderedComponent<Home> cut = Render<Home>();
        var revision = cut.Find("#revision-prompt");

        Assert.True(revision.HasAttribute("disabled"));

        cut.Find("#initial-prompt").Input("topic");

        Assert.False(revision.HasAttribute("disabled"));
        Assert.False(workspace.State.HasDraft);

        revision.Input("revise the introduction");

        Assert.False(revision.HasAttribute("disabled"));

        cut.Find("#initial-prompt").Input("  ");

        Assert.True(revision.HasAttribute("disabled"));
    }

    [Fact]
    public async Task Home_GoDisablesWorkspaceControlsUntilDraftIsPublished()
    {
        var sessions = new StubSessionService { PendingStart = new TaskCompletionSource<BlogSession>() };
        var workspace = new BlogWorkspaceService(sessions, TimeSpan.FromMilliseconds(10));
        Services.AddSingleton(workspace);
        IRenderedComponent<Home> cut = Render<Home>();
        cut.Find("#initial-prompt").Input("topic");

        Task click = cut.Find("button[data-command='go']").ClickAsync();
        await sessions.Started.Task;

        Assert.True(cut.Find("#initial-prompt").HasAttribute("disabled"));
        Assert.True(cut.Find("#revision-prompt").HasAttribute("disabled"));
        Assert.True(cut.Find("#min-words").HasAttribute("disabled"));
        Assert.True(cut.Find("#max-words").HasAttribute("disabled"));
        Assert.All(cut.FindAll(".command-bar button"), button => Assert.True(button.HasAttribute("disabled")));

        sessions.PendingStart.SetResult(new BlogSession
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerId = "owner",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            State = new ResearchState { MainTask = "topic", Draft = "draft", ReviewNotes = "review" },
        });
        await click;

        Assert.False(cut.Find("button[data-command='go']").HasAttribute("disabled"));
        Assert.True(cut.Find("#initial-prompt").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Home_ListLocksWorkspaceButLeavesNewAndSessionSelectionUsable()
    {
        var sessions = new StubSessionService
        {
            PendingList = new TaskCompletionSource<IReadOnlyList<BlogSessionSummary>>(),
            Summaries = [new BlogSessionSummary(Guid.NewGuid().ToString("N"), "saved", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)],
        };
        var workspace = new BlogWorkspaceService(sessions, TimeSpan.FromMilliseconds(10));
        Services.AddSingleton(workspace);
        IRenderedComponent<Home> cut = Render<Home>();

        Task click = cut.Find("button[data-command='list']").ClickAsync();
        await sessions.ListStarted.Task;

        Assert.True(cut.Find("#initial-prompt").HasAttribute("disabled"));
        Assert.True(cut.Find("#revision-prompt").HasAttribute("disabled"));
        Assert.True(cut.Find("#min-words").HasAttribute("disabled"));
        Assert.True(cut.Find("#max-words").HasAttribute("disabled"));
        Assert.True(cut.Find("button[data-command='new']").HasAttribute("disabled") is false);
        Assert.True(cut.Find("button[data-command='list']").HasAttribute("disabled"));
        Assert.True(cut.Find("button[data-command='go']").HasAttribute("disabled"));
        Assert.True(cut.Find("button[data-command='quit']").HasAttribute("disabled"));
        Assert.True(cut.Find("button[data-command='help']").HasAttribute("disabled"));

        sessions.PendingList.SetResult(sessions.Summaries);
        await click;

        Assert.False(cut.Find("#command-session-number").HasAttribute("disabled"));
        Assert.True(cut.Find("button[data-command='new']").HasAttribute("disabled") is false);
    }

    [Fact]
    public async Task Home_PopulatedDraftKeepsRevisionEnabledWhileQueryIsPresent()
    {
        BlogWorkspaceService workspace = RegisterWorkspace();
        workspace.State.InitialPrompt = "topic";
        await workspace.SubmitInitialAsync();
        IRenderedComponent<Home> cut = Render<Home>();

        Assert.True(workspace.State.HasDraft);
        Assert.True(cut.Find("#initial-prompt").HasAttribute("disabled"));
        Assert.False(cut.Find("#revision-prompt").HasAttribute("disabled"));

        cut.Find("#revision-prompt").Input("make it shorter");
        cut.WaitForAssertion(() => Assert.False(cut.Find("#revision-prompt").HasAttribute("disabled")));

        cut.Find("button[data-command='go']").Click();
        cut.WaitForAssertion(() => Assert.Contains("revised", workspace.State.Draft));

        Assert.False(cut.Find("#revision-prompt").HasAttribute("disabled"));
        Assert.False(cut.Find("button[data-command='go']").HasAttribute("disabled"));
    }

    [Fact]
    public void Home_KeepsNewListAndQuitEnabledWhileRevisionIsConditional()
    {
        RegisterWorkspace();
        IRenderedComponent<Home> cut = Render<Home>();

        Assert.False(cut.Find("button[data-command='new']").HasAttribute("disabled"));
        Assert.False(cut.Find("button[data-command='list']").HasAttribute("disabled"));
        Assert.False(cut.Find("button[data-command='quit']").HasAttribute("disabled"));
        Assert.Empty(cut.FindAll("button[data-command='revise']"));
        Assert.True(cut.Find("#revision-prompt").HasAttribute("disabled"));
    }

    [Fact]
    public void Home_UsesAccessibleRegionsLabelsAndLiveStatus()
    {
        RegisterWorkspace();
        IRenderedComponent<Home> cut = Render<Home>();

        Assert.Equal("New query", cut.Find("label[for='initial-prompt']").TextContent.Trim());
        Assert.Equal("Revision request", cut.Find("label[for='revision-prompt']").TextContent.Trim());
        Assert.NotNull(cut.Find("nav[aria-label='Workspace commands']"));
        Assert.NotNull(cut.Find("[aria-live='polite']"));
        Assert.NotNull(cut.Find("[aria-labelledby='draft-heading']"));
        Assert.NotNull(cut.Find("[aria-labelledby='review-heading']"));
    }

    [Fact]
    public void Home_PlacesWorkflowLogDirectlyBelowCommandBar()
    {
        RegisterWorkspace();
        IRenderedComponent<Home> cut = Render<Home>();
        string markup = cut.Markup;

        Assert.True(markup.IndexOf("command-bar", StringComparison.Ordinal) <
            markup.IndexOf("workflow-log", StringComparison.Ordinal));
        Assert.DoesNotContain("status-stack", markup);
    }

    [Fact]
    public void Home_PreservesKeyboardOrderFromCommandsToLogToContent()
    {
        RegisterWorkspace();
        IRenderedComponent<Home> cut = Render<Home>();
        string markup = cut.Markup;

        Assert.True(markup.IndexOf("command-bar", StringComparison.Ordinal) <
            markup.IndexOf("workflow-log", StringComparison.Ordinal));
        Assert.Equal("0", cut.Find(".workflow-log").GetAttribute("tabindex"));
    }

    [Fact]
    public void Home_OnlyGoSubmitsPromptText()
    {
        BlogWorkspaceService workspace = RegisterWorkspace();
        IRenderedComponent<Home> cut = Render<Home>();
        var prompt = cut.Find("#initial-prompt");
        prompt.Input("topic");

        Assert.False(prompt.HasAttribute("onkeydown"));
        Assert.Equal(WorkspaceMode.New, workspace.State.Mode);

        cut.Find("button[data-command='go']").Click();
        cut.WaitForAssertion(() => Assert.Equal(WorkspaceMode.Draft, workspace.State.Mode));
    }

    [Fact]
    public void Home_ValidSelectionLoadsSavedSession()
    {
        BlogWorkspaceService workspace = RegisterWorkspace([
            new BlogSessionSummary(Guid.NewGuid().ToString("N"), "first topic", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
        ]);
        IRenderedComponent<Home> cut = Render<Home>();
        cut.Find("button[data-command='list']").Click();
        cut.WaitForElement("#command-session-number").Change("1");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("draft", workspace.State.Draft);
            Assert.False(cut.Find("#revision-prompt").HasAttribute("disabled"));
        });
    }

    [Fact]
    public void Home_DisplaysCompletedDraftAndReviewerNotes()
    {
        BlogWorkspaceService workspace = RegisterWorkspace();
        IRenderedComponent<Home> cut = Render<Home>();
        workspace.State.InitialPrompt = "topic";

        cut.Find("#initial-prompt").Input("topic");
        cut.Find("button[data-command='go']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("draft", cut.Find("[aria-labelledby='draft-heading']").TextContent);
            Assert.Contains("review", cut.Find("[aria-labelledby='review-heading']").TextContent);
        });
    }

    [Fact]
    public void Home_RevisionFieldTracksQueryRatherThanDraft()
    {
        BlogWorkspaceService workspace = RegisterWorkspace();
        workspace.State.InitialPrompt = "topic";
        IRenderedComponent<Home> cut = Render<Home>();

        Assert.False(cut.Find("#revision-prompt").HasAttribute("disabled"));

        workspace.State.Draft = "draft";
        cut.Render();

        Assert.False(cut.Find("#revision-prompt").HasAttribute("disabled"));

        workspace.State.Draft = "  ";
        cut.Render();

        Assert.False(cut.Find("#revision-prompt").HasAttribute("disabled"));

        cut.Find("button[data-command='new']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[role='dialog']")));
        cut.Find("button[data-confirm='discard']").Click();

        Assert.True(cut.Find("#revision-prompt").HasAttribute("disabled"));
    }

    [Fact]
    public void Home_WritingPromptKeepsTextAndLocksWhileRevisingUntilNew()
    {
        BlogWorkspaceService workspace = RegisterWorkspace();
        IRenderedComponent<Home> cut = Render<Home>();
        Assert.False(cut.Find("#initial-prompt").HasAttribute("disabled"));

        cut.Find("#initial-prompt").Input("topic");
        cut.Find("button[data-command='go']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("topic", cut.Find("#initial-prompt").GetAttribute("value"));
            Assert.True(cut.Find("#initial-prompt").HasAttribute("disabled"));
            Assert.False(cut.Find("#revision-prompt").HasAttribute("disabled"));
        });

        cut.Find("button[data-command='new']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.False(cut.Find("#initial-prompt").HasAttribute("disabled"));
            Assert.True(cut.Find("#revision-prompt").HasAttribute("disabled"));
        });
    }

    private BlogWorkspaceService RegisterWorkspace(IReadOnlyList<BlogSessionSummary>? summaries = null)
    {
        var sessions = new StubSessionService { Summaries = summaries ?? [] };
        var workspace = new BlogWorkspaceService(sessions, TimeSpan.FromMilliseconds(10));
        Services.AddSingleton(workspace);
        return workspace;
    }

    private sealed class StubSessionService : IBlogWriterSessionService
    {
        public IReadOnlyList<BlogSessionSummary> Summaries { get; init; } = [];
        public TaskCompletionSource<IReadOnlyList<BlogSessionSummary>>? PendingList { get; init; }
        public TaskCompletionSource<BlogSession>? PendingStart { get; init; }
        public TaskCompletionSource ListStarted { get; } = new();
        public TaskCompletionSource Started { get; } = new();

        public Task<BlogSession> StartAsync(string prompt, int minWords = ResearchState.DefaultMinWords, int maxWords = ResearchState.DefaultMaxWords, CancellationToken cancellationToken = default, IProgress<WorkflowOutputUpdate>? output = null)
        {
            Started.TrySetResult();
            return PendingStart?.Task ?? Task.FromResult(CreateSession(prompt));
        }

        public Task<BlogSession> ReviseAsync(
            BlogSession session,
            string revision,
            int minWords,
            int maxWords,
            CancellationToken cancellationToken = default,
            IProgress<WorkflowOutputUpdate>? output = null) =>
            Task.FromResult(new BlogSession
            {
                Id = session.Id,
                OwnerId = session.OwnerId,
                CreatedAt = session.CreatedAt,
                UpdatedAt = DateTimeOffset.UtcNow,
                State = new ResearchState
                {
                    MainTask = session.State.MainTask,
                    MinWords = minWords,
                    MaxWords = maxWords,
                    Draft = $"revised: {revision}",
                    ReviewNotes = "revision review",
                },
            });

        public Task<IReadOnlyList<BlogSessionSummary>> ListAsync(CancellationToken cancellationToken = default)
        {
            ListStarted.TrySetResult();
            return PendingList?.Task ?? Task.FromResult(Summaries);
        }

        public Task<BlogSession?> LoadAsync(string sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<BlogSession?>(new BlogSession
            {
                Id = sessionId,
                OwnerId = "owner",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                State = new ResearchState { MainTask = "loaded", Draft = "loaded", ReviewNotes = "loaded review" },
            });

        private static BlogSession CreateSession(string prompt) => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerId = "owner",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            State = new ResearchState { MainTask = prompt, Draft = "draft", ReviewNotes = "review" },
        };
    }
}
