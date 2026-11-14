namespace Daoris.Driver;

/// <summary>
/// The broker's order, pure: one check runs at a time on this machine, the oldest ask goes first. The machine's admission (<see cref="MachineAdmission"/>)
/// still decides when a heavy run may start beside the terminal's and the merge tool's.
/// </summary>
public static class CheckQueue
{
    /// <summary>The next ask to run, or null where one runs already or none waits.</summary>
    public static CheckAsk? Next(IReadOnlyList<CheckAsk> asks, Func<string, CheckState?> state, DateTimeOffset? now = null) =>
        asks.Any(ask => state(ask.Id)?.State == CheckStates.Running) ? null : Pending(asks, state, now ?? DateTimeOffset.UtcNow).FirstOrDefault();

    /// <summary>How long a check Daoris could not run waits before it is asked again.</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(1);

    /// <summary>How many checks are to finish before <paramref name="ask"/> starts: the one running, and the waiting ones before it.</summary>
    public static int Ahead(CheckAsk ask, IReadOnlyList<CheckAsk> asks, Func<string, CheckState?> state) =>
        Ordered(asks, state).ToList().FindIndex(each => each.Id == ask.Id) is var at and >= 0 ? at : 0;

    private static IEnumerable<CheckAsk> Pending(IReadOnlyList<CheckAsk> asks, Func<string, CheckState?> state, DateTimeOffset? now = null) =>
        asks.Where(ask => state(ask.Id) is null or { State: CheckStates.Queued }
                || (state(ask.Id) is { State: CheckStates.Errored } errored && errored.At + RetryAfter <= (now ?? DateTimeOffset.UtcNow)))
            .OrderBy(ask => ask.At).ThenBy(ask => ask.Id, StringComparer.Ordinal);

    private static IEnumerable<CheckAsk> Ordered(IReadOnlyList<CheckAsk> asks, Func<string, CheckState?> state) =>
        asks.Where(ask => state(ask.Id)?.State == CheckStates.Running).Concat(Pending(asks, state));
}
