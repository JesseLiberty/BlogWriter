namespace BlogWriter.Web.Services;

public sealed class BlogWorkspaceService : IDisposable
{
    private readonly IBlogWriterSessionService _sessions;
    private readonly TimeSpan _cancellationTimeout;
    private CancellationTokenSource? _operationCancellation;
    private Task? _activeOperation;
    private long _operationVersion;

    public BlogWorkspaceService(IBlogWriterSessionService sessions)
        : this(sessions, TimeSpan.FromSeconds(10))
    {
    }

    public BlogWorkspaceService(IBlogWriterSessionService sessions, TimeSpan cancellationTimeout)
    {
        _sessions = sessions;
        _cancellationTimeout = cancellationTimeout;
    }

    public BlogWorkspaceState State { get; } = new();

    public event Action? Changed;

    public Task SubmitAsync()
    {
        // While revising, the writing prompt is locked, so only the revision can be submitted.
        if (State.IsRevisionRequested)
        {
            return string.IsNullOrWhiteSpace(State.RevisionPrompt)
                ? Task.CompletedTask
                : SubmitRevisionAsync();
        }

        if (!string.IsNullOrWhiteSpace(State.InitialPrompt))
        {
            return SubmitInitialAsync();
        }

        return Task.CompletedTask;
    }

    public Task SubmitInitialAsync()
    {
        if (!TryCaptureRange(out WordRange range))
        {
            return Task.CompletedTask;
        }

        string prompt = State.InitialPrompt;
        return RunSessionOperationAsync(
            prompt,
            range,
            (cancellationToken, output) => _sessions.StartAsync(
                prompt,
                range.Min,
                range.Max,
                cancellationToken,
                output));
    }

    public Task SubmitRevisionAsync()
    {
        if (State.ActiveSession is null)
        {
            SetValidation("Create or load a session before submitting a revision.");
            return Task.CompletedTask;
        }

        BlogSession activeSession = State.ActiveSession;
        if (!TryCaptureRange(out WordRange range))
        {
            return Task.CompletedTask;
        }

        string revision = State.RevisionPrompt;
        return RunSessionOperationAsync(
            revision,
            range,
            (cancellationToken, output) => _sessions.ReviseAsync(
                activeSession,
                revision,
                range.Min,
                range.Max,
                cancellationToken,
                output));
    }

    public void UpdateMinWords(string value)
    {
        State.MinWordsInput = value;
        ValidateVisibleRange();
    }

    public void UpdateMaxWords(string value)
    {
        State.MaxWordsInput = value;
        ValidateVisibleRange();
    }

    public async Task<WorkspaceTransitionResult> NewAsync(bool discardConfirmed)
    {
        if (RequiresDiscardConfirmation(discardConfirmed))
        {
            return WorkspaceTransitionResult.RequiresConfirmation;
        }

        await CancelActiveOperationAsync();
        ClearWorkspace(WorkspaceMode.New);
        return WorkspaceTransitionResult.Completed;
    }

    public async Task<WorkspaceTransitionResult> ListAsync(bool discardConfirmed)
    {
        if (RequiresDiscardConfirmation(discardConfirmed))
        {
            return WorkspaceTransitionResult.RequiresConfirmation;
        }

        await CancelActiveOperationAsync();
        long listVersion = ++_operationVersion;
        RestoreAcceptedRange();
        State.InitialPrompt = "";
        State.RevisionPrompt = "";
        State.MarkPromptsSubmitted();
        State.SelectionInput = "";
        State.SelectionError = null;
        State.ValidationMessage = null;
        State.IsListing = true;
        State.StatusMessage = "Loading saved sessions...";
        State.AppendLog(State.StatusMessage, WorkflowOutputOutcome.Progress);
        NotifyChanged();

        try
        {
            IReadOnlyList<BlogSessionSummary> summaries = await _sessions.ListAsync();
            if (listVersion != _operationVersion)
            {
                return WorkspaceTransitionResult.Completed;
            }

            State.DisplayedSessions = summaries.Take(20).ToList();
            State.Mode = WorkspaceMode.List;
            State.StatusMessage = State.DisplayedSessions.Count == 0
                ? "No saved sessions are available."
                : $"{State.DisplayedSessions.Count} saved sessions loaded.";
            State.AppendLog(State.StatusMessage, WorkflowOutputOutcome.Success);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (listVersion != _operationVersion)
            {
                return WorkspaceTransitionResult.Completed;
            }

            State.DisplayedSessions = [];
            State.Mode = WorkspaceMode.Draft;
            State.ValidationMessage = "Unable to load saved sessions. Try again.";
            State.StatusMessage = null;
        }
        finally
        {
            if (listVersion == _operationVersion)
            {
                State.IsListing = false;
            }
        }

        NotifyChanged();
        return WorkspaceTransitionResult.Completed;
    }

    public async Task LoadSelectionAsync()
    {
        if (!SessionListSelection.TryResolve(State.SelectionInput, State.DisplayedSessions, out BlogSessionSummary? summary))
        {
            SetValidation("Enter a number from the current saved-session list.");
            return;
        }

        State.ValidationMessage = null;
        try
        {
            BlogSession? session = await _sessions.LoadAsync(summary!.Id);
            if (session is null)
            {
                SetValidation("That saved session is no longer available. Refresh the list and try again.");
                return;
            }

            Publish(session);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            SetValidation("Unable to load the selected session. Try again.");
        }
    }

    public async Task LaunchSelectionAsync(string input)
    {
        State.SelectionInput = input;
        State.SelectionError = null;

        if (State.IsProcessing)
        {
            SetSelectionError("Wait for the current writing operation to finish.");
            return;
        }

        if (!SessionListSelection.TryResolve(input, State.DisplayedSessions, out BlogSessionSummary? summary))
        {
            SetSelectionError("Enter a valid number from the current saved-session list.");
            return;
        }

        BlogSession? session;
        try
        {
            session = await _sessions.LoadAsync(summary!.Id);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            SetSelectionError("Unable to load the selected session. Try again.");
            return;
        }

        if (session is null)
        {
            SetSelectionError("That saved session is no longer available. Refresh the list and try again.");
            return;
        }

        State.Draft = "";
        State.Review = "";
        State.ReviewerUpdateKeys.Clear();
        State.InitialPrompt = session.State.MainTask;
        State.RevisionPrompt = session.State.CurrentSubTask;
        State.IsRevisionRequested = true;
        State.ActiveSession = null;
        State.SelectionError = null;
        State.ValidationMessage = null;
        NotifyChanged();

        await SubmitInitialAsync();
    }

    public async Task<WorkspaceTransitionResult> QuitAsync(bool discardConfirmed)
    {
        if (RequiresDiscardConfirmation(discardConfirmed))
        {
            return WorkspaceTransitionResult.RequiresConfirmation;
        }

        await CancelActiveOperationAsync();
        ClearWorkspace(WorkspaceMode.Ended);
        State.StatusMessage = "This Blog Writer session has ended.";
        State.AppendLog(State.StatusMessage, WorkflowOutputOutcome.Success);
        NotifyChanged();
        return WorkspaceTransitionResult.Completed;
    }

    public async Task EndForAuthenticationLossAsync()
    {
        await CancelActiveOperationAsync();
        ClearWorkspace(WorkspaceMode.Ended);
        State.ValidationMessage = "Microsoft Entra sign-in is required.";
        NotifyChanged();
    }

    public void Dispose()
    {
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();
    }

    private async Task RunSessionOperationAsync(
        string input,
        WordRange submittedRange,
        Func<CancellationToken, IProgress<WorkflowOutputUpdate>, Task<BlogSession>> operation)
    {
        if (State.Mode == WorkspaceMode.Ended)
        {
            SetValidation("This session has ended. Refresh the page to start again.");
            return;
        }

        if (State.IsProcessing)
        {
            SetValidation("A writing operation is already in progress.");
            return;
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            SetValidation("Enter a prompt before submitting.");
            return;
        }

        long version = ++_operationVersion;
        var cancellation = new CancellationTokenSource();
        _operationCancellation = cancellation;
        State.IsProcessing = true;
        State.ValidationMessage = null;
        State.Draft = "";
        State.StatusMessage = "Writing in progress...";
        State.AppendLog(State.StatusMessage, WorkflowOutputOutcome.Progress);
        NotifyChanged();

        var output = new Progress<WorkflowOutputUpdate>(update => HandleOutput(version, update));
        Task<BlogSession> task = operation(cancellation.Token, output);
        _activeOperation = task;
        try
        {
            BlogSession session = await task;
            if (version != _operationVersion)
            {
                return;
            }

            State.MarkPromptsSubmitted();
            Publish(session, submittedRange);
            State.StatusMessage = "Writing complete.";
            State.AppendLog(State.StatusMessage, WorkflowOutputOutcome.Success);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (version == _operationVersion)
            {
                State.StatusMessage = "Writing cancelled.";
                State.AppendLog(State.StatusMessage, WorkflowOutputOutcome.Cancellation);
            }
        }
        catch (Exception exception)
        {
            if (version == _operationVersion)
            {
                State.ValidationMessage = exception is SessionConflictException
                    ? "This session changed elsewhere. Refresh the list before retrying."
                    : "The writing operation failed. Your previous draft is unchanged.";
                State.AppendLog(
                    State.ValidationMessage,
                    exception is SessionConflictException
                        ? WorkflowOutputOutcome.Conflict
                        : WorkflowOutputOutcome.Failure);
                State.StatusMessage = null;
            }
        }
        finally
        {
            if (version == _operationVersion)
            {
                State.IsProcessing = false;
                _activeOperation = null;
                _operationCancellation = null;
                cancellation.Dispose();
                NotifyChanged();
            }
        }
    }

    private bool RequiresDiscardConfirmation(bool discardConfirmed) =>
        State.HasUnsavedText && !discardConfirmed;

    private async Task CancelActiveOperationAsync()
    {
        Task? activeOperation = _activeOperation;
        CancellationTokenSource? cancellation = _operationCancellation;
        ++_operationVersion;
        _activeOperation = null;
        _operationCancellation = null;
        State.IsProcessing = false;

        if (activeOperation is null || cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        Task completed = await Task.WhenAny(activeOperation, Task.Delay(_cancellationTimeout));
        if (completed == activeOperation)
        {
            try
            {
                await activeOperation;
            }
            catch
            {
            }

            cancellation.Dispose();
            return;
        }

        _ = activeOperation.ContinueWith(
            task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void Publish(BlogSession session, WordRange? submittedRange = null)
    {
        WordRange persistedRange = GetSessionRange(session);
        if (submittedRange is null)
        {
            SetAcceptedAndVisibleRange(persistedRange);
        }
        else
        {
            WordRangeValidation visibleRange = WordRange.Parse(State.MinWordsInput, State.MaxWordsInput);
            bool editedDuringProcessing = !visibleRange.IsValid || visibleRange.Range != submittedRange;
            State.AcceptedRange = submittedRange.Value;
            if (!editedDuringProcessing)
            {
                SetVisibleRange(persistedRange);
            }
            else
            {
                ValidateVisibleRange(notify: false);
            }
        }

        State.ActiveSession = session;
        State.Draft = session.State.Draft;
        if (submittedRange is null)
        {
            State.Review = session.State.ReviewNotes;
            State.ReviewerUpdateKeys.Clear();
            if (!string.IsNullOrWhiteSpace(State.Review))
            {
                State.ReviewerUpdateKeys.Add($"loaded-{session.Id}");
            }
            State.ClearOutput();
        }
        else if (!string.IsNullOrWhiteSpace(session.State.ReviewNotes) &&
                 !State.Review.Contains(session.State.ReviewNotes, StringComparison.Ordinal))
        {
            State.AppendReviewerFeedback(session.State.ReviewNotes, $"final-{session.Id}-{_operationVersion}");
        }
        State.DisplayedSessions = [];
        State.SelectionInput = "";
        State.SelectionError = null;
        State.Mode = WorkspaceMode.Draft;
        State.ValidationMessage = null;
        NotifyChanged();
    }

    private void ClearWorkspace(WorkspaceMode mode)
    {
        State.Mode = mode;
        State.InitialPrompt = "";
        State.RevisionPrompt = "";
        State.MarkPromptsSubmitted();
        State.IsRevisionRequested = false;
        SetAcceptedAndVisibleRange(WordRange.Default);
        State.Draft = "";
        State.Review = "";
        State.ClearOutput();
        State.ActiveSession = null;
        State.DisplayedSessions = [];
        State.SelectionInput = "";
        State.SelectionError = null;
        State.IsListing = false;
        State.IsProcessing = false;
        State.StatusMessage = null;
        State.ValidationMessage = null;
        State.CurrentStatus = null;
        State.CurrentStatusOutcome = null;
        NotifyChanged();
    }

    private void SetValidation(string message)
    {
        State.ValidationMessage = message;
        State.AppendLog(message, WorkflowOutputOutcome.Validation);
        NotifyChanged();
    }

    private void SetSelectionError(string message)
    {
        State.SelectionError = message;
        State.ValidationMessage = message;
        State.AppendLog(message, WorkflowOutputOutcome.Validation);
        NotifyChanged();
    }

    private void HandleOutput(long version, WorkflowOutputUpdate update)
    {
        if (version != _operationVersion || State.Mode == WorkspaceMode.Ended)
        {
            return;
        }

        if (update.Kind == WorkflowOutputKind.ReviewerFeedback)
        {
            State.AppendReviewerFeedback(update.Message, update.UpdateKey);
        }
        else
        {
            State.AppendLog(update.Message, update.Outcome);
        }

        NotifyChanged();
    }

    private bool TryCaptureRange(out WordRange range)
    {
        WordRangeValidation validation = ValidateVisibleRange(notify: false);
        if (validation.Range is not WordRange validRange)
        {
            range = default;
            State.ValidationMessage = "Correct Min and Max before submitting.";
            State.AppendLog(State.ValidationMessage, WorkflowOutputOutcome.Validation);
            NotifyChanged();
            return false;
        }

        range = validRange;
        State.ValidationMessage = null;
        return true;
    }

    private WordRangeValidation ValidateVisibleRange(bool notify = true)
    {
        WordRangeValidation validation = WordRange.Parse(State.MinWordsInput, State.MaxWordsInput);
        State.MinWordsError = validation.MinError;
        State.MaxWordsError = validation.MaxError;
        if (validation.IsValid && State.ValidationMessage == "Correct Min and Max before submitting.")
        {
            State.ValidationMessage = null;
        }

        if (notify)
        {
            NotifyChanged();
        }

        return validation;
    }

    private static WordRange GetSessionRange(BlogSession session)
    {
        WordRangeValidation validation = WordRange.Parse(
            session.State.MinWords.ToString(),
            session.State.MaxWords.ToString());
        return validation.Range ?? WordRange.Default;
    }

    private void RestoreAcceptedRange() => SetVisibleRange(State.AcceptedRange);

    private void SetAcceptedAndVisibleRange(WordRange range)
    {
        State.AcceptedRange = range;
        SetVisibleRange(range);
    }

    private void SetVisibleRange(WordRange range)
    {
        State.MinWordsInput = range.Min.ToString();
        State.MaxWordsInput = range.Max.ToString();
        State.MinWordsError = null;
        State.MaxWordsError = null;
    }

    private void NotifyChanged() => Changed?.Invoke();
}
