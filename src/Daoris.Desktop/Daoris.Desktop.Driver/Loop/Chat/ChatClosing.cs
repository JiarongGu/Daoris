namespace Daoris.Driver;

/// <summary>
/// How conversations end at the driver's own hand and at a person's: the shell closing, which stops every conversation and keeps Daoris's
/// restart word on each it cut mid-turn, and a person finishing one.
/// </summary>
internal sealed class ChatClosing(IExecutorWiring wiring, ChatConclusion conclusion)
{
    private ChatContext Context => wiring.Context;

    /// <summary>The conversations the shell's close cut off, whose record keeps the word that Daoris restarted.</summary>
    public System.Collections.Concurrent.ConcurrentDictionary<string, byte> Cuts { get; } = new();

    /// <summary>
    /// What this close's restart word is keyed by, set as the close begins: the update's build, or the moment any other close began, since a
    /// key the line settled answers a later say as kept for seven days and each close's word must be its own.
    /// </summary>
    public string? Restart { get; private set; }

    /// <summary>
    /// End every conversation this runner holds, as the driver's own act, and return once each record says so, or once <paramref name="bound"/>
    /// has passed, since a shutdown must not hang on one. Stopped, not finished: a harness given end-of-input finishes the sentence it is saying
    /// first, which can take minutes, and the person is closing the application now. Whoever closes it, a conversation cut mid-turn goes on
    /// by itself at the next start; one between turns waits for the person's next word.
    /// </summary>
    public void StopAll(TimeSpan bound)
    {
        // No conversation goes on from here, and one already on its way is let finish its start first, so its process is among those stopped
        // below rather than spawned after them.
        var atClose = Context.Turned.ToDictionary(pair => pair.Key, pair => pair.Value.Turns.State);
        var update = Context.Processes.Close.Build;
        Restart ??= update ?? $"closed-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}";
        Context.Close();
        wiring.Of<ChatGoOn>().Stop();
        foreach (var (id, chat) in Context.Turned)
        {
            var state = atClose.GetValueOrDefault(id) ?? chat.Turns.State;
            var cut = chat.Turns.HoldForClose();
            if (cut || state.Taking || state.Opening || state.Queued.Count > 0) Cuts.TryAdd(id, 0);
        }
        foreach (var id in Context.GoingOn.Keys) Cuts.TryAdd(id, 0);
        try
        {
            Task.WaitAll([.. Context.Opening.Values, .. Context.GoingOn.Values], bound);
        }
        catch (AggregateException)
        {
            // A start that failed has said so where it could; either way it has ended.
        }

        var watches = Context.Watching.ToArray();
        foreach (var (id, _) in watches)
        {
            if (Context.Turned.TryGetValue(id, out var chat)) chat.Turns.HoldForClose();
            Context.Processes.Stop(id, Cuts.ContainsKey(id) ? CutNoted
                : update is not null && conclusion.Spoken(id) ? Noted.Of(ChatNoteCodes.ChatRestarted,
                    "Daoris restarted for an update; this conversation goes on in its own conversation at your next word.")
                : ChatRunner.ClosedNoted);
        }

        try
        {
            Task.WaitAll([.. watches.Select(entry => entry.Value)], bound);
        }
        catch (AggregateException)
        {
            // A watch that failed has written its own record, or could not; either way it has ended.
        }
        foreach (var id in Cuts.Keys)
        {
            if (watches.FirstOrDefault(entry => entry.Key == id).Value is not { IsCompletedSuccessfully: true }) continue;
            KeepRestart(id);
        }
    }

    /// <summary>The note of a conversation the close cut mid-turn: Daoris's own stop, and it goes on by itself.</summary>
    public static Noted CutNoted => Noted.Of(ChatNoteCodes.ChatCut,
        "Daoris closed while this conversation's turn ran; it goes on by itself in its own conversation when Daoris starts again.");

    /// <summary>
    /// Words a turn left waiting on their record, said: the record did not name them marked attempted, so nothing is written for them. They
    /// wait there for the conversation's next go-on, which sends each once, and are never sent by anything else.
    /// </summary>
    public void Unsent(string session, int count)
    {
        var note = WordsNotes.Unreached(count, "were not sent", UnreachedWhy.Unmarked);
        Context.Record(session, new SessionEvent { Kind = SessionEventKind.Note, Text = note.Note, Parts = note.Parts.Count > 0 ? note.Parts : null });
    }

    /// <summary>Daoris's restart word, kept on a conversation's record at the shell's close. It is this close's, and refused it is logged unkept as any refusal is.</summary>
    public void KeepRestart(string session)
    {
        if (Restart is not { } close) return;
        try
        {
            var said = Context.Service.SayAsync(session, ShellClose.Words, from: DaorisWords.From,
                key: DaorisKeys.Restart(session, close)).GetAwaiter().GetResult();
            if (said.Kept) return;
            using var refused = new MachineLog(Context.Home, "driver");
            refused.Warn("conversation.restart-unkept", ("session", session), ("error", "Refused"));
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or OperationCanceledException or ObjectDisposedException)
        {
            // The close must not throw: a client already let go on the same shutdown keeps nothing, and is logged as any refusal is.
            using var log = new MachineLog(Context.Home, "driver");
            log.Warn("conversation.restart-unkept", ("session", session), ("error", error.GetType().Name));
        }
    }

    /// <summary>
    /// End a conversation the way a person finishing does: the harness winds up and exits on its own, which is a `completed` record. False
    /// when nothing here is holding it. On the protocol door that is the turns already asked for, then `session/close`, then the end of input,
    /// never a cut stdin under a turn still running, which the agent would read as the wire breaking rather than the person finishing.
    /// </summary>
    public bool Finish(string sessionId)
    {
        // On the native door too, the turns already asked for run first: finishing is not withdrawing. Then its run's close.
        if (Context.Turned.TryGetValue(sessionId, out var chat))
        {
            Context.Finishing[sessionId] = 0;
            _ = chat.FinishAsync(() => Context.Runs.TryGetValue(sessionId, out var turned) ? turned.CloseAsync(CancellationToken.None) : Task.CompletedTask);
            return true;
        }

        if (!Context.Runs.TryGetValue(sessionId, out var run)) return false;
        Context.Finishing[sessionId] = 0;
        _ = run.CloseAsync(CancellationToken.None);
        return true;
    }

    /// <summary>
    /// Whether the person finished the conversation and it is still winding up: a start carries it on as running no longer, since the person's
    /// next words are a new conversation's.
    /// </summary>
    public bool Finishing(string sessionId) => Context.Finishing.ContainsKey(sessionId);

    /// <summary>
    /// The conversation's run ended, or <paramref name="bound"/> passed: what a start waits on before it opens the next in the workspace a
    /// finishing one still holds. True where it ended.
    /// </summary>
    public async Task<bool> WoundUpAsync(string sessionId, TimeSpan bound, CancellationToken ct = default)
    {
        if (!Context.Watching.TryGetValue(sessionId, out var watch)) return !Context.Processes.Running.Contains(sessionId, StringComparer.OrdinalIgnoreCase);
        return await Task.WhenAny(watch, Task.Delay(bound, ct)).ConfigureAwait(false) == watch;
    }
}
