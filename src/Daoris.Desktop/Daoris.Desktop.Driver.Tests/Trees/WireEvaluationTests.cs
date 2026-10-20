using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-WIRE1b (the fabric events design §E3, §E2, §E7): the wires evaluated. Eligibility from current receipts, each moved through
/// the one table; an <c>anyOf</c>'s witness; a trigger once per receipt; a skip, a failure and a rejection each a scoped receipt read by
/// its own wires; forks within the limit; <c>all</c> joins; an invalidation lapsing downstream. Pure: nothing is stored.
/// </summary>
/// <remarks>Its unit, run, builders and <see cref="Graph"/> are internal: FABRIC-WIRE1g's <see cref="WireEvaluationDecodedTests"/> evaluates on them too.</remarks>
public sealed class WireEvaluationTests
{
    internal const string Unit = "unit-a";
    private const string Plugin = "acme.deploy/deploy";
    private const string Preview = "acme.deploy/preview@1";
    internal static readonly DateTimeOffset Then = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    internal static readonly WireRun Base = new("graph-1", "run-1") { Revisions = new Dictionary<string, string?> { [Unit] = "tip-1" } };

    internal static NodeDeclaration Node(string id, string kind = Plugin) =>
        new(id, kind, 1, NodeAuthorities.BuiltIn.TryGetValue(kind, out var builtIn) ? builtIn.Reach : NodeReach.Performed);

    internal static Dictionary<string, string> Ports(string name, string schema) => new() { [name] = schema };

    internal static Wire After(string id, string from, string to, WireGroup? group = null) => new(id, WireKinds.After, new(from), new(to)) { Group = group };

    private static Wire Trigger(string id, string from, string to) => new(id, WireKinds.Trigger, new(from), new(to));

    internal static Wire Failed(string id, string from, string to) => new(id, WireKinds.Failed, new(from), new(to));

    internal static Wire Rejected(string id, string from, string to) => new(id, WireKinds.Rejected, new(from), new(to));

    internal static Wire Data(string id, string from, string output, string to, string input) => new(id, WireKinds.Data, new(from, output), new(to, input));

    /// <summary>
    /// A graph's nodes on one record's unit (<see cref="Unit"/> unless named: FABRIC-CUTOVER1a's <c>run:r</c>), with their statuses as a record
    /// writer would keep them between evaluations.
    /// </summary>
    internal sealed class Graph(NodeDeclaration[] nodes, Wire[] wires, string unit = Unit)
    {
        private readonly HashSet<string> fired = new(StringComparer.Ordinal);

        public WireRun Run { get; set; } = Base;

        public Dictionary<string, NodeStatus> Statuses { get; } =
            nodes.ToDictionary(node => node.Id, node => NodeLifecycle.Start(unit, node.Id), StringComparer.Ordinal);

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

    /// <summary>
    /// FABRIC-GRAPH1i (§E7's protected order across a skip): a skip satisfies <c>after</c>, so a chain of reach lets the landing go while the
    /// opinion is unsettled; the opinion's own wire into the landing, which a save now asks of an authored graph, holds it. Both graphs are
    /// as <see cref="NodeGraphs.Problems"/> reads them: the first is refused (<c>protected-wire</c>), the second holds.
    /// </summary>
    [Fact]
    public void A_skip_of_the_look_passes_the_landing_only_where_the_opinion_is_wired_into_it()
    {
        NodeDeclaration[] nodes = [Node("opinion", NodeKinds.Opinion), Node("look", NodeKinds.Look), Node("landing", NodeKinds.Handed)];
        Wire[] chain = [After("a", "opinion", "look"), After("b", "look", "landing")];
        Wire[] wired = [.. chain, After("c", "opinion", "landing")];
        Assert.Equal([GraphRefusal.ProtectedWire], NodeGraphs.Problems(new NodeGraph(nodes, chain, ["opinion"], "landing")).Select(problem => problem.Code));
        Assert.Empty(NodeGraphs.Problems(new NodeGraph(nodes, wired, ["opinion"], "landing")));

        var past = new Graph(nodes, chain);
        past.Skip("look");
        past.Evaluate();
        Assert.Equal((NodeStates.Ready, NodeStates.Skipped, NodeStates.Ready), (past.State("opinion"), past.State("look"), past.State("landing")));

        var held = new Graph(nodes, wired);
        held.Skip("look");
        var evaluated = held.Evaluate();
        Assert.Equal((NodeStates.Ready, NodeStates.Skipped, NodeStates.Blocked), (held.State("opinion"), held.State("look"), held.State("landing")));
        Assert.Equal(["c"], evaluated.Of("landing")!.Waiting);
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

        // FABRIC-RUN1e: a graph edit restarts no rejected or failed work. The rejection stays for the person, and is no receipt in the new
        // graph, so its `rejected` wire fires nothing there.
        g.Run = g.Run with { GraphDigest = "graph-2" };
        var other = g.Evaluate();
        Assert.DoesNotContain(other.Moves, move => move.Node == "d");
        Assert.Equal((NodeStates.Rejected, NodeStates.Blocked), (g.State("d"), g.State("redo")));
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
        g.Move("x", new NodeMove(NodeStates.Active, NodeCause.Daoris) { Binding = new NodeAcceptance(Unit, "tip-1", "graph-1", "x", 1, 1), Scope = Base.Scope });
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
        var g = new Graph([new NodeDeclaration("p", NodeKinds.Pushed, 1, NodeReach.Observed), Node("n")], [After("w1", "p", "n")])
        {
            Run = Base with
            {
                Revisions = new Dictionary<string, string?> { [Unit] = "t1" },
                Units = new Dictionary<string, WorkUnit> { [Unit] = PushedUnit() },
            },
        };
        g.Start(g.Evaluate(), "p");

        var unread = g.Evaluate();
        Assert.Equal((NodeStates.Unknown, WireWhy.Observed), (g.State("p"), unread.Moves.Single(move => move.Node == "p").Why));

        g.Run = g.Run with { Git = new NodeGitReads(RemoteTip: _ => "t1") };
        g.Evaluate();
        Assert.Equal((NodeStates.Reached, NodeSource.PluginRef, NodeStates.Ready), (g.State("p"), g.Statuses["p"].By, g.State("n")));
    }

    /// <summary>
    /// FABRIC-WORKFLOW2, canary 49: a decoded D157 version evaluates as its run did. A group skipped (its members, never its join) satisfies
    /// what comes after it; a member failed fails the group under the group's policy (<c>wait</c>: the person's task), not the member's; and a
    /// step's <c>stop</c> takes its sink while what follows waits.
    /// </summary>
    [Fact]
    public void A_decoded_versions_skips_and_groups_evaluate_as_D157s_did()
    {
        static NamedStep Step(string id, string kind, params (string Name, object? Value)[] fields) =>
            new(id, kind, [.. WorkflowKindTable.Of(kind)!.Fields.Select(field => new WorkflowSetting(field.Name, fields.FirstOrDefault(each => each.Name == field.Name) is { Name: not null } named ? named.Value : field.Default))]);

        List<NamedStep> steps =
        [
            Step("work", WorkflowKinds.Work),
            Step("land", WorkflowKinds.Landing, ("form", LandingForm.Merge), ("accept", "you")),
            Step("lint", WorkflowKinds.Check, ("plugin", "acme.ci"), ("failed", "stop")),
            Step("both", WorkflowKinds.All, ("failed", "wait"), ("steps", new List<NamedStep>
            {
                Step("unit", WorkflowKinds.Check, ("plugin", "acme.ci"), ("failed", "stop")),
                Step("smoke", WorkflowKinds.Stage, ("plugin", "acme.deploy"), ("stage", "smoke"), ("start", "automatic")),
            })),
            Step("release", WorkflowKinds.Stage, ("plugin", "acme.deploy"), ("stage", "release"), ("start", "automatic")),
        ];
        var decoded = WorkflowGraphs.Graph(steps);
        Graph Fresh()
        {
            var g = new Graph([.. decoded.Nodes], [.. decoded.Wires]);
            g.Through("work");
            g.Through("land");
            return g;
        }

        // The group skipped: its members, so its join reaches on their skips and the next step is ready.
        var skipped = Fresh();
        skipped.Through("lint");
        skipped.Evaluate();
        foreach (var member in WorkflowGraphs.Skipped(steps, "both")) skipped.Skip(member);
        skipped.Start(skipped.Evaluate(), "both");
        skipped.Evaluate();
        skipped.Evaluate();
        Assert.Equal((NodeStates.Reached, NodeStates.Ready, NodeStates.Blocked), (skipped.State("both"), skipped.State("release"), skipped.State("both:failed")));

        // A member failed: the group fails under its own policy, the person's task, whatever the member's `stop` says; release waits.
        var failed = Fresh();
        failed.Through("lint");
        failed.Start(failed.Evaluate(), "unit");
        failed.Fail("unit");
        failed.Start(failed.Evaluate(), "both");
        failed.Evaluate();
        failed.Evaluate();
        Assert.Equal((NodeStates.Failed, NodeStates.Ready, NodeStates.Blocked), (failed.State("both"), failed.State("both:failed"), failed.State("release")));
        Assert.DoesNotContain(decoded.Wires, wire => wire.From!.Node == "unit" && wire.Kind == WireKinds.Failed);

        // A step's `stop`: its sink is ready, and nothing after it starts.
        var stopped = Fresh();
        stopped.Start(stopped.Evaluate(), "lint");
        stopped.Fail("lint");
        stopped.Evaluate();
        Assert.Equal((NodeStates.Ready, NodeStates.Blocked, NodeStates.Blocked), (stopped.State("lint:failed"), stopped.State("unit"), stopped.State("smoke")));
    }

    /// <summary>A branch unit whose landing a plugin answered it pushed at <c>t1</c>.</summary>
    private static WorkUnit PushedUnit()
    {
        var landing = new LandedBranch("engine", "default", "feature/x", "main", "t1", "s1", "q1", null, Then)
        {
            Units = [Unit], Pushed = true, PushedTip = "t1", Plugin = "acme-pr",
        };
        var grown = new GrownBranch("engine", "default", "daoris/s1", "main", "f0", null, Then) { Unit = Unit };
        var unit = Assert.Single(WorkInventory.Units(new InventoryFacts([grown], [landing], [], [], [], new HashSet<string>(), _ => false, Then.AddDays(1))));
        Assert.Equal((Unit, WorkUnitKind.Branch), (unit.Id, unit.Kind));
        return unit;
    }

    /// <summary>
    /// FABRIC-WIRE1e, defect 1: a failure is a receipt in its attempt's run alone. In its own run it takes the <c>failed</c> path; kept
    /// from run 1 and evaluated in run 2 of the same graph and revision, it takes none (and FABRIC-WIRE1f lapses it there). A run never
    /// returns to an earlier one, so the own run is evaluated first.
    /// </summary>
    [Fact]
    public void A_failure_in_one_run_is_no_receipt_in_another()
    {
        var g = new Graph([Node("d"), Node("roll", NodeKinds.Human)], [Failed("w1", "d", "roll")]);
        g.Start(g.Evaluate(), "d");
        g.Fail("d");

        var own = g.Evaluate();
        Assert.Equal(NodeStates.Ready, g.State("roll"));
        Assert.StartsWith("fld-", Assert.Single(own.Of("roll")!.Receipts), StringComparison.Ordinal);

        g.Run = Base with { Run = "run-2" };
        var other = g.Evaluate();
        Assert.Equal(NodeStates.Blocked, g.State("roll"));
        Assert.Empty(other.Of("roll")!.Receipts);
        Assert.Equal(["w1"], other.Of("roll")!.Waiting);
    }

    /// <summary>FABRIC-WIRE1e, defect 1: a rejection likewise is a receipt in its attempt's run alone.</summary>
    [Fact]
    public void A_rejection_in_one_run_is_no_receipt_in_another()
    {
        var g = new Graph([Node("d") with { Rejectable = true }, Node("redo", NodeKinds.Human)], [Rejected("w1", "d", "redo")]);
        g.Start(g.Evaluate(), "d");
        g.Reject("d");

        var own = g.Evaluate();
        Assert.Equal(NodeStates.Ready, g.State("redo"));
        Assert.StartsWith("rej-", Assert.Single(own.Of("redo")!.Receipts), StringComparison.Ordinal);

        g.Run = Base with { Run = "run-2" };
        var other = g.Evaluate();
        Assert.Equal(NodeStates.Blocked, g.State("redo"));
        Assert.Empty(other.Of("redo")!.Receipts);
    }

    /// <summary>
    /// FABRIC-RUN1e, in place of WIRE1f's lapse: a failure kept from another run or graph stays for the person, however often the graph is
    /// edited back and forth, since a run begins at a graph edit and a Setup save can edit the derived graph; only the person's new attempt
    /// starts it afresh, in this run, its tries counted afresh and its attempt numbers still growing (WIRE1e's note).
    /// </summary>
    [Fact]
    public void Another_runs_or_graphs_failure_is_kept_for_the_person_until_they_restart_it()
    {
        var g = new Graph([Node("d")], []);
        g.Start(g.Evaluate(), "d");
        g.Fail("d");

        foreach (var run in new[] { Base with { Run = "run-2" }, Base with { GraphDigest = "graph-2", Run = "run-3" }, Base, Base with { Run = "run-2" } })
        {
            g.Run = run;
            var kept = g.Evaluate();
            Assert.Empty(kept.Moves);
            Assert.Empty(kept.Starts);
            Assert.Equal(NodeStates.Failed, g.State("d"));
        }

        g.Move("d", new NodeMove(NodeStates.Ready, NodeCause.Person));
        var again = g.Evaluate();
        var start = Assert.Single(again.Starts);
        Assert.Equal((2, "run-2"), (start.Move.Binding!.Attempt, start.Move.Scope!.Run));
        g.Start(again, "d");
        Assert.Equal((2, 1), (g.Statuses["d"].Attempt, g.Statuses["d"].Tries));
    }

    /// <summary>
    /// FABRIC-RUN1e: a new revision is new work, so it still lapses a failure kept from another graph, as it lapses one of this graph
    /// (WIRE1e): the node is judged again on the new tip.
    /// </summary>
    [Fact]
    public void A_new_revision_lapses_a_failure_kept_from_another_graph()
    {
        var g = new Graph([Node("d")], []);
        g.Start(g.Evaluate(), "d");
        g.Fail("d");

        g.Run = Base with { GraphDigest = "graph-2", Run = "run-2", Revisions = new Dictionary<string, string?> { [Unit] = "tip-2" } };
        var lapsed = g.Evaluate();
        var move = Assert.Single(lapsed.Moves);
        Assert.Equal(("d", NodeStates.Invalidated, WireWhy.Lapsed, NodeLapse.Revision), (move.Node, move.Move.To, move.Why, move.Detail));
    }

    /// <summary>FABRIC-WIRE1f, the other direction: in its own run a failure Daoris will not retry is kept for the person, however often evaluated.</summary>
    [Fact]
    public void In_its_own_run_a_failure_is_kept_for_the_person()
    {
        var g = new Graph([Node("d")], []);
        g.Start(g.Evaluate(), "d");
        g.Fail("d");

        for (var pass = 0; pass < 2; pass++)
        {
            var evaluated = g.Evaluate();
            Assert.Empty(evaluated.Moves);
            Assert.Empty(evaluated.Starts);
        }

        Assert.Equal(NodeStates.Failed, g.State("d"));
    }

    /// <summary>FABRIC-RUN1e: a rejection kept from another run stays for the person the same way, and a reach is kept, its acceptance omitting the run (§E2).</summary>
    [Fact]
    public void Another_runs_rejection_is_kept_for_the_person_and_its_reach_kept()
    {
        var g = new Graph([Node("d") with { Rejectable = true }, Node("e")], []);
        var first = g.Evaluate();
        g.Start(first, "d");
        g.Start(first, "e");
        g.Reject("d");
        g.Reach("e");

        g.Run = Base with { Run = "run-2" };
        var kept = g.Evaluate();

        Assert.Empty(kept.Moves);
        Assert.Equal((NodeStates.Rejected, NodeStates.Reached), (g.State("d"), g.State("e")));
    }

    /// <summary>
    /// FABRIC-WIRE1e, defect 2: validity is judged before anything is observed. A push of tip 1 observed after the unit moved to tip 2 is
    /// invalidated, never reached, so no receipt of tip 1 makes a node eligible on tip 2, and nothing starts on one in that evaluation.
    /// </summary>
    [Fact]
    public void An_observation_on_a_moved_tip_gives_no_receipt_and_no_start()
    {
        var g = new Graph([new NodeDeclaration("p", NodeKinds.Pushed, 1, NodeReach.Observed), Node("n")], [After("w1", "p", "n")])
        {
            Run = Base with
            {
                Revisions = new Dictionary<string, string?> { [Unit] = "t1" },
                Units = new Dictionary<string, WorkUnit> { [Unit] = PushedUnit() },
                Git = new NodeGitReads(RemoteTip: _ => "t1"),
            },
        };
        g.Start(g.Evaluate(), "p");

        g.Run = g.Run with { Revisions = new Dictionary<string, string?> { [Unit] = "t2" } };
        var moved = g.Evaluate();
        var lapse = moved.Moves.Single(move => move.Node == "p");
        Assert.Equal((NodeStates.Invalidated, WireWhy.Lapsed, NodeLapse.Revision), (g.State("p"), lapse.Why, lapse.Detail));
        Assert.Equal(NodeStates.Blocked, g.State("n"));
        Assert.Empty(moved.Of("n")!.Receipts);
        Assert.DoesNotContain(moved.Starts, start => start.Node == "n");
    }

    /// <summary>
    /// FABRIC-WIRE1e, defect 3: an <c>all</c> join's outcome is its members' alone. A failure it consumes through its own <c>failed</c>
    /// wire from a node that is no member is a condition of its start, never a member's failure.
    /// </summary>
    [Fact]
    public void A_join_is_not_failed_by_a_failure_that_is_no_members()
    {
        var g = new Graph([Node("m"), Node("x"), Node("j", NodeKinds.All)], [After("w1", "m", "j"), Failed("w2", "x", "j")]);
        var first = g.Evaluate();
        g.Start(first, "m");
        g.Start(first, "x");
        g.Reach("m");
        g.Fail("x");

        var eligible = g.Evaluate();
        Assert.Equal((NodeStates.Ready, 2), (g.State("j"), eligible.Of("j")!.Receipts.Count));
        g.Start(eligible, "j");
        var joined = g.Evaluate();
        Assert.Equal((NodeStates.Reached, WireWhy.Members, (string?)null), (g.State("j"), joined.Moves.Single(move => move.Node == "j").Why, g.Statuses["j"].Code));
    }

    /// <summary>
    /// FABRIC-WIRE1e, choice 7 changed: waiting capacity is apart from execution capacity. Parked <c>wait.unit</c> attempts, as many as the
    /// performed limit, never queue a performed node; waits are bounded by their own limit.
    /// </summary>
    [Fact]
    public void Parked_waits_do_not_queue_a_performed_node()
    {
        string[] waits = ["u1", "u2", "u3", "u4", "u5"];
        var g = new Graph([.. waits.Select(id => Node(id, NodeKinds.WaitUnit)), Node("h", NodeKinds.Human), Node("p")], [After("w1", "h", "p")])
        {
            Run = Base with { WaitLimit = 4 },
        };
        var first = g.Evaluate();
        Assert.Equal(["h", "u1", "u2", "u3", "u4"], first.Starts.Select(start => start.Node));
        Assert.Equal(["u5"], first.Queued);

        foreach (var start in first.Starts) g.Start(first, start.Node);
        g.Reach("h");
        var parked = g.Evaluate();
        Assert.Equal(Base.Limit, waits.Count(id => g.State(id) == NodeStates.Active));
        Assert.Equal(["p"], parked.Starts.Select(start => start.Node));
        Assert.Equal(["u5"], parked.Queued);
    }

    /// <summary>
    /// FABRIC-WIRE1e, choice 9 changed: the revisions are a complete snapshot. A unit a node acts on that they leave out, or a branch they
    /// name with none, refuses the evaluation with its code and the units it names: nothing moves and nothing starts. A quest's is null by
    /// design, and named so.
    /// </summary>
    [Fact]
    public void A_missing_branch_revision_refuses_the_evaluation()
    {
        var g = new Graph([Node("a"), Node("b")], [After("w1", "a", "b")]) { Run = Base with { Revisions = new Dictionary<string, string?>() } };
        var unnamed = g.Evaluate();
        Assert.Equal((WireEvaluation.NoRevision, Unit), (unnamed.Refusal, Assert.Single(unnamed.Missing)));
        Assert.Equal((0, 0, 0), (unnamed.Moves.Count, unnamed.Starts.Count, unnamed.Eligibility.Count));
        Assert.Equal((NodeStates.Blocked, NodeStates.Blocked), (g.State("a"), g.State("b")));

        g.Run = Base with
        {
            Revisions = new Dictionary<string, string?> { [Unit] = null },
            Units = new Dictionary<string, WorkUnit> { [Unit] = PushedUnit() },
        };
        var none = g.Evaluate();
        Assert.Equal((WireEvaluation.NoRevision, Unit), (none.Refusal, Assert.Single(none.Missing)));
        Assert.Empty(none.Starts);

        const string Quest = "quest:q1";
        var quest = WireEvaluation.Evaluate([Node("c")], [], new Dictionary<string, NodeStatus> { ["c"] = NodeLifecycle.Start(Quest, "c") },
            Base with { Revisions = new Dictionary<string, string?> { [Quest] = null } });
        Assert.Null(quest.Refusal);
        Assert.Null(Assert.Single(quest.Starts).Move.Binding!.SubjectRevision);
    }

    // ——— FABRIC-CUTOVER1a: a run's graph evaluated over its subjects (the cutover design §2.1, §2.2, §11), each seen failing first

    private const string RunRecord = "run:r";
    private const string Act = "deploy the release to production";

    /// <summary>A named version's graph as a run binds it: <c>claimed</c>, the opinion and the look beside it into <c>handed</c>, a push and a release after.</summary>
    private static readonly NodeDeclaration[] RunNodes =
    [
        Node("claimed", NodeKinds.Claimed), Node("opinion", NodeKinds.Opinion), Node("look", NodeKinds.Look), Node("handed", NodeKinds.Handed),
        Node("pushed", NodeKinds.Pushed), Node("release"),
    ];

    private static readonly Wire[] RunWires =
    [
        After("c>o", "claimed", "opinion"), After("c>l", "claimed", "look"), After("o>h", "opinion", "handed"), After("l>h", "look", "handed"),
        After("h>p", "handed", "pushed"), After("p>r", "pushed", "release"),
    ];

    /// <summary>The run's quest here, done unless said, with the steps its chain still names.</summary>
    private static WorkUnit QuestOf(string id = "q1", string status = "Done", params QuestStepView[] then) =>
        Assert.Single(QuestUnits.Read([new QuestView(id, "game", "engine", "The work", "Body.", status) { Updated = Then, Then = then }], []));

    /// <summary>A branch unit its records name; landed at <paramref name="landed"/> where given, and pushed there.</summary>
    private static WorkUnit BranchOf(string id, string? landed = null) => new(id, WorkUnitKind.Branch,
        new WorkOwner("engine", "default", SessionBranches.FileName, WorkRecord.DriverWriter), new WorkSubject("daoris/s1", null),
        new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), [])
    {
        Records = new WorkUnitRecords(null, landed is null ? [] :
        [
            new LandedBranch("engine", "default", "feature/q1", "main", landed, "s1", "q1", null, Then) { Units = [id], Pushed = true, PushedTip = landed, Plugin = "acme-pr" },
        ], null),
    };

    /// <summary>
    /// A pass's read for run <c>r</c>: its record's own unit with no revision; <c>claimed</c> on <paramref name="quest"/>; the opinion, the look and
    /// <c>handed</c> on <paramref name="branch"/> at <paramref name="tip"/>, its gates read at that tip; what follows the landing on the landing's
    /// tip, and on no subject while there is no landing. No tip read, the branch's subject has no revision and its gates no read.
    /// </summary>
    private static WireRun RunOver(WorkUnit quest, WorkUnit branch, string? tip, string? landed = null)
    {
        var revisions = new Dictionary<string, string?> { [RunRecord] = null, [quest.Id] = quest.Subject.Revision };
        if (tip is not null) revisions[branch.Id] = tip;
        var after = landed is null ? null : branch.Id;
        return new WireRun("graph-1", "r")
        {
            Revisions = revisions,
            Subjects = new Dictionary<string, string?>
            {
                ["claimed"] = quest.Id, ["opinion"] = branch.Id, ["look"] = branch.Id, ["handed"] = branch.Id, ["pushed"] = after, ["release"] = after,
            },
            NodeRevisions = landed is null ? new Dictionary<string, string?>() : new Dictionary<string, string?> { ["pushed"] = landed, ["release"] = landed },
            Units = new Dictionary<string, WorkUnit> { [quest.Id] = quest, [branch.Id] = branch },
            Opinions = tip is null
                ? new Dictionary<string, OpinionGateState>()
                : new Dictionary<string, OpinionGateState> { [branch.Id] = new(OpinionGateStates.Settled, "engine") { Tip = tip } },
            Reviews = tip is null
                ? new Dictionary<string, ReviewGateState>()
                : new Dictionary<string, ReviewGateState>
                {
                    [branch.Id] = new(ReviewStates.Reviewed, new ReviewDecision(ReviewLevels.Repository, "preview") { Repository = "engine" })
                    {
                        Tip = tip, Verdict = new QuestReviewVerdictView(ReviewVerdicts.Reviewed) { Commit = tip, At = Then },
                    },
                },
            Git = new NodeGitReads(RemoteTip: landing => landing.PushedTip),
        };
    }

    /// <summary>Run <c>r</c> taken to its landing: <c>claimed</c> reached on q1, the gates and <c>handed</c> on b1 at t1, the push started on the landing's tip.</summary>
    private static Graph ThroughItsLanding(out WorkUnit quest)
    {
        quest = QuestOf();
        var g = new Graph(RunNodes, RunWires, RunRecord) { Run = RunOver(quest, BranchOf("unit-b1"), "t1") };
        var first = g.Evaluate();
        Assert.Equal(["claimed"], first.Starts.Select(start => start.Node));
        g.Start(first, "claimed");

        var gates = g.Evaluate();
        Assert.Equal(["look", "opinion"], gates.Starts.Select(start => start.Node));
        g.Start(gates, "opinion");
        g.Start(gates, "look");

        var landing = g.Evaluate();
        Assert.Equal(["handed"], landing.Starts.Select(start => start.Node));
        g.Start(landing, "handed");

        g.Run = RunOver(quest, BranchOf("unit-b1", landed: "t1"), "t1", landed: "t1");
        var pushing = g.Evaluate();
        Assert.Equal(NodeStates.Reached, g.State("handed"));
        Assert.Equal(["pushed"], pushing.Starts.Select(start => start.Node));
        g.Start(pushing, "pushed");
        return g;
    }

    /// <summary>
    /// One record of unit <c>run:r</c>: <c>claimed</c> observes the run's quest and reaches on its done; the opinion and the look observe the run's
    /// branch's gates at the tip handed in, and <c>handed</c> its landing, reached through its wires; the push follows on the landing's tip. Every
    /// status stays the record's, and every acceptance names its record's unit and the subject it observed.
    /// </summary>
    [Fact]
    public void A_runs_record_observes_each_node_on_the_subject_its_kind_names()
    {
        var g = ThroughItsLanding(out var quest);
        g.Evaluate();

        foreach (var (node, subject, revision) in new[]
        {
            ("claimed", quest.Id, quest.Subject.Revision), ("opinion", "unit-b1", "t1"), ("look", "unit-b1", "t1"), ("handed", "unit-b1", "t1"),
            ("pushed", "unit-b1", "t1"),
        })
        {
            var status = g.Statuses[node];
            Assert.Equal((node, RunRecord, NodeStates.Reached), (node, status.Unit, status.State));
            Assert.Equal((node, RunRecord, subject, revision), (node, status.Current!.Unit, status.Current.Subject, status.Current.SubjectRevision));
        }

        Assert.Equal(new[] { g.Statuses["opinion"].Current!.Id, g.Statuses["look"].Current!.Id }.Order(StringComparer.Ordinal),
            g.Statuses["handed"].Current!.PrerequisiteReceipts);
        Assert.Equal(NodeStates.Ready, g.State("release"));
    }

    /// <summary>
    /// <c>claimed</c> on a run waits for its chain's last step here: its quest's done, with a step still to come in this repository, leaves it
    /// active and the nodes after it unstarted. The chain's next quest moves its subject, so it lapses as <c>subject</c>, is begun on that quest,
    /// and reaches there.
    /// </summary>
    [Fact]
    public void Claimed_on_a_run_waits_for_its_chains_last_step_here_and_moves_with_it()
    {
        var branch = BranchOf("unit-b1");
        var g = new Graph(RunNodes, RunWires, RunRecord) { Run = RunOver(QuestOf("q1", "Done", new QuestStepView("engine", "Next", "More of it.")), branch, "t1") };
        g.Start(g.Evaluate(), "claimed");

        var waiting = g.Evaluate();
        Assert.Equal((NodeStates.Active, NodeStates.Blocked, NodeStates.Blocked), (g.State("claimed"), g.State("opinion"), g.State("look")));
        Assert.Empty(waiting.Starts);
        Assert.Equal(["c>o"], waiting.Of("opinion")!.Waiting);

        var next = QuestOf("q2");
        g.Run = RunOver(next, branch, "t1");
        var moved = Assert.Single(g.Evaluate().Moves);
        Assert.Equal(("claimed", NodeStates.Invalidated, WireWhy.Lapsed, NodeLapse.Subject), (moved.Node, moved.Move.To, moved.Why, moved.Detail));

        var again = g.Evaluate();
        Assert.Equal((next.Id, next.Subject.Revision), (again.Starts.Single().Move.Binding!.Subject, again.Starts.Single().Move.Binding!.SubjectRevision));
        g.Start(again, "claimed");
        g.Evaluate();
        Assert.Equal((NodeStates.Reached, NodeStates.Ready, NodeStates.Ready), (g.State("claimed"), g.State("opinion"), g.State("look")));
    }

    /// <summary>
    /// The chain's next session replaces the run's branch with another at the same commit: a binding names its subject, so the gates and
    /// <c>handed</c> lapse as <c>subject</c> though no revision moved, the push resting on <c>handed</c> lapses <c>upstream</c>, and <c>claimed</c>,
    /// on its quest, stands.
    /// </summary>
    [Fact]
    public void A_branch_replaced_at_the_same_commit_lapses_handed_and_what_rests_on_it()
    {
        var g = ThroughItsLanding(out var quest);
        var handed = g.Statuses["handed"].Current!.Id;

        g.Run = RunOver(quest, BranchOf("unit-b2"), "t1");
        var replaced = g.Evaluate();

        Assert.Equal(
            [
                ("opinion", NodeStates.Invalidated, WireWhy.Lapsed, NodeLapse.Subject),
                ("look", NodeStates.Invalidated, WireWhy.Lapsed, NodeLapse.Subject),
                ("handed", NodeStates.Invalidated, WireWhy.Lapsed, NodeLapse.Subject),
                ("pushed", NodeStates.Invalidated, WireWhy.Upstream, handed),
            ],
            replaced.Moves.Select(move => (move.Node, move.Move.To, move.Why, move.Detail)));
        Assert.Equal(NodeStates.Reached, g.State("claimed"));
    }

    /// <summary>
    /// The session's branch tidied after its landing: no tip is handed in for it, and the evaluation goes on. What is bound on the branch keeps its
    /// binding, offered no new start; the push, bound to the landing's tip as the landing's record keeps it, is observed there and reached, and the
    /// release after it is offered its start on that tip.
    /// </summary>
    [Fact]
    public void A_node_after_handed_binds_the_landings_tip_and_is_kept_with_no_tip_handed_in()
    {
        var g = ThroughItsLanding(out var quest);

        g.Run = RunOver(quest, BranchOf("unit-b1", landed: "t1"), tip: null, landed: "t1");
        var tidied = g.Evaluate();

        Assert.Null(tidied.Refusal);
        Assert.Equal([("pushed", NodeStates.Reached), ("release", NodeStates.Ready)], tidied.Moves.Select(move => (move.Node, move.Move.To)));
        Assert.Equal((NodeStates.Reached, NodeStates.Reached, NodeStates.Reached), (g.State("opinion"), g.State("look"), g.State("handed")));
        Assert.Equal(WireEvaluation.NoRevision, tidied.Of("handed")!.Withheld);
        var start = Assert.Single(tidied.Starts).Move.Binding!;
        Assert.Equal(("release", RunRecord, "unit-b1", "t1"), (start.Node, start.Unit, start.Subject, start.SubjectRevision));
    }

    /// <summary>
    /// A data wire joins a quest-side node and a branch-side one, whose revisions differ by design: the input is resolved on its producer's
    /// reach, the consumer bound to the branch's tip, and the dispatch's check takes it on the producer's own subject.
    /// </summary>
    [Fact]
    public void A_data_wire_from_a_quest_side_node_into_a_branch_side_node_resolves()
    {
        var quest = QuestOf();
        NodeDeclaration[] nodes =
        [
            Node("claimed", NodeKinds.Claimed), Node("deploy") with { Inputs = Ports("claim", BuiltInNodes.EvidenceSchema(NodeKinds.Claimed)) },
        ];
        var run = RunOver(quest, BranchOf("unit-b1"), "t1");
        var g = new Graph(nodes, [Data("d", "claimed", WireEnd.Evidence, "deploy", "claim")], RunRecord)
        {
            Run = run with { Subjects = new Dictionary<string, string?> { ["claimed"] = quest.Id, ["deploy"] = "unit-b1" } },
        };
        g.Start(g.Evaluate(), "claimed");

        var evaluated = g.Evaluate();
        var claimed = g.Statuses["claimed"].Current!;
        var binding = evaluated.Starts.Single(start => start.Node == "deploy").Move.Binding!;
        Assert.Equal(("unit-b1", "t1", claimed.Evidence), (binding.Subject, binding.SubjectRevision, binding.Inputs["claim"]));
        Assert.NotEqual(claimed.SubjectRevision, binding.SubjectRevision);

        g.Start(evaluated, "deploy");
        Assert.Null(WorkBelief.Unresolved(new NodeRecord(RunRecord) { Statuses = g.Statuses }, g.Statuses["deploy"].Binding!));
    }

    /// <summary>
    /// The run's own nodes observe the run, projected as a unit for the evaluation alone: its go-ahead is answered only through the list the pass
    /// hands in for the run, never another subject's, and its binding names no subject.
    /// </summary>
    [Fact]
    public void A_go_ahead_on_the_run_is_answered_only_through_the_list_handed_in_for_it()
    {
        var go = Node("go", NodeKinds.GoAhead) with { Options = new Dictionary<string, string> { ["act"] = WorkflowCurrent.Quoted(Act) } };
        var run = new WorkUnit(RunRecord, WorkUnitKind.Run, new WorkOwner("engine", "default", null, WorkRecord.DriverWriter), new WorkSubject("r", null),
            new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "running"), []);
        IReadOnlyList<GoAheadView> yes =
        [
            new GoAheadView(1, "write", "production", Act, [new GoAheadRequestView("s1", "q1", Then, "to ship")]) { Answer = new GoAheadAnswerView(true, null, Then) },
        ];
        var g = new Graph([go, Node("handed", NodeKinds.Handed)], [After("g>h", "go", "handed")], RunRecord)
        {
            Run = new WireRun("graph-1", "r")
            {
                Revisions = new Dictionary<string, string?> { [RunRecord] = null, ["unit-b1"] = "t1" },
                Subjects = new Dictionary<string, string?> { ["handed"] = "unit-b1" },
                Units = new Dictionary<string, WorkUnit> { [RunRecord] = run, ["unit-b1"] = BranchOf("unit-b1") },
                GoAheads = new Dictionary<string, IReadOnlyList<GoAheadView>> { ["unit-b1"] = yes, ["quest:q1"] = yes },
            },
        };
        g.Start(g.Evaluate(), "go");

        g.Evaluate();
        Assert.Equal((NodeStates.Active, NodeStates.Blocked), (g.State("go"), g.State("handed")));

        g.Run = g.Run with { GoAheads = new Dictionary<string, IReadOnlyList<GoAheadView>> { [RunRecord] = yes } };
        g.Evaluate();
        Assert.Equal((NodeStates.Reached, NodeStates.Ready, (string?)null), (g.State("go"), g.State("handed"), g.Statuses["go"].Current!.Subject));
    }

    /// <summary>
    /// A subject the pass did not find, or a branch subject whose revision it did not read, withholds its own nodes alone: they are judged on their
    /// wires and offered no start while the rest are evaluated, and a started one is neither observed nor lapsed, keeping its binding. A record's
    /// own unit left out still refuses the whole evaluation (<see cref="A_missing_branch_revision_refuses_the_evaluation"/>).
    /// </summary>
    [Fact]
    public void A_subject_not_found_or_not_read_withholds_its_own_nodes_alone()
    {
        var quest = QuestOf();
        WireRun Read(string? subject, bool named, string? tip)
        {
            var revisions = new Dictionary<string, string?> { [quest.Id] = quest.Subject.Revision };
            if (named) revisions["unit-b1"] = tip;
            return new WireRun("graph-1", "r")
            {
                Revisions = revisions,
                Subjects = new Dictionary<string, string?> { ["claimed"] = quest.Id, ["opinion"] = subject, ["check"] = null },
                Units = new Dictionary<string, WorkUnit> { [quest.Id] = quest, ["unit-b1"] = BranchOf("unit-b1") },
                Opinions = new Dictionary<string, OpinionGateState> { ["unit-b1"] = new(OpinionGateStates.Settled, "engine") { Tip = "t1" } },
            };
        }

        var g = new Graph([Node("claimed", NodeKinds.Claimed), Node("opinion", NodeKinds.Opinion), Node("check")], [], RunRecord) { Run = Read("unit-b1", false, null) };
        var unread = g.Evaluate();
        Assert.Null(unread.Refusal);
        Assert.Equal(["claimed"], unread.Starts.Select(start => start.Node));
        Assert.Equal((WireEvaluation.NoRevision, WireEvaluation.NoSubject, (string?)null),
            (unread.Of("opinion")!.Withheld, unread.Of("check")!.Withheld, unread.Of("claimed")!.Withheld));
        Assert.Equal((true, (NodeAcceptance?)null), (unread.Of("opinion")!.Eligible, unread.Of("opinion")!.Binding));

        g.Run = Read("unit-b1", true, null);
        Assert.Equal(WireEvaluation.NoRevision, g.Evaluate().Of("opinion")!.Withheld);

        g.Run = Read("unit-b1", true, "t1");
        g.Start(g.Evaluate(), "opinion");
        Assert.Equal(("unit-b1", "t1"), (g.Statuses["opinion"].Binding!.Subject, g.Statuses["opinion"].Binding!.SubjectRevision));

        g.Run = Read(null, true, "t1");
        var unfound = g.Evaluate();
        Assert.DoesNotContain(unfound.Moves, move => move.Node == "opinion");
        Assert.Equal((NodeStates.Active, "t1"), (g.State("opinion"), g.Statuses["opinion"].Binding!.SubjectRevision));
    }
}
