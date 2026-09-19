using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The live session processes, by session id — the one piece of session state that never reaches the
/// service (D46 §4): the record travels, the process handle stays with whoever spawned it. This
/// registry is what makes "stop that session" reachable from a control surface, across the per-tick
/// driver instances that actually own the spawning.
/// </summary>
public sealed class SessionProcesses
{
    private sealed class Entry
    {
        public required Process Process;
        public bool StopRequested;
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _running = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Session ids with a live process, for whoever renders "what is running right now".</summary>
    public IReadOnlyList<string> Running
    {
        get
        {
            lock (_gate) return [.. _running.Keys];
        }
    }

    /// <summary>
    /// End a session's process, marking the end as the person's. True when there was one to stop;
    /// false is an answer too — the session already finished, and its record says how.
    /// </summary>
    public bool Stop(string sessionId)
    {
        Entry? entry;
        lock (_gate)
        {
            if (!_running.TryGetValue(sessionId, out entry)) return false;
            entry.StopRequested = true;
        }

        try
        {
            entry.Process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Exited between the lookup and the kill — the flag still marks whose decision the end was.
        }

        return true;
    }

    /// <summary>Whether this session's end was asked for — read by the driver before it concludes.</summary>
    public bool WasStopRequested(string sessionId)
    {
        lock (_gate) return _running.TryGetValue(sessionId, out var entry) && entry.StopRequested;
    }

    /// <summary>Register a live process. Dispose the handle when the session concludes.</summary>
    public IDisposable Track(string sessionId, Process process)
    {
        lock (_gate) _running[sessionId] = new Entry { Process = process };
        return new Untrack(this, sessionId);
    }

    private sealed class Untrack(SessionProcesses owner, string sessionId) : IDisposable
    {
        public void Dispose()
        {
            lock (owner._gate) owner._running.Remove(sessionId);
        }
    }
}
