namespace Daoris.Driver;

/// <summary>
/// The transient retry a driven quest session is handed (AGENTERRORS1a, D210, the agent-errors design §5.1, §5.5): the one reader over
/// the adapter set, the driven check, Daoris's go-on kept through the say door, the conversation's one line, the machine log's lines and
/// the watch's heard stamp. Row 1a hands it at a start's <c>HoldAsync</c> and a continuation's alone; every other kind of run keeps
/// today's end until its row.
/// </summary>
internal static class TransientRetries
{
    /// <param name="questId">The quest the session works.</param>
    /// <param name="wanted">
    /// The owner's check: the driven check, <see cref="WantedAsync"/>, read against the quest as the run's start was planned (finding 5).
    /// </param>
    /// <param name="profile">The account the run is on, for the machine log; null for the tool's own sign-in.</param>
    internal static TransientRetry For(
        AdapterSet adapters, ISessionAdapter adapter, HarnessRoster harnesses, ServiceClient service, ISessionEvents events,
        ISessionOutput? output, SessionWatch watch, MachineLog? log, string sessionId, string questId, string? profile, TimeProvider clock,
        Func<CancellationToken, Task<bool>> wanted)
    {
        var keepGoOn = AgentCourse.Current.RetryGoOn(service, sessionId);
        return new TransientRetry
        {
            Clock = clock,
            Read = refused =>
            {
                var read = AgentErrors.Read(adapters, adapter, DoorFailure.OfPrompt(refused), clock.GetUtcNow(), harnesses.Zone);
                log?.Write("info", "agent.error", [
                    ("session", sessionId), ("adapter", adapter.Name), ("account", profile), ("kind", read.Spelled), ("by", read.By),
                ]);
                return read;
            },
            Wanted = wanted,
            KeepGoOn = keepGoOn,
            Began = (episode, next) =>
            {
                var noted = TransientRetry.Retrying(adapter.Name, episode.Words, next);
                output?.Append(sessionId, noted.Note);
                events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = noted.Note, Parts = noted.Parts }, say: null);
            },
            Tried = (episode, number, next) =>
            {
                log?.Write("info", "agent.transient", [
                    ("session", sessionId), ("adapter", adapter.Name), ("account", profile), ("by", episode.First.By), ("try", number),
                    ("next", next),
                ]);
                // AGENTERRORS1a2 (§5.1, §8): the record's quiet event, a try going, which the head says nothing for.
                events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Transient, Try = number }, say: null);
            },
            // And a wait beginning, with when it ends: the head's *Trying again at* while it runs.
            WaitsUntil = (_, until) => events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Transient, Next = until }, say: null),
            // The wait's end stamps the session heard (round 3, R2-O7): a try's quiet counts from its prompt, never from the last line.
            Waits = waiting =>
            {
                if (!waiting) watch.Heard(sessionId);
            },
        };
    }

    /// <summary>
    /// The driven check (§5.5, round 3, R2-O6, <c>TransientRetry.Wanted</c>): no try where this run's quest reached a new authoritative
    /// end, read from the quest as the ledger answers it now against the run's start: done, submitted, a new question asked of another
    /// repository, stood down, closed or deleted. An Open quest goes on, and so does one Taken by this session; a check that throws is
    /// the retry's yes.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>The run's intent, never the quest's history</b> (Codex's owed read of D210, finding 5). <c>Awaits</c> stays set once its
    /// question is answered (<c>Quests.cs</c>), so a D79 resume would read its own answered question as a wait: only a question this run
    /// asked, one other than <paramref name="awaitsBefore"/>, is one, as <see cref="Observation.Conclude"/>'s <c>Asks</c> reads it. And a
    /// quest already closed when the person's word began (<paramref name="startedOn"/>) is that word's run, which no close can have
    /// completed since: it is tried again while its quest exists.
    /// </remarks>
    /// <param name="startedOn">The quest's status as the look planned the run.</param>
    /// <param name="awaitsBefore">The question the quest named as the run started (D79), or null.</param>
    internal static async Task<bool> WantedAsync(
        ServiceClient service, string questId, string sessionId, string startedOn, string? awaitsBefore, CancellationToken ct)
    {
        var quest = await service.FindQuestAsync(questId, ct).ConfigureAwait(false);
        if (quest is null) return false;
        if (startedOn is not ("Open" or "Taken")) return true;
        if (QuestSubmissions.Current(quest)) return false;
        if (quest.Awaits is { Length: > 0 } awaits && !string.Equals(awaits, awaitsBefore, StringComparison.Ordinal)) return false;
        // A quest Taken as the run started is the run's to carry on, a D79 resume's new session included, which holds no take of its own
        // (the conclusion's `Parks` reads `resumed` so); one taken since the start is this session's only where it took it.
        return quest.Status switch
        {
            "Open" => true,
            "Taken" => startedOn == "Taken" || await service.TookAsync(sessionId, ct).ConfigureAwait(false),
            _ => false,
        };
    }

    /// <summary>
    /// The fallback at both conclusions (§5.1, R3-O5): Daoris's go-on, where its take-off did not land, comes off the record by its key, so
    /// none waits on a record the run then parked and none reaches a new part. Never faults.
    /// </summary>
    internal static Task GoOnOffAsync(ServiceClient service, string sessionId, CancellationToken ct) =>
        AgentCourse.Current.GoOnOffAsync(service, sessionId, ct);
}
