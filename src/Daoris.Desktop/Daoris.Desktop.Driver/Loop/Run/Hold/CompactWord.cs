using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// Daoris's word to a quest session whose harness compacted its context: composed from Daoris's records when the compaction is read
/// (<see cref="AcpSession.Compacted"/>), kept through the session's inbox, urgent, under <see cref="DaorisKeys.Compact"/>, once per
/// compaction. One still waiting on the record when the run ends is taken off, since the next part is handed a brief of its own; one
/// already on the wire stays, and says what Daoris recorded.
/// </summary>
public sealed class CompactWord : IDisposable
{
    private readonly string _session;
    private readonly DrivenInbox _inbox;
    private readonly ISaidDoor? _door;
    private readonly Func<CancellationToken, Task<string>> _compose;
    private readonly Action<string>? _failed;
    private readonly ConcurrentDictionary<int, string?> _told = new();
    private readonly Action? _unsubscribe;
    private int _count;
    private int _disposed;

    internal CompactWord(
        string session, DrivenInbox inbox, ISaidDoor? door, Func<CancellationToken, Task<string>> compose, Action<string>? failed,
        Action<Action>? subscribe = null, Action<Action>? unsubscribe = null)
    {
        _session = session;
        _inbox = inbox;
        _door = door;
        _compose = compose;
        _failed = failed;
        if (subscribe is not null && unsubscribe is not null)
        {
            Action handler = Compacted;
            subscribe(handler);
            _unsubscribe = () => unsubscribe(handler);
        }
    }

    /// <summary>The word's watch of a protocol session: told each time its context counter says the harness compacted.</summary>
    internal static CompactWord Tell(
        AcpSession session, string id, DrivenInbox inbox, ISaidDoor? door, Func<CancellationToken, Task<string>> compose, Action<string>? failed) =>
        new(id, inbox, door, compose, failed, handler => session.Compacted += handler, handler => session.Compacted -= handler);

    /// <summary>The compactions told so far, with the record's id of each word kept there (none where it is held in this process alone).</summary>
    public IReadOnlyDictionary<int, string?> Told => _told;

    internal void Compacted()
    {
        if (Volatile.Read(ref _disposed) == 0) _ = Guard(TellAsync());
    }

    /// <summary>Compose and keep the next compaction's word; false where the run ended or the inbox is closed.</summary>
    public async Task<bool> TellAsync(CancellationToken ct = default)
    {
        var episode = Interlocked.Increment(ref _count);
        _told[episode] = null;
        var word = new ChatMessage(await _compose(ct).ConfigureAwait(false), [])
        {
            Level = MessageLevel.Urgent,
            Daoris = new DaorisWord(DaorisKeys.Compact(_session, episode))
            {
                Stands = _ => Task.FromResult(Volatile.Read(ref _disposed) == 0 && _told.ContainsKey(episode)),
            },
        };
        var kept = await _inbox.KeepOrHoldAsync(_door, word, ct).ConfigureAwait(false);
        if (kept.Outcome is InboxKeep.Closed or InboxKeep.Continued)
        {
            _told.TryRemove(episode, out _);
            return false;
        }

        var id = kept.Said?.Word?.Id;
        // The run's end during the keep removed the entry and found no id to take off: take it off here.
        if (!_told.TryUpdate(episode, id, null))
        {
            if (id is not null && _door is not null) await _door.TakeOffAsync([id], ct).ConfigureAwait(false);
            return false;
        }

        return true;
    }

    /// <summary>The run's end: every word still waiting is taken off before its conclusion is written, and the watch is let go.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _unsubscribe?.Invoke();
        foreach (var episode in _told.Keys.ToList())
        {
            if (!_told.TryRemove(episode, out var id) || id is null || _door is null) continue;
            try
            {
                _door.TakeOffAsync([id], CancellationToken.None).Wait(TimeSpan.FromSeconds(5));
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
        catch (Exception error) when (error is HttpRequestException or DriverException or IOException or OperationCanceledException or ObjectDisposedException
                                          or System.Text.Json.JsonException or InvalidOperationException)
        {
            // A word that cannot be composed, kept or taken off is said, never the run's failure.
            _failed?.Invoke(error.Message);
        }
    }
}
