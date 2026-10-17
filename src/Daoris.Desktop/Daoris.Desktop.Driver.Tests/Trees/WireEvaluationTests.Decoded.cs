using System.Reflection;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-WIRE1g (the fabric events design §E2, §E4, §E7): the decoded nodes evaluated. A <c>stop</c> sink is reached once on the receipt
/// of the path into it, and never on a success, a skip, an unknown, a retryable failure or another run's failure; an <c>opinion</c> is
/// reached, pending or passed over on its gate's judgement or on the opinion a recorded landing kept, and invents nothing; and the decoded
/// order that holds a look behind a disputed opinion, which a schema-1 run does not, is recorded as it stands.
/// </summary>
public sealed partial class WireEvaluationTests
{
    private const string Repository = "engine";

    private static NodeDeclaration Sink(string id) => Node(id, NodeKinds.Stop) with { Synthetic = true };

    /// <summary>A D157 step with its kind's defaults, as a stored version holds it, the fields named overriding them.</summary>
    private static NamedStep Step(string id, string kind, params (string Name, object? Value)[] fields) =>
        new(id, kind, [.. WorkflowKindTable.Of(kind)!.Fields.Select(field => new WorkflowSetting(field.Name, fields.FirstOrDefault(each => each.Name == field.Name) is { Name: not null } named ? named.Value : field.Default))]);

    /// <summary>The branch unit the opinion's gate reads for: its repository, its tip, and the landings recorded for it.</summary>
    private static WorkUnit Branch(params LandedBranch[] landings) =>
        new(Unit, WorkUnitKind.Branch, new WorkOwner(Repository, "default", SessionBranches.FileName, WorkRecord.DriverWriter),
            new WorkSubject("daoris/s1", "tip-1"), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "not landed yet"), [])
        {
            Records = new WorkUnitRecords(null, landings, null),
        };

    private static LandedBranch Landed(LandingOpinion? opinion, string tip = "tip-1") =>
        new(Repository, "default", "feature/x", "main", tip, "s1", "q1", null, Then) { Units = [Unit], Opinion = opinion };

    /// <summary>The gate as <see cref="OpinionGate.JudgeAsync"/> gives it, at the bound tip, under a rule that requires an opinion or not.</summary>
    private static OpinionGateState Gate(string state, bool required = false) =>
        new(state, Repository)
        {
            Rule = new ResolvedOpinion(new OpinionRule(["codex"], [OpinionRules.Landing], Required: required), OpinionSource.Repository),
            Tip = "tip-1",
        };

    private static WireRun Judged(OpinionGateState? gate, WorkUnit? unit = null) => Base with
    {
        Units = new Dictionary<string, WorkUnit> { [Unit] = unit ?? Branch() },
        Opinions = gate is null ? new Dictionary<string, OpinionGateState>() : new Dictionary<string, OpinionGateState> { [Unit] = gate },
    };

    /// <summary>An opinion before a look, its gate's judgement handed in, the opinion started on the binding offered.</summary>
    private static Graph Opinioned(OpinionGateState? gate, WorkUnit? unit = null)
    {
        var g = new Graph([Node("opinion", NodeKinds.Opinion), Node("look", NodeKinds.Look)], [After("opinion>look", "opinion", "look")]) { Run = Judged(gate, unit) };
        g.Start(g.Evaluate(), "opinion");
        return g;
    }

    public static IEnumerable<string> GateStates() =>
        typeof(OpinionGateStates).GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()!);

    public static TheoryData<string, bool> EveryGateState()
    {
        var data = new TheoryData<string, bool>();
        foreach (var state in GateStates())
        {
            data.Add(state, false);
            data.Add(state, true);
        }

        return data;
    }

    // ---- the sink

    /// <summary>
    /// The row's proof: a decoded check's <c>failed: stop</c> takes its sink, reached once on that failure's receipt, by its path. Evaluated
    /// again and replayed across a restart it reaches nothing more, and what follows the check, the graph's end, is never reached by it.
    /// </summary>
    [Fact]
    public void A_sink_is_reached_once_on_its_failures_receipt_and_never_reaches_the_end()
    {
        List<NamedStep> steps =
        [
            Step("work", WorkflowKinds.Work),
            Step("land", WorkflowKinds.Landing, ("form", LandingForm.Merge), ("accept", "you")),
            Step("lint", WorkflowKinds.Check, ("plugin", "acme.ci"), ("failed", "stop")),
            Step("release", WorkflowKinds.Stage, ("plugin", "acme.deploy"), ("stage", "release"), ("start", "automatic")),
        ];
        var decoded = WorkflowGraphs.Graph(steps);
        Assert.Equal("release", decoded.End);
        var g = new Graph([.. decoded.Nodes], [.. decoded.Wires]);
        g.Through("work");
        g.Through("land");
        g.Start(g.Evaluate(), "lint");
        g.Fail("lint");
        g.Start(g.Evaluate(), "lint:failed");

        var reaches = new List<WireMove>();
        for (var again = 0; again < 3; again++) reaches.AddRange(g.Evaluate().Moves.Where(move => move.Move.To == NodeStates.Reached));

        var reach = Assert.Single(reaches);
        var sink = g.Statuses["lint:failed"];
        var receipt = Assert.Single(sink.Current!.PrerequisiteReceipts);
        Assert.Equal(("lint:failed", WireWhy.Path, receipt, NodeSource.Path), (reach.Node, reach.Why, reach.Detail, sink.By));
        Assert.StartsWith("fld-", receipt, StringComparison.Ordinal);
        Assert.Equal(NodeStates.Blocked, g.State("release"));

        var restarted = WireEvaluation.Evaluate([.. decoded.Nodes], [.. decoded.Wires], new Dictionary<string, NodeStatus>(g.Statuses), g.Run);
        Assert.True(restarted.Settled);
        Assert.Empty(restarted.Starts);
        Assert.Equal(sink, restarted.Statuses["lint:failed"]);
    }

    /// <summary>
    /// The trap of a sink reached on the wrong receipt: a success, a skip, an unknown, a failure Daoris still retries and another run's
    /// failure each leave it unstarted; one reached in its own run lapses in another; a sink started on a success never reaches; and a
    /// go-ahead's refusal takes its sink where its yes does not.
    /// </summary>
    [Fact]
    public void A_sink_is_never_reached_on_a_success_a_skip_an_unknown_a_retryable_failure_or_another_runs_failure()
    {
        NodeDeclaration[] nodes = [Node("d") with { Retry = new NodeRetry(3, [0]) }, Sink("d:failed")];
        Wire[] wires = [Failed("d>d:failed", "d", "d:failed")];
        Graph Started()
        {
            var g = new Graph(nodes, wires);
            g.Start(g.Evaluate(), "d");
            return g;
        }

        var succeeded = Started();
        succeeded.Reach("d");
        var skipped = Started();
        skipped.Skip("d");
        var unknown = Started();
        unknown.Move("d", new NodeMove(NodeStates.Unknown, NodeCause.Daoris) { Attempt = 1 });
        var retrying = Started();
        retrying.Fail("d", transient: true);
        var elsewhere = Started();
        elsewhere.Fail("d");
        elsewhere.Run = Base with { Run = "run-2" };
        foreach (var (why, g) in new[] { ("success", succeeded), ("skip", skipped), ("unknown", unknown), ("retried", retrying), ("another run", elsewhere) })
        {
            g.Evaluate();
            g.Evaluate();
            Assert.Equal((why, NodeStates.Blocked), (why, g.State("d:failed")));
        }

        var there = Started();
        there.Fail("d");
        there.Start(there.Evaluate(), "d:failed");
        there.Evaluate();
        Assert.Equal(NodeStates.Reached, there.State("d:failed"));
        there.Run = Base with { Run = "run-2" };
        Assert.Equal(WireWhy.Upstream, there.Evaluate().Moves.Single(move => move.Node == "d:failed").Why);
        there.Evaluate();
        Assert.Equal(NodeStates.Blocked, there.State("d:failed"));

        var authored = new Graph([Node("a"), Sink("s")], [After("a>s", "a", "s")]);
        authored.Through("a");
        authored.Start(authored.Evaluate(), "s");
        Assert.DoesNotContain(authored.Evaluate().Moves, move => move.Node == "s");
        Assert.Equal(NodeStates.Active, authored.State("s"));

        NodeDeclaration[] asked = [Node("go", NodeKinds.GoAhead), Sink("go:rejected"), Node("next")];
        Wire[] paths = [After("go>next", "go", "next"), Rejected("go>go:rejected", "go", "go:rejected")];
        var refused = new Graph(asked, paths);
        refused.Start(refused.Evaluate(), "go");
        refused.Reject("go");
        refused.Start(refused.Evaluate(), "go:rejected");
        refused.Evaluate();
        Assert.Equal((NodeStates.Reached, NodeStates.Blocked), (refused.State("go:rejected"), refused.State("next")));
        Assert.StartsWith("rej-", Assert.Single(refused.Statuses["go:rejected"].Current!.PrerequisiteReceipts), StringComparison.Ordinal);

        var yes = new Graph(asked, paths);
        yes.Start(yes.Evaluate(), "go");
        yes.Reach("go");
        yes.Evaluate();
        Assert.Equal((NodeStates.Blocked, NodeStates.Ready), (yes.State("go:rejected"), yes.State("next")));
    }

    // ---- the opinion

    /// <summary>
    /// The row's proof, over every state the gate has, under a rule that requires an opinion and one that does not: what lets the work go
    /// is reached (an answer covering the tip, or the person's in its place) or passed over (none asked here, or an optional one none could
    /// be had), as D157's run reads it; everything else is pending, never failed. A skip is the gate's, in no one's words.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryGateState))]
    public void An_opinion_reads_every_state_of_its_gate_required_and_optional(string state, bool required)
    {
        var gate = Gate(state, required);
        var g = Opinioned(gate);
        g.Evaluate();
        g.Evaluate();

        var expected = state switch
        {
            OpinionGateStates.Settled or OpinionGateStates.Anyway or OpinionGateStates.Myself or OpinionGateStates.Answered => NodeStates.Reached,
            OpinionGateStates.None => NodeStates.Skipped,
            OpinionGateStates.Unavailable when !required => NodeStates.Skipped,
            _ => NodeStates.Active,
        };
        var opinion = g.Statuses["opinion"];
        Assert.Equal((state, required, expected), (state, required, opinion.State));
        Assert.Equal(gate.LetsGo, expected != NodeStates.Active);
        Assert.Equal(expected == NodeStates.Active ? NodeStates.Blocked : NodeStates.Ready, g.State("look"));
        if (expected != NodeStates.Active) Assert.Equal((NodeSource.Opinion, (string?)null), (opinion.By, opinion.Words));
        if (expected == NodeStates.Reached) Assert.StartsWith(OpinionNode.GateRecord + "#", opinion.Current!.Evidence!.Ref, StringComparison.Ordinal);
    }

    /// <summary>
    /// The trap of a stale or foreign opinion read as approval: a judgement at another commit, with no tip read, for another repository, not
    /// handed in at all (a teammate's work with no local record), unread because the service did not answer, or over records missing or
    /// corrupt (nothing asked) completes nothing and passes nothing over; and a newer commit lapses an opinion bound to the older one.
    /// </summary>
    [Fact]
    public void An_opinion_judged_elsewhere_or_not_read_invents_nothing()
    {
        var settled = Gate(OpinionGateStates.Settled);
        foreach (var (why, gate) in new (string, OpinionGateState?)[]
        {
            ("judged at an older commit", settled with { Tip = "tip-0" }),
            ("no tip read", settled with { Tip = null }),
            ("another repository's", settled with { Repository = "shop" }),
            ("none handed in", null),
            ("the service did not answer", Gate(OpinionGateStates.Unread) with { Problem = "the service did not answer" }),
            ("its records missing or corrupt", Gate(OpinionGateStates.NotAsked)),
            ("passed over at an older commit", Gate(OpinionGateStates.None) with { Tip = "tip-0" }),
        })
        {
            var g = Opinioned(gate);
            var evaluated = g.Evaluate();
            Assert.Equal((why, NodeStates.Active, NodeStates.Blocked, 0), (why, g.State("opinion"), g.State("look"), evaluated.Moves.Count));
        }

        var moved = Opinioned(settled);
        moved.Run = moved.Run with { Revisions = new Dictionary<string, string?> { [Unit] = "tip-2" } };
        Assert.Equal(WireWhy.Lapsed, moved.Evaluate().Moves.Single(move => move.Node == "opinion").Why);
        moved.Start(moved.Evaluate(), "opinion");
        moved.Evaluate();
        Assert.Equal((NodeStates.Active, NodeStates.Blocked), (moved.State("opinion"), moved.State("look")));
    }

    /// <summary>
    /// The trap of <i>lets go</i> read as <i>reached</i>: the gate's skip satisfies <c>after</c> and no <c>data</c> wire, holds for its own
    /// run alone, and, being the gate's judgement of its commit, lapses on a newer one, where the person's skip of the look holds.
    /// </summary>
    [Fact]
    public void The_gates_skip_satisfies_after_gives_no_output_and_lapses_outside_its_run_or_commit()
    {
        NodeDeclaration[] nodes =
        [
            Node("opinion", NodeKinds.Opinion), Node("look", NodeKinds.Look),
            Node("deploy") with { Inputs = Ports("opinion", BuiltInNodes.EvidenceSchema(NodeKinds.Opinion)) },
        ];
        Wire[] wires = [After("a", "opinion", "look"), Data("b", "opinion", WireEnd.Evidence, "deploy", "opinion")];
        Graph Skipped()
        {
            var g = new Graph(nodes, wires) { Run = Judged(Gate(OpinionGateStates.None)) };
            g.Start(g.Evaluate(), "opinion");
            return g;
        }

        var g = Skipped();
        var evaluated = g.Evaluate();
        Assert.Equal((NodeStates.Skipped, NodeStates.Ready, NodeStates.Blocked), (g.State("opinion"), g.State("look"), g.State("deploy")));
        Assert.StartsWith("skp-", Assert.Single(evaluated.Of("look")!.Receipts), StringComparison.Ordinal);
        Assert.Equal(["b"], evaluated.Of("deploy")!.Waiting);

        g.Run = g.Run with { Run = "run-2" };
        Assert.Equal(WireWhy.SkipScope, g.Evaluate().Moves.Single(move => move.Node == "opinion").Why);
        Assert.Equal(NodeStates.Blocked, g.State("look"));

        var newer = Skipped();
        newer.Evaluate();
        newer.Skip("look");
        newer.Run = newer.Run with { Revisions = new Dictionary<string, string?> { [Unit] = "tip-2" } };
        var lapsed = newer.Evaluate();
        Assert.Equal((WireWhy.Lapsed, NodeStates.Invalidated, NodeStates.Skipped), (lapsed.Moves.Single(move => move.Node == "opinion").Why, newer.State("opinion"), newer.State("look")));
    }

    /// <summary>
    /// The trap of a historical opinion read anew: a landing recorded at the bound commit keeps today's reading of what its gate let go, as
    /// <c>WorkflowRun</c> reads a landing done, whatever the gate says now; one recorded at another commit says nothing of this one.
    /// </summary>
    [Fact]
    public void A_recorded_landings_opinion_keeps_todays_reading()
    {
        foreach (var (why, kept, state) in new (string, LandingOpinion?, string)[]
        {
            ("none kept: what its gate let go", null, NodeStates.Reached),
            ("settled", new LandingOpinion(OpinionGateStates.Settled) { Opinion = "op-1" }, NodeStates.Reached),
            ("the person went on", new LandingOpinion(OpinionGateStates.Anyway) { Words = "a typo" }, NodeStates.Reached),
            ("none could be had, and none was required", new LandingOpinion(OpinionGateStates.Unavailable) { Code = ReviewerUnavailable.NoReviewer }, NodeStates.Skipped),
        })
        {
            var g = Opinioned(Gate(OpinionGateStates.Disputed), Branch(Landed(kept)));
            g.Evaluate();
            Assert.Equal((why, state, NodeSource.Opinion), (why, g.State("opinion"), g.Statuses["opinion"].By));
        }

        var elsewhere = Opinioned(Gate(OpinionGateStates.Disputed), Branch(Landed(new LandingOpinion(OpinionGateStates.Settled), tip: "tip-0")));
        elsewhere.Evaluate();
        Assert.Equal(NodeStates.Active, elsewhere.State("opinion"));
    }

    /// <summary>
    /// The mismatch, recorded and not settled here (the parent's decision; D173's FABRIC-WIRE1g note): a schema-1 run lets the look go on
    /// beside a disputed opinion, whose Reviewed answers it (<c>WorkflowRunTests</c>, <i>the look goes on beside a dispute, whose Reviewed
    /// answers it</i>), and schema 1 is unchanged. A decoded version's order (§E7; GRAPH1's choice 5: every opinion reaches every look) holds
    /// the look until the opinion is reached, so the person's Reviewed could not answer the dispute there. WORKFLOW2b and the cutover settle it.
    /// </summary>
    [Fact]
    public void Mismatch_a_disputed_opinion_holds_a_decoded_look_that_a_schema_1_run_lets_go_on()
    {
        var decoded = WorkflowGraphs.Graph(
        [
            Step("work", WorkflowKinds.Work), Step("opinion", WorkflowKinds.Opinion), Step("look", WorkflowKinds.Look),
            Step("land", WorkflowKinds.Landing, ("form", LandingForm.Merge), ("accept", "you")),
        ]);
        var g = new Graph([.. decoded.Nodes], [.. decoded.Wires]) { Run = Judged(Gate(OpinionGateStates.Disputed) with { Disputes = new OpinionDisputes([1], []) }) };
        g.Through("work");
        g.Start(g.Evaluate(), "opinion");
        g.Evaluate();
        g.Evaluate();

        Assert.Equal((NodeStates.Active, NodeStates.Blocked, NodeStates.Blocked), (g.State("opinion"), g.State("look"), g.State("land")));
    }

    /// <summary>
    /// The trap of a read that writes: the evaluation takes no home, writer, port or performer, so what it could change is only what it is
    /// handed, and that stays as it was; evaluated again on the same, it says the same, so a replay keeps nothing twice.
    /// </summary>
    [Fact]
    public void Evaluating_the_decoded_nodes_changes_nothing_it_is_handed()
    {
        NodeDeclaration[] nodes = [Node("opinion", NodeKinds.Opinion), Node("d"), Sink("d:failed")];
        Wire[] wires = [Failed("d>d:failed", "d", "d:failed")];
        var g = new Graph(nodes, wires) { Run = Judged(Gate(OpinionGateStates.Settled)) };
        g.Start(g.Evaluate(), "d");
        g.Fail("d");
        var offered = g.Evaluate();
        g.Start(offered, "opinion");
        g.Start(offered, "d:failed");

        var handed = new Dictionary<string, NodeStatus>(g.Statuses);
        var kept = handed.ToDictionary(each => each.Key, each => each.Value);
        var opinions = g.Run.Opinions.ToDictionary(each => each.Key, each => each.Value);
        var once = WireEvaluation.Evaluate(nodes, wires, handed, g.Run);
        var twice = WireEvaluation.Evaluate(nodes, wires, handed, g.Run);

        Assert.Equal(kept, handed);
        Assert.Equal(opinions, g.Run.Opinions);
        Assert.Equal(2, once.Moves.Count(move => move.Move.To == NodeStates.Reached));
        Assert.Equal(once.Moves.Select(move => (move.Node, move.Move.To, move.Why)), twice.Moves.Select(move => (move.Node, move.Move.To, move.Why)));
        static IEnumerable<(string, string, string?, string?)> Said(WireEvaluated evaluated) =>
            evaluated.Statuses.OrderBy(each => each.Key, StringComparer.Ordinal).Select(each => (each.Key, each.Value.State, each.Value.By, each.Value.Current?.Id));
        Assert.Equal(Said(once), Said(twice));
    }
}
