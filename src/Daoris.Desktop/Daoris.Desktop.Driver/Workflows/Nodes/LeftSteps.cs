namespace Daoris.Driver;

/// <summary>
/// The step a run ended failed at, its holder's to settle (<see cref="FailedHeldRuns"/>), as <i>What needs you</i> rows it whatever its quest's
/// state: a <c>step</c> row waiting on the line's lead (<see cref="PersonWaits.Left"/>) with <i>Try again</i> (a new attempt, which governs the run
/// again; a plugin's held attempt's own where it holds one), <i>Done outside Daoris…</i> where its kind reads the proof
/// (<see cref="RunNodeWords.Outside"/>) or its plugin's attempt offers it, and <i>End this work…</i>, the step's stop, which ends the run.
/// **Read, never stored**: from the runs bound here and each one's record now, so a step a word is kept on, or that moved, has no row.
/// </summary>
public static class LeftSteps
{
    /// <summary>The step the run ended failed at that still waits on its holder: failed there, no word pending on it; null otherwise.</summary>
    public static string? Waiting(GraphRun run, NodeRecord? record) =>
        run.Line is null && FailedHeldRuns.Step(record) is { } node && !record!.Pending.Any(word => word.Node == node) ? node : null;

    /// <summary>The steps waiting on a role the person holds in the runs last governed (none before a pass), and each run's left step.</summary>
    internal static IReadOnlyList<HeldStep> With(IEnumerable<GovernedUnit>? governed, NodeRecordFiles records, GraphRuns runs, DriverConfig? config) =>
    [
        .. governed is null ? [] : HeldSteps.Of(governed, records.Read, new StepTenureFiles(records.Home, records.HoldsLease), new PlaceCastFiles(records.Home).Book(),
            config?.PlaceRisk, new StepVacancyFiles(records.Home), VacancySetting.Of(config)),
        .. Of(runs.Read(), records.Read),
    ];

    public static IReadOnlyList<HeldStep> Of(IEnumerable<GraphRun> runs, Func<string, NodeRecord?> record)
    {
        var steps = new List<HeldStep>();
        foreach (var run in runs.Where(run => run.Ended is { Outcome: RunOutcomes.Failed }))
        {
            var kept = record(run.Unit);
            if (Waiting(run, kept) is { } failed && run.Version.Graph?.Nodes.FirstOrDefault(node => node.Id == failed) is { } node)
            {
                steps.Add(Step(run.Run, run.Binding.Repository, run.Binding.Workspace, node, kept!));
            }
        }

        return [.. steps.OrderBy(step => step.Run, StringComparer.Ordinal)];
    }

    /// <summary>The row of the step <paramref name="run"/> ended failed at, as <paramref name="record"/> holds it.</summary>
    public static HeldStep Step(string run, string repository, string workspace, NodeDeclaration node, NodeRecord record)
    {
        var status = WorkPerformer.StatusOf(record, node.Id);
        var held = PluginFailureHeld.Of(record, node, status) is { Words.Count: > 0 } attempt ? attempt : null;
        List<string> presses = held is not null ? [.. held.Words] : node.Reach == NodeReach.Performed ? [NodeWordActs.Again] : [];
        // A claim already kept is watched for at each look: it is not offered again.
        if (held is null && status.Holds != NodeWordActs.DoneOutside && RunNodeWords.Outside(node, status)) presses.Add(NodeWordActs.DoneOutside);
        presses.Add(NodeWordActs.Stop);
        var ended = record.Ended!;
        return new HeldStep(run, node.Id, repository, workspace, PersonWaits.Left, StepWaiting.RoleOf(PersonWaits.Left),
            [.. presses.Distinct(StringComparer.Ordinal)], ended.Quest, ended.At)
        {
            Held = held, Words = status.Words ?? status.Code,
        };
    }
}
