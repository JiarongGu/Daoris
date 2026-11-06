namespace Daoris.Driver;

/// <summary>
/// What the outgoing part left running when a hand-over or an account's move ended it (COAGENT1g; the co-agent design §2.6, §9 row 7): how
/// many processes its job still held once its own process had gone, and whether they were gone before the next part started.
/// </summary>
public sealed record PartLeft(int Count, bool Emptied)
{
    /// <summary>The old part's line that says it, or null where it left nothing.</summary>
    public Noted? Note(TimeSpan bound)
    {
        if (Count == 0) return null;
        if (Emptied)
        {
            return Noted.Of(
                ChatNoteCodes.ChatLeftEnded,
                $"Daoris ended what this part left running, {Count} in all, and waited until it was gone before the next part started in this tree.",
                ("count", Count));
        }

        var seconds = (int)bound.TotalSeconds;
        return Noted.Of(
            ChatNoteCodes.ChatLeftRunning,
            $"What this part left running, {Count} in all, had not ended after {seconds} seconds: the next part started in this tree beside it.",
            ("count", Count), ("seconds", seconds));
    }
}

/// <summary>
/// The wait a hand-over makes before its next part starts in the same tree (COAGENT1g). The runner's stop kills the part's process tree and
/// ends its job, but waits for neither, and a command the agent left in the background whose parent had already exited is out of the tree's
/// reach. So the job is taken from the part's tracking before the stop (<see cref="SessionProcesses.TakeJob"/>), and once the part's own
/// process has gone, what the job still holds is ended and waited for, as a partner's strict job is at its turn's end.
/// </summary>
public static class PartLeftovers
{
    /// <summary>How long the wait lasts: past it, the next part starts and the old part's line says what still runs.</summary>
    public static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    /// <summary>
    /// End what the job still holds and wait, within <paramref name="bound"/>, until it holds nothing; then let go of it, which ends anything
    /// left. Null where there is no job to read: none was held, or the system keeps no count (elsewhere than Windows).
    /// </summary>
    public static async Task<PartLeft?> EndAsync(ProcessJob? job, TimeSpan bound)
    {
        if (job is null) return null;
        using (job)
        {
            if (job.Active is not { } held) return null;
            if (held == 0) return new PartLeft(0, Emptied: true);
            job.Terminate();
            return new PartLeft(held, await job.WaitEmptyAsync(bound).ConfigureAwait(false));
        }
    }
}
