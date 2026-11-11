using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A supervisor's decision on a review bound to the record it read (<see cref="SupervisorReviewBinding"/>): it names the record's revision,
/// is released only while the attempt's record holds it, carries it onto the run's record for the pass to hold it to, and one that names
/// none stands on a branch's review alone; a step that keeps no record (an approval) holds it to its attempt as before.
/// </summary>
public sealed class SupervisorReviewBindingTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "supervisor-review", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-12T09:00:00Z");

    private const string Revision = "aaaaaaa1bbbbbbb2";

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private void Kept(string kind) =>
        ReviewRecords.Keep(_home, new ReviewRecord("q7", "review", 1, kind, Revision, At) { Place = "reports" });

    [Theory]
    [InlineData(ReviewKinds.Folder, Revision, null)]
    [InlineData(ReviewKinds.Folder, "ccccccc3", SupervisorReviewBinding.Moved)]
    [InlineData(ReviewKinds.Folder, null, SupervisorReviewBinding.Moved)]
    [InlineData(ReviewKinds.Plan, null, SupervisorReviewBinding.Moved)]
    [InlineData(ReviewKinds.Branch, null, null)]
    public void A_decision_stands_only_on_the_record_it_names_and_one_naming_none_on_a_branch_alone(string kind, string? named, string? refused)
    {
        Kept(kind);

        Assert.Equal(refused, SupervisorReviewBinding.Refused(_home, "q7", "review", 1, named));
        Assert.Null(SupervisorReviewBinding.Refused(_home, "q7", "approval", 1, null));
        Assert.Equal(Revision, SupervisorReviewBinding.RevisionOf(_home, "q7", "review", 1));
    }

    [Fact]
    public void A_supervisors_word_naming_a_revision_the_work_no_longer_holds_is_dropped_moved_at_the_pass()
    {
        var word = new NodeWord("review", NodeWordActs.Accept, null, At) { By = new NodeWordBy("g-1", "d1"), Attempt = 1, Revision = Revision };
        var standing = new NodeStatus(RunUnits.IdOf("q7"), "review", NodeStates.Active) { Attempt = 1 };

        Assert.True(ReviewAccepts.MovedFrom(word, standing, new Dictionary<string, string> { ["review"] = "ccccccc3" }));
        Assert.False(ReviewAccepts.MovedFrom(word, standing, new Dictionary<string, string> { ["review"] = Revision }));
    }

    [Fact]
    public void The_supervisor_reads_the_records_facts_its_kind_revision_rows_gaps_and_handbacks()
    {
        var record = new ReviewRecord("q7", "review", 1, ReviewKinds.Folder, Revision, At)
        {
            Place = "reports", Coverage = ReviewCoverage.Retained,
            Rows = [new ReviewRow("notes.md", "modified") { Marks = [ReviewMarks.Outside] }],
            Incomplete = [new ReviewGap(ReviewGaps.NotKept, "chart.png", "not text")],
            Parts = [new ReviewPart("q7", "Write the notes") { Note = "Done as asked.", Unchecked = true }],
        };

        var facts = LandingReviewFacts.Of(record);

        Assert.Contains("a folder's files, worked in place", facts);
        Assert.Contains(Revision, facts);
        Assert.Contains("modified notes.md (changed-outside-its-turns)", facts);
        Assert.Contains("chart.png: not text", facts);
        Assert.Contains("since-this-retained-baseline", facts);
        Assert.Contains("q7 Write the notes: Done as asked. (nothing checked its done)", facts);
    }

    private sealed class Admits : IDecisionAdmission
    {
        public DecisionAdmitted Admit(string kind, RolePlace place, string? act) => new("g-1", null) { Window = TimeSpan.FromMinutes(10) };
    }

    [Fact]
    public async Task A_held_decision_carries_its_revision_to_the_release_and_into_its_file()
    {
        var decisions = new QuestionSupervisorDecisions(_home, new Admits()) { LandingApplied = LandingApplied.Of(_home) };
        var landing = new SupervisorLanding("s1", "q7", "review", 1, "g-1", SupervisorLandingActs.Accept,
            [new SupervisorReason("The files are the brief's.", "the record")], new SupervisorHolder(RoleHolders.Terminal, null)) { Revision = Revision };
        var kept = await decisions.LandingAsync(landing, "reports", null, At);
        var handed = new List<SupervisorLandingDue>();

        await decisions.ReleaseLandingsAsync(new LandingRelease(_ => Task.FromResult<string?>(null), due => { handed.Add(due); return null; }), At.AddMinutes(11));

        Assert.Equal(Revision, Assert.Single(handed).Revision);
        Assert.Contains(Revision, File.ReadAllText(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1" + QuestionSupervisorDecisions.DecisionsSuffix), kept.Decision + ".json")));
    }
}
