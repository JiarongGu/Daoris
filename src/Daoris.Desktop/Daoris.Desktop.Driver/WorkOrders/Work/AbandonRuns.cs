namespace Daoris.Driver;

/// <summary>
/// The runs of abandoned work: each live step of a run the work names is stopped as the person's, with their reason, through the run's node
/// door as their own stop is (<see cref="RunNodeWords.WordAsync"/>), so the next pass ends it and no review or landing of the work stays
/// offered. A done quest is not declined by an abandon, so its run would otherwise wait for a review nobody wants.
/// </summary>
internal static class AbandonRuns
{
    /// <summary>Every step stopped, <c>&lt;run&gt;/&lt;node&gt;</c>, of each run here not ended that <paramref name="of"/> takes.</summary>
    public static async Task<IReadOnlyList<string>> EndAsync(
        NodeRecordFiles records, Func<GraphRun, bool> of, string words, Func<DateTimeOffset> clock, CancellationToken ct = default)
    {
        var stopped = new List<string>();
        var door = NodeRuntime.HomeDoor(records.Home);
        foreach (var run in new GraphRuns(records).Read().Where(run => run.Ended is null && of(run)))
        {
            if (run.Version.Graph is not { } graph || records.Read(run.Unit) is not { } record) continue;
            foreach (var node in graph.Nodes.Where(node => NodeStates.Live.Contains(WorkPerformer.StatusOf(record, node.Id).State)))
            {
                var done = await RunNodeWords.WordAsync(records, door, run.Run, node.Id, NodeWordActs.Stop, words, clock, ct).ConfigureAwait(false);
                if (done.Done) stopped.Add($"{run.Run}/{node.Id}");
            }
        }

        return stopped;
    }
}
