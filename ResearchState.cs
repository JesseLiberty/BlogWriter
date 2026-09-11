namespace BlogWriter;

/// <summary>State for the research workflow.</summary>
public class ResearchState
{
    /// <summary>Hard upper bound on author/review revision cycles. Guarantees the workflow terminates.</summary>
    public const int MaxRevisions = 2;

    /// <summary>The single source-of-truth marker written to <see cref="ReviewNotes"/> on approval.</summary>
    public const string ApprovedMarker = "APPROVED";

    /// <summary>Default lower bound on the target word count for the blog post.</summary>
    public const int DefaultMinWords = 1000;

    /// <summary>Default upper bound on the target word count for the blog post.</summary>
    public const int DefaultMaxWords = 2000;

    public string MainTask { get; set; } = "";

    /// <summary>Minimum target word count for the draft. Used by the author and reviewer stages.</summary>
    public int MinWords { get; set; } = DefaultMinWords;

    /// <summary>Maximum target word count for the draft. Used by the author and reviewer stages.</summary>
    public int MaxWords { get; set; } = DefaultMaxWords;
    public List<string> ResearchFindings { get; set; } = [];
    public List<string> SearchRefinements { get; set; } = [];
    public string Draft { get; set; } = "";
    public string ReviewNotes { get; set; } = "";
    public int RevisionNumber { get; set; }
    public string NextStep { get; set; } = "";
    public string CurrentSubTask { get; set; } = "";

    /// <summary>
    /// Builds the full research query for a follow-up refinement. It keeps the
    /// original task, includes the new refinement request, and folds in the
    /// previously discovered research context so the next search narrows the
    /// earlier work instead of replacing it.
    /// </summary>
    public string BuildResearchQuery()
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(MainTask))
        {
            parts.Add($"Original task: {MainTask.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(CurrentSubTask))
        {
            parts.Add($"Follow-up refinement: {CurrentSubTask.Trim()}");
        }

        if (SearchRefinements.Count > 0)
        {
            string uniqueRefinements = string.Join(" | ", SearchRefinements
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(uniqueRefinements))
            {
                parts.Add($"Refinement history: {uniqueRefinements}");
            }
        }

        if (ResearchFindings.Count > 0)
        {
            string context = string.Join("\n\n", ResearchFindings
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(f => f.Trim()));

            if (!string.IsNullOrWhiteSpace(context))
            {
                parts.Add($"Prior research context:\n{context}");
            }
        }

        return string.Join("\n\n", parts);
    }

    /// <summary>Prepares an approved or revision-capped draft for a user-requested follow-up.</summary>
    public void StartFollowUp(string followUp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(followUp);

        string trimmed = followUp.Trim();
        if (!string.IsNullOrWhiteSpace(CurrentSubTask) &&
            !string.Equals(CurrentSubTask, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            SearchRefinements.Add(CurrentSubTask.Trim());
        }

        SearchRefinements.Add(trimmed);
        CurrentSubTask = trimmed;
        ReviewNotes = "";
        RevisionNumber = 0;
        NextStep = "";
    }

    /// <summary>True when the given review text contains the approval marker (case-insensitive).</summary>
    public static bool IsApproved(string? reviewNotes) =>
        !string.IsNullOrEmpty(reviewNotes) && reviewNotes.Contains(ApprovedMarker, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True while the draft is not yet approved AND the revision cap has not been
    /// reached. Drives the bounded review loop; when false the workflow terminates.
    /// </summary>
    public bool NeedsRevision => !IsApproved(ReviewNotes) && RevisionNumber < MaxRevisions;

    /// <summary>True when review ended only because the revision cap was reached.</summary>
    public bool RevisionLimitReached => !IsApproved(ReviewNotes) && RevisionNumber >= MaxRevisions;
}
