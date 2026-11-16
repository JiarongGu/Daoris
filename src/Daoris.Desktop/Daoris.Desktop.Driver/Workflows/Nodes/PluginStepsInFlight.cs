namespace Daoris.Driver;

/// <summary>
/// The plugin steps in flight the runtime governs: each attempt a plugin performs (its step <c>&lt;plugin&gt;/&lt;node&gt;</c>) that is
/// recorded, sent or unknown, so sent or perhaps sent and its outcome not settled, on a run that goes on, at a node its graph declares, while
/// its plugin's process is up. What an update's drain waits on beside the driven sessions and the turns: a swap then cuts the call off, and
/// its outcome is not known until the restart asks after it.
/// </summary>
/// <remarks>
/// None of these is in flight: a settled attempt; a held one, which waits on the person's word rather than its plugin; Daoris's own
/// kinds (a check, a close, a request), which a restart asks after or sends again itself; an ended run's, which nothing moves any more;
/// one on a record the runtime does not govern or at a node its graph does not declare (a retired repository, a version that no longer
/// reads), and one whose plugin's process is down, neither of which the runtime asks after; and one whose step waits on a role's act
/// outside its plugin (its progress names the role, an approver, a deployer, whoever holds it), which its plugin's watch resumes after a
/// restart. Read through the runtime (<see cref="NodeRuntime.PluginStepsInFlight"/>), never written.
/// </remarks>
public static class PluginStepsInFlight
{
    /// <summary>Whether an attempt is a plugin's step sent, or perhaps sent, and not settled.</summary>
    public static bool InFlight(NodeAttempt attempt) =>
        attempt.State is AttemptStates.Recorded or AttemptStates.Sent or AttemptStates.Unknown && NodeKit.IsPlugin(attempt.Call.Step);

    /// <summary>
    /// The plugin steps in flight on the <paramref name="governed"/> units' records as <paramref name="record"/> reads them now, at the nodes
    /// each one's graph declares, whose plugin is <paramref name="up"/>.
    /// </summary>
    public static int Count(IEnumerable<GovernedUnit> governed, Func<string, NodeRecord?> record, Func<string, bool> up) =>
        governed.Sum(each => record(each.Unit.Id) is { Ended: null } kept
            ? kept.Attempts.Count(attempt => InFlight(attempt) && each.Declared.GetValueOrDefault(attempt.Node) is { } node
                && !WaitsOnARole(kept, node, attempt) && up(attempt.Plugin))
            : 0);

    /// <summary>Whether the attempt's step waits on a role's act outside its plugin: its progress names the role, whoever holds it.</summary>
    public static bool WaitsOnARole(NodeRecord record, NodeDeclaration node, NodeAttempt attempt) =>
        StepWaiting.Of(record, node, WorkPerformer.StatusOf(record, node.Id)) is { Actor: not null }
        && record.Newest(node.Id)?.Attempt == attempt.Attempt;
}
