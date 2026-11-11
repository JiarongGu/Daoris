using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run's review bound to its review record, through the node runtime: a review of work done in place in a folder becomes active on its
/// record rather than withheld for want of a branch, and work that changed nothing waits for the person rather than passing unread.
/// </summary>
public sealed class ReviewRecordRunTests : NodeRunFixture
{
    private const string Workspace = "default";

    private const string Department = RunHome.Repository;

    private string Folder => Path.Combine(_home, "folder");

    private static NodeDeclaration Review() => new("review", NodeKinds.Review, 1, NodeReach.Pressed);

    private const string LandingGraph = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>r","kind":"after","from":"claimed","to":"review"},
                  {"id":"r>h","kind":"after","from":"review","to":"handed"}]}
        """;

    private void InPlace() => BoundTo(WorkflowDefaults.Of("in-place")!.GraphJson);

    private async Task TurnAsync(string record, Action work)
    {
        Directory.CreateDirectory(Folder);
        await PlaceTurns.StartAsync(_home, Workspace, Department, Folder, record, null, CancellationToken.None, run: RunId);
        work();
        await PlaceTurns.EndAsync(_home, record, CancellationToken.None);
    }

    private void Write(string name, string text)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, name), text);
    }

    /// <summary>The run's quest done, its handback the submission numbered <paramref name="sequence"/>.</summary>
    private void Handed(long sequence = 1) =>
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done") with { Submission = new QuestSubmissionView("m1", sequence, _now, $"answer {sequence}") })];

    private ReviewRecord RecordOf(NodeStatus review) => ReviewRecords.Read(_home, RunId, "review", review.Attempt)!;

    private async Task<NodeStatus> ActiveAsync()
    {
        await RunPassAsync(Runtime());
        var review = RunStatus("review");
        Assert.Equal(NodeStates.Active, review.State);
        return review;
    }

    [Fact]
    public async Task A_review_of_a_folder_worked_in_place_is_active_on_its_record_never_withheld_for_want_of_a_branch()
    {
        InPlace();
        await TurnAsync("s-aaaaaaaa", () => Write("plan.md", "the plan\n"));
        Handed();

        var review = await ActiveAsync();

        var record = RecordOf(review);
        Assert.Equal((ReviewKinds.Folder, review.Binding!.SubjectRevision), (record.Kind, record.Revision));
        Assert.Equal(RunUnit, review.Binding.Observes);
        Assert.Equal(("plan.md", "added"), (Assert.Single(record.Rows).Path, record.Rows[0].Change));
        Assert.Equal(("answer 1", true), (Assert.Single(record.Parts).Note, record.Parts[0].Unchecked));
    }

    [Fact]
    public async Task The_persons_accept_reaches_a_folders_review_and_keeps_the_records_revision()
    {
        InPlace();
        await TurnAsync("s-aaaaaaaa", () => Write("plan.md", "the plan\n"));
        Handed();
        var waiting = await ActiveAsync();

        Assert.True(RunNodeWords.Word(Files, RunId, "review", NodeWordActs.Accept, null, () => _now).Done);
        await RunPassAsync(Runtime());

        var accepted = RunStatus("review");
        Assert.Equal((NodeStates.Reached, NodeSource.Person), (accepted.State, accepted.By));
        Assert.Equal(RecordOf(waiting).Revision, accepted.Acceptance?.SubjectRevision);
    }

    [Fact]
    public async Task A_moved_revision_is_a_new_attempt_and_a_new_record_and_the_older_record_stays()
    {
        InPlace();
        await TurnAsync("s-aaaaaaaa", () => Write("plan.md", "the plan\n"));
        Handed();
        var first = await ActiveAsync();

        await TurnAsync("s-aaaaaaaa", () => Write("plan.md", "the plan, mended\n"));
        var second = await ActiveAsync();

        Assert.Equal(first.Attempt + 1, second.Attempt);
        Assert.NotEqual(RecordOf(first).Revision, RecordOf(second).Revision);
        Assert.Equal(first.Binding!.SubjectRevision, RecordOf(first).Revision);
        Assert.Equal(second.Binding!.SubjectRevision, RecordOf(second).Revision);
    }

    [Fact]
    public async Task A_corrected_answer_at_the_same_content_is_a_new_record()
    {
        InPlace();
        await TurnAsync("s-aaaaaaaa", () => Write("plan.md", "the plan\n"));
        Handed(1);
        var first = await ActiveAsync();

        Handed(2);
        var second = await ActiveAsync();

        Assert.Equal(first.Attempt + 1, second.Attempt);
        Assert.Equal(RecordOf(first).Rows.Select(row => (row.Path, row.After)), RecordOf(second).Rows.Select(row => (row.Path, row.After)));
        Assert.Equal(2, Assert.Single(RecordOf(second).Parts).Sequence);
    }

    [Fact]
    public async Task A_folders_review_across_several_turns_shows_the_whole_interval_and_marks_a_change_between_them()
    {
        InPlace();
        await TurnAsync("s-aaaaaaaa", () => Write("a.md", "first turn\n"));
        Write("c.md", "the person's, between turns\n");
        await TurnAsync("s-aaaaaaaa", () => Write("b.md", "second turn\n"));
        Handed();

        var record = RecordOf(await ActiveAsync());

        Assert.Equal(["a.md", "b.md", "c.md"], record.Rows.Select(row => row.Path).Order(StringComparer.Ordinal));
        Assert.Equal([ReviewMarks.Outside], record.Rows.Single(row => row.Path == "c.md").Marks);
        Assert.Empty(record.Rows.Single(row => row.Path == "a.md").Marks);
        Assert.Equal(ReviewCoverage.Whole, record.Coverage);
        Assert.Equal(["b.md"], PlaceChanges.Of(_home, "s-aaaaaaaa")!.Files.Select(file => file.Path));
    }

    [Fact]
    public async Task The_runs_baseline_and_its_content_survive_the_manifest_tidy_while_a_review_record_names_it()
    {
        InPlace();
        Write("notes.md", "first\n");
        await TurnAsync("s-aaaaaaaa", () => Write("notes.md", "second\n"));
        await TurnAsync("s-aaaaaaaa", () => Write("notes.md", "third\n"));
        Handed();
        var record = RecordOf(await ActiveAsync());
        var store = new ManifestStore(_home, Workspace, Department);
        var first = store.Read(RunTurns.BaselineOf(RunId), ManifestMoment.Start)!.Listed("notes.md")!.Sha256!;
        for (var filler = 0; filler < 24; filler++)
        {
            store.Keep($"s-{filler:x8}", ManifestMoment.End, new RunManifest(PlaceKind.Folder, null, null, _now.AddDays(1).AddMinutes(filler), []));
        }

        store.Tidy(ReviewRecords.Manifests(_home));

        Assert.NotNull(store.Read(RunTurns.BaselineOf(RunId), ManifestMoment.Start));
        Assert.True(File.Exists(store.Content.PathOf(first)));
        Assert.Equal(("modified", first), (Assert.Single(record.Rows).Change, record.Rows[0].Before));
    }

    [Fact]
    public async Task Work_that_committed_nothing_waits_for_the_persons_review_rather_than_passing_unread()
    {
        BoundTo(LandingGraph);
        _branchTips["daoris/s1"] = "f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0";
        var branch = RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId]);
        var held = new HeldFact(Opened, Opened, EndBy.Empty, "f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0", "main", "origin/main", "f9", "repo-1");
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), branch with { Records = branch.Records with { Branch = branch.Records.Branch! with { Held = held } } }];

        var review = await ActiveAsync();

        var record = RecordOf(review);
        Assert.Equal((ReviewKinds.Branch, "daoris/s1", "f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0f0"), (record.Kind, record.Reference.Branch, record.Reference.Tip));
        Assert.Empty(record.Rows);
    }
}
