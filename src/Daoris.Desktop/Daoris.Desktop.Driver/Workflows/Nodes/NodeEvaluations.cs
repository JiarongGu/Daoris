namespace Daoris.Driver;

/// <summary>
/// A governed unit's graph judged on its record as it stands, and the pass's reads that judgement waits on: the gates, the line's content,
/// git's answers, and the subjects a run's nodes observe. Pure: what an evaluation moves is the caller's to keep, and none of it starts git
/// beyond the reads it is handed.
/// </summary>
internal static class NodeEvaluations
{
    /// <summary>
    /// A unit's graph evaluated on its record as it stands: each node's status before (the record's, else a fresh one), and the evaluation on
    /// the unit's revision and the triggers its record has fired; a branch unit's plugin nodes on the tip its pass read, and what git said for
    /// its built-ins (<paramref name="git"/>), handed in.
    /// </summary>
    public static (Dictionary<string, NodeStatus> Before, WireEvaluated Evaluated) Evaluate(
        GovernedUnit governed, NodeRecord record, bool tipped = false, string? tip = null, NodeGitReads? git = null,
        (OpinionGateState? Opinion, ReviewGateState? Review) gates = default)
    {
        var unit = governed.Unit.Id;
        var nodes = governed.Graph.Nodes.Select(node => governed.Declared[node.Id]).ToList();
        var before = nodes.ToDictionary(node => node.Id, node => record.Statuses.GetValueOrDefault(node.Id) ?? NodeLifecycle.Start(unit, node.Id), StringComparer.Ordinal);
        var run = new WireRun(governed.GraphDigest, governed.Run)
        {
            Places = governed.Places,
            Legacy = NodeRuns.Of(unit, governed.GraphDigest),
            Revisions = new Dictionary<string, string?>(StringComparer.Ordinal) { [unit] = governed.Unit.Subject.Revision },
            NodeRevisions = tipped ? TipRevisions(governed, tip, before) : new Dictionary<string, string?>(StringComparer.Ordinal),
            Fired = record.Fired.ToHashSet(StringComparer.Ordinal),
            Units = new Dictionary<string, WorkUnit>(StringComparer.Ordinal) { [unit] = governed.Unit },
            Git = git,
            Opinions = gates.Opinion is { } opinion
                ? new Dictionary<string, OpinionGateState>(StringComparer.Ordinal) { [unit] = opinion }
                : new Dictionary<string, OpinionGateState>(StringComparer.Ordinal),
            Reviews = gates.Review is { } review
                ? new Dictionary<string, ReviewGateState>(StringComparer.Ordinal) { [unit] = review }
                : new Dictionary<string, ReviewGateState>(StringComparer.Ordinal),
        };
        return (before, WireEvaluation.Evaluate(nodes, governed.Graph.Wires, before, run));
    }

    /// <summary>
    /// A run's graph evaluated on its own record over its subjects: <c>claimed</c> on its newest work quest here; its gates, <c>handed</c> and
    /// a plugin's node before it on its branch at the tip read in the pass; what follows <c>handed</c> on the branch it reached on, at that
    /// landing's tip as its record keeps it; and its own nodes on the run itself, its go-ahead answered by the go-aheads its own sessions and
    /// quests asked. A subject not found this pass withholds its nodes alone. Its opinion and look observe its branch's gates as read at that
    /// tip (<paramref name="gates"/>).
    /// </summary>
    public static (Dictionary<string, NodeStatus> Before, WireEvaluated Evaluated) EvaluateRun(
        GovernedUnit governed, NodeRecord record, string? tip, NodeGitReads? git, (OpinionGateState? Opinion, ReviewGateState? Review) gates)
    {
        var scope = governed.Scope!;
        var self = governed.Unit;
        var nodes = governed.Graph.Nodes.Select(node => governed.Declared[node.Id]).ToList();
        var before = nodes.ToDictionary(node => node.Id, node => record.Statuses.GetValueOrDefault(node.Id) ?? NodeLifecycle.Start(self.Id, node.Id), StringComparer.Ordinal);
        var units = new Dictionary<string, WorkUnit>(StringComparer.Ordinal) { [self.Id] = self };
        // A verification run's nodes observe the run itself at its commit.
        var revisions = new Dictionary<string, string?>(StringComparer.Ordinal) { [self.Id] = scope.Tip };
        if (scope.Quest is { } quest) (units[quest.Id], revisions[quest.Id]) = (quest, quest.Subject.Revision);
        if (scope.Branch is { } branch)
        {
            units[branch.Id] = branch;
            if (tip is not null) revisions[branch.Id] = tip;
        }

        var landed = Landed(governed, before);
        if (landed is { } on) units.TryAdd(on.Unit.Id, on.Unit);
        var subjects = new Dictionary<string, string?>(StringComparer.Ordinal);
        var own = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var node in scope.Tip is null ? nodes : [])
        {
            switch (RunSubjects.Of(node, scope.AfterHanded))
            {
                case RunSubject.Quest:
                    subjects[node.Id] = scope.Quest?.Id;
                    break;
                case RunSubject.Landing:
                    subjects[node.Id] = landed?.Unit.Id;
                    if (landed is { } at) own[node.Id] = at.Tip;
                    break;
                case RunSubject.Branch:
                    subjects[node.Id] = scope.Branch?.Id;
                    // A branch whose tip was not read (its tree tidied after its landing): a node bound there keeps its binding's own revision, so
                    // only a moved subject lapses it.
                    if (tip is null && scope.Branch is { } held && before[node.Id].Binding is { } bound && bound.Observes == held.Id) own[node.Id] = bound.SubjectRevision;
                    break;
                default:
                    // The run itself: left out, so it observes its record's unit, with no revision and no subject named on its binding.
                    break;
            }
        }

        var run = new WireRun(governed.GraphDigest, governed.Run)
        {
            Revisions = revisions,
            Subjects = subjects,
            NodeRevisions = own,
            Units = units,
            Fired = record.Fired.ToHashSet(StringComparer.Ordinal),
            Git = git,
            GoAheads = scope.GoAheads is { } asked
                ? new Dictionary<string, IReadOnlyList<GoAheadView>>(StringComparer.Ordinal) { [self.Id] = asked }
                : new Dictionary<string, IReadOnlyList<GoAheadView>>(StringComparer.Ordinal),
            Requests = new Dictionary<string, IReadOnlyList<QuestView>>(StringComparer.Ordinal) { [self.Id] = scope.Requests },
            Opinions = gates.Opinion is { } opinion && scope.Branch is { } judged
                ? new Dictionary<string, OpinionGateState>(StringComparer.Ordinal) { [judged.Id] = opinion }
                : new Dictionary<string, OpinionGateState>(StringComparer.Ordinal),
            Reviews = gates.Review is { } review && scope.Branch is { } looked
                ? new Dictionary<string, ReviewGateState>(StringComparer.Ordinal) { [looked.Id] = review }
                : new Dictionary<string, ReviewGateState>(StringComparer.Ordinal),
        };
        return (before, WireEvaluation.Evaluate(nodes, governed.Graph.Wires, before, run));
    }

    /// <summary>
    /// The landing a run's <c>handed</c> reached on: the branch unit its reach names and the tip its evidence names, the landing's own, which
    /// what follows binds; null while it is not reached, or its branch was not read.
    /// </summary>
    public static (WorkUnit Unit, string Tip)? Landed(GovernedUnit governed, IReadOnlyDictionary<string, NodeStatus> statuses)
    {
        if (governed.Scope is not { } scope || governed.Graph.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Land) is not { } handed) return null;
        if (statuses.GetValueOrDefault(handed.Id) is not { State: NodeStates.Reached, Acceptance: { Subject: { } subject, Evidence.Revision: { } tip } }) return null;
        var unit = scope.Branch?.Id == subject ? scope.Branch : scope.Branches.FirstOrDefault(each => each.Id == subject);
        return unit is null ? null : (unit, tip);
    }

    /// <summary>
    /// The landing an <c>integrate</c> asks for: the landed branch at the tip <c>handed</c> recorded, with its landing's record. A unit's own
    /// <c>handed</c> reached on a landing's tip, or a run's (<see cref="Landed"/>); null while <c>handed</c> is not reached or no entry of the
    /// unit's landings holds that tip.
    /// </summary>
    public static (LandedBranch Entry, string Tip, string Root)? LandingOf(GovernedUnit governed, IReadOnlyDictionary<string, NodeStatus> statuses)
    {
        if (governed.Scope is { } scope)
        {
            if (Landed(governed, statuses) is not { } on) return null;
            return on.Unit.Records.Landings.LastOrDefault(each => each.GoneAt is null && string.Equals(each.Tip, on.Tip, StringComparison.OrdinalIgnoreCase)) is { } held
                ? (held, on.Tip, scope.Root)
                : null;
        }

        if (governed.Graph.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Land) is not { } handed
            || statuses.GetValueOrDefault(handed.Id) is not { State: NodeStates.Reached, Acceptance.Evidence.Revision: { } tip })
        {
            return null;
        }

        return governed.Unit.Records.Landings.LastOrDefault(each => each.GoneAt is null && string.Equals(each.Tip, tip, StringComparison.OrdinalIgnoreCase)) is { } entry
            ? (entry, tip, governed.Watched.Root)
            : null;
    }

    /// <summary>
    /// Whether a unit's plugin nodes bind to its branch's tip read in the pass, its plugins are told that tip and an answer lapses on it: a
    /// branch unit's with an observed node. A quest's revision is null by design; a unit observing nothing reads no git, and every node of it
    /// is the unit's revision, as told.
    /// </summary>
    public static bool Tipped(GovernedUnit governed) => governed.Unit.Kind == WorkUnitKind.Branch && governed.Observable.Count > 0;

    /// <summary>
    /// The revision each of a branch unit's plugin nodes, observed and performed, is judged on: its branch's tip read this pass, so a moved
    /// branch lapses the old binding and the node is begun again on the new tip. Where the tip was not read, a bound node keeps its binding's
    /// revision, so nothing lapses on a read that failed; a node not bound is not begun or sent.
    /// </summary>
    private static Dictionary<string, string?> TipRevisions(GovernedUnit governed, string? tip, IReadOnlyDictionary<string, NodeStatus> statuses)
    {
        var revisions = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var node in governed.Observable.Concat(governed.Dispatchable))
        {
            if (tip is not null) revisions[node] = tip;
            else if (statuses.GetValueOrDefault(node)?.Binding is { } binding) revisions[node] = binding.SubjectRevision;
        }

        return revisions;
    }

    /// <summary>
    /// The governed units by each unit their plugins are told under: a unit's own, and each branch a run's quests produced, its former subjects
    /// included, mapped back to the run, as the read that keeps Current off them maps it.
    /// </summary>
    public static Dictionary<string, GovernedUnit> ByTold(IEnumerable<GovernedUnit> governed)
    {
        var told = new Dictionary<string, GovernedUnit>(StringComparer.Ordinal);
        foreach (var each in governed)
        {
            told.TryAdd(each.Unit.Id, each);
            foreach (var unit in each.Scope?.Told ?? []) told.TryAdd(unit, each);
        }

        return told;
    }

    /// <summary>
    /// The gates a unit's <c>opinion</c> and <c>look</c> observe: read through the pass's one read of the unit, and only where one of them is
    /// active or unknown on its record, so a unit whose graph holds neither, or whose gates are settled, asks nothing. Nothing read leaves
    /// both null, and the nodes wait.
    /// </summary>
    /// <remarks>
    /// A run's gates are its branch's, read at the tip this pass read (<paramref name="tip"/>), which its gate nodes bind, so the opinion and
    /// the look are judged at their bound commit by construction, never at a tree's <c>HEAD</c> a commit since has moved. A run with no branch,
    /// or whose branch's tip was not read (its tree tidied after its landing), reads none, and its gate nodes keep their bindings, a landing
    /// at the bound commit answering them from its record.
    /// </remarks>
    public static async Task<(OpinionGateState? Opinion, ReviewGateState? Review)> GatesAsync(
        GovernedUnit governed, NodeRecord record, string? tip, PassGates gates, CancellationToken ct)
    {
        if (!governed.Graph.Nodes.Any(node => node.Kind is NodeKinds.Opinion or NodeKinds.Look
                && record.Statuses.GetValueOrDefault(node.Id)?.State is NodeStates.Active or NodeStates.Unknown))
        {
            return default;
        }

        if (governed.Scope is null) return await gates.OfAsync(governed.Unit, null, ct).ConfigureAwait(false);
        return governed.Scope.Branch is { } branch && tip is not null ? await gates.OfAsync(branch, tip, ct).ConfigureAwait(false) : default;
    }

    /// <summary>
    /// <paramref name="reads"/> with what the line holds of a run's branch at the tip this pass read, where its <c>content.holds</c> waits
    /// active or unknown; as they were for every other unit, so a run whose hold is settled, or holds none, asks nothing.
    /// </summary>
    public static async Task<NodeGitReads?> ContentAsync(
        GovernedUnit governed, NodeRecord record, string? tip, Func<WorkUnit, string, CancellationToken, Task<HoldContent?>> content, NodeGitReads? reads,
        CancellationToken ct)
    {
        if (governed.Scope?.Branch is not { } branch || tip is null
            || !governed.Graph.Nodes.Any(node => node.Kind == NodeKinds.ContentHolds && record.Statuses.GetValueOrDefault(node.Id)?.State is NodeStates.Active or NodeStates.Unknown))
        {
            return reads;
        }

        var read = await content(branch, tip, ct).ConfigureAwait(false);
        return (reads ?? new NodeGitReads()) with { Content = (unit, at) => unit == branch.Id && at == tip ? read : null };
    }

    /// <summary>The look's content read, asked once a pass for a branch and a tip however many rounds ask it.</summary>
    public static Func<WorkUnit, string, CancellationToken, Task<HoldContent?>> Once(Func<WorkUnit, string, CancellationToken, Task<HoldContent?>> content)
    {
        var known = new Dictionary<(string Unit, string Tip), Task<HoldContent?>>();
        return (unit, tip, ct) => known.TryGetValue((unit.Id, tip), out var read) ? read : known[(unit.Id, tip)] = content(unit, tip, ct);
    }

    /// <summary>
    /// What git says for a branch unit's observed built-ins that wait on it: a <c>push</c> active or unknown on a landing Daoris pushed reads the
    /// landed branch's remote-tracking ref, through the pass's one read (<paramref name="git"/>). Null where nothing waits on git, so a unit with
    /// no such node asks it nothing; what was not read stays absent, and its node unknown.
    /// </summary>
    public static async Task<NodeGitReads?> GitReadsAsync(GovernedUnit governed, NodeRecord record, PassGit git, CancellationToken ct)
    {
        // A run's `push` observes its branch at the landing `land` reached on.
        var unit = governed.Scope is null ? governed.Unit : Landed(governed, record.Statuses)?.Unit;
        if (unit?.Kind != WorkUnitKind.Branch || BuiltInNodes.LandingOf(unit) is not { } landing) return null;
        var waits = governed.Graph.Nodes.Any(node => node.Kind == NodeKinds.Push
            && record.Statuses.GetValueOrDefault(node.Id)?.State is NodeStates.Active or NodeStates.Unknown);
        if (!waits || !BuiltInNodes.AsksRemote(landing)) return null;

        var tip = await git.RemoteAsync(governed.Watched.Root, landing.Branch, ct).ConfigureAwait(false);
        return new NodeGitReads(each => string.Equals(each.Branch, landing.Branch, StringComparison.Ordinal) ? tip : null);
    }

    /// <summary>Each checkout's landed branches a pass may judge <c>pushed</c> on: what the pass's one read of its remote refs serves.</summary>
    public static Dictionary<string, IReadOnlyCollection<string>> Pushing(IEnumerable<GovernedUnit> governed)
    {
        var branches = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
        var pushing = governed.Where(each => each.Unit.Kind is WorkUnitKind.Branch or WorkUnitKind.Run && each.Watched.Root.Length > 0
                && each.Graph.Nodes.Any(node => node.Kind == NodeKinds.Push))
            // A run's landed branches are its branches' landings, each read once a checkout with the rest.
            .SelectMany(each => (each.Scope?.Branches ?? [each.Unit]).Select(unit => (each.Watched.Root, Landing: BuiltInNodes.LandingOf(unit))))
            .Where(each => each.Landing is { } landing && BuiltInNodes.AsksRemote(landing));
        foreach (var same in pushing.GroupBy(each => each.Root, StringComparer.Ordinal))
        {
            branches[same.Key] = same.Select(each => each.Landing!.Branch).Distinct(StringComparer.Ordinal).ToList();
        }

        return branches;
    }

    /// <summary>Each checkout's branches whose unit binds an observed node to its tip: what the pass's one read of it serves.</summary>
    public static Dictionary<string, IReadOnlyCollection<string>> Observing(IEnumerable<GovernedUnit> governed)
    {
        var branches = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
        // A run's branch too, whose tip its branch-side nodes bind, read with the rest of its checkout.
        foreach (var same in governed.Where(each => Tipped(each) || each.Scope is { Branch: not null }).Where(each => each.Watched.Root.Length > 0)
                     .GroupBy(each => each.Watched.Root, StringComparer.Ordinal))
        {
            branches[same.Key] = same.Select(each => each.Watched.Branch).Where(branch => branch.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        }

        return branches;
    }
}
