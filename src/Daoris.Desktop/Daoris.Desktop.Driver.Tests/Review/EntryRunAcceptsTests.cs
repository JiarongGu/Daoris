using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An entry's Accept keeps each run's word as the node door does: a run whose review stands on another record than the one drawn is refused
/// <c>moved</c> alone, and the answer says which parts accepted, which moved and which failed.
/// </summary>
public sealed class EntryRunAcceptsTests : NodeRunFixture
{
    private string Folder => Path.Combine(_home, "folder");

    private async Task<(WorkflowProcess Process, string Revision)> ActiveAsync()
    {
        BoundTo(WorkflowDefaults.Of("in-place")!.GraphJson);
        Directory.CreateDirectory(Folder);
        await PlaceTurns.StartAsync(_home, "default", RunHome.Repository, Folder, "s-aaaaaaaa", null, CancellationToken.None, run: RunId);
        File.WriteAllText(Path.Combine(Folder, "plan.md"), "the plan\n");
        await PlaceTurns.EndAsync(_home, "s-aaaaaaaa", CancellationToken.None);
        var done = RunHome.Quest(RunId, "Done") with { From = "ask:a1", Submission = new QuestSubmissionView("m1", 1, _now, "answer 1") };
        _units = [RunHome.QuestUnit(done)];
        await RunPassAsync(Runtime());
        var review = RunStatus("review");
        Assert.Equal(NodeStates.Active, review.State);
        var process = new WorkflowProcess(RunHome.Repository, Landing.Merge, null, null)
        {
            Binding = WorkflowRunBindings.Read(_home, RunId)!, Graph = new GraphRuns(Files).Find(RunId)!.Version.Graph,
        };
        return (process, ReviewRecords.Read(_home, RunId, "review", review.Attempt)!.Revision);
    }

    [Fact]
    public async Task A_run_drawn_on_another_record_is_refused_moved_alone_with_the_revision_standing()
    {
        var (process, revision) = await ActiveAsync();
        var operations = new WorkplaceWorkflowsOperations();

        var moved = operations.KeepReviewWord(_home, () => true, process, _now, revision: "0123456789abcdef");
        var kept = operations.KeepReviewWord(_home, () => true, process, _now, revision: revision);

        Assert.Equal((false, revision), (moved.Kept, moved.Moved));
        Assert.True(kept is { Kept: true, Moved: null });
        Assert.Equal(revision, Assert.Single(Files.Read(RunUnit)!.Pending).Revision);
    }

    [Fact]
    public void The_entrys_answer_says_which_parts_accepted_which_moved_and_which_failed()
    {
        var said = EntryRunAccepts.Said([
            new("s-1", EntryPartOutcome.Accepted), new("s-2", EntryPartOutcome.Moved), new("s-3", EntryPartOutcome.Refused), new("s-4", EntryPartOutcome.Landed),
        ]);

        Assert.Equal("Landed: s-4; Accepted, landing at Daoris's next pass: s-1; Moved since you read them, so not accepted: s-2; Failed: s-3.", said);
        Assert.Contains("now reads revision 0123456789ab", EntryRunAccepts.MovedSaid("s-2", "0123456789abcdef"), StringComparison.Ordinal);
    }
}
