namespace Daoris.Driver;

/// <summary>
/// The lease's and the move record's test of a live process, read through the machine's liveness the root chose: a static funnel
/// over the port, since <c>HomeLease</c> is compiled into the launcher and the git door too, which compile the machine adapter's own
/// <c>ProcessLiveness</c> under this name.
/// </summary>
internal static class ProcessLiveness
{
    /// <summary>Whether the process <paramref name="pid"/> runs as the one that started at <paramref name="started"/> (UTC ticks).</summary>
    public static bool Alive(int pid, long started) => MachinePorts.Liveness.Runs(pid, new DateTimeOffset(started, TimeSpan.Zero));
}
