namespace Daoris.Driver;

/// <summary>The record of a run whose wire broke after a stop made first.</summary>
internal static class StoppedRecord
{
    /// <summary>
    /// Ended as that stop's conclusion would have ended it, never interrupted, and shared by every run that catches a failure after
    /// <c>HoldAsync</c>. Best-effort, as the failure's own write is.
    /// </summary>
    public static async Task<SessionConclusion> EndAsync(ServiceClient service, string sessionId, SessionStop stop)
    {
        var (stopped, _) = Observation.Closed(stop);
        try
        {
            await service.AdvanceAsync(sessionId, stopped.State, stopped.AsNoted(), ct: CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The first failure is the report; a host that is gone here must not turn the stop into a second throw.
        }

        return stopped;
    }
}
