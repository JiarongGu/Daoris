namespace Daoris.Driver;

/// <summary>Who asks a tree's session to commit what its tree holds: one door, counted by that door's bound.</summary>
public static class CommitAskers
{
    /// <summary>The <c>resolve</c> of an open run whose <c>rebase</c> will read the tree's work: its tries per stop bound it.</summary>
    public const string Resolve = "resolve";

    /// <summary>Nobody: a hand-off reserves the tree, and its resolver is the one asked anything there.</summary>
    public const string Nobody = "nobody";

    /// <summary>The turn's end (<see cref="TurnCarries"/>), for work no <c>rebase</c> reads: <see cref="TurnCarry.Bound"/> bounds it.</summary>
    public const string TurnsEnd = "turn";

    /// <summary>
    /// Who asks the session in <paramref name="tree"/> to commit: nobody while a hand-off reserves it; the <c>resolve</c> of an open run whose
    /// <c>rebase</c> is fed that tree's work and has neither reached nor been skipped, since that rebase finds the paths and its <c>resolve</c>
    /// asks; else the turn's end.
    /// </summary>
    public static string Of(string home, string tree)
    {
        if (new TreeHandOffs(home).Of(tree) is not null) return Nobody;
        if (UnitOf(home, tree) is not { } unit) return TurnsEnd;
        var (records, _) = new NodeRecordFiles(home).All();
        var store = new StampedWorkflows(new WorkflowFiles(home), home);
        var runs = new WorkflowRunFiles(home);
        foreach (var record in records.Where(each => each.Ended is null && RunUnits.Is(each.Unit) && Observes(each.Statuses, unit)))
        {
            if (WorkflowRunNodes.VersionOf(store, runs, record.Unit[RunUnits.Prefix.Length..])?.Graph is { } graph && Rebases(graph.Nodes, record.Statuses)) return Resolve;
        }

        return TurnsEnd;
    }

    /// <summary>Whether a run's graph holds a <c>rebase</c> its record shows neither reached nor skipped.</summary>
    public static bool Rebases(IReadOnlyList<NodeDeclaration> nodes, IReadOnlyDictionary<string, NodeStatus> statuses) =>
        nodes.Any(node => node.Kind == NodeKinds.Rebase && statuses.GetValueOrDefault(node.Id) is not { State: NodeStates.Reached or NodeStates.Skipped });

    private static bool Observes(IReadOnlyDictionary<string, NodeStatus> statuses, string unit) =>
        statuses.Values.Any(status => status.Current?.Observes == unit || status.Binding?.Observes == unit);

    /// <summary>The branch unit whose session tree <paramref name="tree"/> is; null where no branch record names it.</summary>
    private static string? UnitOf(string home, string tree)
    {
        var key = TreeHandOffs.KeyOf(tree);
        return new SessionBranches(home).All().FirstOrDefault(grown => grown.Unit is not null
            && DaorisHome.Places(home, HomeEntry.Trees, grown.Workspace, grown.Repository, Path.GetFileName(grown.Branch)).Any(place => TreeHandOffs.KeyOf(place) == key))?.Unit;
    }
}
