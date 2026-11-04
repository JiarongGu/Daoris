namespace Daoris.Driver;

/// <summary>
/// After the sync, every session this driver runs whose take came back lost is stopped: its quest is another machine's, and its
/// work would double theirs. An unconfirmed take keeps working. The claim is asked of this machine's host; a driver stops only its
/// own processes, and the record says why, not that the person did.
/// </summary>
/// <remarks>
/// Once each: a session already stopped for its lost take still runs until it has written how it ended, and a pass in between would
/// ask again and say a second stop. Under the watch the stop is still the look's, so a stop is said with the look that made it: at
/// every whole look, and at the first look after a sync beside the looks ended while a session here held a quest.
/// </remarks>
[LookPass("claims", LookPhase.Before, "sync")]
internal sealed class LostClaimStop(ILookWiring wiring) : ILookPass
{
    /// <summary>What a session whose take lost is told, in its record.</summary>
    public const string LostClaim =
        "stopped by this machine's driver: another machine's take on the quest reached the remote first, so this "
        + "session's take is a conflict on the quest and its work would double someone else's.";

    /// <summary><see cref="LostClaim"/> with its code.</summary>
    public static Noted LostClaimNoted => Noted.Of(NoteCodes.EndedLostClaim, LostClaim);

    /// <summary>
    /// What a session whose take lost to the person's reopen of the work its step grew from is told: the claim's answer says so beside
    /// the lost claim, and no other machine took anything first.
    /// </summary>
    public const string LostClaimToReopen =
        "stopped by this machine's driver: you reopened the work this session's step grew from, and that reached the remote before this "
        + "session's take did, so the take is a conflict on the quest. Its work stays in its tree; the step is open again for the work as "
        + "it stands now.";

    /// <summary><see cref="LostClaimToReopen"/> with its code.</summary>
    public static Noted LostClaimToReopenNoted => Noted.Of(NoteCodes.EndedLostClaimReopened, LostClaimToReopen);

    /// <summary>How many claim questions are asked of this machine's host together: local, and each one small.</summary>
    internal const int ClaimsAtOnce = 4;

    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        // `|`, not `||`: a whole look takes the due a sync beside the looks left, so the next plan does not ask it again.
        if (wiring.Sync is not null && (round.Part == LookPart.Look | wiring.Runs.TakeClaimsDue()))
        {
            await wiring.Stages.TimeAsync("claims", () => StopAsync(round.Events, ct)).ConfigureAwait(false);
        }
    }

    /// <summary>Asks after each running session's claim and stops each one lost, with what it stopped added to <paramref name="events"/>.</summary>
    public async Task StopAsync(List<string> events, CancellationToken ct)
    {
        // Asked at once, at most ClaimsAtOnce together, so a host slow to answer for one quest holds that question alone, not every
        // session's after it until the client gives up on it.
        await Parallel.ForEachAsync(
            wiring.Runs.Live.ToArray(), new ParallelOptions { MaxDegreeOfParallelism = ClaimsAtOnce, CancellationToken = ct },
            async (live, token) =>
            {
                var (quest, session) = (live.Key, live.Value);
                string claim;
                string? why;
                try
                {
                    (claim, why) = await wiring.Service.ClaimStandingAsync(quest, token).ConfigureAwait(false);
                }
                catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                                  // The client's own timeout: a host that took the question and stalled.
                                                  || (error is OperationCanceledException && !ct.IsCancellationRequested))
                {
                    return; // The host did not answer; the next pass asks again.
                }

                // A take lost to the person's reopen is stopped alike, and said as that, never as another machine's take.
                var reopened = why == "source-reopened";
                if (claim == "lost" && (wiring.Stops ?? Stop)(session, reopened ? LostClaimToReopenNoted : LostClaimNoted))
                {
                    var news = new LookNews(reopened ? LookSaid.LostToReopen : LookSaid.LostClaim, session, session, quest);
                    lock (events)
                    {
                        events.Add(wiring.Lines.News("stop", news, $"stop  session {session} (#{quest}): {(reopened ? LostClaimToReopen : LostClaim)}"));
                    }
                }
            }).ConfigureAwait(false);
    }

    /// <summary>The process's stop, unless the driver already stopped it for its reason: true only when it stopped it now.</summary>
    private bool Stop(string session, Noted reason) =>
        wiring.Processes.StopReason(session) is null && wiring.Processes.Stop(session, reason);
}
