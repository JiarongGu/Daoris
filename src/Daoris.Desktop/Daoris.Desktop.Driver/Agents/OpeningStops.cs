using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// The conversations being opened here whose process is not started yet, each with how the person's stop ends its opening: a stop of one
/// with no process to end reaches it here (<see cref="SessionProcesses.Stop"/>).
/// </summary>
public sealed class OpeningStops
{
    private readonly ConcurrentDictionary<string, Action> _openings = new(StringComparer.Ordinal);

    /// <summary>Hold <paramref name="sessionId"/>'s opening, ended by <paramref name="stop"/>, until the answer is disposed.</summary>
    public IDisposable Hold(string sessionId, Action stop)
    {
        _openings[sessionId] = stop;
        return new Release(_openings, new KeyValuePair<string, Action>(sessionId, stop));
    }

    /// <summary>End <paramref name="sessionId"/>'s opening as the person's stop; false where none is being opened here.</summary>
    public bool Stop(string sessionId)
    {
        if (!_openings.TryGetValue(sessionId, out var stop)) return false;
        stop();
        return true;
    }

    private sealed class Release(ConcurrentDictionary<string, Action> openings, KeyValuePair<string, Action> held) : IDisposable
    {
        public void Dispose() => openings.TryRemove(held);
    }
}
