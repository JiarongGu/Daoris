namespace Daoris.Driver;

/// <summary>
/// A run ended failed at a step its holder may still settle (<see cref="EmptyRunEnds.FailedHeld"/>): its end ends no session, and the step
/// stays the person's to try again, say done outside Daoris or end (<see cref="LeftSteps"/>). Where the step's kind reads a failed attempt
/// (<see cref="IObserves.ObservesFailed"/>) it is still observed at each look, and an observed reach governs the run again, its end lifted
/// as the holder's word at the step lifts it, so the run goes on: an observed fact settles a step, whoever made it.
/// </summary>
public static class FailedHeldRuns
{
    /// <summary>The step <paramref name="record"/> ended failed at, while it still stands failed there; null for any other end or none.</summary>
    public static string? Step(NodeRecord? record) =>
        record?.Ended is { Outcome: RunOutcomes.Failed, Node: { Length: > 0 } node } && WorkPerformer.StatusOf(record, node).State == NodeStates.Failed ? node : null;

    /// <summary>Whether a run's end may be one an observation settles, read off its binding alone: ended failed at a step whose kind observes.</summary>
    public static bool Candidate(GraphRun run) =>
        run is { Line: null, Ended: { Outcome: RunOutcomes.Failed, Node: { } node }, Version.Graph: { } graph }
        && graph.Nodes.FirstOrDefault(each => each.Id == node) is { } declared && NodeKit.Of(declared.Kind) is IObserves;

    /// <summary>Whether an observed fact may still settle the step the run ended failed at: its kind reads its failure where it stands.</summary>
    public static bool Observed(NodeGraph? graph, NodeRecord? record) =>
        Step(record) is { } node && graph?.Nodes.FirstOrDefault(each => each.Id == node) is { } declared
        && NodeKit.Of(declared.Kind) is IObserves observes && observes.ObservesFailed(WorkPerformer.StatusOf(record!, node));

    /// <summary>Whether the run arms the runtime for its failed end: an observed fact may still settle its step.</summary>
    public static bool Arms(GraphRun run, Func<string, NodeRecord?> record) => Candidate(run) && Observed(run.Version.Graph, record(run.Unit));

    /// <summary>The runs read, each ended failed at a step an observed fact may still settle marked so (<see cref="GraphRun.Observed"/>).</summary>
    public static List<GraphRun> Mark(List<GraphRun> runs, Func<string, NodeRecord?> record) =>
        [.. runs.Select(run => Candidate(run) && Observed(run.Version.Graph, record(run.Unit)) ? run with { Observed = true } : run)];

    /// <summary>
    /// Whether the run's failed step reads reached now, its graph judged on its record as its rounds judge it, writing nothing: where it
    /// does, its end is lifted, only while the record still ends failed there, and the run's rounds go on.
    /// </summary>
    public static async Task<bool> AgainAsync(
        NodeRecordFiles records, GraphRuns runs, GovernedUnit governed, string? tip, PassGit git, Func<DateTimeOffset> clock, CancellationToken ct)
    {
        if (records.Read(governed.Unit.Id) is not { } record || !Observed(governed.Graph, record) || Step(record) is not { } step) return false;
        var reads = await NodeEvaluations.GitReadsAsync(governed, record, git, ct).ConfigureAwait(false);
        if (NodeEvaluations.EvaluateRun(governed, record, tip, reads, (null, null), clock).Evaluated.Statuses.GetValueOrDefault(step)?.State is not (NodeStates.Reached or NodeStates.Skipped)) return false;
        var (refused, lifted) = records.Edit<bool>(governed.Unit.Id, current => Step(current) == step ? (current with { Ended = null }, true) : (null, false));
        if (refused is not null || lifted != true) return false;
        runs.Ended(governed.Run, null);
        return true;
    }
}
