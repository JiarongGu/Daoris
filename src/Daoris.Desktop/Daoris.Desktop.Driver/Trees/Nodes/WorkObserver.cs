namespace Daoris.Driver;

/// <summary>
/// An observed plugin node reached by its believed event (FABRIC-PERFORM1i; the fabric events design §C4, §E2, §E10): a <c>work/step</c>
/// naming no invocation, bound to the last <c>work/watch</c> frame actually sent to the process it was read from and to one eligible observed
/// node of its unit's governing graph, believed by §C4's five rules with each tip read fresh once per judgement, and accepted through §E2's
/// one table on that node's binding, written before the evaluation that fires its wires.
/// </summary>
/// <remarks>
/// <para><b>Binding.</b> The frame's repository and branch are looked up in the frame the process was actually sent (<see cref="IWorkRuntimePort.SentTo"/>),
/// whose host-side half names the canonical unit and the repository's identity (<see cref="WatchedUnit.Unit"/>, <see cref="WatchedUnit.Identity"/>),
/// never in the set desired since nor by the repository's name alone. The step, normalised (<c>&lt;plugin&gt;/</c> taken off, and only its sender's),
/// names one observed node of the governing graph: a graph holding two of that kind names neither, never the first.</para>
/// <para><b>Identity and order.</b> An event is its plugin, its repository's identity, its unit, its step, its commit and its id; the process's generation
/// fences delivery and is no part of it. The record keeps the newest event taken for each step and commit (<see cref="NodeRecord.Observations"/>):
/// the same identity again is a replay and mints nothing; an id reused for another outcome or time, an older time, and an equal time under
/// another id are <c>late</c> (the first taken stands). Each commit is ordered apart, and each still passes the fourth rule. An
/// <c>unknown</c> establishes nothing and is not kept, so no later word is ordered behind it.</para>
/// <para><b>One outcome per binding.</b> Only an active node bound to the event's commit is moved. Its binding's first outcome stands: the same
/// outcome again is a replay, another is <c>late</c>; a node the person stopped or skipped is no longer awaited, and evidence never brings it back.</para>
/// </remarks>
public sealed class WorkObserver(NodeRecordFiles records, IWorkRuntimePort port, Func<DateTimeOffset> clock)
{
    /// <summary>What the history names a move an event made.</summary>
    public const string Why = "observed";

    private sealed record Bound(WorkStepHeard Heard, string Step, WatchedUnit Told, GovernedUnit Governed, NodeDeclaration Node);

    /// <summary>
    /// The events heard that name no invocation, judged and taken: each refusal counted by its code (<see cref="IWorkPerformPort.Refused"/>),
    /// each read of a checkout's refs made once for the whole judgement, whatever number of events and branches it serves.
    /// </summary>
    public async Task<IReadOnlyList<WorkPerformed>> HearAsync(
        IReadOnlyList<WorkStepHeard> heard, IReadOnlyDictionary<string, GovernedUnit> governed,
        Func<string, IReadOnlyCollection<string>, CancellationToken, Task<IReadOnlyDictionary<string, string>>> tips, CancellationToken ct)
    {
        var done = new List<WorkPerformed>();
        var bound = new List<Bound>();
        foreach (var each in heard.Where(each => each.Step.Invocation is null))
        {
            var (binding, unit, node, code) = Bind(each, governed);
            if (code is not null) done.Add(Counted(each.Plugin, new(unit, node, PerformedWhat.NotTaken, code)));
            else bound.Add(binding!);
        }

        if (bound.Count == 0) return done;

        // §C4's fourth rule: each tip read fresh, never a cached unit's revision, with one read of each checkout's refs for the judgement.
        var fresh = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var root in bound.Select(each => each.Told.Root).Distinct(StringComparer.Ordinal))
        {
            var branches = bound.Where(each => each.Told.Root == root).Select(each => each.Told.Branch).Distinct(StringComparer.Ordinal).ToList();
            fresh[root] = root.Length == 0 ? new Dictionary<string, string>() : await tips(root, branches, ct).ConfigureAwait(false);
        }

        var now = clock();
        foreach (var each in bound)
        {
            var frame = each.Heard.Step;
            var unit = each.Governed.Unit.Id;

            // An unread tip establishes nothing; a tip moved since the frame was told makes the event another commit's.
            var code = fresh[each.Told.Root].GetValueOrDefault(each.Told.Branch) is not { } tip || !string.Equals(tip, frame.Commit, StringComparison.OrdinalIgnoreCase)
                ? WorkRefusals.Stale
                : frame.At > now + WorkBounds.Minute ? WorkRefusals.Shape
                : null;
            done.Add(Counted(each.Heard.Plugin, code is not null ? new(unit, each.Node.Id, PerformedWhat.NotTaken, code) : Take(each, now)));
        }

        return done;
    }

    /// <summary>§C4's first three rules and the second's naming: the event's sender, its unit in the frame actually sent, and its one node.</summary>
    private (Bound? Bound, string Unit, string Node, string? Code) Bind(WorkStepHeard heard, IReadOnlyDictionary<string, GovernedUnit> governed)
    {
        var frame = heard.Step;
        if (port.GenerationOf(heard.Plugin) != heard.Generation) return (null, "", "", WorkRefusals.NotSender);

        var slash = frame.Step.LastIndexOf('/');
        if (slash >= 0 && !string.Equals(frame.Step[..slash], heard.Plugin, StringComparison.Ordinal)) return (null, "", "", WorkRefusals.NotNamed);
        var step = frame.Step[(slash + 1)..];

        // §C4's third rule: the unit this very process was told of, by the frame whose send completed, and none other.
        var told = port.SentTo(heard.Plugin, heard.Generation)?.Units
            .Where(unit => unit.Unit is not null && string.Equals(unit.Repository, frame.Repository, StringComparison.Ordinal)
                           && string.Equals(unit.Branch, frame.Branch, StringComparison.Ordinal) && unit.Awaiting.Contains(step, StringComparer.Ordinal))
            .ToList() ?? [];
        // The unit still governed, in the repository the process was told of: a checkout moved since names another repository's identity.
        if (told.Count != 1 || !governed.TryGetValue(told[0].Unit!, out var unit) || unit.Watched.Identity != told[0].Identity)
        {
            return (null, "", "", WorkRefusals.NotWatched);
        }

        var kind = heard.Plugin + "/" + step;
        var named = unit.Graph.Nodes.Where(node => node.Kind == kind && node.Reach == NodeReach.Observed).ToList();
        if (named.Count != 1) return (null, unit.Unit.Id, "", WorkRefusals.NotNamed);
        if (!unit.Observable.Contains(named[0].Id)) return (null, unit.Unit.Id, named[0].Id, WorkRefusals.NotDeclared);
        return (new Bound(heard, step, told[0], unit, unit.Declared[named[0].Id]), unit.Unit.Id, named[0].Id, null);
    }

    /// <summary>
    /// The fifth rule's order and §E2's acceptance, in one locked edit of the unit's record with the lease asked inside it: the node moved on its
    /// own binding and the event kept beside it, both written before the evaluation reads the record.
    /// </summary>
    private WorkPerformed Take(Bound bound, DateTimeOffset now)
    {
        var (unit, node, frame) = (bound.Governed.Unit.Id, bound.Node, bound.Heard.Step);
        var (plugin, identity, step, commit, state) = (bound.Heard.Plugin, bound.Told.Identity ?? "", bound.Step, frame.Commit!, frame.Outcome.State);
        var (refused, result) = records.Edit<WorkPerformed>(unit, record =>
        {
            bool Same(ObservedEvent kept) => kept.Plugin == plugin && kept.Repository == identity && kept.Step == step;
            var kept = record.Observations.FirstOrDefault(each => Same(each) && string.Equals(each.Commit, commit, StringComparison.OrdinalIgnoreCase));
            if (kept is not null)
            {
                // The same identity again is a replay; its id reused for another outcome or time is late, the first word under it standing.
                var replay = kept.Id == frame.Id && kept.State == state && kept.At == frame.At;
                if (replay) return (null, new(unit, node.Id, PerformedWhat.Replay));
                if (kept.Id == frame.Id || frame.At <= kept.At) return (null, new(unit, node.Id, PerformedWhat.NotTaken, WorkRefusals.Late));
            }

            if (state == WorkStates.Unknown) return (null, new(unit, node.Id, PerformedWhat.Noted));

            var status = WorkPerformer.StatusOf(record, node.Id);
            if (status is not { State: NodeStates.Active, Binding: { } binding } || !string.Equals(binding.SubjectRevision, commit, StringComparison.OrdinalIgnoreCase))
            {
                var on = string.Equals(status.Binding?.SubjectRevision, commit, StringComparison.OrdinalIgnoreCase);
                var code = kept is not null && kept.State == state ? null
                    : on && status.State is NodeStates.Reached or NodeStates.Failed ? WorkRefusals.Late
                    : status.State == NodeStates.Active ? WorkRefusals.Stale
                    : WorkRefusals.NotWatched;
                return (null, new(unit, node.Id, code is null ? PerformedWhat.Replay : PerformedWhat.NotTaken, code));
            }

            var move = state == WorkStates.Reached
                ? new NodeMove(NodeStates.Reached, NodeCause.Authority)
                {
                    Source = NodeSource.Plugin, Attempt = status.Attempt, Acceptance = WorkPerformer.Accepted(node, frame.Id, binding, frame.Outcome),
                }
                : new NodeMove(NodeStates.Failed, NodeCause.Authority) { Source = NodeSource.Plugin, Attempt = status.Attempt, Code = frame.Outcome.Code ?? WorkPerformer.NoCode };
            var moved = NodeLifecycle.Apply(node, status, move);
            if (!moved.Moved) return (null, new(unit, node.Id, PerformedWhat.Refused, moved.Refused));

            // The newest taken for this step: another commit's is dropped, as the fourth rule makes its events stale while the tip stands here.
            var observed = new ObservedEvent(plugin, identity, step, commit, frame.Id, state, frame.At, node.Id);
            return (WorkPerformer.Kept(record, moved, Why, now) with { Observations = [.. record.Observations.Where(each => !Same(each)), observed] },
                new(unit, node.Id, PerformedWhat.Applied, state));
        });
        return refused is null ? result! : new(unit, node.Id, PerformedWhat.Refused, refused);
    }

    /// <summary>A refusal of §C4's codes counted as a <c>plugin.step</c> line of its plugin and code, with no words.</summary>
    private WorkPerformed Counted(string plugin, WorkPerformed result)
    {
        if (result is { What: PerformedWhat.NotTaken, Code: { } code }) port.Refused(plugin, code);
        return result;
    }
}

/// <summary>
/// Each named branch's tip in one checkout, read fresh (FABRIC-PERFORM1i, §C4's fourth rule; D88's PROC1d-ii): one <c>for-each-ref</c> of its
/// refs for the whole judgement, a snapshot of its own, never one held across judgements, so a ref moved outside Daoris is read at the next.
/// A pass asks it once a checkout, for its events and its observed nodes' bindings alike (<see cref="PassTips"/>, FABRIC-PERFORM1m).
/// </summary>
public static class NodeTips
{
    /// <summary>The tip of each of <paramref name="branches"/> in <paramref name="root"/>; a branch not read (no such ref, a read that failed) is absent.</summary>
    public static async Task<IReadOnlyDictionary<string, string>> ReadAsync(string root, IReadOnlyCollection<string> branches, CancellationToken ct)
    {
        var tips = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root.Length == 0 || !Directory.Exists(root)) return tips;
        var refs = RefSnapshot.Take(root);
        foreach (var branch in branches.Distinct(StringComparer.Ordinal))
        {
            if (await refs.CommitAsync("refs/heads/" + branch, ct).ConfigureAwait(false) is (true, { } commit)) tips[branch] = commit;
        }

        return tips;
    }
}

/// <summary>
/// One pass's tips (FABRIC-PERFORM1m; §C4's fourth rule, D88's PROC1d-ii): each checkout read once, by whatever asks of it first (an event's
/// belief, a rebinding's check, an observed node's binding), for every branch the pass governs there as well as those asked, so they share
/// the one read. Made for one pass and dropped with it, so no tip is carried into the next (LANDGIT1).
/// </summary>
/// <param name="read">The look's read of one checkout (<see cref="NodeRuntimeLook.Tips"/>).</param>
/// <param name="governed">Each checkout's branches the pass may bind an observed node on.</param>
public sealed class PassTips(
    Func<string, IReadOnlyCollection<string>, CancellationToken, Task<IReadOnlyDictionary<string, string>>> read,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> governed)
{
    private static readonly IReadOnlyDictionary<string, string> None = new Dictionary<string, string>();

    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _read = new(StringComparer.Ordinal);

    /// <summary>The tips of <paramref name="root"/>'s branches, read at the first ask; a branch not read is absent, and a later ask reads nothing again.</summary>
    public async Task<IReadOnlyDictionary<string, string>> ReadAsync(string root, IReadOnlyCollection<string> branches, CancellationToken ct)
    {
        if (root.Length == 0) return None;
        if (_read.TryGetValue(root, out var tips)) return tips;
        var all = (governed.GetValueOrDefault(root) ?? []).Concat(branches).Where(branch => branch.Length > 0)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        return _read[root] = all.Count == 0 ? None : await read(root, all, ct).ConfigureAwait(false);
    }

    /// <summary>One branch's tip read this pass, or null where it was not read.</summary>
    public async Task<string?> TipAsync(string root, string branch, CancellationToken ct) =>
        branch.Length == 0 ? null : (await ReadAsync(root, [branch], ct).ConfigureAwait(false)).GetValueOrDefault(branch);
}

/// <summary>
/// A repository's identity (§B5; FABRIC-PERFORM1i): its common git folder's file identity, read from git's own files with no process, else that
/// folder's spelling, so the main checkout and any linked worktree of it name one repository, as LANDRACE1's branch hold does.
/// </summary>
public static class RepositoryIdentity
{
    public static string Of(string root)
    {
        if (root.Length == 0) return "";
        var common = TreeTips.CommonDirectory(root) ?? root;
        try
        {
            return (OperatingSystem.IsWindows() ? FolderIdentity.Of(common) : null) ?? Path.TrimEndingDirectorySeparator(Path.GetFullPath(common)).ToLowerInvariant();
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException)
        {
            return common;
        }
    }
}
