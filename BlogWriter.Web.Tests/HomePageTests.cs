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
    public void Home_UsesRequestedHeadingAndUnnumberedDraft()
    {
        RegisterWorkspace();
        var cut = Render<Home>();
        Assert.Equal("Research. Write. Revise.", cut.Find("h1").TextContent);
        Assert.DoesNotContain("A researcher and author for blog pages", cut.Markup);
        Assert.Empty(cut.FindAll(".draft-pane .pane-index"));
        Assert.Equal("Draft", cut.Find("#draft-heading").TextContent);
    }

    [Fact]
    public void Home_RendersWritingWorkspaceAndSixCommands()
    {
        BlogWorkspaceService workspace = RegisterWorkspace();

        IRenderedComponent<Home> cut = Render<Home>();

        Assert.NotNull(cut.Find("#initial-prompt"));
        Assert.Empty(cut.FindAll("#revision-prompt"));
        Assert.NotNull(cut.Find("[aria-labelledby='draft-heading']"));
        Assert.Empty(cut.FindAll("[aria-labelledby='review-heading']"));
        Assert.Equal(["New", "List", "Go", "Stop", "Quit", "?"],
            cut.FindAll(".command-bar button").Select(button => button.TextContent.Trim()).ToArray());
        Assert.Single(cut.FindAll("textarea"));
        Assert.False(workspace.State.IsSessionSelectionEnabled);
    }

    [Fact]
    public void Home_PlacesDefaultWordRangeBesideQuery()
    {
        RegisterWorkspace();
        IRenderedComponent<Home> cut = Render<Home>();

        Assert.NotNull(cut.Find(".prompt-strip .word-range-row"));
        Assert.Empty(cut.FindAll(".command-bar .command-range"));
        Assert.Equal("1000", cut.Find("#min-words").GetAttribute("value"));
        Assert.Equal("2000", cut.Find("#max-words").GetAttribute("value"));
        Assert.NotNull(cut.Find("button[data-command='go']"));
    }

    [Fact]
    public void Home_ListShowsAccessibleSavedRowsWithoutNumericInput()
    {
        BlogWorkspaceService workspace = RegisterWorkspace([
            new BlogSessionSummary(Guid.NewGuid().ToString("N"), "first topic", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
        ]);
        IRenderedComponent<Home> cut = Render<Home>();

        cut.Find("button[data-command='list']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("#command-session-number"));
            Assert.Single(cut.FindAll(".session-list button.session-entry"));
            Assert.Empty(cut.FindAll("#revision-prompt"));
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
    public void Home_NewResetsEditedWordRangeToDefaults()
    {
        BlogWorkspaceService workspace = RegisterWorkspace();
        IRenderedComponent<Home> cut = Render<Home>();

        cut.Find("#min-words").Input("700");
        cut.Find("#max-words").Input("900");
        cut.Find("button[data-command='new']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[role='dialog']")));
        cut.Find("button[data-confirm='discard']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("1000", cut.Find("#min-words").GetAttribute("value"));
            Assert.Equal("2000", cut.Find("#max-words").GetAttribute("value"));
            Assert.Equal(WordRange.Default, workspace.State.AcceptedRange);
        });
    }

    [Fact]
    public void Home_NewResetsSubmittedWordRangeToDefaults()
    {
        BlogWorkspaceService workspace = RegisterWorkspace();
        IRenderedComponent<Home> cut = Render<Home>();

        cut.Find("#initial-prompt").Input("topic");
        cut.Find("#min-words").Input("600");
        cut.Find("#max-words").Input("850");
        cut.Find("button[data-command='go']").Click();
        cut.WaitForAssertion(() => Assert.Equal(new WordRange(600, 850), workspace.State.AcceptedRange));

        cut.Find("button[data-command='new']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("1000", cut.Find("#min-words").GetAttribute("value"));
            Assert.Equal("2000", cut.Find("#max-words").GetAttribute("value"));
            Assert.Equal(WordRange.Default, workspace.State.AcceptedRange);
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
            Assert.Empty(cut.FindAll("#revision-prompt"));
            Assert.False(cut.Find("#min-words").HasAttribute("disabled"));
            Assert.False(cut.Find("#max-words").HasAttribute("disabled"));
            Assert.All(
                cut.FindAll(".command-bar button").Where(button => button.GetAttribute("data-command") != "stop"),
                button => Assert.False(button.HasAttribute("disabled")));
            Assert.True(cut.Find("button[data-command='stop']").HasAttribute("disabled"));
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
        Assert.Empty(cut.FindAll("#revision-prompt"));
    }

    [Fact]
    public void Home_RevisionWindowRemainsAbsentWhileEditingQuery()
    {
        BlogWorkspaceService workspace = RegisterWorkspace();
        IRenderedComponent<Home> cut = Render<Home>();
        Assert.Empty(cut.FindAll("#revision-prompt"));

        cut.Find("#initial-prompt").Input("topic");

        Assert.Empty(cut.FindAll("#revision-prompt"));
        Assert.False(workspace.State.HasDraft);

        cut.Find("#initial-prompt").Input("  ");

        Assert.Empty(cut.FindAll("#revision-prompt"));
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
        Assert.Empty(cut.FindAll("#revision-prompt"));
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
    public async Task Home_ListLocksWorkspaceButLeavesNewAndSavedRowsUsable()
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
        Assert.Empty(cut.FindAll("#revision-prompt"));
        Assert.True(cut.Find("#min-words").HasAttribute("disabled"));
        Assert.True(cut.Find("#max-words").HasAttribute("disabled"));
        Assert.True(cut.Find("button[data-command='new']").HasAttribute("disabled") is false);
        Assert.True(cut.Find("button[data-command='list']").HasAttribute("disabled"));
        Assert.True(cut.Find("button[data-command='go']").HasAttribute("disabled"));
        Assert.True(cut.Find("button[data-command='stop']").HasAttribute("disabled"));
        Assert.True(cut.Find("button[data-command='quit']").HasAttribute("disabled"));
        Assert.True(cut.Find("button[data-command='help']").HasAttribute("disabled"));

        sessions.PendingList.SetResult(sessions.Summaries);
        await click;

        Assert.False(cut.Find("button.session-entry").HasAttribute("disabled"));
        Assert.True(cut.Find("button[data-command='new']").HasAttribute("disabled") is false);
    }

    [Fact]
    public async Task Home_PopulatedDraftDoesNotOfferOrSubmitRevision()
    {
        BlogWorkspaceService workspace = RegisterWorkspace();
        workspace.State.InitialPrompt = "topic";
        await workspace.SubmitInitialAsync();
        IRenderedComponent<Home> cut = Render<Home>();

        Assert.True(workspace.State.HasDraft);
        Assert.True(cut.Find("#initial-prompt").HasAttribute("disabled"));
        Assert.Empty(cut.FindAll("#revision-prompt"));
        workspace.State.RevisionPrompt = "make it shorter";
        string draft = workspace.State.Draft;

        cut.Find("button[data-command='go']").Click();
        cut.WaitForAssertion(() => Assert.Equal(draft, workspace.State.Draft));

        Assert.Empty(cut.FindAll("#revision-prompt"));
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
        Assert.Empty(cut.FindAll("#revision-prompt"));
    }

    [Fact]
    public void Home_UsesAccessibleRegionsLabelsAndLiveStatus()
    {
        RegisterWorkspace();
        IRenderedComponent<Home> cut = Render<Home>();

        Assert.Equal("New query", cut.Find("label[for='initial-prompt']").TextContent.Trim());
        Assert.Empty(cut.FindAll("label[for='revision-prompt']"));
        Assert.NotNull(cut.Find("nav[aria-label='Workspace commands']"));
        Assert.NotNull(cut.Find("[aria-live='polite']"));
        Assert.NotNull(cut.Find("[aria-labelledby='draft-heading']"));
        Assert.Empty(cut.FindAll("[aria-labelledby='review-heading']"));
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
    public void Home_SavedRowRestoresSessionWithoutRunningIt()
    {
        BlogWorkspaceService workspace = RegisterWorkspace([
            new BlogSessionSummary(Guid.NewGuid().ToString("N"), "first topic", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
        ]);
        IRenderedComponent<Home> cut = Render<Home>();
        cut.Find("button[data-command='list']").Click();
        cut.Find("button.session-entry").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("loaded", workspace.State.InitialPrompt);
            Assert.Empty(workspace.State.Draft);
            Assert.Empty(workspace.State.Review);
            Assert.Null(workspace.State.ActiveSession);
            Assert.False(cut.Find("#initial-prompt").HasAttribute("disabled"));
            Assert.Empty(cut.FindAll("#revision-prompt"));
            Assert.Empty(cut.FindAll("#command-session-number"));
        });
    }

    [Fact]
    public void Home_DisplaysDraftAndRetainsReviewerNotesInternally()
    {
        BlogWorkspaceService workspace = RegisterWorkspace();
        IRenderedComponent<Home> cut = Render<Home>();
        workspace.State.InitialPrompt = "topic";

        cut.Find("#initial-prompt").Input("topic");
        cut.Find("button[data-command='go']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("draft", cut.Find("[aria-labelledby='draft-heading']").TextContent);
            Assert.Empty(cut.FindAll("[aria-labelledby='review-heading']"));
            Assert.Contains("review", workspace.State.Review);
        });
    }

    [Fact]
    public void Home_RevisionFieldTracksQueryRatherThanDraft()
    {
        BlogWorkspaceService workspace = RegisterWorkspace();
        workspace.State.InitialPrompt = "topic";
        IRenderedComponent<Home> cut = Render<Home>();

        Assert.Empty(cut.FindAll("#revision-prompt"));

        workspace.State.Draft = "draft";
        cut.Render();

        Assert.Empty(cut.FindAll("#revision-prompt"));

        workspace.State.Draft = "  ";
        cut.Render();

        Assert.Empty(cut.FindAll("#revision-prompt"));

        cut.Find("button[data-command='new']").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[role='dialog']")));
        cut.Find("button[data-confirm='discard']").Click();

        Assert.Empty(cut.FindAll("#revision-prompt"));
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
            Assert.Empty(cut.FindAll("#revision-prompt"));
        });

        cut.Find("button[data-command='new']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.False(cut.Find("#initial-prompt").HasAttribute("disabled"));
            Assert.Empty(cut.FindAll("#revision-prompt"));
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
            return PendingStart?.Task ?? Task.FromResult(CreateSession(prompt, minWords, maxWords));
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

        private static BlogSession CreateSession(string prompt, int minWords, int maxWords) => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerId = "owner",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            State = new ResearchState
            {
                MainTask = prompt,
                Draft = "draft",
                ReviewNotes = "review",
                MinWords = minWords,
                MaxWords = maxWords,
            },
        };
    }
}
