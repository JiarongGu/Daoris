using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using static Daoris.Driver.WireEligible;
using static Daoris.Driver.WireLapsing;
using static Daoris.Driver.WireReceipts;

namespace Daoris.Driver;

/// <summary>
/// The wires evaluated: which nodes are eligible, on which receipts, and which
/// may start. Pure, and stored nowhere: every move goes through <see cref="NodeLifecycle.Apply"/>, never around it, and the statuses
/// after them are returned for the record writer to keep.
/// </summary>
/// <remarks>
/// <para><b>One evaluation, in four passes, one move per node at most,</b> on a complete snapshot of the units' revisions or not at all
/// (<see cref="WireEvaluated.Refusal"/>): every bound node whose binding lapsed, or rests on a receipt or input no longer current,
/// invalidated, transitively to a fixpoint; the active observed built-ins still bound read from their records with the caller's git
/// reads; each started <c>all</c> join judged on its members; then each blocked, ready or invalidated node judged on its wires. Validity
/// comes before observation, so no start rests on a receipt the same evaluation would invalidate. A node invalidated here
/// is judged again at the next evaluation, so the lapse is seen and no other witness is taken silently. The caller evaluates
/// again after applying what it starts, until <see cref="WireEvaluated.Settled"/>.</para>
/// <para><b>A node observes a subject</b>: its record's unit, or on a run's record the quest,
/// the branch or the run its kind names (<see cref="WireRun.Subjects"/>). It is observed there, bound there at that subject's revision, and
/// lapses as <see cref="NodeLapse.Subject"/> once the subject moves; a named subject not found or not read withholds its own nodes alone.</para>
/// <para><b>Receipts</b> (<see cref="WireReceipt"/>): a reach's acceptance; this run's skip; a failure whose declared retries are spent
/// and a rejection, each of an attempt started in this graph and run. Each wire kind reads its own: <c>after</c> a reach or a skip,
/// <c>data</c> and <c>trigger</c> a reach, <c>failed</c> a failure, <c>rejected</c> a rejection, <c>when</c> a decision's reach with its outcome.</para>
/// <para><b>A trigger fires once per receipt</b>: it makes a blocked or invalidated target eligible only on a receipt it has not fired on
/// (<see cref="WireRun.Fired"/>, or the receipt in the target's binding), keeps a ready target eligible (a declared retry or the
/// person's new attempt is no second firing), and holds a started attempt that rests on it.</para>
/// </remarks>
public static class WireEvaluation
{
    /// <summary>Performed attempts at once, by default: the measured limit of four process starts at once.</summary>
    public const int DefaultLimit = 4;

    /// <summary>
    /// Waited attempts at once, by default: a graph's bound of 24 nodes, so every <c>wait.unit</c> of one graph may wait at once.
    /// A wait starts no process; its adapter reads a local projection.
    /// </summary>
    public const int DefaultWaitLimit = 24;

    /// <summary>The <c>all</c> join's failure code when a member failed with its retries exhausted.</summary>
    public const string MemberFailed = "member-failed";

    /// <summary>
    /// The evaluation's refusal when a unit a node acts on has no revision handed in: a unit missing from
    /// <see cref="WireRun.Revisions"/>, or a branch's named with none. A quest's is null by design, and named so. For a node whose subject is
    /// named (<see cref="WireRun.Subjects"/>), why that node alone is withheld.
    /// </summary>
    public const string NoRevision = "no-revision";

    /// <summary>Why a node is withheld when no subject is found for it this pass: a landing not made yet, a session not read.</summary>
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
        var held = new List<string>();

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

        // 0. The revisions are a complete snapshot, or nothing is judged: a unit read as having none would bind and lapse
        // on a guess. A quest's is named, as null. A node whose subject is named is judged on that subject alone, for itself.
        var missing = declared.Where(node => !run.Subjects.ContainsKey(node.Id)).Select(node => now[node.Id].Unit).Distinct(StringComparer.Ordinal)
            .Where(unit => !Revised(run, unit)).Order(StringComparer.Ordinal).ToList();
        if (missing.Count > 0) return new(now, [], [], [], [], []) { Refusal = NoRevision, Missing = missing };
        var subjects = declared.ToDictionary(node => node.Id, node => SubjectOf(run, node, now[node.Id]), StringComparer.Ordinal);

        // A step past a landing that leaves it nothing to deliver is Daoris's skip on the path, which holds while that landing does.
        WorkUnit? UnitOf(string id) =>
            subjects.GetValueOrDefault(id) is { Withheld: null, Subject: { } subject } && run.Units.TryGetValue(subject, out var unit) ? unit : null;
        NodeMove? Undelivered(NodeDeclaration node) => NothingToDeliver.Skip(node, declared, wires, now, UnitOf, run.ScopeOf(node.Id));
        bool Delivers(NodeDeclaration node) => Undelivered(node) is null;

        // 1. Invalidations, transitively, before anything is observed: a node whose binding lapsed is invalidated, never
        // observed, so no receipt the same evaluation would invalidate is exposed, and nothing starts on one. Each round takes away the
        // receipts of what the last one lapsed, so it ends within the nodes. A back wire's target goes first, so what rests on it lapses
        // after it, its source's receipt included.
        var backs = Backs(declared, now, wires, run);
        var lapsing = new Dictionary<string, (string Why, string? Detail)>(StringComparer.Ordinal);
        foreach (var back in backs) lapsing.TryAdd(back.Node, (WireWhy.Back, back.Wire));
        for (var found = true; found;)
        {
            found = false;
            var standing = Receipts(declared, now, run, lapsing.Keys);
            var current = standing.Values.SelectMany(Named).ToHashSet(StringComparer.Ordinal);
            foreach (var node in declared)
            {
                if (lapsing.ContainsKey(node.Id)) continue;
                var lapse = Lapse(node, now[node.Id], run, current, subjects[node.Id]) ?? WireTargets.Lapse(node, now[node.Id], run, now, wires)
                    ?? (WireSkips.Lapses(node, now[node.Id], wires, standing) && Delivers(node) ? (WireWhy.Upstream, null) : null);
                if (lapse is null) continue;
                lapsing[node.Id] = lapse.Value;
                found = true;
            }
        }

        foreach (var node in declared)
        {
            if (lapsing.TryGetValue(node.Id, out var lapse)) Make(node, new NodeMove(NodeStates.Invalidated, NodeCause.Daoris), lapse.Why, lapse.Detail);
        }

        // 2. The observed built-ins still bound, read from their records with the caller's git reads; and the gates, each
        // from its records as the caller read them: an opinion from its gate's judgement, a look from its gate's verdict and a
        // go-ahead from the service's answers. An observation moves an active or unknown node, which holds no receipt, so it takes none
        // away from what step 1 judged current. Each is read from its subject's records and reads, never its record's unit's; a node
        // withheld, its subject not found or not read, is not observed.
        foreach (var node in declared.Where(node => !moved.Contains(node.Id)))
        {
            var status = now[node.Id];
            if (status.State is not (NodeStates.Active or NodeStates.Unknown) || status.Binding is not { } binding
                || NodeKit.Of(node.Kind) is not IObserves observes
                || subjects[node.Id] is not { Withheld: null, Subject: { } subject } || !run.Units.TryGetValue(subject, out var unit))
            {
                continue;
            }

            var follows = Members(node.Id, wires).Select(id => nodes.FirstOrDefault(each => each.Id == id)).OfType<NodeDeclaration>().ToList();
            var observation = observes.Observe(NodeReading.Of(run, subject, unit, node, binding, follows));
            if (observation.Move is not { } move)
            {
                // A hold that keeps its node active says why on the node's record, which every reader of the run reads.
                if (observation.Holds is { } holds && status.State == NodeStates.Active && !string.Equals(status.Holds, holds, StringComparison.Ordinal))
                {
                    now[node.Id] = status with { Holds = holds };
                    held.Add(node.Id);
                }

                continue;
            }

            if (status.State == NodeStates.Unknown && move.To == NodeStates.Unknown) continue;

            // A record's skip holds for the attempt's graph and run, as the person's does for theirs. An attempt with no scope kept
            // takes its binding's own rule, a place with a place and a whole-graph binding with that graph, so the lifecycle compares like with like.
            Make(node, move.To == NodeStates.Skipped ? move with { Scope = status.Scope ?? run.Own(node.Id, binding.GraphDigest) } : move, WireWhy.Observed, observation.Said);
        }

        // 3. Each started sink, on the receipt of the path into it: a failure or a rejection its binding rests on, read through a failed or
        // rejected wire. Any other receipt, a success's or a skip's, never reaches it; one no longer current invalidated it above; and a
        // reached sink is no longer active, so it is reached once.
        var receipts = Receipts(declared, now, run, []);
        foreach (var node in declared.Where(node => node.Kind == NodeKinds.Stop && !moved.Contains(node.Id)))
        {
            var status = now[node.Id];
            if (status.State != NodeStates.Active || status.Binding is not { } binding) continue;
            var path = PathInto(node.Id, wires, receipts).FirstOrDefault(receipt => binding.PrerequisiteReceipts.Contains(receipt, StringComparer.Ordinal));
            if (path is null) continue;
            Make(node, new NodeMove(NodeStates.Reached, NodeCause.Authority) { Source = NodeSource.Path, Attempt = status.Attempt, Acceptance = binding }, WireWhy.Path, path);
        }

        // Then each started join, on its members' receipts alone: its binding's, every one still current, or it is
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

        // 4. Eligibility, on the receipts as they now stand, with the routes the outcomes reached leave untaken.
        receipts = Receipts(declared, now, run, []);
        var untaken = NotTaken(declared, now, wires, receipts);
        var eligibility = new List<WireEligibility>();
        foreach (var node in declared)
        {
            var status = now[node.Id];
            var judged = Eligible(node, status, wires, receipts, run, subjects[node.Id], untaken);
            eligibility.Add(judged);
            if (moved.Contains(node.Id)) continue;
            if (status.State is NodeStates.Blocked or NodeStates.Ready or NodeStates.Invalidated && WireSkips.Producer(node, wires, receipts) is { } producer)
            {
                Make(node, WireSkips.With(producer, run.ScopeOf(node.Id)), WireWhy.Path, producer);
                continue;
            }

            if (status.State is NodeStates.Blocked or NodeStates.Ready or NodeStates.Invalidated && Undelivered(node) is { } nothing)
            {
                Make(node, nothing, WireWhy.Path, null);
                continue;
            }

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

        // 5. The starts: every ready node, a performed one within the execution limit and a waited one within the waiting limit
        // (a parked wait runs no process, so it never holds a performed node's slot), in node-id order.
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

            starts.Add(new WireStart(judged.Node, new NodeMove(NodeStates.Active, NodeCause.Daoris) { Binding = judged.Binding, Scope = run.ScopeOf(judged.Node), At = run.Clock?.Invoke() })
            {
                Firings = judged.Firings,
            });
        }

        return new(now, moves, refused, eligibility, starts, queued)
        {
            NotTaken = [.. untaken.Keys.Order(StringComparer.Ordinal)],
            Backs = [.. backs.Where(back => moves.Any(move => move.Node == back.Node && move.Why == WireWhy.Back))],
            Held = held,
        };

        // The dispatched attempts of one reach not settled yet: active, unknown, or unreconciled whatever moved the node off it.
        int Running(string reach) => declared.Count(node => NodeAuthorities.Of(node) is { Dispatches: true } authority && authority.Reach == reach
            && (now[node.Id].State is NodeStates.Active or NodeStates.Unknown || now[node.Id].Unreconciled is not null));
    }

    /// <summary>An <c>all</c> join's members, in wire-id order: its <c>after</c> wires' sources; any node's are what it follows.</summary>
    internal static IEnumerable<string> Members(string join, IReadOnlyList<Wire> wires) =>
        wires.Where(wire => wire.Kind == WireKinds.After && wire.To is { Unit: null } to && to.Node == join && wire.From is { Unit: null })
            .OrderBy(wire => wire.Id, StringComparer.Ordinal)
            .Select(wire => wire.From!.Node);

    /// <summary>
    /// The current receipts of the paths into a sink, in wire-id order: each <c>failed</c> wire's failure and each <c>rejected</c>
    /// wire's rejection. A success's or a skip's receipt is no path.
    /// </summary>
    internal static IEnumerable<string> PathInto(string sink, IReadOnlyList<Wire> wires, Dictionary<string, WireReceipt> receipts) =>
        wires.Where(wire => wire.To is { Unit: null } to && to.Node == sink && wire.From is { Unit: null })
            .OrderBy(wire => wire.Id, StringComparer.Ordinal)
            .Select(wire => receipts.GetValueOrDefault(wire.From!.Node) is { } receipt
                && (wire.Kind, receipt.Kind) is (WireKinds.Failed, WireReceiptKinds.Failed) or (WireKinds.Rejected, WireReceiptKinds.Rejected) ? receipt.Id : null)
            .OfType<string>();

    /// <summary>
    /// Whether the caller handed in a revision for <paramref name="unit"/>: named in <see cref="WireRun.Revisions"/>, and not
    /// null where <see cref="WireRun.Units"/> knows it is a branch. A quest's is null by design.
    /// </summary>
    internal static bool Revised(WireRun run, string unit) =>
        run.Revisions.TryGetValue(unit, out var revision)
        && (revision is not null || !(run.Units.TryGetValue(unit, out var known) && known.Kind == WorkUnitKind.Branch));

    /// <summary>
    /// The subject a node observes this pass and the revision it is judged on there, or why it cannot be judged on it:
    /// <see cref="NoSubject"/> with no subject, <see cref="NoRevision"/> with the subject whose revision is not read.
    /// </summary>
    internal sealed record Subjected(string? Subject, string? Revision, string? Withheld);

    /// <summary>
    /// The subject <paramref name="node"/> observes this pass: the one <see cref="WireRun.Subjects"/>
    /// names, else its record's unit on that unit's revision. A named subject is judged for this node alone: none found, or a branch
    /// whose revision is not handed in (its own in <see cref="WireRun.NodeRevisions"/> first), withholds the node, and the rest are evaluated.
    /// </summary>
    internal static Subjected SubjectOf(WireRun run, NodeDeclaration node, NodeStatus status)
    {
        if (!run.Subjects.TryGetValue(node.Id, out var subject)) return new(status.Unit, Revision(run, status.Unit, node.Id), null);
        if (subject is null) return new(null, null, NoSubject);
        var read = run.NodeRevisions.TryGetValue(node.Id, out var revision) || run.Revisions.TryGetValue(subject, out revision);
        return read && (revision is not null || !(run.Units.TryGetValue(subject, out var known) && known.Kind == WorkUnitKind.Branch))
            ? new(subject, revision, null)
            : new(subject, null, NoRevision);
    }

    public static string OutcomeReceipt(string acceptance, string outcome) => WireReceipts.OutcomeReceipt(acceptance, outcome);

    public static IReadOnlyList<string> NotTakenOn(
        IReadOnlyList<NodeDeclaration> nodes, IReadOnlyList<Wire> wires, IReadOnlyDictionary<string, NodeStatus> statuses, string unit) =>
        WireReceipts.NotTakenOn(nodes, wires, statuses, unit);

    /// <summary>
    /// The <c>back</c> wires to take now: each whose source holds the receipt of its <c>on</c> (its rejection, its spent failure, or its
    /// reach with that outcome), keyed as a trigger's firing and not taken on that receipt yet, whose target stands bound to an attempt. A target
    /// not bound (blocked, ready, judged again, passed over or stopped) has no attempt to take back, so the wire waits.
    /// </summary>
    private static List<WireFiring> Backs(List<NodeDeclaration> nodes, Dictionary<string, NodeStatus> statuses, IReadOnlyList<Wire> wires, WireRun run)
    {
        var taken = new List<WireFiring>();
        if (!wires.Any(wire => wire.Kind == WireKinds.Back)) return taken;
        var receipts = Receipts(nodes, statuses, run, []);
        foreach (var wire in wires.Where(wire => wire.Kind == WireKinds.Back && wire.From is { Unit: null } && wire.To.Unit is null).OrderBy(wire => wire.Id, StringComparer.Ordinal))
        {
            if (!statuses.TryGetValue(wire.To.Node, out var target) || !NodeStates.Bound.Contains(target.State)) continue;
            if (receipts.GetValueOrDefault(wire.From!.Node) is not { } receipt || !Takes(wire, receipt)) continue;
            var key = FiringKey(run, wire.To.Node, wire.Id, receipt.Id);
            if (!run.Fired.Contains(key)) taken.Add(new WireFiring(key, wire.Id, receipt.Id, wire.To.Node));
        }

        return taken;
    }

    /// <summary>Whether <paramref name="receipt"/> is what a <c>back</c> wire is taken on: a rejection, a spent failure, or a reach with that outcome.</summary>
    internal static bool Takes(Wire wire, WireReceipt receipt) => wire.On switch
    {
        WireKinds.Rejected => receipt.Kind == WireReceiptKinds.Rejected,
        WireKinds.Failed => receipt.Kind == WireReceiptKinds.Failed,
        { } outcome => receipt is { Kind: WireReceiptKinds.Reached, Outcome: { } reached } && string.Equals(reached, outcome, StringComparison.Ordinal),
        null => false,
    };

    /// <summary>
    /// A trigger's firing key: its target's place, else the graph, the run, the wire and the receipt, so an edit elsewhere fires
    /// nothing again.
    /// </summary>
    public static string FiringKey(WireRun run, string target, string wire, string receipt) => Digest("trg-", run.GraphOf(target), run.Run, wire, receipt);

    /// <summary>A firing's key by the whole graph's digest and the run's earlier name.</summary>
    internal static string LegacyKey(WireRun run, string wire, string receipt) => Digest("trg-", run.GraphDigest, run.Legacy ?? run.Run, wire, receipt);

    internal static string Digest(string prefix, params string?[] parts)
    {
        var text = new StringBuilder();
        foreach (var part in parts) text.Append(part?.Replace("\\", "\\\\").Replace("\n", "\\n") ?? "\\0").Append('\n');
        return prefix + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..32];
    }
}
