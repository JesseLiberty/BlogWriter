using BlogWriter;
using Xunit;

namespace BlogWriter.Tests;

public class ResearchStateTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("Needs more detail on X.", false)]
    [InlineData("APPROVED", true)]
    [InlineData("approved - nice work", true)]
    [InlineData("APPROVED - Maximum revisions reached.", true)]
    public void IsApproved_DetectsMarkerCaseInsensitively(string? reviewNotes, bool expected)
    {
        Assert.Equal(expected, ResearchState.IsApproved(reviewNotes));
    }

    [Fact]
    public void NeedsRevision_TrueWhenNotApprovedAndUnderCap()
    {
        var state = new ResearchState { ReviewNotes = "Please revise the intro.", RevisionNumber = 1 };

        Assert.True(state.NeedsRevision);
    }

    [Fact]
    public void NeedsRevision_FalseWhenApproved()
    {
        var state = new ResearchState { ReviewNotes = ResearchState.ApprovedMarker, RevisionNumber = 1 };

        Assert.False(state.NeedsRevision);
    }

    [Fact]
    public void NeedsRevision_FalseWhenRevisionCapReached()
    {
        var state = new ResearchState { ReviewNotes = "Still needs work.", RevisionNumber = ResearchState.MaxRevisions };

        Assert.False(state.NeedsRevision);
    }

    [Fact]
    public void NeedsRevision_FalseOneStepBelowCap_TrueWhenBelow()
    {
        var belowCap = new ResearchState { ReviewNotes = "revise", RevisionNumber = ResearchState.MaxRevisions - 1 };
        var atCap = new ResearchState { ReviewNotes = "revise", RevisionNumber = ResearchState.MaxRevisions };

        Assert.True(belowCap.NeedsRevision);
        Assert.False(atCap.NeedsRevision);
    }

    [Theory]
    [InlineData("APPROVED", ResearchState.MaxRevisions, false)]
    [InlineData("Still needs work.", ResearchState.MaxRevisions, true)]
    [InlineData("Still needs work.", ResearchState.MaxRevisions - 1, false)]
    public void RevisionLimitReached_RequiresUnapprovedReviewAtCap(
        string reviewNotes,
        int revisionNumber,
        bool expected)
    {
        var state = new ResearchState { ReviewNotes = reviewNotes, RevisionNumber = revisionNumber };

        Assert.Equal(expected, state.RevisionLimitReached);
    }

    [Fact]
    public void StartFollowUp_ResetsResearchAndDraftSoRefinedSearchTriggersFreshResearch()
    {
        var state = new ResearchState
        {
            MainTask = "topic",
            ResearchFindings = ["finding"],
            Draft = "draft",
            ReviewNotes = ResearchState.ApprovedMarker,
            RevisionNumber = ResearchState.MaxRevisions,
            NextStep = "END",
        };

        state.StartFollowUp("Add a caching section.");

        Assert.Equal("Add a caching section.", state.CurrentSubTask);
        Assert.Empty(state.Draft);
        Assert.Empty(state.ResearchFindings);
        Assert.Empty(state.ReviewNotes);
        Assert.Equal(0, state.RevisionNumber);
        Assert.Empty(state.NextStep);
    }

    [Fact]
    public void BuildResearchQuery_IncludesOriginalTopicAndFollowUpRefinement()
    {
        var state = new ResearchState
        {
            MainTask = "How to build a blog app",
            ResearchFindings = ["Concepts: context, prompts, and workflow.", "Drafting is done after research."],
            CurrentSubTask = "Add a caching section."
        };

        string query = state.BuildResearchQuery();

        Assert.Contains("How to build a blog app", query);
        Assert.Contains("Add a caching section.", query);
        Assert.Contains("Concepts: context, prompts, and workflow.", query);
        Assert.Contains("Drafting is done after research.", query);
    }
}
