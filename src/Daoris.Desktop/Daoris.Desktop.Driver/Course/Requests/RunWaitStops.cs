namespace Daoris.Driver;

/// <summary>What the person's stop of a session awaiting its run's step came to (D316): the steps whose stop is kept, or why none is.</summary>
/// <param name="Run">The run the session waits on, as its record's wait names it.</param>
/// <param name="Steps">The steps whose stop is kept, in the graph's order, for Daoris's next look to apply.</param>
public sealed record RunWaitStopped(string Run, IReadOnlyList<string> Steps)
{
    /// <summary>Why nothing is stopped: the node door's code, or <see cref="RunWaitStops.NothingStands"/>; null where a stop is kept.</summary>
    public string? Refused { get; init; }

    /// <summary>The step a refusal is about; null where the run names none.</summary>
    public string? Node { get; init; }

    /// <summary>What the refusal needs to say itself (<see cref="NodeWorded.Detail"/>).</summary>
    public string? Detail { get; init; }

    /// <summary>Where the refused step stands (<see cref="NodeWorded.State"/>).</summary>
    public string? State { get; init; }

    public bool Done => Refused is null;
}

/// <summary>How the stop reaches a run's steps: the home's node records, the integration record a stop reads first, and who speaks it.</summary>
/// <param name="Speaker">Who the stop is from at the settle door, by its channel (<see cref="SettleBy.OfChannel"/>); null is the person's, which a stop makes the owner role's.</param>
public sealed record RunWaitStop(NodeRecordFiles Records, IIntegrationDoor Door, SettleBy? Speaker = null)
{
    public Func<DateTimeOffset> Clock { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>The stop over <paramref name="home"/>'s records, read as the node door's own callers read them.</summary>
    public static RunWaitStop At(string home, SettleBy? speaker = null) => new(new NodeRecordFiles(home), NodeRuntime.HomeDoor(home), speaker);
}

/// <summary>
/// The person's stop of a session awaiting its run's step is their stop of that run (D316): every step of the run a stop still reaches (one
/// live, or the one its failure holds), stopped through the run's node door as their own <c>daoris workflow stop &lt;run&gt; &lt;node&gt;</c> is
/// (<see cref="RunNodeWords.WordAsync"/>), at the settle door as its speaker's. The next pass applies it, the run ends stopped and its end
/// ends the session (<see cref="RunSessionEnds"/>); the record is never moved alone, which would leave the run waiting with no face.
/// </summary>
public static class RunWaitStops
{
    /// <summary>The run stands at no step a stop reaches: none live, none failed.</summary>
    public const string NothingStands = "nothing-stands";

    /// <summary>The states a step's stop is offered at (<see cref="RunNodeWords.Acts"/>), not reached yet included, as a whole stop takes them.</summary>
    private static readonly string[] Reached = [.. NodeStates.Live, NodeStates.Failed];

    /// <summary>The run <paramref name="record"/> waits on, where it is this machine's record awaiting a run's step; null for any other.</summary>
    public static string? RunOf(SessionRecord record) =>
        record is { State: SessionStates.AwaitingStep, Teammate: false } ? StepWaits.From(record.NoteParts)?.Run : null;

    /// <summary>Keeps the stop of the run <paramref name="record"/> waits on; null where it waits on no run (<see cref="RunOf"/>).</summary>
    public static async Task<RunWaitStopped?> StopAsync(RunWaitStop how, SessionRecord record, CancellationToken ct = default)
    {
        if (RunOf(record) is not { } run) return null;
        var records = how.Records;
        if (new GraphRuns(records).Find(run) is not { } bound) return Refused(run, RunNodeWords.NotRun);
        if (bound.Version.Graph is not { } graph || records.Read(bound.Unit) is not { } kept)
        {
            return Refused(run, RunNodeWords.UnreadVersion) with { Detail = bound.Version.Unreadable };
        }

        // A run that came to its end ends its sessions at the look that reads it; only the step a failure holds still takes a stop.
        if (bound.Ended is { Outcome: not RunOutcomes.Failed } ended) return Refused(run, RunNodeWords.Ended) with { Detail = ended.Quest };

        var steps = graph.Nodes.Where(node => Reached.Contains(WorkPerformer.StatusOf(kept, node.Id).State)).ToList();
        var stopped = new List<string>();
        RunWaitStopped? refused = null;
        foreach (var node in steps)
        {
            var done = await RunNodeWords.WordAsync(records, how.Door, run, node.Id, NodeWordActs.Stop, words: null, how.Clock, ct, settle: how.Speaker)
                .ConfigureAwait(false);
            if (done.Done) stopped.Add(node.Id);
            else refused ??= Refused(run, done.Refused!) with { Node = node.Id, Detail = done.Detail, State = done.State };
        }

        return stopped.Count > 0 ? new RunWaitStopped(run, stopped) : refused ?? Refused(run, NothingStands);
    }

    /// <summary>The sentence the doors say for <paramref name="stopped"/> of session <paramref name="session"/>: what is kept, or why nothing is.</summary>
    public static string Said(string session, RunWaitStopped stopped) => stopped.Done
        ? $"sessions: {session} waits on run {stopped.Run}: your stop of it is kept at {string.Join(", ", stopped.Steps.Select(step => $"`{step}`"))}, "
          + "and Daoris's next look ends the run and the session with it."
        : $"sessions: {session} waits on run {stopped.Run}, and nothing of it was stopped: {Why(stopped)}";

    /// <summary>Why nothing of the run is stopped, the node door's own sentence where it refused; null where the stop is kept.</summary>
    public static string? Why(RunWaitStopped stopped) => stopped switch
    {
        { Done: true } => null,
        { Refused: NothingStands } => "it stands at no step a stop reaches now; Daoris's next look moves it on.",
        _ => RunNodeWords.Said(new NodeWorded(stopped.Run, stopped.Node ?? "", NodeWordActs.Stop, stopped.Refused) { Detail = stopped.Detail, State = stopped.State })
            ["workflow: ".Length..],
    };

    private static RunWaitStopped Refused(string run, string code) => new(run, []) { Refused = code };
}
