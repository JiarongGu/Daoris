using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A landing supervisor's word on a run's approval, kept on the run's record by the node's own press door and applied by the node pass: its
/// author and attempt written whole, the person's word taking the place of a pending one, and the pass dropping a word whose attempt has
/// lapsed or whose decision is not live in this run. Each seen failing first with its guard taken out.
/// </summary>
public sealed class SupervisedWordsTests : NodeRunFixture
{
    private const string ApprovalGraph = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"sign","kind":"human","version":1,"reach":"pressed","outcomes":["approved"]},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"},
                  {"id":"redo","kind":"human","version":1,"reach":"pressed","outcomes":["done"]}],
         "wires":[{"id":"c>s","kind":"after","from":"claimed","to":"sign"},
                  {"id":"s>h","kind":"when","from":"sign","to":"handed","outcome":"approved"},
                  {"id":"s>r","kind":"rejected","from":"sign","to":"redo"}]}
        """;

    private static readonly NodeWordBy Gatekeeper = new("gatekeeper@1", "d1");

    private sealed class Decisions(params string[] live) : ILandingWords
    {
        public List<(string Decision, SupervisorState To, string? Why)> Settles { get; } = [];

        public bool Live(string decision) => live.Contains(decision);

        public void Settled(string decision, SupervisorState to, string? why) => Settles.Add((decision, to, why));
    }

    private void Read()
    {
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
    }

    private NodeWorded Supervisor(string act, string? words = null, int? attempt = null) =>
        RunNodeWords.Word(Files, RunId, "sign", act, words, () => _now, by: Gatekeeper, attempt: attempt ?? RunStatus("sign").Attempt);

    private NodeWorded Person(string act, string? words = null) => RunNodeWords.Word(Files, RunId, "sign", act, words, () => _now);

    private async Task<NodeRuntime> WaitingAsync(ILandingWords? decisions)
    {
        BoundTo(ApprovalGraph);
        Read();
        var runtime = Runtime();
        runtime.Landings = decisions;
        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Active, RunStatus("sign").State);
        return runtime;
    }

    private async Task PassAsync(NodeRuntime runtime)
    {
        Read();
        await RunPassAsync(runtime, whole: false);
    }

    [Fact]
    public async Task A_supervisors_approve_is_applied_as_the_supervisors_while_its_decision_is_live()
    {
        var decisions = new Decisions("d1");
        var runtime = await WaitingAsync(decisions);

        Assert.True(Supervisor(NodeWordActs.Approve).Done);
        var pending = Files.Read(RunUnit)!.Pending.Single();
        Assert.Equal((Gatekeeper, RunStatus("sign").Attempt, NodeWordActs.Approve), (pending.By, pending.Attempt, pending.Act));
        await PassAsync(runtime);

        var signed = RunStatus("sign");
        Assert.Equal((NodeStates.Reached, NodeSource.Supervisor, "approved"), (signed.State, signed.By, signed.Acceptance!.Outcome));
        Assert.Equal(NodeStates.Active, RunStatus("handed").State);
        Assert.Equal([("d1", SupervisorState.WentOn, (string?)null)], decisions.Settles);
    }

    [Fact]
    public async Task A_supervisors_reject_rejects_the_approval_as_the_supervisors_with_its_words()
    {
        var decisions = new Decisions("d1");
        var runtime = await WaitingAsync(decisions);

        Assert.True(Supervisor(NodeWordActs.Reject, "The notes miss the migration.").Done);
        await PassAsync(runtime);

        var signed = RunStatus("sign");
        Assert.Equal((NodeStates.Rejected, NodeSource.Supervisor, "The notes miss the migration."), (signed.State, signed.By, signed.Words));
        Assert.Equal(NodeStates.Active, RunStatus("redo").State);
        Assert.Equal(SupervisorState.WentOn, decisions.Settles.Single().To);
    }

    [Fact]
    public async Task The_persons_reject_after_a_supervisors_approve_took_its_place_is_kept_as_the_persons_and_the_decision_superseded()
    {
        var decisions = new Decisions("d1");
        var runtime = await WaitingAsync(decisions);
        Assert.True(Supervisor(NodeWordActs.Approve).Done);

        Assert.True(Person(NodeWordActs.Reject, "I read the diff; no.").Done);
        var pending = Files.Read(RunUnit)!.Pending.Single();
        Assert.Equal((null, "d1", NodeWordActs.Reject), (pending.By, pending.Supersedes, pending.Act));
        await PassAsync(runtime);

        var signed = RunStatus("sign");
        Assert.Equal((NodeStates.Rejected, NodeSource.Person), (signed.State, signed.By));
        Assert.Equal([("d1", SupervisorState.Superseded, (string?)null)], decisions.Settles);
    }

    [Fact]
    public async Task A_supervisors_word_is_refused_where_any_word_waits_and_a_second_person_word_where_the_persons_waits()
    {
        await WaitingAsync(new Decisions("d1"));
        Assert.True(Person(NodeWordActs.Approve).Done);
        Assert.Equal(RunNodeWords.Worded, Supervisor(NodeWordActs.Approve).Refused);
        Assert.Equal(RunNodeWords.Worded, Person(NodeWordActs.Reject).Refused);
    }

    [Fact]
    public async Task A_supervisors_word_on_a_lapsed_attempt_is_dropped_stale_and_the_step_stays_the_persons()
    {
        var decisions = new Decisions("d1");
        var runtime = await WaitingAsync(decisions);
        var attempt = RunStatus("sign").Attempt;

        Assert.Equal(RunNodeWords.Stale, Supervisor(NodeWordActs.Approve, attempt: attempt + 1).Refused);

        // The word as the record keeps it from a time the step stood on another attempt.
        var (refused, _) = Files.Edit(RunUnit, record => (record with
        {
            Pending = [new NodeWord("sign", NodeWordActs.Approve, null, _now) { By = Gatekeeper, Attempt = attempt + 1 }],
        }, true));
        Assert.Null(refused);
        await PassAsync(runtime);

        Assert.Equal(NodeStates.Active, RunStatus("sign").State);
        Assert.Empty(Files.Read(RunUnit)!.Pending);
        Assert.Equal([("d1", SupervisorState.Stale, (string?)RunNodeWords.Stale)], decisions.Settles);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_supervisors_word_whose_decision_is_not_live_in_this_run_is_dropped_by_the_first_pass(bool wired)
    {
        // A restart empties the decisions, the record keeps the word.
        var live = new Decisions("d1");
        var runtime = await WaitingAsync(live);
        Assert.True(Supervisor(NodeWordActs.Approve).Done);

        var restarted = new Decisions();
        runtime.Landings = wired ? restarted : null;
        await PassAsync(runtime);

        Assert.Equal(NodeStates.Active, RunStatus("sign").State);
        Assert.Empty(Files.Read(RunUnit)!.Pending);
        Assert.Equal(wired ? [("d1", SupervisorState.Stale, (string?)RunNodeWords.Stale)] : [], restarted.Settles);
    }

    [Fact]
    public void A_review_and_an_approval_move_with_the_word_authors_source_and_cause()
    {
        var word = new NodeWord("sign", NodeWordActs.Reject, "no", _now) { By = Gatekeeper, Attempt = 1 };
        var approval = new NodeDeclaration("sign", NodeKinds.Human, 1, NodeReach.Pressed) { Outcomes = ["approved"] };
        var status = NodeLifecycle.Start("run:q7", "sign") with { State = NodeStates.Active, Attempt = 1 };

        var rejected = ((IPressed)NodeKit.Of(NodeKinds.Human)!).Move(approval, word, status, new NodeScope("digest", null!));
        var accepted = ((IPressed)NodeKit.Of(NodeKinds.Review)!).Move(
            new NodeDeclaration("review", NodeKinds.Review, 1, NodeReach.Pressed), word with { Act = NodeWordActs.Accept }, status, new NodeScope("digest", null!));

        Assert.Equal((NodeStates.Rejected, NodeSource.Supervisor), (rejected!.To, rejected.Source));
        Assert.Equal((NodeStates.Reached, NodeSource.Supervisor), (accepted!.To, accepted.Source));
        Assert.Equal(NodeSource.Person, new NodeWord("sign", NodeWordActs.Accept, null, _now).Source);
    }
}
