namespace Daoris.Driver;

/// <summary>
/// The remote sync a whole look starts with, so the look plans over what the team has done. Its failure is a line, never a dead
/// look: the next look retries, and every verb has already committed here. A sync dying quietly looks exactly like a family with
/// nothing to say, so the wall is named.
/// </summary>
/// <remarks>
/// Under the watch the sync runs beside the look, on the running set's own task, so a remote that does not answer holds that pass
/// and no local start: the look plans over what the last pass brought, and what this pass says joins a later look's report. One
/// pass runs at a time, and one asked for while a pass works follows it once. A wake's plan does not sync: the claims it would ask
/// after move only when the sync runs, so a wake costs the remote nothing.
/// </remarks>
[LookPass("sync", LookPhase.Before)]
internal sealed class LookSync(ILookWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct) =>
        round.Part == LookPart.Look ? SyncAsync(round.Events, ct) : Task.CompletedTask;

    /// <summary>The sync, beside the look under the watch and otherwise in it: the look's head's, and the one after what ended.</summary>
    public Task SyncAsync(List<string> events, CancellationToken ct)
    {
        if (!wiring.Beside) return OnceAsync(events, ct);
        Beside(ct);
        return Task.CompletedTask;
    }

    /// <summary>The sync, once, in the caller, with what it said added to <paramref name="events"/>.</summary>
    /// <param name="beside">Whether it runs beside the look, for its stage's mark.</param>
    public async Task OnceAsync(List<string> events, CancellationToken ct, bool beside = false)
    {
        if (wiring.Sync is not { } sync) return;
        SyncReport synced;
        using (var syncing = wiring.Stages.Begin("sync", beside: beside))
        {
            // Its gate's queue, and its git's waits for a place, said as one.
            synced = await syncing.RunAsync(() => sync.RunOnceAsync(ct)).ConfigureAwait(false);
            syncing.Queued(synced.Queued);
        }

        lock (events)
        {
            if (synced.Problem is not null)
            {
                events.Add(wiring.Lines.Retry("sync", $"sync  {synced.Problem}"));
            }

            // What the remote understood and deliberately did not take: a stale or branch feed, a quest it would not keep, a
            // move that lost to another machine's. Each is news about the family, not a fault here.
            foreach (var note in synced.Notes) events.Add(wiring.Lines.Progress("sync", $"held  {note}"));
        }
    }

    /// <summary>
    /// The sync beside the look: the lost-claim stop after it is the next look's (<see cref="RunningSessions.TakeClaimsDue"/>), so a
    /// stop is still said by the look that made it.
    /// </summary>
    private void Beside(CancellationToken ct)
    {
        if (wiring.Sync is null) return;
        wiring.Runs.SyncBeside(async token =>
        {
            var said = new List<string>();
            await OnceAsync(said, token, beside: true).ConfigureAwait(false);
            return said;
        }, ct);
    }
}
