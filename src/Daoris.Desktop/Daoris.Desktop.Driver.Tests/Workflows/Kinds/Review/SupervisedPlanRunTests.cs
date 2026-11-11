using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A system's run whose plan a landing supervisor accepts, through the node runtime and the release's own check
/// (<see cref="LandingReleaseLine"/>): a go-ahead after the review still holds the write after the supervisor's Accept until the person's yes;
/// a released Accept whose record moved before the pass is dropped <c>moved</c>; and a decision held from before the record, naming no revision,
/// goes stale <c>moved</c> at its release on work that is no branch.
/// </summary>
public sealed class SupervisedPlanRunTests : NodeRunFixture
{
    private const string System = "acme.console/store";

    private const string PlanGoAheadApply = """
        {"schemaVersion":2,"entries":["work"],"end":"apply",
         "nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"requests","kind":"work.requests","version":1,"reach":"observed"},
                  {"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"go","kind":"go-ahead","version":1,"reach":"pressed","options":{"act":"write the store"}},
                  {"id":"apply","kind":"apply","version":1,"reach":"performed"}],
         "wires":[{"id":"a","kind":"after","from":"work","to":"requests"},{"id":"b","kind":"after","from":"requests","to":"review"},
                  {"id":"c","kind":"after","from":"review","to":"go"},{"id":"d","kind":"after","from":"go","to":"apply"},
                  {"id":"e","kind":"back","from":"review","on":"rejected","to":"work"}]}
        """;

    private sealed class Admits : IDecisionAdmission
    {
        public DecisionAdmitted Admit(string kind, RolePlace place, string? act) => new("g-1", null) { Window = TimeSpan.FromMinutes(10) };
    }

    /// <summary>The door the runtime hands an apply to: it keeps the start, the write the test watches for.</summary>
    private sealed class Held : IApplyDoor
    {
        public ApplyStart? Start { get; private set; }

        bool IApplyDoor.Start(ApplyStart start)
        {
            Start = start;
            return true;
        }
    }

    private readonly Held _held = new();

    private QuestionSupervisorDecisions? _decisions;

    private QuestionSupervisorDecisions Decisions => _decisions ??= new(_home, new Admits()) { LandingApplied = LandingApplied.Of(_home) };

    private NodeRuntime Applying() => new(Files, _port, () => _now, () => true)
    {
        Doors = NodeDoors.None.With(NodeKinds.Apply, _held), Landings = Decisions.LandingWords,
    };

    private void Plan(string value) =>
        new SystemPlans(_home).Keep("rec-1", System, [new SystemOperation("release", "update", "1", value, new SystemPrecondition("etag", "e1"))], _now);

    /// <summary>The run's quest done, its handback numbered <paramref name="sequence"/>: a new one moves the review record's revision.</summary>
    private WorkUnit Quest(long sequence = 1) => RunHome.QuestUnit(RunHome.Quest(RunId, "Done") with
    {
        From = "ask:a1", Submission = new QuestSubmissionView("m1", sequence, _now, $"plan {sequence}"),
    }) with { Executors = ["rec-1"] };

    private static WorkUnit Ask(params GoAheadView[] goAheads) =>
        Assert.Single(AskUnits.Read([new AskView("a1", "default", "Write the store.", "Published", "repository") { Quests = [RunId], GoAheads = goAheads }]));

    private async Task<NodeRuntime> ReviewingAsync(string graph)
    {
        BoundTo(graph, ask: "a1");
        _units = [Quest(), Ask()];
        Plan("2");
        var runtime = Applying();
        await RunPassAsync(runtime);
        Assert.Equal((NodeStates.Active, 1), (RunStatus("review").State, RunStatus("review").Attempt));
        return runtime;
    }

    private async Task<SupervisorKept> AcceptedAsync(string? revision)
    {
        var landing = new SupervisorLanding("s2", RunId, "review", 1, "g-1", SupervisorLandingActs.Accept,
            [new SupervisorReason("The plan is the brief's.", "the record")], new SupervisorHolder(RoleHolders.Terminal, null)) { Revision = revision };
        var kept = await Decisions.LandingAsync(landing, "store", null, _now);
        Assert.True(kept.Kept, kept.Why);
        return kept;
    }

    private Task<IReadOnlyList<SupervisorState>> ReleaseAsync() =>
        Decisions.ReleaseLandingsAsync(LandingReleaseLine.Of(_home, Files, _ => false), _now.AddMinutes(11));

    [Fact]
    public async Task A_go_ahead_after_the_review_holds_the_write_after_the_supervisors_accept_until_the_persons_yes()
    {
        var runtime = await ReviewingAsync(PlanGoAheadApply);
        var kept = await AcceptedAsync(SupervisorReviewBinding.RevisionOf(_home, RunId, "review", 1));

        Assert.Equal([SupervisorState.Released], await ReleaseAsync());
        await RunPassAsync(runtime);

        Assert.Equal((NodeStates.Reached, NodeSource.Supervisor), (RunStatus("review").State, RunStatus("review").By));
        Assert.Equal("went-on", RunReviewSupervisor.Of(Decisions.LandingOn(RunId, "review", 1), null)!.State);
        Assert.Equal(NodeStates.Active, RunStatus("go").State);
        // No supervisor decides a go-ahead: the step is offered to none, so the write waits for the person.
        Assert.Null(LandingSteps.Find(_home, RunId, "go", RunStatus("go").Attempt));
        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Blocked, RunStatus("apply").State);
        Assert.Null(_held.Start);

        var yes = new GoAheadView(1, "write", "store", "write the store", [new GoAheadRequestView("s2", RunId, _now, "to write the plan")])
        {
            Answer = new GoAheadAnswerView(true, null, _now),
        };
        _units = [Quest(), Ask(yes)];
        await RunPassAsync(runtime);

        Assert.Equal((NodeStates.Reached, NodeStates.Active), (RunStatus("go").State, RunStatus("apply").State));
        Assert.NotNull(_held.Start);
        Assert.NotNull(kept.Decision);
    }

    /// <summary>A newer plan is no move of the record, which names its own plan; a corrected handback is, so the review asks again on its new record.</summary>
    [Fact]
    public async Task A_released_accept_whose_record_moved_before_the_pass_is_dropped_moved_and_the_review_asks_again()
    {
        var runtime = await ReviewingAsync(PlanGoAheadApply);
        var first = ReviewRecords.Read(_home, RunId, "review", 1)!;
        await AcceptedAsync(first.Revision);
        Assert.Equal([SupervisorState.Released], await ReleaseAsync());

        Assert.Equal(first.Revision, Assert.Single(Files.Read(RunUnit)!.Pending).Revision);
        _units = [Quest(2), Ask()];
        await RunPassAsync(runtime);

        var dropped = RunReviewSupervisor.Of(Decisions.LandingOn(RunId, "review", 1), first)!;
        Assert.Equal(("stale", ReviewMoved.Word), (dropped.State, dropped.Why));
        var review = RunStatus("review");
        Assert.Equal((NodeStates.Active, 2), (review.State, review.Attempt));
        Assert.NotEqual(first.Revision, ReviewRecords.Read(_home, RunId, "review", 2)?.Revision);
        Assert.Equal(NodeStates.Blocked, RunStatus("go").State);
        Assert.Null(_held.Start);
    }

    [Fact]
    public async Task A_decision_held_from_before_the_record_naming_no_revision_is_released_stale_moved_and_the_review_waits_for_the_person()
    {
        var runtime = await ReviewingAsync(PlanGoAheadApply);
        await AcceptedAsync(revision: null);

        Assert.Equal([SupervisorState.Stale], await ReleaseAsync());

        var dropped = RunReviewSupervisor.Of(Decisions.LandingOn(RunId, "review", 1), ReviewRecords.Read(_home, RunId, "review", 1))!;
        Assert.Equal(("stale", SupervisorReviewBinding.Moved, false), (dropped.State, dropped.Why, dropped.OnRecord));
        Assert.Empty(Files.Read(RunUnit)!.Pending);
        await RunPassAsync(runtime);
        Assert.Equal((NodeStates.Active, 1), (RunStatus("review").State, RunStatus("review").Attempt));
    }
}
