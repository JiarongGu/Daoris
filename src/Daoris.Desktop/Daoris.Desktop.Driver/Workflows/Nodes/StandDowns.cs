namespace Daoris.Driver;

/// <summary>
/// An attempt in flight stood down as its step closes: a skip, a stop or its holder's <i>Done outside Daoris</i>
/// settles the plugin's attempt its step holds, its outcome <see cref="AttemptOutcomes.StoodDown"/> and its code the act, so nothing the run
/// waits on is left with it, its plugin is told (<see cref="WorkPerformer.StandDown"/>), and the plugin's later outcome of it is a replay.
/// Written as <c>settled</c>, never a state of its own: an older build rejects a record holding a state it does not know.
/// </summary>
public static class StandDowns
{
    /// <summary>
    /// Whether <paramref name="attempt"/> is one a step's close stands down: a plugin's, never Daoris's own close or request, whose doors answer
    /// for them, nor a check's, whose executor ends its job and says how it came out.
    /// </summary>
    public static bool Stands(NodeAttempt attempt) =>
        attempt.Plugin != QuestClosing.Sender && attempt.Plugin != RequestStart.Sender && !WorkPerformer.OnPort(attempt);

    /// <summary>Whether it is still in flight: nothing has established how it came out.</summary>
    public static bool InFlight(NodeAttempt attempt) => attempt.State != AttemptStates.Settled;

    /// <summary>
    /// The record with <paramref name="node"/>'s attempt <paramref name="attempt"/> stood down by <paramref name="act"/>, its node's gate lifted,
    /// where it is a plugin's still in flight, or a check's with <paramref name="checks"/>, for an end that reads no verdict (<see cref="QuestDoneRuns"/>).
    /// </summary>
    public static NodeRecord Settle(NodeRecord record, string node, int attempt, string act, bool checks = false)
    {
        if (record.Attempts.LastOrDefault(each => each.Node == node && each.Attempt == attempt) is not { } flying || !InFlight(flying)
            || !(Stands(flying) || (checks && WorkPerformer.OnPort(flying))))
        {
            return record;
        }

        record = WorkPerformer.With(record, flying, flying with { State = AttemptStates.Settled, Outcome = AttemptOutcomes.StoodDown, Code = act, AskAt = null });
        return record with { Statuses = WorkPerformer.With(record.Statuses, NodeLifecycle.Reconciled(WorkPerformer.StatusOf(record, node), attempt)) };
    }

    /// <summary>
    /// Whether its holder's <i>Done outside Daoris</i> is taken on <paramref name="attempt"/>: a plugin's attempt its step is still on, under way,
    /// held or unknown, or a failure kept for the person.
    /// </summary>
    public static bool Outside(NodeRecord record, NodeAttempt attempt) =>
        Stands(attempt)
        && (WorkPerformer.KeptFailure(record, attempt)
            || (InFlight(attempt) && WorkPerformer.StatusOf(record, attempt.Node) is { State: NodeStates.Active or NodeStates.Unknown } status
                && status.Attempt == attempt.Attempt));
}
