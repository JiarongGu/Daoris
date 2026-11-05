using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// Daoris's one word to a quest session the watcher marked quiet (SESSWATCH1b, D200; the session-watch design §2.1): kept on the record's
/// <c>said</c> through the session's inbox, urgent, under <see cref="DaorisKeys.Watch"/>, once per quiet episode, and taken off again when
/// the episode ends or the run ends, so a word not yet on the wire never outlives the quiet it speaks of and never becomes a carry-on's
/// resume. A word already on the wire cannot be called back: its words say only what ran, true whenever they are read. An intake takes
/// no words, so it gets the mark alone; where <see cref="LongCalls"/> already told the same call, its word is the episode's.
/// Every door that takes words has one: the protocol door's reaches the agent at its next step, and the native and the text-only doors'
/// waits for the turn's end, where it is judged by its episode and goes to no turn once the session was heard again.
/// </summary>
public sealed class QuietWord : IDisposable
{
    private readonly SessionWatch _watch;
    private readonly string _session;
    private readonly DrivenInbox _inbox;
    private readonly ISaidDoor? _door;
    private readonly RunningCalls? _calls;
    private readonly Action<string>? _failed;
    private readonly ConcurrentDictionary<int, string?> _told = new();
    private int _disposed;

    private QuietWord(SessionWatch watch, string session, DrivenInbox inbox, ISaidDoor? door, RunningCalls? calls, Action<string>? failed)
    {
        _watch = watch;
        _session = session;
        _inbox = inbox;
        _door = door;
        _calls = calls;
        _failed = failed;
        _watch.Stepped += OnStep;
    }

    /// <summary>The word's watch for a run whose door takes words; null for a door with no inbox. Only a quest session's mark is told.</summary>
    public static QuietWord? Tell(
        SessionWatch watch, string session, DrivenInbox? inbox, ISaidDoor? door, RunningCalls? calls = null, Action<string>? failed = null) =>
        inbox is null ? null : new QuietWord(watch, session, inbox, door, calls, failed);

    /// <summary>The episodes told so far, with the record's id of each word kept there (none where it was held in this process alone).</summary>
    public IReadOnlyDictionary<int, string?> Told => _told;

    /// <summary>The words (§2.1): how long nothing came and what it was inside, what to do, and that Daoris stopped nothing.</summary>
    public static string Words(QuietStep step)
    {
        var inside = step.Call is { Length: > 0 } call ? $", while your call `{call}` ran"
            : step.Works.Count > 0 ? $", while your background work {string.Join(", ", step.Works.Select(work => $"`{work}`"))} showed no progress"
            : "";
        return $"Daoris: nothing came from this session for {step.Minutes} minutes{inside}. If a command waits on input, a lock or the network, "
               + "stop it and go another way; run anything long in the background and read its output; if something only the person can give "
               + "blocks you, ask with `question_ask`. Daoris has stopped nothing.";
    }

    private void OnStep(QuietStep step)
    {
        if (!string.Equals(step.Session, _session, StringComparison.OrdinalIgnoreCase) || Volatile.Read(ref _disposed) != 0) return;
        if (step is { Step: SessionWatch.Marked, Kind: SessionWatch.Quest }) _ = Guard(TellAsync(step));
        else if (step.Step == "over") _ = Guard(WithdrawAsync(step.Episode));
    }

    /// <summary>Keep the episode's word, once; a call <see cref="LongCalls"/> told already is the episode's word, and none is kept.</summary>
    public async Task<bool> TellAsync(QuietStep step, CancellationToken ct = default)
    {
        if (_calls?.Calling(_session) is { Told: true }) return false;
        if (!_told.TryAdd(step.Episode, null)) return false;
        var word = new ChatMessage(Words(step), [])
        {
            Level = MessageLevel.Urgent,
            // Judged where it leaves for the wire: a word whose episode ended or whose run ended goes to no turn, on any door.
            Daoris = new DaorisWord(DaorisKeys.Watch(_session, step.Episode))
            {
                Stands = _ => Task.FromResult(Volatile.Read(ref _disposed) == 0 && _told.ContainsKey(step.Episode)),
            },
        };
        var kept = await _inbox.KeepOrHoldAsync(_door, word, ct).ConfigureAwait(false);
        if (kept.Outcome is InboxKeep.Closed or InboxKeep.Continued)
        {
            _told.TryRemove(step.Episode, out _);
            return false;
        }

        _told[step.Episode] = kept.Said?.Word?.Id;
        return true;
    }

    /// <summary>Take the episode's word off the record where it is still waiting there; one on the wire stays, and says only what ran.</summary>
    public async Task WithdrawAsync(int episode, CancellationToken ct = default)
    {
        if (!_told.TryRemove(episode, out var id) || id is null || _door is null) return;
        await _door.TakeOffAsync([id], ct).ConfigureAwait(false);
    }

    /// <summary>The run's end: every word still waiting is taken off before its conclusion is written, and the watch is let go.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _watch.Stepped -= OnStep;
        foreach (var episode in _told.Keys.ToList())
        {
            try
            {
                WithdrawAsync(episode).Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException error)
            {
                _failed?.Invoke(error.InnerException?.Message ?? error.Message);
            }
        }
    }

    private async Task Guard(Task work)
    {
        try
        {
            await work.ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or IOException or OperationCanceledException or ObjectDisposedException)
        {
            // A word that cannot be kept or taken off is said, never the run's failure: the mark and the row stand.
            _failed?.Invoke(error.Message);
        }
    }
}
