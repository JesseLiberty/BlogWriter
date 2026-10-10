namespace BlogWriter.Web.Services;

public sealed record WorkflowLogEntry(string Message, WorkflowOutputOutcome Outcome);

public enum WorkspaceMode
{
    New,
    Draft,
    List,
    Ended,
}

public enum WorkspaceTransitionResult
{
    Completed,
    RequiresConfirmation,
}

public sealed class BlogWorkspaceState
{
    public WorkspaceMode Mode { get; internal set; } = WorkspaceMode.New;
    public string InitialPrompt { get; set; } = "";
    public string RevisionPrompt { get; set; } = "";
    public string MinWordsInput { get; internal set; } = ResearchState.DefaultMinWords.ToString();
    public string MaxWordsInput { get; internal set; } = ResearchState.DefaultMaxWords.ToString();
    public WordRange AcceptedRange { get; internal set; } = WordRange.Default;
    public string? MinWordsError { get; internal set; }
    public string? MaxWordsError { get; internal set; }
    public string Draft
    {
        get => _draft;
        set
        {
            _draft = value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                IsRevisionRequested = true;
            }
        }
    }

    private string _draft = "";
    public string Review { get; set; } = "";
    public BlogSession? ActiveSession { get; internal set; }
    public IReadOnlyList<BlogSessionSummary> DisplayedSessions { get; internal set; } = [];
    public bool IsProcessing { get; internal set; }
    public bool IsListing { get; internal set; }
    public bool IsSelecting { get; internal set; }
    internal bool IsRestoredPending { get; set; }
    public string? StatusMessage { get; internal set; }
    public string? ValidationMessage { get; internal set; }
    public IReadOnlyList<WorkflowLogEntry> WorkflowLog { get; internal set; } = [];
    public string? CurrentStatus { get; internal set; }
    public WorkflowOutputOutcome? CurrentStatusOutcome { get; internal set; }

    internal HashSet<string> ReviewerUpdateKeys { get; } = new(StringComparer.Ordinal);

    internal void AppendLog(string message, WorkflowOutputOutcome outcome)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        WorkflowLog = [.. WorkflowLog, new WorkflowLogEntry(message, outcome)];
        CurrentStatus = message;
        CurrentStatusOutcome = outcome;
    }

    internal void ClearOutput()
    {
        WorkflowLog = [];
        ReviewerUpdateKeys.Clear();
    }

    internal void AppendReviewerFeedback(string message, string updateKey)
    {
        if (string.IsNullOrWhiteSpace(message) || !ReviewerUpdateKeys.Add(updateKey))
        {
            return;
        }

        if (string.Equals(Review, message, StringComparison.Ordinal) ||
            Review.EndsWith($"\n\n{message}", StringComparison.Ordinal))
        {
            return;
        }

        Review = string.IsNullOrWhiteSpace(Review)
            ? message
            : $"{Review}\n\n{message}";
    }

    public bool IsSessionSelectionEnabled => Mode == WorkspaceMode.List && !IsBusy;
    public bool HasDraft => !string.IsNullOrWhiteSpace(Draft);
    public bool IsWordRangeEnabled => !IsBusy && (Mode is WorkspaceMode.New or WorkspaceMode.Draft);

    public bool IsNewCommandEnabled => !IsProcessing && !IsSelecting && Mode != WorkspaceMode.Ended;
    public bool IsListCommandEnabled => !IsBusy && (Mode is WorkspaceMode.New or WorkspaceMode.Draft);
    public bool IsGoCommandEnabled => !IsBusy && (Mode is WorkspaceMode.New or WorkspaceMode.Draft);
    public bool IsQuitCommandEnabled => !IsBusy && (Mode is WorkspaceMode.New or WorkspaceMode.Draft);
    public bool IsHelpCommandEnabled => !IsBusy && (Mode is WorkspaceMode.New or WorkspaceMode.Draft);

    /// <summary>
    /// Latched once a draft exists. A restored saved session remains an initial
    /// prompt until its first new run completes.
    /// </summary>
    public bool IsRevisionRequested { get; internal set; }

    public bool IsRevisionInputEnabled =>
        !IsBusy &&
        Mode != WorkspaceMode.List &&
        Mode != WorkspaceMode.Ended &&
        !string.IsNullOrWhiteSpace(InitialPrompt);

    /// <summary>
    /// The writing prompt is locked while revising; New unlocks it again.
    /// </summary>
    public bool IsInitialPromptEnabled =>
        !IsBusy &&
        !IsRevisionRequested &&
        (Mode == WorkspaceMode.New || IsRestoredPending);

    private bool IsBusy => IsProcessing || IsListing || IsSelecting;

    /// <summary>
    /// Min and Max are locked after Go until the draft is populated (or the operation ends).
    /// </summary>
    public bool IsWordRangeEnabled => !IsProcessing;

    /// <summary>
    /// Prompt text as last accepted by a completed writing operation. Prompts keep their
    /// text after Go, so only text that differs from these counts as unsaved.
    /// </summary>
    internal string SubmittedInitialPrompt { get; set; } = "";
    internal string SubmittedRevisionPrompt { get; set; } = "";

    public bool HasUnsavedRange
    {
        get
        {
            WordRangeValidation validation = WordRange.Parse(MinWordsInput, MaxWordsInput);
            return !validation.IsValid || validation.Range != AcceptedRange;
        }
    }

    public bool HasUnsavedText =>
        IsUnsaved(InitialPrompt, SubmittedInitialPrompt) ||
        IsUnsaved(RevisionPrompt, SubmittedRevisionPrompt) ||
        HasUnsavedRange;

    internal void MarkPromptsSubmitted()
    {
        SubmittedInitialPrompt = InitialPrompt;
        SubmittedRevisionPrompt = RevisionPrompt;
    }

    private static bool IsUnsaved(string prompt, string submitted) =>
        !string.IsNullOrWhiteSpace(prompt) && !string.Equals(prompt, submitted, StringComparison.Ordinal);
}
