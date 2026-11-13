using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// Which of Ask Daoris's conversations are between their record and their agent: placed, their start not over. It holds no word:
/// a word said to one meanwhile is kept on its record, which survives this process, and is handed to its door once the door opens.
/// </summary>
public sealed class HelpStarting
{
    private readonly object _gate = new();
    private readonly HashSet<string> _starting = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A conversation is placed: it is starting until its start is <see cref="Over"/>.</summary>
    public void Placed(string session)
    {
        lock (_gate) _starting.Add(session);
    }

    /// <summary>Whether the conversation is starting.</summary>
    public bool Is(string session)
    {
        lock (_gate) return _starting.Contains(session);
    }

    /// <summary>The queue of a conversation starting, <c>opening</c>, with nothing of its own listed: its words are its record's. Null where it is not starting.</summary>
    public ChatQueue? Queue(string session) => Is(session) ? new ChatQueue(false, [], Opening: true) : null;

    /// <summary>
    /// Held while a word is kept on a starting conversation's record, so its start cannot end between the keep and the door's reading
    /// of what waits; null where the conversation is not starting, and the word goes the way any conversation's goes.
    /// </summary>
    public async Task<IDisposable?> KeepingAsync(string session, CancellationToken ct)
    {
        await _keeping.WaitAsync(ct).ConfigureAwait(false);
        if (Is(session)) return new Held(_keeping);
        _keeping.Release();
        return null;
    }

    /// <summary>The start is over, whichever way it came out, once any word being kept is on the record.</summary>
    public async Task OverAsync(string session)
    {
        await _keeping.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_gate) _starting.Remove(session);
        }
        finally
        {
            _keeping.Release();
        }
    }

    private readonly SemaphoreSlim _keeping = new(1, 1);

    private sealed class Held(SemaphoreSlim gate) : IDisposable
    {
        public void Dispose() => gate.Release();
    }
}
