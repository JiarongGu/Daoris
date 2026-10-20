using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>The kinds of a typed transition receipt (§E3): the one transition of its source that an eligibility wire reads.</summary>
public static class WireReceiptKinds
{
    /// <summary>A current <c>reached</c> acceptance: read by <c>after</c>, <c>data</c> and <c>trigger</c> wires. Its id is the acceptance's.</summary>
    public const string Reached = "reached";

    /// <summary>The person's skip for this graph and run: read by <c>after</c> wires alone, since a skipped node produces no outputs (§E2).</summary>
    public const string Skipped = "skipped";

    /// <summary>A failure with its declared retries exhausted, on this graph's binding: read by <c>failed</c> wires alone.</summary>
    public const string Failed = "failed";

    /// <summary>The person's recorded rejection, on this graph's binding: read by <c>rejected</c> wires alone.</summary>
    public const string Rejected = "rejected";
}

/// <summary>
/// One typed transition receipt (FABRIC-WIRE1b; §E3), scoped to its graph, run and binding: what a downstream binding's
/// <see cref="NodeAcceptance.PrerequisiteReceipts"/> names, so losing it invalidates what rested on it.
/// </summary>
/// <param name="Id">
/// A reach's acceptance id; else a digest of its kind, graph, run and binding (<c>skp-</c>, <c>fld-</c>, <c>rej-</c>), worked out from its status's
/// own scope (FABRIC-RUN1f): its node's place where that names one, else the graph and the run as the scope names them.
/// </param>
/// <param name="Kind">One of <see cref="WireReceiptKinds"/>.</param>
/// <param name="Node">The node whose transition it is.</param>
public sealed record WireReceipt(string Id, string Kind, string Node)
{
    /// <summary>A reach's acceptance, whose evidence and outputs a <c>data</c> wire passes on; null on every other kind.</summary>
    public NodeAcceptance? Acceptance { get; init; }
}

/// <summary>
/// The run an evaluation judges, and what its caller supplies (FABRIC-WIRE1b). Nothing here is stored by the evaluation: the fired
/// triggers are the record writer's to keep (PERFORM1a), as <see cref="NodeAcceptances.Keep"/>'s results are.
/// </summary>
/// <param name="GraphDigest">The graph that binds the run.</param>
/// <param name="Run">The run's id: a skip and a transition receipt hold for this graph and run alone.</param>
public sealed record WireRun(string GraphDigest, string Run)
{
    public NodeScope Scope => new(GraphDigest, Run);

    /// <summary>
    /// Each node's place on a live graph, by node id (FABRIC-RUN1f; the cutover design §9, <see cref="NodeGraphs.Places"/>): a node named here is
    /// scoped, bound, receipted and fired on by its place where every other is by <see cref="GraphDigest"/>, so an edit elsewhere in the graph
    /// leaves it current. Empty on a named version's run, which is never edited (D157 point 6).
    /// </summary>
    public IReadOnlyDictionary<string, string> Places { get; init; } = ImmutableDictionary<string, string>.Empty;

    /// <summary>
    /// The run's name before FABRIC-RUN1f, worked out from the graph now (<see cref="NodeRuns.Of(string, string)"/>): what a scope written before
    /// names, so a record naming the whole graph's digest is judged as it was until the graph is edited. Null where the run had no other name.
    /// </summary>
    public string? Legacy { get; init; }

    /// <summary>What <paramref name="node"/> is bound and scoped to now: its place where it has one, else the whole graph's digest.</summary>
    public string GraphOf(string node) => Places.TryGetValue(node, out var place) ? place : GraphDigest;

    /// <summary>The scope a start or a skip of <paramref name="node"/> takes now: its place, or the graph, and the run.</summary>
    public NodeScope ScopeOf(string node) => new(GraphOf(node), Run);

    /// <summary>
    /// The scope a record of <paramref name="node"/> bound or skipped under <paramref name="graph"/> is judged against (FABRIC-RUN1f, the cutover
    /// design §9: like with like): a record naming the whole graph's digest where the node now has a place was written before, and is judged on
    /// the graph's digest and the run's old name, as it was; any other on <see cref="ScopeOf"/>.
    /// </summary>
    public NodeScope Own(string node, string? graph) =>
        Places.ContainsKey(node) && graph is not null && !graph.StartsWith(NodeGraphs.PlacePrefix, StringComparison.Ordinal)
            ? new(GraphDigest, Legacy ?? Run)
            : ScopeOf(node);

    /// <summary>
    /// Each unit's current revision, by <see cref="WorkUnit.Id"/>: a branch's full tip, null for a unit with none (a run's own). A complete
    /// snapshot of the units the nodes' statuses name: a record's unit that a node not named in <see cref="Subjects"/> acts on, left out or a
    /// branch named with none, refuses the evaluation (FABRIC-WIRE1e). A named subject's is judged for its own nodes alone (FABRIC-CUTOVER1a).
    /// </summary>
    public IReadOnlyDictionary<string, string?> Revisions { get; init; } = ImmutableDictionary<string, string?>.Empty;

    /// <summary>
    /// A node's own revision where it is not its unit's, by node id (FABRIC-PERFORM1m): a branch unit's observed plugin node, bound to its
    /// branch's tip read in the caller's pass rather than the end fact's revision, which lags it; a run's node after <c>handed</c>, bound to its
    /// landing's tip as the landing's record keeps it (FABRIC-CUTOVER1a). The node's binding is made and lapses on it; every other node on
    /// <see cref="Revisions"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string?> NodeRevisions { get; init; } = ImmutableDictionary<string, string?>.Empty;

    /// <summary>
    /// The unit each node observes where it is not its record's, by node id (FABRIC-CUTOVER1a; the cutover design §2.1, §2.2): a run's record
    /// keeps every node of its graph under <c>run:&lt;run&gt;</c>, each observing the quest, the branch or the run its kind names. The node is
    /// observed and bound there, and its binding names it (<see cref="NodeAcceptance.Subject"/>), lapsing as <see cref="NodeLapse.Subject"/>
    /// when it moves. A node named with null had no subject found this pass, and a branch subject with no revision handed in was not read:
    /// either is judged on its wires and offered no start (<see cref="WireEligibility.Withheld"/>), a started one neither observed nor lapsed,
    /// while the rest are evaluated. A node left out observes its record's unit, as every node did before.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Subjects { get; init; } = ImmutableDictionary<string, string?>.Empty;

    /// <summary>The trigger firings already recorded (<see cref="WireFiring.Key"/>): a trigger fires once per receipt, whatever replays.</summary>
    public IReadOnlySet<string> Fired { get; init; } = ImmutableHashSet<string>.Empty;

    /// <summary>How many performed attempts may run at once (§A3's local limits): a fork starts its targets within it.</summary>
    public int Limit { get; init; } = WireEvaluation.DefaultLimit;

    /// <summary>
    /// How many waited attempts may wait at once, apart from <see cref="Limit"/> (FABRIC-WIRE1e): a parked <c>wait.unit</c> runs no process,
    /// so it never holds a performed node's slot.
    /// </summary>
    public int WaitLimit { get; init; } = WireEvaluation.DefaultWaitLimit;

    /// <summary>The units an observed built-in is read from, by id: where one is absent, its active node is not observed here.</summary>
    public IReadOnlyDictionary<string, WorkUnit> Units { get; init; } = ImmutableDictionary<string, WorkUnit>.Empty;

    /// <summary>
    /// The git reads an observation needs (NODE1's choice 8): <c>pushed</c>'s remote-tracking ref, and an ahead <c>pull-request</c>'s
    /// ancestry. Absent, those outcomes stay unknown, never guessed.
    /// </summary>
    public NodeGitReads? Git { get; init; }

    /// <summary>
    /// The second opinion's gate for each unit's work, by <see cref="WorkUnit.Id"/>, as <see cref="OpinionGate.JudgeAsync"/> judged it: the
    /// caller's read, which an <c>opinion</c> node observes (FABRIC-WIRE1g, <see cref="OpinionNode"/>). A unit it leaves out has no judgement
    /// here, so its opinion stays pending, never guessed.
    /// </summary>
    public IReadOnlyDictionary<string, OpinionGateState> Opinions { get; init; } = ImmutableDictionary<string, OpinionGateState>.Empty;

    /// <summary>
    /// The review's gate for each unit's work, by <see cref="WorkUnit.Id"/>, as <see cref="ReviewGate"/> read it: the caller's read, which a
    /// <c>look</c> node observes only for the choice that turns the look off (FABRIC-WIRE1h, <see cref="LookNode"/>). A unit it leaves out
    /// has no read, so its look stays the person's to answer.
    /// </summary>
    public IReadOnlyDictionary<string, ReviewGateState> Reviews { get; init; } = ImmutableDictionary<string, ReviewGateState>.Empty;

    /// <summary>
    /// The go-aheads each unit's work holds on its ask, by <see cref="WorkUnit.Id"/>, as the service answers them (D135): the caller's read,
    /// which a started <c>go-ahead</c> node observes (FABRIC-NODE1b, <see cref="GoAheadNode"/>). A unit it leaves out has none read, so its
    /// go-ahead waits, never guessed.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<GoAheadView>> GoAheads { get; init; } = ImmutableDictionary<string, IReadOnlyList<GoAheadView>>.Empty;
}

/// <summary>One trigger's firing (§E3): its wire started its target on one receipt. Its key is what the record writer keeps.</summary>
/// <param name="Key">A digest of the graph, run, wire and receipt: the same firing is the same key, so a replay fires nothing.</param>
public sealed record WireFiring(string Key, string Wire, string Receipt, string Node);

/// <summary>
/// One node's eligibility as evaluated (§E2's blocked → ready): whether its groups and data inputs are satisfied, the witness of each
/// <c>anyOf</c> group, the receipts it would consume, and the binding a start takes.
/// </summary>
public sealed record WireEligibility(string Node, bool Eligible)
{
    /// <summary>Each <c>anyOf</c> group's witness wire, by group id: one, by wire-id order when several qualify at once.</summary>
    public IReadOnlyDictionary<string, string> Witnesses { get; init; } = ImmutableDictionary<string, string>.Empty;

    /// <summary>The receipts its groups consume, in ordinal order: every <c>allOf</c> member's, each <c>anyOf</c>'s witness's.</summary>
    public IReadOnlyList<string> Receipts { get; init; } = [];

    /// <summary>The wires not satisfied, in wire-id order: what it waits on.</summary>
    public IReadOnlyList<string> Waiting { get; init; } = [];

    /// <summary>
    /// The binding its next attempt starts on, while eligible on a subject it can be judged on: its inputs from <c>data</c> wires and its
    /// receipts, and the subject it observes where that is not its record's unit.
    /// </summary>
    public NodeAcceptance? Binding { get; init; }

    /// <summary>
    /// Why it is offered no start whatever its wires say (FABRIC-CUTOVER1a): <see cref="WireEvaluation.NoSubject"/>, no subject found for it this
    /// pass, or <see cref="WireEvaluation.NoRevision"/>, its branch subject's revision not handed in; null where it is judged on its subject.
    /// </summary>
    public string? Withheld { get; init; }

    /// <summary>What a <c>wait.unit</c>'s <c>wait</c> wire names, for its adapter (WAITUNIT1): never an eligibility condition.</summary>
    public WaitTarget? Waits { get; init; }

    /// <summary>The trigger firings a start on <see cref="Binding"/> makes: each trigger receipt it consumes that has not fired yet.</summary>
    public IReadOnlyList<WireFiring> Firings { get; init; } = [];
}

/// <summary>Why the evaluation moved a node (<see cref="WireMove.Why"/>).</summary>
public static class WireWhy
{
    /// <summary>An observed built-in's records said an outcome, through <see cref="BuiltInNodes.Observe(WorkUnit, NodeDeclaration, NodeAcceptance, NodeGitReads?)"/>.</summary>
    public const string Observed = "observed";

    /// <summary>Its binding's revision, graph or version is no longer current (<see cref="NodeAcceptances.Lapsed"/>).</summary>
    public const string Lapsed = "lapsed";

    /// <summary>A receipt or an input its binding rests on is no longer current: an upstream invalidated, a witness lost.</summary>
    public const string Upstream = "upstream";

    /// <summary>A skip of another graph or run: it holds for its own alone.</summary>
    public const string SkipScope = "skip-scope";

    /// <summary>
    /// A failure or a rejection of another run (FABRIC-WIRE1f): an attempt's outcome other than a reach is its own run's alone, so a new run
    /// lapsed it, as a skip of another run lapses, and the node was judged again in this run. No longer given since FABRIC-RUN1e, which keeps
    /// such a failure for the person; kept so a history entry written with it still reads.
    /// </summary>
    public const string RunScope = "run-scope";

    /// <summary>An <c>all</c> join's members settled: every one reached or skipped, or one failed.</summary>
    public const string Members = "members";

    /// <summary>A <c>stop</c> sink reached on the receipt of the path into it: a failure or a rejection its binding rests on (FABRIC-WIRE1g).</summary>
    public const string Path = "path";

    /// <summary>Its wires and inputs are satisfied.</summary>
    public const string Eligible = "eligible";

    /// <summary>Its wires or inputs are no longer satisfied.</summary>
    public const string Ineligible = "ineligible";
}

/// <summary>One move the evaluation made through <see cref="NodeLifecycle.Apply"/>: the move, what it came to, and why.</summary>
/// <param name="Detail">The lapse's reason, or the receipt or input no longer current; null where the why says it all.</param>
public sealed record WireMove(string Node, NodeMove Move, NodeMoved Moved, string Why, string? Detail);

/// <summary>
/// A start the evaluation proposes (ready → active), for its caller to apply through <see cref="NodeLifecycle.Apply"/> when it observes,
/// dispatches, offers the task or waits (PERFORM1, HUMAN1, WAITUNIT1). Its move names the binding and the run (FABRIC-WIRE1e); it carries
/// the trigger firings it consumes, for the record writer.
/// </summary>
public sealed record WireStart(string Node, NodeMove Move)
{
    public IReadOnlyList<WireFiring> Firings { get; init; } = [];
}

/// <summary>What one evaluation came to (FABRIC-WIRE1b).</summary>
/// <param name="Statuses">Every node's status after the moves, by node id.</param>
/// <param name="Moves">The moves made, in the order made: invalidations, observations, joins, then eligibility.</param>
/// <param name="Refused">Moves the one table refused, kept apart: none is expected, and each is a defect to name.</param>
/// <param name="Eligibility">Each node's eligibility on the statuses after the invalidations, in declaration order.</param>
/// <param name="Starts">The ready nodes that may start now, in node-id order, within the limit.</param>
/// <param name="Queued">The ready dispatched nodes over the limit, in node-id order: started once a running one settles.</param>
public sealed record WireEvaluated(
    IReadOnlyDictionary<string, NodeStatus> Statuses,
    IReadOnlyList<WireMove> Moves,
    IReadOnlyList<WireMove> Refused,
    IReadOnlyList<WireEligibility> Eligibility,
    IReadOnlyList<WireStart> Starts,
    IReadOnlyList<string> Queued)
{
    /// <summary>
    /// Why the evaluation judged nothing, or null (FABRIC-WIRE1e): <see cref="WireEvaluation.NoRevision"/> when the caller's revisions
    /// are not a complete snapshot. A refused evaluation moves nothing, offers no start and returns the statuses as handed in.
    /// </summary>
    public string? Refusal { get; init; }

    /// <summary>The units the refusal names, in ordinal order: each a unit a node acts on with no revision handed in for it.</summary>
    public IReadOnlyList<string> Missing { get; init; } = [];

    /// <summary>
    /// Whether the evaluator moved nothing: evaluated again on the same statuses, it moves nothing. It says nothing of pending starts
    /// (<see cref="Starts"/>, <see cref="Queued"/>) nor of execution finished (FABRIC-WIRE1e), and a refused evaluation moved nothing too:
    /// read <see cref="Refusal"/> first.
    /// </summary>
    public bool Settled => Moves.Count == 0;

    /// <summary>The eligibility of <paramref name="node"/>, or null for a node not evaluated.</summary>
    public WireEligibility? Of(string node) => Eligibility.FirstOrDefault(each => each.Node == node);
}

/// <summary>
/// The wires evaluated (FABRIC-WIRE1b; the fabric events design §E3, §E2, §E7): which nodes are eligible, on which receipts, and which
/// may start. Pure, and stored nowhere: every move goes through <see cref="NodeLifecycle.Apply"/>, never around it, and the statuses
/// after them are returned for the record writer to keep.
/// </summary>
/// <remarks>
/// <para><b>One evaluation, in four passes, one move per node at most,</b> on a complete snapshot of the units' revisions or not at all
/// (<see cref="WireEvaluated.Refusal"/>): every bound node whose binding lapsed, or rests on a receipt or input no longer current,
/// invalidated, transitively to a fixpoint; the active observed built-ins still bound read from their records with the caller's git
/// reads; each started <c>all</c> join judged on its members; then each blocked, ready or invalidated node judged on its wires. Validity
/// comes before observation (FABRIC-WIRE1e), so no start rests on a receipt the same evaluation would invalidate. A node invalidated here
/// is judged again at the next evaluation, so the lapse is seen and no other witness is taken silently (canary 41). The caller evaluates
/// again after applying what it starts, until <see cref="WireEvaluated.Settled"/>.</para>
/// <para><b>A node observes a subject</b> (FABRIC-CUTOVER1a; the cutover design §2.1, §2.2): its record's unit, or on a run's record the quest,
/// the branch or the run its kind names (<see cref="WireRun.Subjects"/>). It is observed there, bound there at that subject's revision, and
/// lapses as <see cref="NodeLapse.Subject"/> once the subject moves; a named subject not found or not read withholds its own nodes alone.</para>
/// <para><b>Receipts</b> (<see cref="WireReceipt"/>): a reach's acceptance; this run's skip; a failure whose declared retries are spent
/// and a rejection, each of an attempt started in this graph and run. Each wire kind reads its own: <c>after</c> a reach or a skip,
/// <c>data</c> and <c>trigger</c> a reach, <c>failed</c> a failure, <c>rejected</c> a rejection.</para>
/// <para><b>A trigger fires once per receipt</b>: it makes a blocked or invalidated target eligible only on a receipt it has not fired on
/// (<see cref="WireRun.Fired"/>, or the receipt in the target's binding), keeps a ready target eligible (a declared retry or the
/// person's new attempt is no second firing), and holds a started attempt that rests on it.</para>
/// </remarks>
public static class WireEvaluation
{
    /// <summary>Performed attempts at once, by default: PROC1a's measured limit of four process starts at once (§A3).</summary>
    public const int DefaultLimit = 4;

    /// <summary>
    /// Waited attempts at once, by default: a graph's bound of 24 nodes (§E7), so every <c>wait.unit</c> of one graph may wait at once.
    /// A wait starts no process; its adapter reads a local projection (§E6).
    /// </summary>
    public const int DefaultWaitLimit = 24;

    /// <summary>The <c>all</c> join's failure code when a member failed with its retries exhausted.</summary>
    public const string MemberFailed = "member-failed";

    /// <summary>
    /// The evaluation's refusal when a unit a node acts on has no revision handed in (FABRIC-WIRE1e): a unit missing from
    /// <see cref="WireRun.Revisions"/>, or a branch's named with none. A quest's is null by design, and named so. For a node whose subject is
    /// named (<see cref="WireRun.Subjects"/>), why that node alone is withheld (FABRIC-CUTOVER1a).
    /// </summary>
    public const string NoRevision = "no-revision";

    /// <summary>Why a node is withheld when no subject was found for it this pass (FABRIC-CUTOVER1a): a landing not made yet, a session not read.</summary>
    public const string NoSubject = "no-subject";

    /// <summary>
    /// Evaluates <paramref name="wires"/> over <paramref name="nodes"/> and their <paramref name="statuses"/> for <paramref name="run"/>.
    /// A node with no status is not evaluated; one never moved is <see cref="NodeLifecycle.Start"/>'s.
    /// </summary>
    public static WireEvaluated Evaluate(
        IReadOnlyList<NodeDeclaration> nodes, IReadOnlyList<Wire> wires, IReadOnlyDictionary<string, NodeStatus> statuses, WireRun run)
    {
        var declared = new List<NodeDeclaration>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (statuses.ContainsKey(node.Id) && seen.Add(node.Id)) declared.Add(node);
        }

        var now = statuses.ToDictionary(each => each.Key, each => each.Value, StringComparer.Ordinal);
        var moved = new HashSet<string>(StringComparer.Ordinal);
        var moves = new List<WireMove>();
        var refused = new List<WireMove>();

        void Make(NodeDeclaration node, NodeMove move, string why, string? detail)
        {
            var result = NodeLifecycle.Apply(node, now[node.Id], move);
            var made = new WireMove(node.Id, move, result, why, detail);
            if (!result.Moved)
            {
                refused.Add(made);
                return;
            }

            now[node.Id] = result.Status;
            moved.Add(node.Id);
            moves.Add(made);
        }

        // 0. FABRIC-WIRE1e: the revisions are a complete snapshot, or nothing is judged: a unit read as having none would bind and lapse
        // on a guess. A quest's is named, as null. FABRIC-CUTOVER1a: a node whose subject is named is judged on that subject alone, for itself.
        var missing = declared.Where(node => !run.Subjects.ContainsKey(node.Id)).Select(node => now[node.Id].Unit).Distinct(StringComparer.Ordinal)
            .Where(unit => !Revised(run, unit)).Order(StringComparer.Ordinal).ToList();
        if (missing.Count > 0) return new(now, [], [], [], [], []) { Refusal = NoRevision, Missing = missing };
        var subjects = declared.ToDictionary(node => node.Id, node => SubjectOf(run, node, now[node.Id]), StringComparer.Ordinal);

        // 1. Invalidations, transitively, before anything is observed (FABRIC-WIRE1e): a node whose binding lapsed is invalidated, never
        // observed, so no receipt the same evaluation would invalidate is exposed, and nothing starts on one. Each round takes away the
        // receipts of what the last one lapsed, so it ends within the nodes.
        var lapsing = new Dictionary<string, (string Why, string? Detail)>(StringComparer.Ordinal);
        for (var found = true; found;)
        {
            found = false;
            var current = Receipts(declared, now, run, lapsing.Keys).Values.Select(receipt => receipt.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var node in declared)
            {
                if (lapsing.ContainsKey(node.Id) || Lapse(node, now[node.Id], run, current, subjects[node.Id]) is not { } lapse) continue;
                lapsing[node.Id] = lapse;
                found = true;
            }
        }

        foreach (var node in declared)
        {
            if (lapsing.TryGetValue(node.Id, out var lapse)) Make(node, new NodeMove(NodeStates.Invalidated, NodeCause.Daoris), lapse.Why, lapse.Detail);
        }

        // 2. The observed built-ins still bound, read from their records: NODE1's choice 8, with the caller's git reads; and D157's gates, each
        // from its records as the caller read them: an opinion from its gate's judgement (FABRIC-WIRE1g), a look from its gate's verdict and a
        // go-ahead from D135's answers (FABRIC-NODE1b). An observation moves an active or unknown node, which holds no receipt, so it takes none
        // away from what step 1 judged current. FABRIC-CUTOVER1a: each from its subject's records and reads, never its record's unit's; a node
        // withheld, its subject not found or not read, is not observed.
        foreach (var node in declared.Where(node => !moved.Contains(node.Id)))
        {
            var status = now[node.Id];
            if (status.State is not (NodeStates.Active or NodeStates.Unknown) || status.Binding is not { } binding
                || !(BuiltInNodes.Observed.Contains(node.Kind) || node.Kind is NodeKinds.Opinion or NodeKinds.Look or NodeKinds.GoAhead)
                || subjects[node.Id] is not { Withheld: null, Subject: { } subject } || !run.Units.TryGetValue(subject, out var unit))
            {
                continue;
            }

            var observation = node.Kind switch
            {
                NodeKinds.Opinion => OpinionNode.Observe(unit, node, binding, run.Opinions.GetValueOrDefault(subject)),
                NodeKinds.Look => LookNode.Observe(unit, node, binding, run.Reviews.GetValueOrDefault(subject)),
                NodeKinds.GoAhead => GoAheadNode.Observe(unit, node, binding, run.GoAheads.GetValueOrDefault(subject)),
                _ => BuiltInNodes.Observe(unit, node, binding, run.Git),
            };
            if (observation.Move is not { } move || (status.State == NodeStates.Unknown && move.To == NodeStates.Unknown)) continue;

            // A record's skip holds for the attempt's graph and run, as the person's does for theirs. FABRIC-RUN1f: an attempt with no scope kept
            // takes its binding's own rule, a place with a place and a graph written before with that graph, so the lifecycle compares like with like.
            Make(node, move.To == NodeStates.Skipped ? move with { Scope = status.Scope ?? run.Own(node.Id, binding.GraphDigest) } : move, WireWhy.Observed, observation.Said);
        }

        // 3. Each started sink, on the receipt of the path into it (FABRIC-WIRE1g; §E7's synthetic sink, D173's FABRIC-WORKFLOW2 note,
        // choice 5): a failure or a rejection its binding rests on, read through a failed or rejected wire. Any other receipt, a success's or
        // a skip's, never reaches it; one no longer current invalidated it above; and a reached sink is no longer active, so it is reached once.
        var receipts = Receipts(declared, now, run, []);
        foreach (var node in declared.Where(node => node.Kind == NodeKinds.Stop && !moved.Contains(node.Id)))
        {
            var status = now[node.Id];
            if (status.State != NodeStates.Active || status.Binding is not { } binding) continue;
            var path = PathInto(node.Id, wires, receipts).FirstOrDefault(receipt => binding.PrerequisiteReceipts.Contains(receipt, StringComparer.Ordinal));
            if (path is null) continue;
            Make(node, new NodeMove(NodeStates.Reached, NodeCause.Authority) { Source = NodeSource.Path, Attempt = status.Attempt, Acceptance = binding }, WireWhy.Path, path);
        }

        // Then each started join, on its members' receipts alone (FABRIC-WIRE1e): its binding's, every one still current, or it was
        // invalidated above. A failure it consumed through its own failed wire is a condition of its start, never a member's failure.
        foreach (var node in declared.Where(node => node.Kind == NodeKinds.All && !moved.Contains(node.Id)))
        {
            var status = now[node.Id];
            if (status.State != NodeStates.Active || status.Binding is not { } binding) continue;
            var failure = Members(node.Id, wires)
                .Select(member => receipts.GetValueOrDefault(member))
                .FirstOrDefault(receipt => receipt is { Kind: WireReceiptKinds.Failed } && binding.PrerequisiteReceipts.Contains(receipt.Id, StringComparer.Ordinal))?.Id;
            Make(node, failure is null
                ? new NodeMove(NodeStates.Reached, NodeCause.Authority) { Source = NodeSource.Members, Attempt = status.Attempt, Acceptance = binding }
                : new NodeMove(NodeStates.Failed, NodeCause.Authority) { Source = NodeSource.Members, Attempt = status.Attempt, Code = MemberFailed },
                WireWhy.Members, failure);
        }

        // 4. Eligibility, on the receipts as they now stand.
        receipts = Receipts(declared, now, run, []);
        var eligibility = new List<WireEligibility>();
        foreach (var node in declared)
        {
            var status = now[node.Id];
            var judged = Eligible(node, status, wires, receipts, run, subjects[node.Id]);
            eligibility.Add(judged);
            if (moved.Contains(node.Id)) continue;
            var to = status.State switch
            {
                NodeStates.Blocked when judged.Eligible => NodeStates.Ready,
                NodeStates.Ready when !judged.Eligible => NodeStates.Blocked,
                NodeStates.Invalidated => judged.Eligible ? NodeStates.Ready : NodeStates.Blocked,
                _ => null,
            };

            if (to is not null)
            {
                Make(node, new NodeMove(to, NodeCause.Daoris), judged.Eligible ? WireWhy.Eligible : WireWhy.Ineligible, judged.Waiting.FirstOrDefault());
            }
        }

        // 5. The starts: every ready node, a performed one within the execution limit and a waited one within the waiting limit (§A3;
        // FABRIC-WIRE1e: a parked wait runs no process, so it never holds a performed node's slot), in node-id order.
        var slots = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [NodeReach.Performed] = Math.Max(0, run.Limit - Running(NodeReach.Performed)),
            [NodeReach.Waited] = Math.Max(0, run.WaitLimit - Running(NodeReach.Waited)),
        };
        var starts = new List<WireStart>();
        var queued = new List<string>();
        foreach (var judged in eligibility.OrderBy(each => each.Node, StringComparer.Ordinal))
        {
            var status = now[judged.Node];
            if (status.State != NodeStates.Ready || !judged.Eligible || judged.Binding is null || status.Unreconciled is not null) continue;
            if (NodeAuthorities.Of(declared.First(node => node.Id == judged.Node)) is { Dispatches: true } authority)
            {
                if (slots[authority.Reach] == 0)
                {
                    queued.Add(judged.Node);
                    continue;
                }

                slots[authority.Reach]--;
            }

            starts.Add(new WireStart(judged.Node, new NodeMove(NodeStates.Active, NodeCause.Daoris) { Binding = judged.Binding, Scope = run.ScopeOf(judged.Node) })
            {
                Firings = judged.Firings,
            });
        }

        return new(now, moves, refused, eligibility, starts, queued);

        // The dispatched attempts of one reach not settled yet: active, unknown, or unreconciled whatever moved the node off it.
        int Running(string reach) => declared.Count(node => NodeAuthorities.Of(node) is { Dispatches: true } authority && authority.Reach == reach
            && (now[node.Id].State is NodeStates.Active or NodeStates.Unknown || now[node.Id].Unreconciled is not null));
    }

    /// <summary>An <c>all</c> join's members, in wire-id order: its <c>after</c> wires' sources (§E7).</summary>
    private static IEnumerable<string> Members(string join, IReadOnlyList<Wire> wires) =>
        wires.Where(wire => wire.Kind == WireKinds.After && wire.To is { Unit: null } to && to.Node == join && wire.From is { Unit: null })
            .OrderBy(wire => wire.Id, StringComparer.Ordinal)
            .Select(wire => wire.From!.Node);

    /// <summary>
    /// The current receipts of the paths into a sink, in wire-id order (FABRIC-WIRE1g): each <c>failed</c> wire's failure and each <c>rejected</c>
    /// wire's rejection. A success's or a skip's receipt is no path.
    /// </summary>
    private static IEnumerable<string> PathInto(string sink, IReadOnlyList<Wire> wires, Dictionary<string, WireReceipt> receipts) =>
        wires.Where(wire => wire.To is { Unit: null } to && to.Node == sink && wire.From is { Unit: null })
            .OrderBy(wire => wire.Id, StringComparer.Ordinal)
            .Select(wire => receipts.GetValueOrDefault(wire.From!.Node) is { } receipt
                && (wire.Kind, receipt.Kind) is (WireKinds.Failed, WireReceiptKinds.Failed) or (WireKinds.Rejected, WireReceiptKinds.Rejected) ? receipt.Id : null)
            .OfType<string>();

    /// <summary>
    /// Whether the caller handed in a revision for <paramref name="unit"/> (FABRIC-WIRE1e): named in <see cref="WireRun.Revisions"/>, and not
    /// null where <see cref="WireRun.Units"/> knows it is a branch. A quest's is null by design.
    /// </summary>
    private static bool Revised(WireRun run, string unit) =>
        run.Revisions.TryGetValue(unit, out var revision)
        && (revision is not null || !(run.Units.TryGetValue(unit, out var known) && known.Kind == WorkUnitKind.Branch));

    /// <summary>
    /// The subject a node observes this pass and the revision it is judged on there, or why it cannot be judged on it (FABRIC-CUTOVER1a):
    /// <see cref="NoSubject"/> with no subject, <see cref="NoRevision"/> with the subject whose revision was not read.
    /// </summary>
    private sealed record Subjected(string? Subject, string? Revision, string? Withheld);

    /// <summary>
    /// The subject <paramref name="node"/> observes this pass (FABRIC-CUTOVER1a; the cutover design §2.2): the one <see cref="WireRun.Subjects"/>
    /// names, else its record's unit on that unit's revision, as before. A named subject is judged for this node alone: none found, or a branch
    /// whose revision was not handed in (its own in <see cref="WireRun.NodeRevisions"/> first), withholds the node, and the rest are evaluated.
    /// </summary>
    private static Subjected SubjectOf(WireRun run, NodeDeclaration node, NodeStatus status)
    {
        if (!run.Subjects.TryGetValue(node.Id, out var subject)) return new(status.Unit, Revision(run, status.Unit, node.Id), null);
        if (subject is null) return new(null, null, NoSubject);
        var read = run.NodeRevisions.TryGetValue(node.Id, out var revision) || run.Revisions.TryGetValue(subject, out revision);
        return read && (revision is not null || !(run.Units.TryGetValue(subject, out var known) && known.Kind == WorkUnitKind.Branch))
            ? new(subject, revision, null)
            : new(subject, null, NoRevision);
    }

    /// <summary>
    /// Each node's one current receipt, by node id: its reach, this run's skip, a spent failure or a rejection of an attempt started in
    /// this graph and run (FABRIC-WIRE1e: the attempt's scope, never the caller's run hashed into another run's outcome). The nodes in
    /// <paramref name="lapsing"/> have none, since they are about to be invalidated.
    /// </summary>
    private static Dictionary<string, WireReceipt> Receipts(
        List<NodeDeclaration> nodes, Dictionary<string, NodeStatus> statuses, WireRun run, IEnumerable<string> lapsing)
    {
        var gone = lapsing.ToHashSet(StringComparer.Ordinal);
        var receipts = new Dictionary<string, WireReceipt>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            var status = statuses[node.Id];
            if (gone.Contains(node.Id)) continue;

            // FABRIC-RUN1f (the cutover design §9): an id is worked out from its status's own scope, a place's from the place and one written
            // before from the whole graph's digest and the run's old name, so a receipt kept before keeps its id and nothing resting on it lapses.
            var skipped = run.Own(node.Id, status.Scope?.GraphDigest);
            var (ours, bound) = Ours(node, status, run);
            WireReceipt? receipt = status.State switch
            {
                NodeStates.Reached when status.Current is { } accepted => new(accepted.Id, WireReceiptKinds.Reached, node.Id) { Acceptance = accepted },
                NodeStates.Skipped when NodeLifecycle.Skips(status, skipped) =>
                    new(Digest("skp-", skipped.GraphDigest, skipped.Run, status.Unit, node.Id), WireReceiptKinds.Skipped, node.Id),
                NodeStates.Failed when ours is not null && !Retries(node, status) =>
                    new(Digest("fld-", bound.GraphDigest, bound.Run, ours.Id), WireReceiptKinds.Failed, node.Id),
                NodeStates.Rejected when ours is not null => new(Digest("rej-", bound.GraphDigest, bound.Run, ours.Id), WireReceiptKinds.Rejected, node.Id),
                _ => null,
            };

            if (receipt is not null) receipts[node.Id] = receipt;
        }

        return receipts;
    }

    /// <summary>
    /// The binding of <paramref name="status"/>'s attempt where it was started in this graph and run, by its own rule (FABRIC-RUN1f,
    /// <see cref="WireRun.Own"/>), and that scope; a null binding where it is another's or there is none.
    /// </summary>
    private static (NodeAcceptance? Binding, NodeScope Scope) Ours(NodeDeclaration node, NodeStatus status, WireRun run)
    {
        var scope = run.Own(node.Id, status.Binding?.GraphDigest);
        return (status.Binding is { } binding && binding.GraphDigest == scope.GraphDigest && status.Scope == scope ? binding : null, scope);
    }

    /// <summary>Whether Daoris still retries this failure (§D6): a transient code with attempts left, so its <c>failed</c> path is not taken yet.</summary>
    private static bool Retries(NodeDeclaration node, NodeStatus status) => status.Transient && status.Tries < (node.Retry?.MaxAttempts ?? 1);

    /// <summary>
    /// Why a node is invalidated now, or null: a skip of another graph or run; a bound node whose binding's subject, revision, graph or version
    /// is no longer current; or one resting on a receipt or an input's acceptance that is no longer current.
    /// </summary>
    private static (string Why, string? Detail)? Lapse(NodeDeclaration node, NodeStatus status, WireRun run, HashSet<string> current, Subjected subjected)
    {
        // FABRIC-WIRE1g: a skip by its kind's record (an opinion's gate, NodeAuthority.SkipBy) is that record's judgement of its binding, so
        // a new revision or a lost receipt lapses it, as it lapses an outcome; the person's skip is their word for the work, and holds.
        var recorded = status.State == NodeStates.Skipped && status.By is { } by && by != NodeSource.Person && NodeAuthorities.Of(node)?.SkipBy == by;
        if (status.State == NodeStates.Skipped)
        {
            if (!NodeLifecycle.Skips(status, run.Own(node.Id, status.Scope?.GraphDigest))) return (WireWhy.SkipScope, null);
            if (!recorded) return null;
        }

        if (!(recorded || NodeStates.Bound.Contains(status.State)) || status.Binding is not { } binding) return null;

        // FABRIC-CUTOVER1a (the cutover design §2.2): a binding lapses as `subject` once its node's subject moves, whatever the revision. A node
        // withheld this pass, its subject not found or its revision not read, is judged on its binding's own, so nothing lapses on a read not
        // made, and only a moved subject lapses it there.
        var subject = subjected.Subject ?? binding.Observes;
        var revision = subjected.Withheld is null ? subjected.Revision : binding.SubjectRevision;

        // FABRIC-RUN1e, in place of WIRE1f's lapse of another run's failure: a failure or a rejection of another graph or run is kept for the
        // person, since a run begins at a graph edit and a Setup save can edit a derived graph, so edits back and forth would restart failed
        // work past its retry bound for ever. Only new work lapses it, a new subject or a new revision, as in its own graph and run; the
        // person's new attempt restarts it. It is no receipt here (Receipts reads this graph's and run's alone), so nothing fires on it.
        // FABRIC-RUN1f: this graph and run are the node's place and the run, or for an attempt bound before, the graph and the run's old name.
        var (ours, own) = Ours(node, status, run);
        if (status.State is NodeStates.Failed or NodeStates.Rejected && ours is null)
        {
            return !string.Equals(binding.Observes, subject, StringComparison.Ordinal) ? (WireWhy.Lapsed, NodeLapse.Subject)
                : string.Equals(binding.SubjectRevision, revision, StringComparison.Ordinal) ? null : (WireWhy.Lapsed, NodeLapse.Revision);
        }

        if (NodeAcceptances.Lapsed(binding, subject, revision, own.GraphDigest, node.Version) is { } why) return (WireWhy.Lapsed, why);
        var lost = binding.PrerequisiteReceipts.FirstOrDefault(receipt => !current.Contains(receipt))
            ?? binding.Inputs.Values.Select(port => port.Acceptance).FirstOrDefault(acceptance => acceptance is not null && !current.Contains(acceptance));
        return lost is null ? null : (WireWhy.Upstream, lost);
    }

    /// <summary>The revision a node is bound to and lapses on: its own where the caller gives one (FABRIC-PERFORM1m), else its unit's.</summary>
    private static string? Revision(WireRun run, string unit, string node) =>
        run.NodeRevisions.TryGetValue(node, out var own) ? own : run.Revisions.TryGetValue(unit, out var revision) ? revision : null;

    /// <summary>
    /// One node's eligibility on <paramref name="receipts"/>: its data inputs, its join members, and its groups (§E3, §E7); and the binding a
    /// start takes on its subject, none while it is withheld there (FABRIC-CUTOVER1a). An input from another subject is resolved on its producer's
    /// reach as any is: the producer's acceptance was judged on its own subject in step 1.
    /// </summary>
    private static WireEligibility Eligible(
        NodeDeclaration node, NodeStatus status, IReadOnlyList<Wire> wires, Dictionary<string, WireReceipt> receipts, WireRun run, Subjected subjected)
    {
        var join = node.Kind == NodeKinds.All;
        var into = wires.Where(wire => wire.To is { Unit: null } to && to.Node == node.Id).OrderBy(wire => wire.Id, StringComparer.Ordinal).ToList();
        var byId = into.GroupBy(wire => wire.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var waiting = new List<string>();
        var consumed = new List<string>();
        var firings = new List<WireFiring>();
        var witnesses = new Dictionary<string, string>(StringComparer.Ordinal);
        var inputs = new Dictionary<string, NodePort>(StringComparer.Ordinal);
        WireReceipt? From(Wire wire) => wire.From is { Unit: null } from && receipts.TryGetValue(from.Node, out var receipt) ? receipt : null;

        // A data wire is an input, mandatory whatever the groups say: a current reach exposing the port at the input's schema.
        foreach (var wire in into.Where(wire => wire.Kind == WireKinds.Data))
        {
            var port = From(wire) is { Kind: WireReceiptKinds.Reached, Acceptance: { } accepted } && wire.From!.Port is { } name
                ? name == WireEnd.Evidence ? accepted.Evidence : accepted.Outputs.GetValueOrDefault(name)
                : null;
            if (port is not null && wire.To.Port is { } input && node.Inputs.TryGetValue(input, out var schema) && port.Schema == schema) inputs[input] = port;
            else waiting.Add(wire.Id);
        }

        // An all join's members are its after wires' sources: every one reached or skipped, or one failed (§E7, the group's policy).
        if (join)
        {
            var members = into.Where(wire => wire.Kind == WireKinds.After).ToList();
            var failed = members.Select(From).FirstOrDefault(receipt => receipt?.Kind == WireReceiptKinds.Failed);
            if (failed is not null) consumed.Add(failed.Id);
            else
            {
                foreach (var member in members)
                {
                    if (From(member) is { Kind: WireReceiptKinds.Reached or WireReceiptKinds.Skipped } settled) consumed.Add(settled.Id);
                    else waiting.Add(member.Id);
                }
            }
        }

        (string? Receipt, WireFiring? Firing) Satisfied(Wire wire)
        {
            var receipt = From(wire);
            switch (wire.Kind)
            {
                case WireKinds.After when receipt?.Kind is WireReceiptKinds.Reached or WireReceiptKinds.Skipped:
                case WireKinds.Failed when receipt?.Kind == WireReceiptKinds.Failed:
                case WireKinds.Rejected when receipt?.Kind == WireReceiptKinds.Rejected:
                    return (receipt!.Id, null);

                case WireKinds.Trigger when receipt?.Kind == WireReceiptKinds.Reached:
                    // FABRIC-RUN1f: a firing kept before was keyed by the graph and the run's old name, so it is looked for under both rules.
                    var key = FiringKey(run, node.Id, wire.Id, receipt.Id);
                    var rests = status.Binding?.PrerequisiteReceipts.Contains(receipt.Id, StringComparer.Ordinal) == true;
                    var spent = rests || run.Fired.Contains(key) || (run.Places.ContainsKey(node.Id) && run.Fired.Contains(LegacyKey(run, wire.Id, receipt.Id)));
                    var firing = spent ? null : new WireFiring(key, wire.Id, receipt.Id, node.Id);
                    return status.State == NodeStates.Ready || (rests && NodeStates.Bound.Contains(status.State)) || !spent ? (receipt.Id, firing) : (null, null);

                default:
                    return (null, null);
            }
        }

        foreach (var group in NodeWires.Groups(node.Id, wires))
        {
            var members = group.Wires.Where(id => byId.ContainsKey(id) && !(join && byId[id].Kind == WireKinds.After)).ToList();
            if (members.Count == 0) continue;
            var judged = members.Select(id => (Wire: id, Said: Satisfied(byId[id]))).ToList();
            if (group.Mode == WireGroupModes.AnyOf)
            {
                // The witness a started attempt rests on is kept while it holds; otherwise the first by wire id.
                var kept = NodeStates.Bound.Contains(status.State) && status.Binding is { } binding
                    ? judged.FirstOrDefault(each => each.Said.Receipt is { } receipt && binding.PrerequisiteReceipts.Contains(receipt, StringComparer.Ordinal))
                    : default;
                var witness = kept.Wire is not null ? kept : judged.FirstOrDefault(each => each.Said.Receipt is not null);
                if (witness.Wire is null)
                {
                    waiting.AddRange(members);
                    continue;
                }

                witnesses[group.Id ?? witness.Wire] = witness.Wire;
                consumed.Add(witness.Said.Receipt!);
                if (witness.Said.Firing is { } firing) firings.Add(firing);
                continue;
            }

            foreach (var (wire, said) in judged)
            {
                if (said.Receipt is null) waiting.Add(wire);
                else
                {
                    consumed.Add(said.Receipt);
                    if (said.Firing is { } firing) firings.Add(firing);
                }
            }
        }

        var eligible = waiting.Count == 0;
        var bound = eligible && subjected.Withheld is null;
        return new WireEligibility(node.Id, eligible)
        {
            Witnesses = witnesses,
            Receipts = [.. consumed.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            Waiting = [.. waiting.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            Binding = bound
                ? new NodeAcceptance(status.Unit, subjected.Revision, run.GraphOf(node.Id), node.Id, node.Version, status.Attempt + 1)
                {
                    // Named only where it is not the record's unit, so every binding made as before keeps its id.
                    Subject = subjected.Subject == status.Unit ? null : subjected.Subject,
                    Inputs = inputs,
                    PrerequisiteReceipts = [.. consumed.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
                }
                : null,
            Withheld = subjected.Withheld,
            Waits = into.FirstOrDefault(wire => wire.Kind == WireKinds.Wait)?.Waits,
            Firings = bound ? firings : [],
        };
    }

    /// <summary>
    /// A trigger's firing key: its target's place (FABRIC-RUN1f), else the graph, the run, the wire and the receipt, so an edit elsewhere fires
    /// nothing again.
    /// </summary>
    public static string FiringKey(WireRun run, string target, string wire, string receipt) => Digest("trg-", run.GraphOf(target), run.Run, wire, receipt);

    /// <summary>A firing's key as it was kept before FABRIC-RUN1f: the whole graph's digest and the run's old name.</summary>
    private static string LegacyKey(WireRun run, string wire, string receipt) => Digest("trg-", run.GraphDigest, run.Legacy ?? run.Run, wire, receipt);

    private static string Digest(string prefix, params string?[] parts)
    {
        var text = new StringBuilder();
        foreach (var part in parts) text.Append(part?.Replace("\\", "\\\\").Replace("\n", "\\n") ?? "\\0").Append('\n');
        return prefix + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..32];
    }
}
