namespace Daoris.Driver;

/// <summary>
/// The awaited set the plugins are told (<c>work/watch</c>): each governed unit's frame as its plugins are told it, the tips last told, the
/// text of the set last told and the next timer the records persist.
/// </summary>
internal sealed class NodeTelling(NodeRecordFiles records, IWorkRuntimePort port, Func<bool> holdsLease, ResourceCleaner cleaner, Func<string, bool?> ended)
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>> NoneAwaited = new Dictionary<string, IReadOnlyList<WatchedUnit>>();

    /// <summary>Each tipped unit's tip last told, by unit: what a pass that read none tells again.</summary>
    private Dictionary<string, string> _tipsTold = new(StringComparer.Ordinal);

    /// <summary>The awaited set last told, as text: what <see cref="NodeRuntimePass.AwaitedChanged"/> compares.</summary>
    private string _toldText = "";

    /// <summary>Whether the plugins were last told a set naming anything: a disarmed pass empties it once.</summary>
    private volatile bool _told;

    /// <summary>When the next timer the records persist comes due (a retry's delay, a running answer's wait, a deadline), or null.</summary>
    public DateTimeOffset? NextDue { get; private set; }

    /// <summary>Whether anything told is held that a disarm withdraws.</summary>
    public bool Holding => _told || NextDue is not null || _toldText.Length > 0;

    /// <summary>
    /// Disarmed: the plugins are told nothing is awaited where something is, so their processes stop, and what is told is forgotten, a
    /// pending retry's timer included, so nothing wakes the loop. A cleanup still due is asked once the runtime is armed again.
    /// </summary>
    public void Withdraw()
    {
        if (_told) port.Watch(NoneAwaited);
        port.Owe(new HashSet<string>(StringComparer.Ordinal));
        (_told, _toldText, NextDue) = (false, "", null);
        _tipsTold = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// The unit as its plugins are told it now (<c>work/watch</c>, what <see cref="WorkBelief.Lapsed"/> judges an answer on): a tipped unit's
    /// tip the one this pass read, else the one told before, so nothing lapses on a read that failed or a pass that read nothing; else, and
    /// for any other unit, its revision.
    /// </summary>
    public WatchedUnit Told(GovernedUnit governed, PassTips tips) => TipOf(governed, tips) is { } tip ? governed.Watched with { Tip = tip } : governed.Watched;

    private string? TipOf(GovernedUnit governed, PassTips tips) =>
        NodeEvaluations.Tipped(governed) ? tips.Known(governed.Watched.Root, governed.Watched.Branch) ?? _tipsTold.GetValueOrDefault(governed.Unit.Id) : null;

    /// <summary>
    /// The frame a run's plugin is told an attempt under: a node after <c>handed</c>'s, the landed branch at the landing's tip; any other, the
    /// branch its call is sent for, at the tip this pass read, a branch the run has since left included, so its answer still reaches the
    /// run's record. Null where that branch is not the run's.
    /// </summary>
    public WatchedUnit? RunTold(GovernedUnit governed, NodeAttempt attempt, PassTips tips)
    {
        var scope = governed.Scope!;
        if (scope.AfterHanded.Contains(attempt.Node))
        {
            return records.Read(governed.Unit.Id) is { } record ? LandedFrame(governed, record) ?? LandingSentFor.Frame(governed, attempt) : null;
        }

        var branch = scope.Branch?.Id == attempt.Call.Unit ? scope.Branch : scope.Branches.FirstOrDefault(each => each.Id == attempt.Call.Unit);
        return branch is null ? null : BranchFrame(governed, branch, tips);
    }

    /// <summary>A run's branch as its plugins are told it: under its branch unit, at the tip this pass read, else the one told before.</summary>
    private WatchedUnit BranchFrame(GovernedUnit governed, WorkUnit branch, PassTips tips)
    {
        var scope = governed.Scope!;
        var name = RunBranches.Standing(branch);
        var tip = tips.Known(scope.Root, name) ?? _tipsTold.GetValueOrDefault(branch.Id) ?? branch.Subject.Revision ?? "";
        return new WatchedUnit(governed.Watched.Repository, governed.Watched.Workspace, scope.Root, name, tip, null, null, [])
        {
            Unit = branch.Id, Identity = scope.Identity,
        };
    }

    /// <summary>
    /// The landed branch a run's node after <c>handed</c> is told: the branch its landing made, at the landing's tip, under the run's branch
    /// unit, marked so an event is judged on that tip; null while <c>handed</c> is not reached.
    /// </summary>
    private static WatchedUnit? LandedFrame(GovernedUnit governed, NodeRecord record)
    {
        if (NodeEvaluations.Landed(governed, record.Statuses) is not { } landed) return null;
        var (unit, tip) = landed;
        var landing = unit.Records.Landings.LastOrDefault(each => each.Tip == tip);
        if (landing is null) return null;
        var scope = governed.Scope!;
        return new WatchedUnit(governed.Watched.Repository, governed.Watched.Workspace, scope.Root, landing.Branch, tip, landing.Line, landing.PullRequest, [])
        {
            Unit = unit.Id, Identity = scope.Identity, Landing = true,
        };
    }

    /// <summary>
    /// A run's plugin node's frame: after <c>handed</c>, the landing's, else its waiting call's (<see cref="LandingSentFor"/>); a waiting attempt,
    /// its call's branch; any other, its binding's branch, else the run's. Null where none is found this pass, and it is told nothing.
    /// </summary>
    private WatchedUnit? RunFrame(GovernedUnit governed, NodeRecord record, string node, PassTips tips)
    {
        var scope = governed.Scope!;
        var waiting = record.Attempts.LastOrDefault(attempt => attempt.Node == node && attempt.State is AttemptStates.Recorded or AttemptStates.Sent or AttemptStates.Unknown);
        if (scope.AfterHanded.Contains(node)) return LandedFrame(governed, record) ?? (waiting is null ? null : LandingSentFor.Frame(governed, waiting));
        if (waiting is not null) return RunTold(governed, waiting, tips);

        var observes = record.Statuses.GetValueOrDefault(node)?.Binding?.Observes;
        var branch = observes is null || scope.Branch?.Id == observes ? scope.Branch : scope.Branches.FirstOrDefault(each => each.Id == observes);
        return branch is null ? null : BranchFrame(governed, branch, tips);
    }

    /// <summary>
    /// The awaited set told (<c>work/watch</c>): each governed unit with a dispatchable node ready, active or unknown, or an attempt still
    /// waiting for its answer, under that node's plugin and step; and the next timer the records persist. Returns whether the set differs from
    /// the one told before. A tipped unit is told the tip its pass read (<see cref="Told"/>).
    /// </summary>
    /// <remarks>
    /// A run's plugin nodes are told under their subject's unit, never <c>run:</c>: before <c>handed</c> its branch at the tip read, after it
    /// the landed branch at the landing's tip; a performed one as a unit's is. One landed branch told at one tip for two runs is told for
    /// neither, and <paramref name="said"/> says so.
    /// </remarks>
    public bool Tell(IEnumerable<GovernedUnit> governed, PassTips tips, List<string> said)
    {
        var awaited = new SortedDictionary<string, List<WatchedUnit>>(StringComparer.Ordinal);
        var tipsTold = new Dictionary<string, string>(StringComparer.Ordinal);
        DateTimeOffset? due = null;
        void Due(DateTimeOffset? at)
        {
            if (at is { } moment && (due is null || moment < due)) due = moment;
        }

        var entries = new List<(string Plugin, string Step, WatchedUnit Frame, string Owner)>();
        foreach (var each in governed)
        {
            if (TipOf(each, tips) is { } tip) tipsTold[each.Unit.Id] = tip;
            if (each.Scope is { Branch: { } branch } scope && (tips.Known(scope.Root, RunBranches.Standing(branch)) ?? _tipsTold.GetValueOrDefault(branch.Id)) is { } read) tipsTold[branch.Id] = read;
            if (records.Read(each.Unit.Id) is not { } record) continue;
            foreach (var id in each.Dispatchable.Concat(each.Observable))
            {
                // An observed node is published once begun, never while ready, so an event never precedes its start.
                var waiting = record.Attempts.Any(attempt => attempt.Node == id && attempt.State is AttemptStates.Recorded or AttemptStates.Sent or AttemptStates.Unknown);
                var state = record.Statuses.GetValueOrDefault(id)?.State;
                if (each.Observable.Contains(id) ? state != NodeStates.Active
                    : !waiting && state is not (NodeStates.Ready or NodeStates.Active or NodeStates.Unknown))
                {
                    continue;
                }

                // A check is no plugin's, and no plugin is told of it.
                if (!NodeKit.IsPlugin(each.Declared[id].Kind)) continue;
                if ((each.Scope is null ? Told(each, tips) : RunFrame(each, record, id, tips)) is not { } frame) continue;
                var kind = each.Declared[id].Kind;
                var plugin = NodeGoverning.PluginOf(kind);
                entries.Add((plugin, kind[(plugin.Length + 1)..], frame, each.Unit.Id));
            }

            foreach (var attempt in record.Attempts)
            {
                if (record.RetryPending(attempt)) Due(attempt.RetryAt);
                if (attempt.State is AttemptStates.Recorded or AttemptStates.Unknown or AttemptStates.Sent) Due(attempt.AskAt);
                if (attempt.State == AttemptStates.Sent && attempt.AskAt is null) Due(attempt.Call.DeadlineAt);
            }
        }

        // A landing carrying two runs' work told at one tip for both is told for neither, said; the person's skip passes what waits on it,
        // and the observer's `not-watched` stays the backstop.
        var shared = entries.Where(entry => entry.Frame.Landing)
            .GroupBy(entry => (entry.Frame.Identity, entry.Frame.Branch, entry.Frame.Tip))
            .Where(same => same.Select(entry => entry.Owner).Distinct(StringComparer.Ordinal).Count() > 1).ToList();
        foreach (var same in shared)
        {
            var line = $"work  {string.Join(", ", same.Select(entry => entry.Owner).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))}: "
                       + $"`{same.Key.Branch}` was landed at {TextBounds.ShortCommit(same.Key.Tip, 8)} carrying each one's work, so neither is told it, and "
                       + "what follows that landing in each waits for your skip.";
            if (!said.Contains(line, StringComparer.Ordinal)) said.Add(line);
        }

        var dropped = shared.SelectMany(same => same).ToHashSet();
        foreach (var plugin in entries.Where(entry => !dropped.Contains(entry)).GroupBy(entry => entry.Plugin, StringComparer.Ordinal))
        {
            var units = awaited[plugin.Key] = [];
            foreach (var frame in plugin.GroupBy(entry => $"{entry.Frame.Unit}\n{entry.Frame.Branch}\n{entry.Frame.Tip}\n{entry.Frame.Landing}", StringComparer.Ordinal))
            {
                units.Add(frame.First().Frame with { Awaiting = [.. frame.Select(entry => entry.Step).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)] });
            }
        }

        // Published only while this loop still holds the lease, asked after the pass's awaits; a loop that lost it meanwhile tells nothing
        // and keeps no timer, and the lease's next holder tells its own set.
        if (!holdsLease())
        {
            NextDue = null;
            return false;
        }

        _tipsTold = tipsTold;
        // A plugin owed a cleanup keeps its process though nothing awaits its steps, and a cleanup's back-off is a timer.
        var owed = cleaner.Owed(ended);
        Due(cleaner.NextDue());
        port.Owe(owed);
        port.Watch(awaited.ToDictionary(each => each.Key, each => (IReadOnlyList<WatchedUnit>)each.Value, StringComparer.Ordinal));
        _told = awaited.Count > 0;
        NextDue = due;
        var text = string.Join("\n", awaited.SelectMany(each => each.Value.Select(unit =>
            $"{each.Key}\t{unit.Repository}\t{unit.Workspace}\t{unit.Branch}\t{unit.Tip}\t{string.Join(",", unit.Awaiting)}"))
            .Concat(owed.Order(StringComparer.Ordinal).Select(plugin => $"{plugin}\towed")));
        var changed = !string.Equals(text, _toldText, StringComparison.Ordinal);
        _toldText = text;
        return changed;
    }
}
