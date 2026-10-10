namespace BlogWriter;

public enum WorkflowOutputKind
{
    Lifecycle,
    ReviewerFeedback,
}

public enum WorkflowOutputOutcome
{
    Progress,
    Success,
    Cancellation,
    Validation,
    Conflict,
    Failure,
    Review,
}

public enum WorkflowAgentStage
{
    None,
    Blogger,
    Researcher,
    Author,
    Reviewer,
}

public sealed record WorkflowOutputUpdate
{
    private WorkflowOutputUpdate(
        WorkflowOutputKind kind,
        WorkflowOutputOutcome outcome,
        string message,
        long operationVersion,
        long sequence,
        string updateKey,
        int? revisionNumber,
        WorkflowAgentStage agentStage)
    {
        Kind = kind;
        Outcome = outcome;
        Message = message;
        OperationVersion = operationVersion;
        Sequence = sequence;
        UpdateKey = updateKey;
        RevisionNumber = revisionNumber;
        AgentStage = agentStage;
    }

    public WorkflowOutputKind Kind { get; }
    public WorkflowOutputOutcome Outcome { get; }
    public string Message { get; }
    public long OperationVersion { get; }
    public long Sequence { get; }
    public string UpdateKey { get; }
    public int? RevisionNumber { get; }
    public WorkflowAgentStage AgentStage { get; }

    public static WorkflowOutputUpdate Create(
        WorkflowOutputKind kind,
        WorkflowOutputOutcome outcome,
        string message,
        long operationVersion,
        long sequence,
        string updateKey,
        int? revisionNumber = null,
        WorkflowAgentStage agentStage = WorkflowAgentStage.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(updateKey);
        ArgumentOutOfRangeException.ThrowIfNegative(operationVersion);
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);

        return new WorkflowOutputUpdate(
            kind,
            outcome,
            message,
            operationVersion,
            sequence,
            updateKey,
            revisionNumber,
            agentStage);
    }
}