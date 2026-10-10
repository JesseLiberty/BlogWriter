using Bunit;
using BlogWriter.Web.Components;
using BlogWriter.Web.Components.Pages;
using BlogWriter.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BlogWriter.Web.Tests;

public sealed class WorkspaceBrowserTests : BunitContext
{
    [Fact]
    public void ListButton_ShowsTenSavedSessionsAndRefreshesOnlyChangedDetails()
    {
        var sessions = new SavedListSessionService();
        DateTimeOffset now = DateTimeOffset.Parse("2026-10-10T12:00:00Z");
        for (int index = 0; index < 10; index++)
        {
            string draft = index switch
            {
                0 => "",
                1 => string.Join("\t", Enumerable.Range(1, 49).Select(number => $"word{number}")),
                2 => string.Join("\n", Enumerable.Range(1, 50).Select(number => $"word{number}")),
                3 => string.Join(" ", Enumerable.Range(1, 51).Select(number => $"word{number}")),
                _ => $"saved draft {index} <b>literal</b>",
            };
            BlogSession session = ListLauncherTestHelpers.Session($"saved topic {index}", draft: draft);
            session.State.MinWords = 600 + index;
            session.State.MaxWords = 1200 + index;
            session.UpdatedAt = now.AddMinutes(-index);
            sessions.Sessions.Add(session);
        }
        string original = System.Text.Json.JsonSerializer.Serialize(sessions.Sessions);
        var workspace = new BlogWorkspaceService(sessions, TimeSpan.FromMilliseconds(25));
        Services.AddSingleton(workspace);
        var cut = Render<Home>();

        cut.Find("button[data-command='list']").Click();

        cut.WaitForAssertion(() => AssertSavedEntries(cut, sessions));
        Assert.Equal(1, sessions.ListCalls);
        Assert.Equal(original, System.Text.Json.JsonSerializer.Serialize(sessions.Sessions));
        string unchanged = System.Text.Json.JsonSerializer.Serialize(sessions.Sessions.Skip(1));
        sessions.Sessions[0].State.Draft = "newly saved opening words";
        sessions.Sessions[0].State.MinWords = 800;
        sessions.Sessions[0].State.MaxWords = 1400;
        sessions.Sessions[0].UpdatedAt = now.AddMinutes(1);

        cut.Find("button[data-command='list']").Click();

        cut.WaitForAssertion(() => AssertSavedEntries(cut, sessions));
        Assert.Equal(2, sessions.ListCalls);
        Assert.Equal(unchanged, System.Text.Json.JsonSerializer.Serialize(sessions.Sessions.Skip(1)));
        Assert.Empty(sessions.LoadedIds);
        Assert.Equal(0, sessions.StartCalls);
        Assert.Equal(0, sessions.RevisionCalls);
    }

    private static void AssertSavedEntries(IRenderedComponent<Home> cut, SavedListSessionService sessions)
    {
        var entries = cut.FindAll("button.session-entry");
        Assert.Equal(10, entries.Count);
        for (int index = 0; index < entries.Count; index++)
        {
            BlogSession saved = sessions.Sessions[index];
            string[] words = saved.State.Draft.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            string expectedPreview = words.Length == 0 ? "No draft yet" : string.Join(" ", words.Take(50));
            if (words.Length > 50) expectedPreview += "...";
            Assert.Equal($"[{index + 1}]", entries[index].QuerySelector(".session-number")!.TextContent);
            Assert.Equal(saved.State.MainTask, entries[index].QuerySelector("strong")!.TextContent);
            Assert.Equal("Updated " + saved.UpdatedAt.ToString("u"), entries[index].QuerySelector("time")!.TextContent);
            var bounds = entries[index].QuerySelectorAll(".session-word-limits > span");
            Assert.Equal("Min words: " + saved.State.MinWords, bounds[0].TextContent);
            Assert.Equal("Max words: " + saved.State.MaxWords, bounds[1].TextContent);
            Assert.Equal(expectedPreview, entries[index].QuerySelector(".session-preview-text")!.TextContent);
        }
        Assert.Empty(cut.FindAll("b"));
    }

    [Fact]
    public void SessionEntries_ShowEachSavedDetailAndLiteralPreview()
    {
        DateTimeOffset updated = DateTimeOffset.Parse("2026-10-02T12:00:00Z");
        BlogSessionSummary[] summaries = Enumerable.Range(1, 10).Select(number =>
            BlogSessionSummary.Create($"id{number}", $"topic {number}", updated, updated,
                100 + number, 200 + number, $"draft {number}")).ToArray();
        summaries[0] = summaries[0] with { DraftPreview = "<script>bad()</script>", IsDraftTruncated = true };
        summaries[1] = summaries[1] with { DraftPreview = "" };
        var cut = Render<SessionList>(parameters => parameters.Add(component => component.Sessions, summaries));

        var entries = cut.FindAll("button.session-entry");
        Assert.Equal(10, entries.Count);
        for (int index = 0; index < entries.Count; index++)
        {
            Assert.Equal($"[{index + 1}]", entries[index].QuerySelector(".session-number")!.TextContent);
            Assert.Equal(summaries[index].MainTask, entries[index].QuerySelector("strong")!.TextContent);
            Assert.Contains("Updated " + updated.ToString("u"), entries[index].TextContent);
            Assert.Contains("Min words: " + summaries[index].MinWords, entries[index].TextContent);
            Assert.Contains("Max words: " + summaries[index].MaxWords, entries[index].TextContent);
            Assert.Contains("Draft preview", entries[index].TextContent);
        }
        Assert.Empty(cut.FindAll("script"));
        Assert.Contains("&lt;script&gt;", cut.Markup);
        Assert.Contains("<script>bad()</script>...", entries[0].TextContent);
        Assert.Contains("No draft yet", entries[1].TextContent);
        Assert.DoesNotContain("...", entries[2].TextContent);
    }

    [Fact]
    public void SessionEntries_PreserveIdentifyingNameAndSelectionOfDuplicateTasks()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        BlogSessionSummary[] summaries =
        [
            BlogSessionSummary.Create("first", "same topic", now, now, 700, 1350, "first draft"),
            BlogSessionSummary.Create("second", "same topic", now, now, 400, 400, "second draft"),
        ];
        BlogSessionSummary? selected = null;
        var cut = Render<SessionList>(parameters => parameters
            .Add(component => component.Sessions, summaries)
            .Add(component => component.Selected, (BlogSessionSummary summary) => { selected = summary; }));

        cut.FindAll("button.session-entry")[1].Click();

        Assert.Same(summaries[1], selected);
        Assert.Equal("Restore saved session 2: same topic", cut.FindAll("button")[1].GetAttribute("aria-label"));
        var disabled = Render<SessionList>(parameters => parameters
            .Add(component => component.Sessions, summaries)
            .Add(component => component.SelectionEnabled, false));
        Assert.All(disabled.FindAll("button"), button => Assert.True(button.HasAttribute("disabled")));
        Assert.Equal("first draft", summaries[0].DraftPreview);
    }

    [Theory]
    [InlineData(390, 844)]
    [InlineData(1440, 900)]
    public void RequiredViewports_AreExplicitlyCovered(int width, int height)
    {
        Assert.Contains((width, height), new[] { (390, 844), (1440, 900) });
    }

    [Fact]
    public void OutputLayoutContract_CapsLogAndProtectsCompactSelector()
    {
        string css = FindRepositoryFile("BlogWriter.Web", "wwwroot", "app.css");

        Assert.Contains("button.session-entry", css);
        Assert.Contains(":focus-visible", css);
        Assert.DoesNotContain(".list-command input", css);
        Assert.Contains("max-height: 4.8rem", css);
        Assert.Contains("overflow: auto", css);
        Assert.Contains("grid-template-columns: repeat(6, minmax(0, 1fr))", css);
        Assert.Contains("button[data-command=\"stop\"]:not(:disabled)", css);
        Assert.Contains("background: #c00;", css);
        Assert.Contains("color: yellow;", css);
    }

    [Fact]
    public void ListSelectionFixture_RestoresSavedSessionWithoutLaunchingIt()
    {
        var sessions = new BrowserSessionService();
        var workspace = new BlogWorkspaceService(sessions, TimeSpan.FromMilliseconds(25));
        Services.AddSingleton(workspace);
        IRenderedComponent<Home> cut = Render<Home>();

        cut.Find("button[data-command='list']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("#command-session-number"));
            Assert.Single(cut.FindAll("button.session-entry"));
        });
        cut.Find("button.session-entry").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, sessions.LoadCalls);
            Assert.Equal(0, sessions.StartCalls);
            Assert.Equal(0, sessions.RevisionCalls);
            Assert.Equal("saved topic", cut.Find("#initial-prompt").GetAttribute("value"));
            Assert.Equal("tighten the ending", cut.Find("#revision-prompt").GetAttribute("value"));
            Assert.Equal("700", cut.Find("#min-words").GetAttribute("value"));
            Assert.Equal("1350", cut.Find("#max-words").GetAttribute("value"));
            Assert.False(cut.Find("#initial-prompt").HasAttribute("disabled"));
            Assert.False(cut.Find("#revision-prompt").HasAttribute("disabled"));
            Assert.False(cut.Find("#min-words").HasAttribute("disabled"));
            Assert.False(cut.Find("#max-words").HasAttribute("disabled"));
            Assert.Empty(workspace.State.Draft);
            Assert.Empty(workspace.State.Review);
            Assert.Null(workspace.State.ActiveSession);
        });
    }

    [Fact]
    public void GoAfterRestoringSession_CreatesNewRunFromEditedValues()
    {
        var sessions = new BrowserSessionService();
        Services.AddSingleton(new BlogWorkspaceService(sessions, TimeSpan.FromMilliseconds(25)));
        IRenderedComponent<Home> cut = Render<Home>();

        cut.Find("button[data-command='list']").Click();
        cut.Find("button.session-entry").Click();
        cut.Find("#initial-prompt").Input("edited topic");
        cut.Find("#min-words").Input("800");
        cut.Find("#max-words").Input("1400");
        cut.Find("button[data-command='go']").Click();

        cut.WaitForAssertion(() => Assert.Equal(1, sessions.StartCalls));

        Assert.Equal("edited topic", sessions.LastPrompt);
        Assert.Equal(new WordRange(800, 1400), sessions.LastStartRange);
        Assert.Equal(0, sessions.RevisionCalls);
        Assert.NotNull(sessions.CreatedSession);
        Assert.NotEqual(sessions.SourceSession.Id, sessions.CreatedSession.Id);
        Assert.Equal("saved topic", sessions.SourceSession.State.MainTask);
        Assert.Equal("saved draft", sessions.SourceSession.State.Draft);
        Assert.Equal("saved review", sessions.SourceSession.State.ReviewNotes);
    }

    [Fact]
    public async Task StopButton_TracksEligibleGoStagesAndLocksUntilOperationEnds()
    {
        var sessions = new BrowserSessionService
        {
            PendingStart = new TaskCompletionSource<BlogSession>(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        var workspace = new BlogWorkspaceService(sessions, TimeSpan.FromMilliseconds(50));
        workspace.State.InitialPrompt = "topic";
        Services.AddSingleton(workspace);
        IRenderedComponent<Home> cut = Render<Home>();

        Assert.True(cut.Find("button[data-command='stop']").HasAttribute("disabled"));

        cut.Find("button[data-command='go']").Click();
        await sessions.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(cut.Find("button[data-command='stop']").HasAttribute("disabled"));

        sessions.ReportStage(WorkflowAgentStage.Blogger);
        cut.WaitForAssertion(() => Assert.True(cut.Find("button[data-command='stop']").HasAttribute("disabled")));

        foreach (WorkflowAgentStage stage in new[]
                 {
                     WorkflowAgentStage.Researcher,
                     WorkflowAgentStage.Author,
                     WorkflowAgentStage.Reviewer,
                 })
        {
            sessions.ReportStage(stage);
            cut.WaitForAssertion(() => Assert.False(cut.Find("button[data-command='stop']").HasAttribute("disabled")));
        }

        sessions.ReportStage(WorkflowAgentStage.None);
        cut.WaitForAssertion(() => Assert.True(cut.Find("button[data-command='stop']").HasAttribute("disabled")));
        sessions.ReportStage(WorkflowAgentStage.Author);
        cut.WaitForAssertion(() => Assert.False(cut.Find("button[data-command='stop']").HasAttribute("disabled")));

        cut.Find("button[data-command='stop']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Equal("stopping", workspace.State.CurrentStatus);
            AssertAllCommandsDisabled(cut);
        });
        cut.WaitForAssertion(
            () => Assert.Equal("Cancellation failed", workspace.State.CurrentStatus),
            TimeSpan.FromSeconds(2));
        AssertAllCommandsDisabled(cut);
        Assert.Equal("topic", workspace.State.InitialPrompt);

        sessions.PendingStart.SetResult(ListLauncherTestHelpers.Session("topic", "", "late draft", "late review"));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(WorkspaceMode.New, workspace.State.Mode);
            Assert.Empty(workspace.State.Draft);
            Assert.Empty(workspace.State.Review);
            Assert.Null(workspace.State.CurrentStatus);
            Assert.True(cut.Find("button[data-command='stop']").HasAttribute("disabled"));
        });
    }

    [Fact]
    public async Task StopButton_CancelsActiveRevision()
    {
        var sessions = new BrowserSessionService();
        var workspace = new BlogWorkspaceService(sessions, TimeSpan.FromSeconds(1));
        Services.AddSingleton(workspace);
        IRenderedComponent<Home> cut = Render<Home>();

        cut.Find("#initial-prompt").Input("topic");
        cut.Find("button[data-command='go']").Click();
        cut.WaitForAssertion(() => Assert.Equal(1, sessions.StartCalls));
        cut.Find("#revision-prompt").Input("tighten the ending");
        sessions.PendingRevision = new TaskCompletionSource<BlogSession>(TaskCreationOptions.RunContinuationsAsynchronously);

        cut.Find("button[data-command='go']").Click();
        await sessions.RevisionStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        sessions.ReportStage(WorkflowAgentStage.Reviewer);
        cut.WaitForAssertion(() => Assert.False(cut.Find("button[data-command='stop']").HasAttribute("disabled")));

        cut.Find("button[data-command='stop']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Equal("stopping", workspace.State.CurrentStatus);
            AssertAllCommandsDisabled(cut);
        });
        Assert.True(sessions.LastCancellationToken.IsCancellationRequested);

        sessions.PendingRevision.SetResult(ListLauncherTestHelpers.Session("topic", "tighten the ending", "late revision", "late review"));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(WorkspaceMode.New, workspace.State.Mode);
            Assert.Empty(workspace.State.Draft);
            Assert.Empty(workspace.State.Review);
        });
    }

    private static void AssertAllCommandsDisabled(IRenderedComponent<Home> cut)
    {
        foreach (string command in new[] { "new", "list", "go", "stop", "quit", "help" })
        {
            Assert.True(cut.Find($"button[data-command='{command}']").HasAttribute("disabled"));
        }
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string path = Path.Combine([directory.FullName, .. segments]);
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file.", Path.Combine(segments));
    }

    private sealed class BrowserSessionService : IBlogWriterSessionService
    {
        private readonly BlogSession _sourceSession = CreateSourceSession();

        public int StartCalls { get; private set; }
        public int RevisionCalls { get; private set; }
        public int LoadCalls { get; private set; }
        public string? LastPrompt { get; private set; }
        public WordRange? LastStartRange { get; private set; }
        public BlogSession? CreatedSession { get; private set; }
        public BlogSession SourceSession => _sourceSession;
        public TaskCompletionSource<BlogSession>? PendingStart { get; set; }
        public TaskCompletionSource<BlogSession>? PendingRevision { get; set; }
        public TaskCompletionSource Started { get; } = new();
        public TaskCompletionSource RevisionStarted { get; } = new();
        public CancellationToken LastCancellationToken { get; private set; }
        public IProgress<WorkflowOutputUpdate>? LastOutput { get; private set; }
        private long _stageSequence;

        public void ReportStage(WorkflowAgentStage stage)
        {
            LastOutput?.Report(WorkflowOutputUpdate.Create(
                WorkflowOutputKind.Lifecycle,
                WorkflowOutputOutcome.Progress,
                $"{stage} started.",
                operationVersion: 1,
                sequence: ++_stageSequence,
                updateKey: $"browser-stage-{_stageSequence}",
                agentStage: stage));
        }

        public Task<BlogSession> StartAsync(string prompt, int minWords = ResearchState.DefaultMinWords, int maxWords = ResearchState.DefaultMaxWords, CancellationToken cancellationToken = default, IProgress<WorkflowOutputUpdate>? output = null)
        {
            StartCalls++;
            LastCancellationToken = cancellationToken;
            LastOutput = output;
            Started.TrySetResult();
            LastPrompt = prompt;
            LastStartRange = new WordRange(minWords, maxWords);
            CreatedSession = ListLauncherTestHelpers.Session(prompt, "", "new draft", "new review");
            CreatedSession.State.MinWords = minWords;
            CreatedSession.State.MaxWords = maxWords;
            return PendingStart?.Task ?? Task.FromResult(CreatedSession);
        }

        public Task<BlogSession> ReviseAsync(BlogSession session, string revision, int minWords, int maxWords, CancellationToken cancellationToken = default, IProgress<WorkflowOutputUpdate>? output = null)
        {
            RevisionCalls++;
            LastCancellationToken = cancellationToken;
            LastOutput = output;
            RevisionStarted.TrySetResult();
            return PendingRevision?.Task ?? Task.FromResult(session);
        }

        public Task<IReadOnlyList<BlogSessionSummary>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BlogSessionSummary>>([
                new(_sourceSession.Id, _sourceSession.State.MainTask, _sourceSession.CreatedAt, _sourceSession.UpdatedAt)]);

        public Task<BlogSession?> LoadAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            LoadCalls++;
            return Task.FromResult<BlogSession?>(_sourceSession.Id == sessionId ? _sourceSession : null);
        }

        private static BlogSession CreateSourceSession()
        {
            BlogSession session = ListLauncherTestHelpers.Session(
                "saved topic",
                "tighten the ending",
                "saved draft",
                "saved review");
            session.State.MinWords = 700;
            session.State.MaxWords = 1350;
            return session;
        }
    }

}