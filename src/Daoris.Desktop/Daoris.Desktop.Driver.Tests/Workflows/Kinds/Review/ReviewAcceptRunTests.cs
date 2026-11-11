using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The person's Accept bound to the review record it names, through the node runtime: checked when it is kept against the record the review
/// stands on, and when the pass applies it against the work read afresh, so a page drawn before the work moved never accepts newer work; an
/// early Accept kept by record; and an unchecked done passed in the same edit as the Accept that named it.
/// </summary>
public sealed class ReviewAcceptRunTests : NodeRunFixture
{
    private const string Workspace = "default";

    private const string Department = RunHome.Repository;

    private string Folder => Path.Combine(_home, "folder");

    /// <summary>A sign-off before the review, so the review has not begun while the person presses.</summary>
    private const string SignedGraph = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"sign","kind":"human","version":1,"reach":"pressed","outcomes":["approved"]},
                  {"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>s","kind":"after","from":"claimed","to":"sign"},
                  {"id":"s>r","kind":"when","from":"sign","to":"review","outcome":"approved"},
                  {"id":"r>h","kind":"after","from":"review","to":"handed"}]}
        """;

    private const string HeldGraph = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"holds","kind":"quest.holds","version":1,"reach":"observed"},
                  {"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>q","kind":"after","from":"claimed","to":"holds"},
                  {"id":"q>r","kind":"after","from":"holds","to":"review"},
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

    private QuestView Done(long sequence, string from = "ask:a1") =>
        RunHome.Quest(RunId, "Done") with { From = from, Submission = new QuestSubmissionView("m1", sequence, _now, $"answer {sequence}") };

    private void Handed(long sequence = 1) => _units = [RunHome.QuestUnit(Done(sequence))];

    /// <summary>The run's branch at <see cref="NodeRunFixture.Tip1"/>, its quest's handback numbered <paramref name="sequence"/>.</summary>
    private void OnBranch(long sequence, string from = "ask:a1")
    {
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(Done(sequence, from)), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
    }

    /// <summary>The revision the branch's record holds at <see cref="NodeRunFixture.Tip1"/> with the handback numbered <paramref name="sequence"/>, as a page draws it.</summary>
    private static string BranchRevision(long sequence) =>
        ReviewWork.RevisionOf(ReviewKinds.Branch, new ReviewReference { Branch = "daoris/s1", Tip = Tip1 }, [new ReviewPart(RunId, "The work") { Machine = "m1", Sequence = sequence }]);

    private ReviewRecord RecordOf(NodeStatus review) => ReviewRecords.Read(_home, RunId, "review", review.Attempt)!;

    private async Task<NodeStatus> ActiveAsync()
    {
        await RunPassAsync(Runtime());
        var review = RunStatus("review");
        Assert.Equal(NodeStates.Active, review.State);
        return review;
    }

    private NodeWorded Accept(string? revision, int? attempt = null) =>
        RunNodeWords.Word(Files, RunId, "review", NodeWordActs.Accept, null, () => _now, attempt: attempt, revision: revision);

    [Fact]
    public async Task An_accept_naming_another_record_than_the_review_stands_on_is_refused_moved_when_it_is_kept()
    {
        InPlace();
        await TurnAsync("s-aaaaaaaa", () => Write("plan.md", "the plan\n"));
        Handed();
        var review = await ActiveAsync();
        var record = RecordOf(review);

        var stale = Accept("0123456789abcdef", review.Attempt);
        var earlier = Accept(record.Revision, review.Attempt - 1);

        Assert.Equal((ReviewMoved.Word, record.Revision), (stale.Refused, stale.Detail));
        Assert.Equal(ReviewMoved.Word, earlier.Refused);
        Assert.Contains(record.Revision[..12], RunNodeWords.Said(stale));
        Assert.Empty(Files.Read(RunUnit)!.Pending);
    }

    [Fact]
    public async Task An_accept_on_the_record_drawn_reaches_the_review_with_that_records_revision()
    {
        InPlace();
        await TurnAsync("s-aaaaaaaa", () => Write("plan.md", "the plan\n"));
        Handed();
        var review = await ActiveAsync();
        var record = RecordOf(review);

        Assert.True(Accept(record.Revision[..12], review.Attempt).Done);
        await RunPassAsync(Runtime());

        var accepted = RunStatus("review");
        Assert.Equal((NodeStates.Reached, record.Revision), (accepted.State, accepted.Acceptance?.SubjectRevision));
    }

    [Fact]
    public async Task An_accept_kept_before_the_work_moved_is_dropped_moved_by_the_pass_that_reads_the_work_afresh()
    {
        InPlace();
        await TurnAsync("s-aaaaaaaa", () => Write("plan.md", "the plan\n"));
        Handed(1);
        var review = await ActiveAsync();
        Assert.True(Accept(RecordOf(review).Revision, review.Attempt).Done);

        Handed(2);
        var pass = await RunPassAsync(Runtime());

        var after = RunStatus("review");
        Assert.Equal((NodeStates.Active, review.Attempt + 1), (after.State, after.Attempt));
        Assert.Empty(Files.Read(RunUnit)!.Pending);
        Assert.Contains(pass.Said, line => line.Contains("(moved)", StringComparison.Ordinal) && line.Contains("accept", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_accept_on_a_branch_kept_before_its_tip_moved_is_dropped_moved()
    {
        BoundTo(HeldGraph);
        OnBranch(1);
        var review = await ActiveAsync();
        Assert.Equal(BranchRevision(1), RecordOf(review).Revision);
        Assert.True(Accept(BranchRevision(1), review.Attempt).Done);

        _branchTips["daoris/s1"] = Tip2;
        await RunPassAsync(Runtime());

        Assert.NotEqual(NodeStates.Reached, RunStatus("review").State);
        Assert.Empty(Files.Read(RunUnit)!.Pending);
    }

    [Fact]
    public async Task An_early_accept_is_kept_by_the_record_drawn_and_applied_once_the_review_begins_on_it()
    {
        BoundTo(SignedGraph);
        OnBranch(1);
        await RunPassAsync(Runtime());
        Assert.Equal(NodeStates.Blocked, RunStatus("review").State);

        var early = Accept(BranchRevision(1));
        Assert.True(early is { Done: true, Early: true });
        Assert.Equal(BranchRevision(1), Files.Read(RunUnit)!.Pending.Single().Revision);

        Assert.True(RunNodeWords.Word(Files, RunId, "sign", NodeWordActs.Approve, null, () => _now).Done);
        await RunPassAsync(Runtime());
        await RunPassAsync(Runtime());

        Assert.Equal((NodeStates.Reached, NodeSource.Person), (RunStatus("review").State, RunStatus("review").By));
    }

    [Fact]
    public async Task An_early_accept_on_a_page_drawn_before_the_handback_arrived_is_not_applied_to_it()
    {
        BoundTo(SignedGraph);
        OnBranch(1);
        await RunPassAsync(Runtime());
        Assert.True(Accept(BranchRevision(1)).Done);

        OnBranch(2);
        Assert.True(RunNodeWords.Word(Files, RunId, "sign", NodeWordActs.Approve, null, () => _now).Done);
        await RunPassAsync(Runtime());
        await RunPassAsync(Runtime());

        Assert.Equal(NodeStates.Active, RunStatus("review").State);
        Assert.Empty(Files.Read(RunUnit)!.Pending);
    }

    [Fact]
    public async Task An_unchecked_done_is_passed_in_the_same_edit_as_the_accept_that_named_it_and_a_replay_is_refused_worded()
    {
        BoundTo(HeldGraph);
        OnBranch(1, from: "ask #a1b2c3");
        await RunPassAsync(Runtime());
        Assert.Equal(NodeStates.Active, RunStatus("holds").State);
        Assert.Equal(NodeStates.Blocked, RunStatus("review").State);

        var kept = UncheckedAccepts.Keep(Files, RunId, "review", BranchRevision(1), () => _now);
        var replayed = UncheckedAccepts.Keep(Files, RunId, "review", BranchRevision(1), () => _now);

        Assert.True(kept.Done);
        Assert.Equal(RunNodeWords.Worded, replayed.Refused);
        var words = Files.Read(RunUnit)!.Pending;
        Assert.Equal([("holds", NodeWordActs.Skip), ("review", NodeWordActs.Accept)], words.Select(word => (word.Node, word.Act)));
        Assert.All(words, word => Assert.Equal(("review", BranchRevision(1)), (word.Review, word.Revision)));
        Assert.Contains("unchecked", words[0].Words);

        await RunPassAsync(Runtime());
        await RunPassAsync(Runtime());

        Assert.Equal(NodeStates.Skipped, RunStatus("holds").State);
        Assert.Equal((NodeStates.Reached, NodeSource.Person), (RunStatus("review").State, RunStatus("review").By));
    }

    [Fact]
    public async Task An_unchecked_done_and_its_accept_are_dropped_together_where_the_work_moved_before_the_pass()
    {
        BoundTo(HeldGraph);
        OnBranch(1, from: "ask #a1b2c3");
        await RunPassAsync(Runtime());
        Assert.True(UncheckedAccepts.Keep(Files, RunId, "review", BranchRevision(1), () => _now).Done);

        OnBranch(2, from: "ask #a1b2c3");
        await RunPassAsync(Runtime());

        Assert.Equal(NodeStates.Active, RunStatus("holds").State);
        Assert.Empty(Files.Read(RunUnit)!.Pending);
    }

    [Fact]
    public async Task Changes_answered_by_a_corrected_handback_at_the_same_commit_ask_the_review_again_on_its_new_record()
    {
        BoundTo(HeldGraph);
        OnBranch(1);
        var review = await ActiveAsync();
        Assert.True(RunNodeWords.Word(Files, RunId, "review", NodeWordActs.Changes, "Name the source.", () => _now).Done);
        await RunPassAsync(Runtime());
        Assert.Equal(NodeStates.Rejected, RunStatus("review").State);

        OnBranch(2);
        await RunPassAsync(Runtime());

        var again = RunStatus("review");
        Assert.Equal(NodeStates.Active, again.State);
        Assert.True(again.Attempt > review.Attempt);
        Assert.Equal((BranchRevision(2), 2L), (RecordOf(again).Revision, RecordOf(again).Parts.Single().Sequence));
    }

    [Theory]
    [InlineData(RunId, "s-made")]
    [InlineData(null, "s-newest")]
    [InlineData("q404", "s-newest")]
    public async Task Changes_naming_a_part_are_told_to_the_session_that_made_it_and_unnamed_to_the_newest(string? part, string session)
    {
        BoundTo(HeldGraph);
        _branchTips["daoris/s1"] = Tip1;
        var made = Done(1) with { Submission = new QuestSubmissionView("m1", 1, _now, "answer 1") { Session = "s-made" } };
        _units = [RunHome.QuestUnit(made) with { Executors = ["s-made", "s-newest"] }, RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
        var told = new List<(string Session, string Key)>();
        var runtime = Runtime();
        runtime.Say = (to, _, key, _) =>
        {
            told.Add((to, key));
            return Task.FromResult(new SayAnswer(true, "kept"));
        };
        await RunPassAsync(runtime);

        Assert.True(RunNodeWords.Word(Files, RunId, "review", NodeWordActs.Changes, "Name the source.", () => _now, part: part).Done);
        await RunPassAsync(runtime);

        Assert.Equal((session, $"changes/{RunId}/review/1"), Assert.Single(told));
    }

    [Fact]
    public async Task The_terminals_accept_names_the_record_with_at_and_without_it_accepts_the_one_standing_and_says_its_revision()
    {
        InPlace();
        await TurnAsync("s-aaaaaaaa", () => Write("plan.md", "the plan\n"));
        Handed();
        var revision = RecordOf(await ActiveAsync()).Revision;
        Assert.Equal(new WorkflowNodeAsk(NodeWordActs.Accept, RunId, "review", null, "abc1234"),
            WorkflowNodeCommand.Read(["accept", RunId, "review", "--at", "abc1234"], out _));
        Assert.Null(WorkflowNodeCommand.Read(["skip", RunId, "review", "--at", "abc1234"], out var problem));
        Assert.Contains("--at", problem, StringComparison.Ordinal);
        Assert.Contains("--at <revision>", WorkflowNodeCommand.Usage, StringComparison.Ordinal);

        async Task<(int, string)> AcceptAsync(string? at)
        {
            var output = new StringWriter { NewLine = "\n" };
            var code = await WorkflowNodeCommand.RunAsync(new(NodeWordActs.Accept, RunId, "review", null, at), Files, output, instance: null, _ => null, () => _now);
            return (code, output.ToString());
        }

        var (refused, moved) = await AcceptAsync("abc1234");
        Assert.Equal(1, refused);
        Assert.Contains(revision[..12], moved, StringComparison.Ordinal);

        var (kept, said) = await AcceptAsync(null);
        Assert.Equal(0, kept);
        Assert.Contains($"revision {revision[..12]}", said, StringComparison.Ordinal);
        Assert.Equal(revision, Files.Read(RunUnit)!.Pending.Single().Revision);
    }

    [Fact]
    public async Task An_unchecked_accept_is_refused_where_no_unchecked_done_holds_the_review()
    {
        BoundTo(HeldGraph);
        OnBranch(1);
        var review = await ActiveAsync();

        Assert.Equal(UncheckedAccepts.NoneUnchecked, UncheckedAccepts.Keep(Files, RunId, "review", RecordOf(review).Revision, () => _now).Refused);
        Assert.Empty(Files.Read(RunUnit)!.Pending);
    }
}
