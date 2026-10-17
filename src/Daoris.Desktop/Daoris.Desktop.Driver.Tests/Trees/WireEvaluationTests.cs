using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-WIRE1b (the fabric events design §E3, §E2, §E7): the wires evaluated. Eligibility from current receipts, each moved through
/// the one table; an <c>anyOf</c>'s witness; a trigger once per receipt; a skip, a failure and a rejection each a scoped receipt read by
/// its own wires; forks within the limit; <c>all</c> joins; an invalidation lapsing downstream. Pure: nothing is stored.
/// </summary>
public sealed class WireEvaluationTests
{
    private const string Unit = "unit-a";
    private const string Plugin = "acme.deploy/deploy";
    private const string Preview = "acme.deploy/preview@1";
    private static readonly DateTimeOffset Then = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    private static readonly WireRun Base = new("graph-1", "run-1") { Revisions = new Dictionary<string, string?> { [Unit] = "tip-1" } };

    private static NodeDeclaration Node(string id, string kind = Plugin) =>
        new(id, kind, 1, NodeAuthorities.BuiltIn.TryGetValue(kind, out var builtIn) ? builtIn.Reach : NodeReach.Performed);

    private static Dictionary<string, string> Ports(string name, string schema) => new() { [name] = schema };

    private static Wire After(string id, string from, string to, WireGroup? group = null) => new(id, WireKinds.After, new(from), new(to)) { Group = group };

    private static Wire Trigger(string id, string from, string to) => new(id, WireKinds.Trigger, new(from), new(to));

    private static Wire Failed(string id, string from, string to) => new(id, WireKinds.Failed, new(from), new(to));

    private static Wire Rejected(string id, string from, string to) => new(id, WireKinds.Rejected, new(from), new(to));

    private static Wire Data(string id, string from, string output, string to, string input) => new(id, WireKinds.Data, new(from, output), new(to, input));

    /// <summary>A graph's nodes on one unit, with their statuses as a record writer would keep them between evaluations.</summary>
    private sealed class Graph(NodeDeclaration[] nodes, Wire[] wires)
    {
        private readonly HashSet<string> fired = new(StringComparer.Ordinal);

        public WireRun Run { get; set; } = Base;

        public Dictionary<string, NodeStatus> Statuses { get; } =
            nodes.ToDictionary(node => node.Id, node => NodeLifecycle.Start(Unit, node.Id), StringComparer.Ordinal);

        public string State(string id) => Statuses[id].State;

        private NodeDeclaration Declared(string id) => nodes.Single(node => node.Id == id);

        public WireEvaluated Evaluate()
        {
            var evaluated = WireEvaluation.Evaluate(nodes, wires, Statuses, Run with { Fired = fired.ToHashSet(StringComparer.Ordinal) });
            Assert.Empty(evaluated.Refused);
            foreach (var (id, status) in evaluated.Statuses) Statuses[id] = status;
            return evaluated;
        }

        /// <summary>The start the evaluation offered, applied through the one table, its firings recorded as the record writer keeps them.</summary>
        public void Start(WireEvaluated evaluated, string id)
        {
            var start = evaluated.Starts.Single(each => each.Node == id);
            Move(id, start.Move);
            foreach (var firing in start.Firings) fired.Add(firing.Key);
        }

        public void Move(string id, NodeMove move)
        {
            var moved = NodeLifecycle.Apply(Declared(id), Statuses[id], move);
            Assert.Equal((id, move.To, (string?)null), (id, move.To, moved.Refused));
            Statuses[id] = moved.Status;
        }

        public void Reach(string id, Dictionary<string, NodePort>? outputs = null)
        {
            var node = Declared(id);
            var status = Statuses[id];
            var authority = NodeAuthorities.Of(node)!;
            Move(id, new NodeMove(NodeStates.Reached, NodeCause.Authority)
            {
                Source = authority.ReachBy,
                Attempt = status.Attempt,
                Acceptance = status.Binding! with
                {
                    Evidence = authority.Evidenced ? new NodePort("acme/receipt@1", $"{id}#{status.Attempt}", status.Binding.SubjectRevision, null) : null,
                    Outputs = outputs ?? NodeAcceptance.NoPorts,
                },
            });
        }

        public void Fail(string id, bool transient = false) =>
            Move(id, new NodeMove(NodeStates.Failed, NodeCause.Authority)
            {
                Source = NodeAuthorities.Of(Declared(id))!.FailureBy, Attempt = Statuses[id].Attempt, Code = "deploy-failed", Transient = transient,
            });

        public void Reject(string id) => Move(id, new NodeMove(NodeStates.Rejected, NodeCause.Person) { Attempt = Statuses[id].Attempt, Words = "not this" });

        public void Skip(string id) => Move(id, new NodeMove(NodeStates.Skipped, NodeCause.Person) { Words = "not needed", Scope = Run.Scope });

        /// <summary>A node evaluated, started on the binding offered, and reached.</summary>
        public void Through(string id, Dictionary<string, NodePort>? outputs = null)
        {
            Start(Evaluate(), id);
            Reach(id, outputs);
        }
    }

    /// <summary>§E2, §E3: <c>after</c> reads a current reach or this run's skip; <c>data</c> a reach alone, passing its port into the binding.</summary>
    [Fact]
    public void After_reads_a_reach_or_a_skip_and_data_a_reach_alone()
    {
        NodeDeclaration[] nodes = [Node("a") with { Outputs = Ports("preview", Preview) }, Node("b"), Node("c") with { Inputs = Ports("preview", Preview) }];
        Wire[] wires = [After("w1", "a", "b"), Data("w2", "a", "preview", "c", "preview")];
        var reached = new Graph(nodes, wires);

        var first = reached.Evaluate();
        Assert.Equal((NodeStates.Ready, NodeStates.Blocked, NodeStates.Blocked), (reached.State("a"), reached.State("b"), reached.State("c")));
        Assert.Equal(["w1"], first.Of("b")!.Waiting);
        Assert.Equal(["w2"], first.Of("c")!.Waiting);

        reached.Start(first, "a");
        reached.Reach("a", new Dictionary<string, NodePort> { ["preview"] = new(Preview, "a.preview", "tip-1", null) });
        var second = reached.Evaluate();
        var accepted = reached.Statuses["a"].Current!;
        Assert.Equal((NodeStates.Ready, NodeStates.Ready), (reached.State("b"), reached.State("c")));
        Assert.Equal([accepted.Id], second.Of("b")!.Receipts);
        Assert.Equal(new NodeAcceptance(Unit, "tip-1", "graph-1", "b", 1, 1) { PrerequisiteReceipts = [accepted.Id] }.Id, second.Of("b")!.Binding!.Id);
        Assert.Equal(accepted.Outputs["preview"], second.Of("c")!.Binding!.Inputs["preview"]);
        Assert.Empty(second.Of("c")!.Receipts);

        var skipped = new Graph(nodes, wires);
        skipped.Skip("a");
        var third = skipped.Evaluate();
        Assert.Equal((NodeStates.Skipped, NodeStates.Ready, NodeStates.Blocked), (skipped.State("a"), skipped.State("b"), skipped.State("c")));
        Assert.StartsWith("skp-", Assert.Single(third.Of("b")!.Receipts), StringComparison.Ordinal);
        Assert.Equal(["w2"], third.Of("c")!.Waiting);
    }

    /// <summary>§E3 and canary 41: an <c>anyOf</c> records one witness, by wire-id order; losing it invalidates its consumer, and no other witness is taken silently.</summary>
    [Fact]
    public void An_anyOf_records_one_witness_and_losing_it_invalidates_its_consumer()
    {
        var either = new WireGroup("either", WireGroupModes.AnyOf);
        var g = new Graph([Node("a"), Node("b"), Node("t")], [After("w2", "b", "t", either), After("w1", "a", "t", either)]);
        var first = g.Evaluate();
        g.Start(first, "a");
        g.Start(first, "b");
        g.Reach("a");
        g.Reach("b");

        var both = g.Evaluate();
        var (a, b) = (g.Statuses["a"].Current!.Id, g.Statuses["b"].Current!.Id);
        Assert.Equal(NodeStates.Ready, g.State("t"));
        Assert.Equal("w1", both.Of("t")!.Witnesses["either"]);
        Assert.Equal([a], both.Of("t")!.Receipts);

        g.Start(both, "t");
        g.Reach("t");
        g.Move("a", new NodeMove(NodeStates.Invalidated, NodeCause.Daoris));
        var lost = g.Evaluate();
        var invalidated = lost.Moves.Single(move => move.Node == "t");
        Assert.Equal((NodeStates.Invalidated, WireWhy.Upstream, a), (g.State("t"), invalidated.Why, invalidated.Detail));
        Assert.Equal(NodeStates.Reached, g.State("b"));

        var again = g.Evaluate();
        Assert.Equal(NodeStates.Ready, g.State("t"));
        Assert.Equal("w2", again.Of("t")!.Witnesses["either"]);
        Assert.Equal([b], again.Of("t")!.Receipts);
    }

    /// <summary>§E3: an <c>allOf</c> waits on its last member, then consumes every member's receipt.</summary>
    [Fact]
    public void An_allOf_waits_on_its_last()
    {
        var every = new WireGroup("every", WireGroupModes.AllOf);
        var g = new Graph([Node("a"), Node("b"), Node("c"), Node("t")], [After("w1", "a", "t", every), After("w2", "b", "t", every), After("w3", "c", "t", every)]);
        var first = g.Evaluate();
        foreach (var id in new[] { "a", "b", "c" }) g.Start(first, id);
        g.Reach("a");
        g.Reach("b");

        var waiting = g.Evaluate();
        Assert.Equal((NodeStates.Blocked, false), (g.State("t"), waiting.Of("t")!.Eligible));
        Assert.Equal(["w3"], waiting.Of("t")!.Waiting);

        g.Reach("c");
        var last = g.Evaluate();
        Assert.Equal(NodeStates.Ready, g.State("t"));
        Assert.Equal(new[] { "a", "b", "c" }.Select(id => g.Statuses[id].Current!.Id).Order(StringComparer.Ordinal), last.Of("t")!.Receipts);
    }

    /// <summary>§E3: a trigger starts its target once per receipt: a second evaluation, a replay or a fresh binding of the target fires nothing; a new receipt fires once.</summary>
    [Fact]
    public void A_trigger_fires_once_per_receipt_across_evaluations()
    {
        var g = new Graph([Node("a"), Node("t")], [Trigger("w1", "a", "t")]);
        g.Through("a");
        var receipt = g.Statuses["a"].Current!.Id;
        var key = WireEvaluation.FiringKey(g.Run, "w1", receipt);

        var fires = g.Evaluate();
        Assert.Equal(NodeStates.Ready, g.State("t"));
        var firing = Assert.Single(fires.Starts.Single(start => start.Node == "t").Firings);
        Assert.Equal((key, "w1", receipt, "t"), (firing.Key, firing.Wire, firing.Receipt, firing.Node));

        var unstarted = g.Evaluate();
        Assert.True(unstarted.Settled);
        Assert.Equal(key, Assert.Single(unstarted.Starts.Single(start => start.Node == "t").Firings).Key);

        g.Start(unstarted, "t");
        var started = g.Evaluate();
        Assert.True(started.Settled);
        Assert.DoesNotContain(started.Starts, start => start.Node == "t");
        g.Reach("t");
        Assert.True(g.Evaluate().Settled);

        g.Move("t", new NodeMove(NodeStates.Invalidated, NodeCause.Daoris));
        var judged = g.Evaluate();
        Assert.Equal(NodeStates.Blocked, g.State("t"));
        Assert.Equal(["w1"], judged.Of("t")!.Waiting);
        Assert.True(g.Evaluate().Settled);
        Assert.Equal(NodeStates.Blocked, g.State("t"));

        g.Move("a", new NodeMove(NodeStates.Invalidated, NodeCause.Daoris));
        g.Through("a");
        var anew = g.Evaluate();
        var second = Assert.Single(anew.Starts.Single(start => start.Node == "t").Firings);
        Assert.Equal((NodeStates.Ready, g.Statuses["a"].Current!.Id), (g.State("t"), second.Receipt));
        Assert.NotEqual(key, second.Key);
    }

    /// <summary>
    /// A failure with its retries exhausted is a receipt its <c>failed</c> wire reads, and no other; a rejection one its <c>rejected</c> wire
    /// reads. Each is scoped: the person's new attempt takes it away, and another graph reads none of them.
    /// </summary>
    [Fact]
    public void A_failure_receipt_is_read_by_its_failed_wire_alone()
    {
        var d = Node("d") with { Retry = new NodeRetry(2, [0]), Rejectable = true };
        var g = new Graph([d, Node("roll", NodeKinds.Human), Node("next"), Node("redo", NodeKinds.Human)],
            [Failed("w1", "d", "roll"), After("w2", "d", "next"), Rejected("w3", "d", "redo")]);
        g.Start(g.Evaluate(), "d");
        g.Fail("d", transient: true);

        var retrying = g.Evaluate();
        Assert.Equal((NodeStates.Blocked, NodeStates.Blocked, NodeStates.Blocked), (g.State("roll"), g.State("next"), g.State("redo")));
        Assert.Equal(["w1"], retrying.Of("roll")!.Waiting);

        g.Move("d", new NodeMove(NodeStates.Ready, NodeCause.Daoris));
        g.Start(g.Evaluate(), "d");
        g.Fail("d", transient: true);
        var exhausted = g.Evaluate();
        Assert.Equal((NodeStates.Ready, NodeStates.Blocked, NodeStates.Blocked), (g.State("roll"), g.State("next"), g.State("redo")));
        Assert.StartsWith("fld-", Assert.Single(exhausted.Of("roll")!.Receipts), StringComparison.Ordinal);

        g.Move("d", new NodeMove(NodeStates.Ready, NodeCause.Person));
        var asked = g.Evaluate();
        Assert.Equal(NodeStates.Blocked, g.State("roll"));
        g.Start(asked, "d");
        g.Reject("d");
        var rejected = g.Evaluate();
        Assert.Equal((NodeStates.Blocked, NodeStates.Blocked, NodeStates.Ready), (g.State("roll"), g.State("next"), g.State("redo")));
        Assert.StartsWith("rej-", Assert.Single(rejected.Of("redo")!.Receipts), StringComparison.Ordinal);

        g.Run = g.Run with { GraphDigest = "graph-2" };
        var other = g.Evaluate();
        var lapsed = other.Moves.Single(move => move.Node == "d");
        Assert.Equal((NodeStates.Invalidated, WireWhy.Lapsed, NodeLapse.Graph), (g.State("d"), lapsed.Why, lapsed.Detail));
        Assert.Equal(NodeStates.Blocked, g.State("redo"));
    }

    /// <summary>§E3: a fork starts every eligible target within the limit, which counts dispatched attempts alone; the rest wait their turn.</summary>
    [Fact]
    public void A_fork_starts_its_targets_within_the_limit()
    {
        var g = new Graph([Node("s"), Node("t1"), Node("t2"), Node("t3"), Node("h", NodeKinds.Human)],
            [After("w1", "s", "t1"), After("w2", "s", "t2"), After("w3", "s", "t3"), After("w4", "s", "h")]) { Run = Base with { Limit = 2 } };
        g.Through("s");

        var fork = g.Evaluate();
        Assert.All(new[] { "t1", "t2", "t3", "h" }, id => Assert.Equal(NodeStates.Ready, g.State(id)));
        Assert.Equal(["h", "t1", "t2"], fork.Starts.Select(start => start.Node));
        Assert.Equal(["t3"], fork.Queued);

        foreach (var id in new[] { "h", "t1", "t2" }) g.Start(fork, id);
        var full = g.Evaluate();
        Assert.Empty(full.Starts);
        Assert.Equal(["t3"], full.Queued);

        g.Reach("t1");
        Assert.Equal(["t3"], g.Evaluate().Starts.Select(start => start.Node));
    }

    /// <summary>§E7: an <c>all</c> join is reached by its members when every one is reached or skipped, and fails when one fails.</summary>
    [Fact]
    public void A_join_reaches_on_its_members_and_fails_on_one()
    {
        NodeDeclaration[] nodes = [Node("m1"), Node("m2"), Node("j", NodeKinds.All), Node("n"), Node("sink", NodeKinds.Human)];
        Wire[] wires = [After("w1", "m1", "j"), After("w2", "m2", "j"), After("w3", "j", "n"), Failed("w4", "j", "sink")];

        var g = new Graph(nodes, wires);
        var first = g.Evaluate();
        g.Start(first, "m1");
        g.Start(first, "m2");
        g.Reach("m1");
        var waiting = g.Evaluate();
        Assert.Equal(NodeStates.Blocked, g.State("j"));
        Assert.Equal(["w2"], waiting.Of("j")!.Waiting);

        g.Skip("m2");
        g.Start(g.Evaluate(), "j");
        var joined = g.Evaluate();
        Assert.Equal((NodeStates.Reached, WireWhy.Members, NodeSource.Members), (g.State("j"), joined.Moves.Single(move => move.Node == "j").Why, g.Statuses["j"].By));
        Assert.Equal(2, g.Statuses["j"].Current!.PrerequisiteReceipts.Count);
        Assert.Equal((NodeStates.Ready, NodeStates.Blocked), (g.State("n"), g.State("sink")));

        var failing = new Graph(nodes, wires);
        var start = failing.Evaluate();
        failing.Start(start, "m1");
        failing.Start(start, "m2");
        failing.Fail("m1");
        failing.Start(failing.Evaluate(), "j");
        failing.Evaluate();
        Assert.Equal((NodeStates.Failed, WireEvaluation.MemberFailed, NodeSource.Members), (failing.State("j"), failing.Statuses["j"].Code, failing.Statuses["j"].By));
        Assert.Equal((NodeStates.Blocked, NodeStates.Ready), (failing.State("n"), failing.State("sink")));
    }

    /// <summary>§E2: a changed dependency invalidates every acceptance downstream, transitively, and a waiting eligibility lapses with it.</summary>
    [Fact]
    public void An_invalidated_upstream_lapses_everything_downstream()
    {
        var g = new Graph([Node("a"), Node("b"), Node("c"), Node("d")], [After("w1", "a", "b"), After("w2", "b", "c"), After("w3", "c", "d")]);
        g.Through("a");
        g.Through("b");
        g.Through("c");
        g.Evaluate();
        Assert.Equal(NodeStates.Ready, g.State("d"));
        var (a, b) = (g.Statuses["a"].Current!.Id, g.Statuses["b"].Current!.Id);

        g.Move("a", new NodeMove(NodeStates.Invalidated, NodeCause.Daoris));
        var lapsed = g.Evaluate();
        Assert.Equal((NodeStates.Invalidated, NodeStates.Invalidated, NodeStates.Blocked), (g.State("b"), g.State("c"), g.State("d")));
        Assert.Equal((WireWhy.Upstream, a), (lapsed.Moves.Single(move => move.Node == "b").Why, lapsed.Moves.Single(move => move.Node == "b").Detail));
        Assert.Equal((WireWhy.Upstream, b), (lapsed.Moves.Single(move => move.Node == "c").Why, lapsed.Moves.Single(move => move.Node == "c").Detail));
        Assert.Equal(WireWhy.Ineligible, lapsed.Moves.Single(move => move.Node == "d").Why);

        var moved = new Graph([Node("a"), Node("b")], [After("w1", "a", "b")]);
        moved.Through("a");
        moved.Through("b");
        moved.Run = moved.Run with { Revisions = new Dictionary<string, string?> { [Unit] = "tip-2" } };
        var tip = moved.Evaluate();
        Assert.Equal((NodeStates.Invalidated, NodeStates.Invalidated), (moved.State("a"), moved.State("b")));
        Assert.All(tip.Moves, move => Assert.Equal((WireWhy.Lapsed, NodeLapse.Revision), (move.Why, move.Detail)));
    }

    /// <summary>An evaluation over wires that come back (a save refuses them, WIRE1a) still ends, and settles.</summary>
    [Fact]
    public void A_cycle_terminates()
    {
        var g = new Graph([Node("x"), Node("y"), Node("z")], [After("w1", "x", "y"), After("w2", "y", "x"), Trigger("w3", "z", "z")]);
        var first = g.Evaluate();
        Assert.True(first.Settled);
        Assert.All(new[] { "x", "y", "z" }, id => Assert.Equal(NodeStates.Blocked, g.State(id)));
        Assert.Equal(["w2"], first.Of("x")!.Waiting);

        g.Move("x", new NodeMove(NodeStates.Ready, NodeCause.Daoris));
        g.Move("x", new NodeMove(NodeStates.Active, NodeCause.Daoris) { Binding = new NodeAcceptance(Unit, "tip-1", "graph-1", "x", 1, 1) });
        g.Reach("x");
        g.Evaluate();
        Assert.Equal((NodeStates.Reached, NodeStates.Ready), (g.State("x"), g.State("y")));
        Assert.True(g.Evaluate().Settled);
    }

    /// <summary>
    /// NODE1's choice 8: an observed built-in's outcome that needs git is read through the git reads the caller hands in. Without them a
    /// pushed node is unknown, never guessed; with them it is reached, and its target is eligible in the same evaluation.
    /// </summary>
    [Fact]
    public void An_observed_built_in_takes_the_callers_git_reads()
    {
        var landing = new LandedBranch("engine", "default", "feature/x", "main", "t1", "s1", "q1", null, Then)
        {
            Units = [Unit], Pushed = true, PushedTip = "t1", Plugin = "acme-pr",
        };
        var grown = new GrownBranch("engine", "default", "daoris/s1", "main", "f0", null, Then) { Unit = Unit };
        var unit = Assert.Single(WorkInventory.Units(new InventoryFacts([grown], [landing], [], [], [], new HashSet<string>(), _ => false, Then.AddDays(1))));
        Assert.Equal(Unit, unit.Id);
        var g = new Graph([new NodeDeclaration("p", NodeKinds.Pushed, 1, NodeReach.Observed), Node("n")], [After("w1", "p", "n")])
        {
            Run = Base with
            {
                Revisions = new Dictionary<string, string?> { [Unit] = "t1" },
                Units = new Dictionary<string, WorkUnit> { [Unit] = unit },
            },
        };
        g.Start(g.Evaluate(), "p");

        var unread = g.Evaluate();
        Assert.Equal((NodeStates.Unknown, WireWhy.Observed), (g.State("p"), unread.Moves.Single(move => move.Node == "p").Why));

        g.Run = g.Run with { Git = new NodeGitReads(RemoteTip: _ => "t1") };
        g.Evaluate();
        Assert.Equal((NodeStates.Reached, NodeSource.PluginRef, NodeStates.Ready), (g.State("p"), g.Statuses["p"].By, g.State("n")));
    }
}
