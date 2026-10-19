using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-NODE1b (the fabric events design §E4, §E2): D157's gates as observed nodes, each from its gate's records alone and through the one
/// table, so a graph holding them has no second state model. The <c>look</c> is reached by the person's <i>reviewed</i> at the bound commit,
/// rejected by their <i>not yet</i> and passed over by their skip, in their words; a landing at the bound commit keeps the review that let it
/// go. The <c>go-ahead</c> is reached by the person's yes to its act and rejected by their refusal (D135's answers). Anything else waits.
/// </summary>
public sealed class GateNodesTests
{
    private const string Unit = "unit-a";
    private const string Tip = "tip-1";
    private const string Act = "deploy the release to production";

    private static readonly DateTimeOffset Then = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private static readonly NodeScope Scope = new("graph-1", "run-1");

    private static NodeDeclaration Node(string kind) => new(kind, kind, 1, NodeAuthorities.BuiltIn[kind].Reach)
    {
        Options = kind == NodeKinds.GoAhead ? new Dictionary<string, string> { ["act"] = WorkflowCurrent.Quoted(Act) } : new Dictionary<string, string>(),
    };

    private static NodeAcceptance Binding(string kind) => new(Unit, Tip, Scope.GraphDigest, kind, 1, 1);

    private static NodeStatus Active(string kind) =>
        new(Unit, kind, NodeStates.Active) { Attempt = 1, Tries = 1, Binding = Binding(kind), Scope = Scope };

    private static LandedBranch Landed(string tip = Tip, LandingReview? review = null) =>
        new("engine", "default", "feature/q1-ship", "main", tip, "s1", "q1", null, Then) { Units = [Unit], Review = review };

    private static WorkUnit Branch(params LandedBranch[] landings) => new(Unit, WorkUnitKind.Branch,
        new WorkOwner("engine", "default", "session-branches.json", WorkRecord.DriverWriter), new WorkSubject("daoris/s1", Tip),
        new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), [])
    {
        Records = new WorkUnitRecords(null, landings, null),
    };

    private static ReviewGateState Gate(string state, QuestReviewVerdictView? verdict = null, string? tip = Tip, string? skipped = null, string repository = "engine") =>
        new(state, new ReviewDecision(skipped is null ? ReviewLevels.Repository : ReviewLevels.Skip, skipped is null ? "preview" : null)
        {
            Repository = repository,
            Skip = skipped is null ? null : new QuestReviewVerdictView(ReviewVerdicts.Skipped) { Words = skipped, At = Then },
        })
        {
            Tip = tip, Verdict = verdict,
        };

    private static GoAheadView Asked(string act, bool? approved, string? words = null, int number = 1) =>
        new(number, "write", "production", act, [new GoAheadRequestView("s1", "q1", Then, "to ship")])
        {
            Answer = approved is { } yes ? new GoAheadAnswerView(yes, words, Then) : null,
        };

    /// <summary>The observation applied as the evaluation applies it: through the one table, a skip scoped to its attempt's run.</summary>
    private static NodeMoved Applied(NodeDeclaration node, NodeObservation seen)
    {
        Assert.Null(seen.Refused);
        var move = Assert.IsType<NodeMove>(seen.Move);
        return NodeLifecycle.Apply(node, Active(node.Kind), move.To == NodeStates.Skipped ? move with { Scope = Scope } : move);
    }

    [Fact]
    public void A_look_is_reached_by_the_persons_reviewed_at_the_bound_commit_and_rejected_by_their_not_yet()
    {
        var look = Node(NodeKinds.Look);

        var reviewed = Applied(look, LookNode.Observe(Branch(), look, Binding(NodeKinds.Look),
            Gate(ReviewStates.Reviewed, new QuestReviewVerdictView(ReviewVerdicts.Reviewed) { Commit = Tip, At = Then })));
        Assert.Null(reviewed.Refused);
        Assert.Equal((NodeStates.Reached, NodeSource.Person), (reviewed.Status.State, reviewed.Status.By));
        Assert.Equal(Binding(NodeKinds.Look).Id, reviewed.Status.Acceptance!.Id);

        var notYet = Applied(look, LookNode.Observe(Branch(), look, Binding(NodeKinds.Look),
            Gate(ReviewStates.NotYet, new QuestReviewVerdictView(ReviewVerdicts.NotYet) { Words = "the chart is cut off", At = Then })));
        Assert.Null(notYet.Refused);
        Assert.Equal((NodeStates.Rejected, NodeSource.Person, "the chart is cut off"), (notYet.Status.State, notYet.Status.By, notYet.Status.Words));
    }

    [Fact]
    public void A_look_the_person_skipped_is_passed_over_in_their_words_and_never_without_them()
    {
        var look = Node(NodeKinds.Look);

        var skipped = Applied(look, LookNode.Observe(Branch(), look, Binding(NodeKinds.Look), Gate(ReviewStates.Skipped, skipped: "a copy edit")));
        Assert.Null(skipped.Refused);
        Assert.Equal((NodeStates.Skipped, NodeSource.Person, "a copy edit", Scope), (skipped.Status.State, skipped.Status.By, skipped.Status.Words, skipped.Status.Scope));

        // A skip kept with no words is not the person's word here: the look waits rather than pass over on words nobody gave.
        Assert.Null(LookNode.Observe(Branch(), look, Binding(NodeKinds.Look), Gate(ReviewStates.Skipped, skipped: "")).Move);
    }

    [Fact]
    public void A_look_waits_on_every_other_state_and_on_a_judgement_of_another_commit_or_repository()
    {
        var look = Node(NodeKinds.Look);
        var reviewed = new QuestReviewVerdictView(ReviewVerdicts.Reviewed) { Commit = Tip, At = Then };
        foreach (var (why, gate) in new (string, ReviewGateState?)[]
        {
            ("no read", null),
            ("not shown", Gate(ReviewStates.NotShown)),
            ("being set up", Gate(ReviewStates.BeingSetUp)),
            ("shown", Gate(ReviewStates.Shown)),
            ("not held", Gate(ReviewStates.NotHeld, reviewed)),
            ("unread", Gate(ReviewStates.Unread)),
            ("cannot start", Gate(ReviewStates.CannotStart)),
            ("a not yet with no words", Gate(ReviewStates.NotYet, new QuestReviewVerdictView(ReviewVerdicts.NotYet) { At = Then })),
            ("reviewed at another commit", Gate(ReviewStates.Reviewed, reviewed, tip: "tip-2")),
            ("reviewed with no commit read", Gate(ReviewStates.Reviewed, reviewed, tip: null)),
            ("reviewed for another repository", Gate(ReviewStates.Reviewed, reviewed, repository: "other")),
        })
        {
            var seen = LookNode.Observe(Branch(), look, Binding(NodeKinds.Look), gate);
            Assert.Equal((why, (string?)null, (NodeMove?)null), (why, seen.Outcome, seen.Move));
        }
    }

    [Fact]
    public void A_landing_recorded_at_the_bound_commit_keeps_the_review_that_let_it_go()
    {
        var look = Node(NodeKinds.Look);
        NodeObservation Seen(params LandedBranch[] landings) => LookNode.Observe(Branch(landings), look, Binding(NodeKinds.Look), Gate(ReviewStates.Shown));

        var reviewed = Applied(look, Seen(Landed(review: new LandingReview(ReviewVerdicts.Reviewed, "preview", "q2") { Commit = Tip, At = Then })));
        Assert.Equal((NodeStates.Reached, NodeSource.Person), (reviewed.Status.State, reviewed.Status.By));

        var skipped = Applied(look, Seen(Landed(review: new LandingReview(ReviewVerdicts.Skipped, null, "q1") { Words = "a copy edit", At = Then })));
        Assert.Equal((NodeStates.Skipped, NodeSource.Person, "a copy edit"), (skipped.Status.State, skipped.Status.By, skipped.Status.Words));

        // A landing that kept no review had none asked of it by the review's gate, whose outcome the landing keeps: that record passes the
        // look over, never in anyone's words.
        var none = Applied(look, Seen(Landed()));
        Assert.Equal((NodeStates.Skipped, NodeSource.Review, (string?)null), (none.Status.State, none.Status.By, none.Status.Words));

        // A landing at another commit says nothing of this one: the gate's own reading stands, and it waits.
        Assert.Null(Seen(Landed(tip: "tip-0", review: new LandingReview(ReviewVerdicts.Reviewed, "preview", "q2") { Commit = "tip-0" })).Move);
    }

    [Fact]
    public void A_go_ahead_is_reached_by_the_persons_yes_to_its_act_and_rejected_by_their_refusal()
    {
        var goAhead = Node(NodeKinds.GoAhead);
        NodeObservation Seen(params GoAheadView[] asked) => GoAheadNode.Observe(Branch(), goAhead, Binding(NodeKinds.GoAhead), asked);

        var yes = Applied(goAhead, Seen(Asked("another act", false, "no"), Asked(Act, true, number: 2)));
        Assert.Equal((NodeStates.Reached, NodeSource.Person), (yes.Status.State, yes.Status.By));

        var no = Applied(goAhead, Seen(Asked(Act, false, "not before the freeze ends")));
        Assert.Equal((NodeStates.Rejected, "not before the freeze ends"), (no.Status.State, no.Status.Words));

        // Asked and unanswered, refused with no reason, another act's answer, or none read: it waits, never reached or rejected on a guess.
        foreach (var (why, asked) in new (string, GoAheadView[]?)[]
        {
            ("unanswered", [Asked(Act, null)]),
            ("refused with no words", [Asked(Act, false)]),
            ("another act", [Asked("another act", true)]),
            ("none asked", []),
            ("none read", null),
        })
        {
            var seen = GoAheadNode.Observe(Branch(), goAhead, Binding(NodeKinds.GoAhead), asked);
            Assert.Equal((why, (NodeMove?)null), (why, seen.Move));
        }

        // A go-ahead that names no act is answered by none.
        Assert.Null(GoAheadNode.Observe(Branch(), goAhead with { Options = new Dictionary<string, string>() }, Binding(NodeKinds.GoAhead), [Asked(Act, true)]).Move);
    }

    /// <summary>
    /// FABRIC-RUN1c, found wiring the live gate in: a gate whose rule asks no second opinion here judges no commit, and a landing door's read of
    /// it carries none (<c>TreeGates.OpinionAsync</c>), so the opinion is passed over whatever commit its node is bound to, a quest's none
    /// included. Every other state still waits on a judgement of the bound commit.
    /// </summary>
    [Fact]
    public void An_opinion_whose_rule_asks_none_is_passed_over_whatever_commit_its_gate_read()
    {
        var opinion = Node(NodeKinds.Opinion);
        foreach (var (unit, binding) in new[] { (Branch(), Binding(NodeKinds.Opinion)), (Branch(), Binding(NodeKinds.Opinion) with { SubjectRevision = null }) })
        {
            var none = Applied(opinion, OpinionNode.Observe(unit, opinion, binding, new OpinionGateState(OpinionGateStates.None, "engine")));
            Assert.Equal((NodeStates.Skipped, NodeSource.Opinion), (none.Status.State, none.Status.By));
        }

        Assert.Null(OpinionNode.Observe(Branch(), opinion, Binding(NodeKinds.Opinion), new OpinionGateState(OpinionGateStates.Settled, "engine")).Move);
        Assert.Null(OpinionNode.Observe(Branch(), opinion, Binding(NodeKinds.Opinion), new OpinionGateState(OpinionGateStates.None, "other")).Move);
        // A none read at a commit is that commit's (FABRIC-WIRE1g): at another, it says nothing of this one.
        Assert.Null(OpinionNode.Observe(Branch(), opinion, Binding(NodeKinds.Opinion), new OpinionGateState(OpinionGateStates.None, "engine") { Tip = "tip-0" }).Move);
    }

    /// <summary>Through the evaluation: the look's gate and the go-ahead's answers handed in by unit move their nodes, and what is wired after them follows.</summary>
    [Fact]
    public void The_evaluation_moves_a_look_and_a_go_ahead_on_their_records_and_their_wires_follow()
    {
        NodeDeclaration[] nodes = [Node(NodeKinds.Look), Node(NodeKinds.GoAhead), new("land", NodeKinds.Handed, 1, NodeReach.Observed)];
        Wire[] wires =
        [
            new("a", WireKinds.After, new WireEnd(NodeKinds.Look), new WireEnd("land")),
            new("b", WireKinds.After, new WireEnd(NodeKinds.GoAhead), new WireEnd("land")),
        ];
        var statuses = new Dictionary<string, NodeStatus>(StringComparer.Ordinal)
        {
            [NodeKinds.Look] = Active(NodeKinds.Look),
            [NodeKinds.GoAhead] = Active(NodeKinds.GoAhead),
            ["land"] = NodeLifecycle.Start(Unit, "land"),
        };
        var run = new WireRun(Scope.GraphDigest, Scope.Run)
        {
            Revisions = new Dictionary<string, string?> { [Unit] = Tip },
            Units = new Dictionary<string, WorkUnit> { [Unit] = Branch() },
            Reviews = new Dictionary<string, ReviewGateState>
            {
                [Unit] = Gate(ReviewStates.Reviewed, new QuestReviewVerdictView(ReviewVerdicts.Reviewed) { Commit = Tip, At = Then }),
            },
            GoAheads = new Dictionary<string, IReadOnlyList<GoAheadView>> { [Unit] = [Asked(Act, true)] },
        };

        var evaluated = WireEvaluation.Evaluate(nodes, wires, statuses, run);

        Assert.Empty(evaluated.Refused);
        Assert.Equal((NodeStates.Reached, NodeStates.Reached, NodeStates.Ready),
            (evaluated.Statuses[NodeKinds.Look].State, evaluated.Statuses[NodeKinds.GoAhead].State, evaluated.Statuses["land"].State));
    }
}
