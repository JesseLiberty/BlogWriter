namespace BlogWriter;

internal sealed class WorkflowOutputPublisher
{
    private readonly IProgress<WorkflowOutputUpdate>? _output;
    private readonly long _operationVersion;
    private long _sequence;

    public WorkflowOutputPublisher(IProgress<WorkflowOutputUpdate>? output, long operationVersion)
    {
        _output = output;
        _operationVersion = operationVersion;
    }

    public void PublishLifecycle(
        WorkflowOutputOutcome outcome,
        string message,
        WorkflowAgentStage agentStage = WorkflowAgentStage.None)
    {
        Publish(WorkflowOutputKind.Lifecycle, outcome, message, revisionNumber: null, agentStage);
    }

    public void PublishReviewer(string message, int revisionNumber)
    {
        Publish(WorkflowOutputKind.ReviewerFeedback, WorkflowOutputOutcome.Review, message, revisionNumber);
    }

    private void Publish(
        WorkflowOutputKind kind,
        WorkflowOutputOutcome outcome,
        string message,
        int? revisionNumber,
        WorkflowAgentStage agentStage = WorkflowAgentStage.None)
    {
        if (_output is null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        long sequence = ++_sequence;
        WorkflowOutputUpdate update = WorkflowOutputUpdate.Create(
            kind,
            outcome,
            message,
            _operationVersion,
            sequence,
            $"workflow-{_operationVersion}-{sequence}",
            revisionNumber,
            agentStage);

        try
        {
            _output.Report(update);
        }
        catch
        {
            // Output is observational; a UI observer must not change workflow semantics.
        }
    }
}
