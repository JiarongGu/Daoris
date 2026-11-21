using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A second opinion's dispute the risk policy classes <c>role</c> holds the opinion step on its own role as the place casts it. No agent's
/// tool answers that hold (<c>step_settle</c> takes a task's Done, a decision, a review's Accept and an approval), so where the role's agent
/// holds it the step is a vacancy, listed, announced once and given up by Daoris past its bound with what it tried: never a silent wait,
/// never the owner's.
/// </summary>
public sealed class OpinionRoleDisputeTests : NodeRunFixture
{
    private const string Opinioned = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"opinion","kind":"opinion","version":1,"reach":"observed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>o","kind":"after","from":"claimed","to":"opinion"},
                  {"id":"o>h","kind":"after","from":"opinion","to":"handed"}]}
        """;

    private const string Bound = """{"risk":{"machine":[{"kind":"work.accept","class":"role"}]},"vacancyHours":2}""";

    private void OnBranch()
    {
        BoundTo(Opinioned);
        _branchTips["daoris/s1"] = Tip1;
        var done = RunHome.Quest(RunId, "Done") with { Submission = new QuestSubmissionView("m1", 1, _now, "answer 1") };
        _units = [RunHome.QuestUnit(done) with { Executors = ["s-worker"] }, RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
        _opinionGate = new OpinionGateState(OpinionGateStates.Disputed, RunHome.Repository)
        {
            Session = "s-worker", Tip = Tip1, Disputes = new OpinionDisputes([1], []),
            Risk = new RiskVerdict(RiskActs.WorkAccept, null, RiskVerdict.Gate, RiskClasses.Role, "supervisor", "machine", "r1", "classed role at the machine"),
        };
    }

    [Fact]
    public async Task A_dispute_classed_role_its_agent_holds_is_said_vacant_once_and_reaches_the_owners_final_exit_past_its_bound()
    {
        OnBranch();
        var runtime = Runtime();
        var first = await RunPassAsync(runtime, Bound);
        Assert.Equal((NodeStates.Active, OpinionGateReading.RoleDisputed), (RunStatus("opinion").State, RunStatus("opinion").Holds));
        Assert.Single(first.Said, line => line.Contains("step `opinion`", StringComparison.Ordinal) && line.Contains("is vacant:", StringComparison.Ordinal));
        Assert.NotNull(new StepVacancyFiles(_home).Current(RunUnit, "opinion", RunStatus("opinion").Attempt));

        _now = _now.AddHours(1);
        var again = await RunPassAsync(runtime, Bound);
        Assert.DoesNotContain(again.Said, line => line.Contains("vacant", StringComparison.Ordinal));

        _now = _now.AddHours(2);
        var past = await RunPassAsync(runtime, Bound);
        Assert.Single(past.Noticed, line => line.Text.Contains("step `opinion`", StringComparison.Ordinal) && line.Text.Contains("final exit, to you:", StringComparison.Ordinal));
        var after = await RunPassAsync(runtime, Bound);
        Assert.Empty(after.Noticed);
        Assert.DoesNotContain(after.Said, line => line.Contains("final exit", StringComparison.Ordinal) || line.Contains("gave it up", StringComparison.Ordinal));
        Assert.Equal(NodeStates.Active, RunStatus("opinion").State);
        Assert.True(new StepVacancyFiles(_home).Current(RunUnit, "opinion", RunStatus("opinion").Attempt)?.Exited);
    }

    /// <summary>With nothing configured the gate's dispute is answered by the policy, so the opinion step is reached and the run goes on to its landing.</summary>
    [Fact]
    public async Task With_nothing_configured_a_dispute_answered_by_the_policy_lets_the_run_go_on_to_its_landing()
    {
        OnBranch();
        _opinionGate = _opinionGate! with
        {
            State = OpinionGateStates.Answered, Answered = OpinionGateStates.Disputed,
            Person = new OpinionPersonWord(OpinionPersonSaid.Policy, Tip1, _now),
            Risk = new RiskVerdict(RiskActs.WorkAccept, null, RiskVerdict.Pass, RiskClasses.None, null, RiskVerdict.Seed, "0", "no rule gates it"),
        };

        await RunPassAsync(Runtime(), """{"risk":{}}""");

        Assert.Equal(NodeStates.Reached, RunStatus("opinion").State);
        Assert.NotEqual(NodeStates.Blocked, WorkPerformer.StatusOf(Files.Read(RunUnit)!, "handed").State);
        Assert.Empty(new StepVacancyFiles(_home).Of(RunUnit));
    }

    private static readonly RiskVerdict Seeded = new(RiskActs.WorkAccept, null, RiskVerdict.Pass, RiskClasses.None, null, RiskVerdict.Seed, "0e80e115d1ce", "no risk declared: it passes");

    private OpinionGateState PassedBySeed(string held, string tip) => new(OpinionGateStates.Answered, RunHome.Repository)
    {
        Session = "s-worker", Tip = tip, Answered = held, Risk = Seeded,
        Disputes = held == OpinionGateStates.Disputed ? new OpinionDisputes([1, 2], []) : new OpinionDisputes([], []),
        Since = held == OpinionGateStates.CommitsSince ? 3 : 0,
        Person = new OpinionPersonWord(OpinionPersonSaid.Policy, tip, _now) { Words = Seeded.Passed },
    };

    /// <summary>A dispute and commits nobody read, each passed by the seed, reach the step with the pass's line, and the landing is let go.</summary>
    [Theory]
    [InlineData(OpinionGateStates.Disputed)]
    [InlineData(OpinionGateStates.CommitsSince)]
    public async Task A_hold_passed_by_the_seed_reaches_the_opinion_step_with_the_passes_line_and_the_run_goes_on_to_its_landing(string held)
    {
        OnBranch();
        _opinionGate = PassedBySeed(held, Tip1);

        await RunPassAsync(Runtime(), """{"risk":{}}""");

        var opinion = RunStatus("opinion");
        Assert.Equal((NodeStates.Reached, Tip1), (opinion.State, opinion.Binding?.SubjectRevision));
        Assert.Equal(Seeded.Passed, opinion.Words);
        Assert.NotEqual(NodeStates.Blocked, WorkPerformer.StatusOf(Files.Read(RunUnit)!, "handed").State);
        var said = WorkflowGraphRuns.Said(Node(), opinion);
        Assert.Contains("risk policy", said, StringComparison.Ordinal);
        Assert.Contains(Seeded.Passed, said, StringComparison.Ordinal);
    }

    /// <summary>A pass at one tip binds that tip alone: new commits rebind the step, judged again at the new tip, and the seed's pass there reaches it again with its own line.</summary>
    [Fact]
    public async Task A_pass_at_one_tip_and_then_new_commits_are_judged_again_at_the_new_tip_and_passed_again()
    {
        OnBranch();
        _opinionGate = PassedBySeed(OpinionGateStates.Disputed, Tip1);
        var runtime = Runtime();
        await RunPassAsync(runtime, """{"risk":{}}""");
        Assert.Equal((NodeStates.Reached, Tip1), (RunStatus("opinion").State, RunStatus("opinion").Binding?.SubjectRevision));

        _branchTips["daoris/s1"] = Tip2;
        _opinionGate = PassedBySeed(OpinionGateStates.CommitsSince, Tip2);
        await RunPassAsync(runtime, """{"risk":{}}""");
        await RunPassAsync(runtime, """{"risk":{}}""");

        var again = RunStatus("opinion");
        Assert.Equal((NodeStates.Reached, Tip2), (again.State, again.Binding?.SubjectRevision));
        Assert.Equal(Seeded.Passed, again.Words);
    }

    /// <summary>A step its record holds on a hold the seed now passes says so with the line, never a silent <i>working</i>, on nobody.</summary>
    [Theory]
    [InlineData(OpinionGateStates.Disputed)]
    [InlineData(OpinionGateStates.CommitsSince)]
    public void A_step_held_on_a_hold_the_seed_now_passes_says_so_and_waits_on_nobody(string held)
    {
        var status = new NodeStatus(RunUnit, "opinion", NodeStates.Active) { Attempt = 1, Holds = held, Since = _now };

        var wait = OpinionStepWaits.WaitOf(Node(), status, PassedBySeed(held, Tip1));

        Assert.Equal(OpinionStepWaits.Passed, wait?.Code);
        Assert.False(OpinionStepWaits.OnPerson(wait!));
        var said = OpinionStepWaits.Said(wait!);
        Assert.Contains("risk policy passed", said, StringComparison.Ordinal);
        Assert.Contains(Seeded.Passed, said, StringComparison.Ordinal);
        Assert.Contains("next look", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_agent_tool_takes_the_role_disputes_answer_so_none_is_offered()
    {
        OnBranch();
        await RunPassAsync(Runtime(), Bound);

        var settled = StepSettles.Settle(Files, new StepSettleAsk(RunId, "opinion", "accepted", "read it", new Dictionary<string, string>()),
            new SettleBy("agent:opinion") { Session = "s-reviewer" }, () => _now, ["s-worker"]);

        Assert.Equal(RunNodeWords.NotWaiting, settled.Refused);
        Assert.False(AgentSettles.Tool(Node(), RunStatus("opinion")));
    }

    /// <summary>
    /// A role dispute the place casts to the person is their row in <i>What needs you</i>: no review's Accept card carries an opinion's hold,
    /// and the gate's own row lists only a dispute the policy leaves the owner's, so without the row it would wait unseen.
    /// </summary>
    [Fact]
    public void A_role_dispute_the_place_casts_to_the_person_is_their_row()
    {
        var record = new NodeRecord(RunUnit)
        {
            Statuses = new Dictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["opinion"] = new(RunUnit, "opinion", NodeStates.Active) { Attempt = 1, Holds = OpinionGateReading.RoleDisputed, Since = _now },
            },
        };

        var row = HeldSteps.Step(RunId, RunHome.Repository, "default", null, Node(), record, cast: _ => CastHolders.Person);
        var review = HeldSteps.Step(RunId, RunHome.Repository, "default", null, new("review", NodeKinds.Review, 1, NodeReach.Pressed),
            new NodeRecord(RunUnit)
            {
                Statuses = new Dictionary<string, NodeStatus>(StringComparer.Ordinal) { ["review"] = new(RunUnit, "review", NodeStates.Active) { Attempt = 1 } },
            },
            cast: _ => CastHolders.Person);

        Assert.Equal((PersonWaits.Accept, StepHolders.Person, StepRoles.Opinion), (row?.Why, row?.Holder, row?.Role));
        Assert.Null(review);
    }

    private static NodeDeclaration Node() => new("opinion", NodeKinds.Opinion, 1, NodeReach.Observed);
}
