namespace Daoris.Driver;

/// <summary>
/// A run whose quest another closed Done, whoever wrote the Done. A quest bound to its run is Done by the run's own
/// end only once that end is kept, and a closure graph's close is <see cref="NodeClosing"/>'s, so a Done on a quest bound to this run whose
/// record holds no end is another's. It ends the run as a stop does: every step still standing is stopped by Daoris before the pass's
/// rounds, so no step starts after it, and the attempt in flight on each stands down, a check's too, its executor told to stop
/// (<see cref="WorkPerformer.StandDown"/>); the run then ends stopped at the step it stood at, its waiting sessions with it
/// (<see cref="RunSessionEnds"/>) and a session still in its turn once it comes to wait (<see cref="RunReEnds"/>). Its tree and branch are
/// kept as the clean-up keeps work the line lacks, and the end's line names the commits the branch holds that the line does not, so what is
/// left is said and never landed by a run nobody owns. Pure: the runtime writes it (<see cref="NodeRunKeeping"/>).
/// </summary>
public static class QuestDoneRuns
{
    /// <summary>The end's reason (<see cref="RunEnd.Why"/>).</summary>
    public const string Why = "quest-done";

    /// <summary>The history entry each step it stops keeps, by which its end names the step the run stood at.</summary>
    public const string Stop = "quest-done-stop";

    /// <summary>How many of the commits the line lacks the end's line names one by one.</summary>
    private const int Named = 10;

    /// <summary>Whether another closed the run's newest quest here Done: the run's own end is not on its record yet.</summary>
    public static bool Applies(GovernedUnit governed, RunScope scope, NodeRecord? record) =>
        !governed.Closes && !AskRunKey.Is(scope.Run.Binding) && record is { Ended: null } && scope.Quest?.Records.Quest is { } newest
        && DoneOutside(newest, scope.Run.Run, scope.Run.Binding.Repository);

    /// <summary>
    /// Whether <paramref name="quest"/> is Done and bound to <paramref name="run"/>, with no step of its chain still to come in
    /// <paramref name="repository"/>, whose next quest the run goes on with. A quest bound to no run closes by its agent's done, as with a host
    /// that answers no bind, and its run goes on.
    /// </summary>
    public static bool DoneOutside(QuestView quest, string run, string repository) =>
        string.Equals(quest.Status, "Done", StringComparison.OrdinalIgnoreCase)
        && string.Equals(quest.Closure, ClosureGraphs.RunClosure(run), StringComparison.Ordinal)
        && !quest.Then.Any(step => step.SetUpIn is null && string.Equals(step.To, repository, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The record with every live or failed step stopped by Daoris and its attempt in flight stood down, and the steps stopped in the
    /// graph's order; an <c>integrate</c> whose member promoted (<paramref name="promoted"/>) is the line's integration's and stays.
    /// </summary>
    public static (NodeRecord Record, IReadOnlyList<string> Stopped) Stopped(
        GovernedUnit governed, NodeRecord record, DateTimeOffset now, IReadOnlySet<string>? promoted = null)
    {
        var next = record;
        var stopped = new List<string>();
        foreach (var node in governed.Graph.Nodes)
        {
            var status = WorkPerformer.StatusOf(next, node.Id);
            if (promoted?.Contains(node.Id) == true || !Stands(status.State)) continue;
            var moved = NodeLifecycle.Apply(governed.Declared[node.Id], status,
                new NodeMove(NodeStates.Stopped, NodeCause.Daoris) { Words = "its quest was closed Done outside its run" });
            if (!moved.Moved) continue;
            next = WorkPerformer.Kept(next, moved, Stop, now);
            // The end names the step the run stood at from these entries, so a stop that cleared nothing keeps one too.
            if (moved.Cleared is null) next = next with { History = [.. next.History, new NodeHistory(now, Stop, status)] };
            next = StandDowns.Settle(next, node.Id, status.Attempt, NodeWordActs.Stop, checks: true);
            stopped.Add(node.Id);
        }

        return (next, stopped);
    }

    /// <summary>
    /// Whether no step of the run is left standing for the end. An <c>integrate</c> is not counted: one not promoted is stopped, and a promoted
    /// one is the line's integration's to land, which reads no run.
    /// </summary>
    public static bool Ends(GovernedUnit governed, NodeRecord record) =>
        !governed.Graph.Nodes.Any(node => node.Kind != NodeKinds.Integrate && Stands(WorkPerformer.StatusOf(record, node.Id).State));

    /// <summary>The run's end: stopped by Daoris at the step it stood at, the first it stopped with an attempt under way, else the first it stopped.</summary>
    public static RunEnd End(NodeRecord record, string quest, DateTimeOffset at)
    {
        var last = record.History.Where(entry => entry.Why == Stop).Select(entry => entry.At).DefaultIfEmpty().Max();
        var stopped = record.History.Where(entry => entry.Why == Stop && entry.At == last).Select(entry => entry.Was).ToList();
        var node = stopped.FirstOrDefault(was => was.State is NodeStates.Active or NodeStates.Unknown)?.Node ?? stopped.FirstOrDefault()?.Node;
        return new RunEnd(quest, at) { Outcome = RunOutcomes.Stopped, Node = node, By = NodeSource.Daoris, Why = Why };
    }

    /// <summary>
    /// What the run's branch leaves that the line lacks, as its end's line says it: the commits one by one, oldest first, up to
    /// <see cref="Named"/>; that the line holds every one; that git could not say; or that no branch of it stands.
    /// </summary>
    public static string Left(string? branch, IReadOnlyList<LandingCommit>? lacks)
    {
        if (branch is null) return "no branch of it stands, so nothing of it is left to land";
        if (lacks is null) return $"git could not say which commits of `{branch}` the line lacks";
        if (lacks.Count == 0) return $"the line holds every commit of `{branch}`";
        var named = string.Join("; ", lacks.Take(Named).Select(commit => $"{TextBounds.ShortCommit(commit.Sha, 8)} {commit.Subject}"));
        var more = lacks.Count > Named ? $"; and {lacks.Count - Named} more" : "";
        return $"its branch `{branch}` holds {lacks.Count} {(lacks.Count == 1 ? "commit" : "commits")} the line does not: {named}{more}";
    }

    /// <summary>A step the done stops: one still live, or one failed and held for its holder's word.</summary>
    private static bool Stands(string state) => NodeStates.Live.Contains(state) || state == NodeStates.Failed;
}
