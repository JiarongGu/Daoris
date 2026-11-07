namespace Daoris.Driver;

/// <summary>
/// A quest's session stopped for its hand-over, waiting for what it left running:
/// its job is taken from its tracking before the stop, so what its process left in the background stays in it; once the session's own process
/// has gone (its run let go of it), what the job still holds is ended and waited for, as a conversation's hand-over and an account's move
/// wait (<see cref="PartLeftovers"/>). The take writes the move's old end only after this returns, so the carry-on starts at a later look,
/// once the stop has ended the job.
/// </summary>
public static class QuestPartStop
{
    /// <summary>How long the stop waits for the session's own process to go before it ends what the job holds.</summary>
    public static readonly TimeSpan ProcessGone = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Stop <paramref name="session"/> as the person's stop with <paramref name="note"/>, then end and wait for what its job still holds.
    /// The stop's answer, and what the part left; null where no job is held here (a session another process runs, or no system count).
    /// </summary>
    public static async Task<(StopAnswer Stop, PartLeft? Left)> StopAsync(
        SessionProcesses processes, ServiceClient? service, string session, Noted note, CancellationToken ct)
    {
        var job = processes.TakeJob(session);
        StopAnswer answer;
        PartLeft? left;
        try
        {
            answer = await SessionMoves.StopAsync(processes, service, session, ct, note).ConfigureAwait(false);
            for (var until = DateTime.UtcNow + ProcessGone; job is not null && processes.RunsHere(session) && DateTime.UtcNow < until;)
            {
                await Task.Delay(50, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            left = await PartLeftovers.EndAsync(job, PartLeftovers.Bound).ConfigureAwait(false);
        }

        return (answer, left);
    }
}
