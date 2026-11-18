namespace Daoris.Driver;

/// <summary>How the stop reaches a run's steps: the home's node records, the integration record a stop reads first, and who speaks it.</summary>
/// <param name="Clock">When the stop is said, the caller's clock.</param>
/// <param name="Speaker">Who the stop is from at the settle door, by its channel (<see cref="SettleBy.OfChannel"/>); null is the person's, which a stop makes the owner role's.</param>
public sealed record RunWaitStop(NodeRecordFiles Records, IIntegrationDoor Door, Func<DateTimeOffset> Clock, SettleBy? Speaker = null)
{
    /// <summary>The stop over <paramref name="home"/>'s records, read as the node door's own callers read them.</summary>
    public static RunWaitStop At(string home, Func<DateTimeOffset> clock, SettleBy? speaker = null) =>
        new(new NodeRecordFiles(home), NodeRuntime.HomeDoor(home), clock, speaker);
}

/// <summary>
/// The person's stop of a session awaiting its run's step is their stop of that run: every step of the run a stop still reaches (one
/// live, or the one its failure holds), stopped through the run's node door as their own <c>daoris workflow stop &lt;run&gt; &lt;node&gt;</c> is
/// (<see cref="RunNodeWords.WordAsync"/>), at the settle door as its speaker's. The next pass applies it, the run ends stopped and its end
/// ends the session (<see cref="RunSessionEnds"/>); the record is never moved alone, which would leave the run waiting with no face. The
/// run is found as <see cref="RunWaiters"/> finds it, by the record's wait.
/// </summary>
public static class RunWaitStops
{
    /// <summary>The states a step's stop is offered at (<see cref="RunNodeWords.Acts"/>), not reached yet included, as a whole stop takes them.</summary>
    private static readonly string[] Reached = [.. NodeStates.Live, NodeStates.Failed];

    /// <summary>The run <paramref name="record"/> waits on, where it is this machine's record awaiting a run's step; null for any other.</summary>
    public static string? RunOf(SessionRecord record) =>
        record is { State: SessionStates.AwaitingStep, Teammate: false } ? StepWaits.From(record.NoteParts)?.Run : null;

    /// <summary>The stop as a session's stop asks it (<see cref="SessionMoves.StopAsync"/>): over <paramref name="home"/>, as <paramref name="speaker"/> speaks.</summary>
    public static Func<SessionRecord, CancellationToken, Task<RunWaitStopped?>> Port(string home, Func<DateTimeOffset> clock, SettleBy? speaker = null) =>
        (record, ct) => StopAsync(RunWaitStop.At(home, clock, speaker), record, ct);

    /// <summary>Keeps the stop of the run <paramref name="record"/> waits on; null where it waits on no run (<see cref="RunOf"/>).</summary>
    public static async Task<RunWaitStopped?> StopAsync(RunWaitStop how, SessionRecord record, CancellationToken ct = default)
    {
        if (RunOf(record) is not { } run) return null;
        var records = how.Records;
        if (new GraphRuns(records).Find(run) is not { } bound) return Refused(new NodeWorded(run, "", NodeWordActs.Stop, RunNodeWords.NotRun));
        if (bound.Version.Graph is not { } graph || records.Read(bound.Unit) is not { } kept)
        {
            return Refused(new NodeWorded(run, "", NodeWordActs.Stop, RunNodeWords.UnreadVersion) { Detail = bound.Version.Unreadable });
        }

        // A run that came to its end ends its sessions at the look that reads it; only the step a failure holds still takes a stop.
        if (bound.Ended is { Outcome: not RunOutcomes.Failed } ended)
        {
            return Refused(new NodeWorded(run, "", NodeWordActs.Stop, RunNodeWords.Ended) { Detail = ended.Quest });
        }

        var stopped = new List<string>();
        NodeWorded? refused = null;
        foreach (var node in graph.Nodes.Where(node => Reached.Contains(WorkPerformer.StatusOf(kept, node.Id).State)))
        {
            var done = await RunNodeWords.WordAsync(records, how.Door, run, node.Id, NodeWordActs.Stop, words: null, how.Clock, ct, settle: how.Speaker)
                .ConfigureAwait(false);
            if (done.Done) stopped.Add(node.Id);
            else refused ??= done;
        }

        if (stopped.Count > 0) return new RunWaitStopped(run, stopped);
        return refused is not null
            ? Refused(refused)
            : new RunWaitStopped(run, [])
            {
                Refused = RunWaitStopped.NothingStands, Why = "it stands at no step a stop reaches now; Daoris's next look moves it on.",
            };
    }

    /// <summary>A refusal of the node door's, with its own sentence.</summary>
    private static RunWaitStopped Refused(NodeWorded refused) => new(refused.Run, [])
    {
        Refused = refused.Refused, Node = refused.Node.Length > 0 ? refused.Node : null, Why = RunNodeWords.Said(refused)["workflow: ".Length..],
    };
}
