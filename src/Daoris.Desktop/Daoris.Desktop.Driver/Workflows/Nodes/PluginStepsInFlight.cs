namespace Daoris.Driver;

/// <summary>
/// The plugin steps in flight the runtime governs: each attempt a plugin performs (its step <c>&lt;plugin&gt;/&lt;node&gt;</c>) that is
/// recorded, sent or unknown, so sent or perhaps sent and its outcome not settled, on a run that goes on, at a node its graph declares, while
/// its plugin's process is up, and whose step acts outside. What an update's drain waits on beside the driven sessions and the turns: a swap
/// then cuts the call off, and its outcome is not known until the restart asks after it. An unknown attempt holds it for
/// <see cref="UnknownBound"/> at most.
/// </summary>
/// <remarks>
/// None of these is in flight: a settled attempt; a held one, which waits on the person's word rather than its plugin; Daoris's own
/// kinds (a check, a close, a request), which a restart asks after, or sends again itself (once more where an executor's own close cut it off, and held for the person only where its process vanishes with nothing to explain it); an ended run's, which nothing moves any more;
/// one on a record the runtime does not govern or at a node its graph does not declare (a retired repository, a version that no longer
/// reads), and one whose plugin's process is down, neither of which the runtime asks after; and one whose step waits on a role's act
/// outside its plugin (its progress names the role, an approver, a deployer, whoever holds it), which its plugin's watch resumes after a
/// restart; and a watch, whose plugin declares it acts on nothing (<see cref="NodePerform.Acts"/>). Read through the runtime (<see cref="NodeRuntime.PluginStepsInFlight"/>), never written.
/// </remarks>
public static class PluginStepsInFlight
{
    /// <summary>How long an attempt whose outcome is not known holds an update, from when it is first counted so.</summary>
    public static readonly TimeSpan UnknownBound = TimeSpan.FromMinutes(15);

    /// <summary>Whether an attempt is a plugin's step sent, or perhaps sent, and not settled.</summary>
    public static bool InFlight(NodeAttempt attempt) =>
        attempt.State is AttemptStates.Recorded or AttemptStates.Sent or AttemptStates.Unknown && NodeKit.IsPlugin(attempt.Call.Step);

    /// <summary>
    /// The plugin steps in flight on the <paramref name="governed"/> units' records as <paramref name="record"/> reads them now, at the nodes
    /// each one's graph declares, whose plugin is <paramref name="up"/>: a watch (its plugin's step acts on nothing, <see cref="NodePerform.Acts"/>)
    /// is none, and an attempt not known is one only within <see cref="UnknownBound"/> of when <paramref name="unknown"/> first counted it so.
    /// </summary>
    public static int Count(
        IEnumerable<GovernedUnit> governed, Func<string, NodeRecord?> record, Func<string, bool> up, UnknownAttempts unknown, DateTimeOffset now)
    {
        var count = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var each in governed)
        {
            if (record(each.Unit.Id) is not { Ended: null } kept) continue;
            foreach (var attempt in kept.Attempts)
            {
                if (!InFlight(attempt) || each.Declared.GetValueOrDefault(attempt.Node) is not { } node || node.Perform is { Acts: false }
                    || WaitsOnARole(kept, node, attempt) || !up(attempt.Plugin)) continue;
                if (attempt.State == AttemptStates.Unknown && !unknown.Holds(UnknownAttempts.Key(each.Unit.Id, attempt), now, seen)) continue;
                count++;
            }
        }

        unknown.Forget(seen);
        return count;
    }

    /// <summary>Whether the attempt's step waits on a role's act outside its plugin: its progress names the role, whoever holds it.</summary>
    public static bool WaitsOnARole(NodeRecord record, NodeDeclaration node, NodeAttempt attempt) =>
        StepWaiting.Of(record, node, WorkPerformer.StatusOf(record, node.Id)) is { Actor: not null }
        && record.Newest(node.Id)?.Attempt == attempt.Attempt;
}

/// <summary>
/// When this process first counted each attempt whose outcome is not known, in memory: such an attempt holds an update for
/// <see cref="PluginStepsInFlight.UnknownBound"/> from then. One counted otherwise since is forgotten, so an unknown again later has its own
/// bound; a restart forgets all, and its plugin is asked after each attempt as after any stop.
/// </summary>
public sealed class UnknownAttempts
{
    private readonly Dictionary<string, DateTimeOffset> _first = new(StringComparer.Ordinal);

    /// <summary>The attempt's key: its unit, node, number and invocation.</summary>
    public static string Key(string unit, NodeAttempt attempt) => $"{unit}\n{attempt.Node}\n{attempt.Attempt}\n{attempt.Call.Invocation}";

    /// <summary>Whether the attempt under <paramref name="key"/>, unknown now, still holds an update; kept among <paramref name="seen"/>.</summary>
    public bool Holds(string key, DateTimeOffset now, ISet<string> seen)
    {
        seen.Add(key);
        lock (_first)
        {
            if (!_first.TryGetValue(key, out var since)) _first[key] = since = now;
            return now - since < PluginStepsInFlight.UnknownBound;
        }
    }

    /// <summary>Forgets every attempt but <paramref name="seen"/>, those counted unknown now.</summary>
    public void Forget(IReadOnlySet<string> seen)
    {
        lock (_first)
        {
            foreach (var gone in _first.Keys.Where(key => !seen.Contains(key)).ToList()) _first.Remove(gone);
        }
    }
}
