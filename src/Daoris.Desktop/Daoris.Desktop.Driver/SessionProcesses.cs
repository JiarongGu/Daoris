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

        /// <summary>Why the driver ended it, when the driver did — null when the person did.</summary>
        public string? Reason;
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
    /// End a session's process, marking the end as the person's — or, with a <paramref name="reason"/>,
    /// as the driver's own, for that reason. True when there was one to stop; false is an answer too —
    /// the session already finished, and its record says how.
    /// </summary>
    public bool Stop(string sessionId, string? reason = null)
    {
        Entry? entry;
        lock (_gate)
        {
            if (!_running.TryGetValue(sessionId, out entry)) return false;
            entry.StopRequested = true;
            entry.Reason ??= reason;
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

    /// <summary>Why the driver ended this session itself — null when nobody did, or the person did.</summary>
    public string? StopReason(string sessionId)
    {
        lock (_gate) return _running.TryGetValue(sessionId, out var entry) ? entry.Reason : null;
    }

    /// <summary>
    /// Send a person's message to a chat session (D49 §3) — one line into the harness's stdin.
    /// </summary>
    /// <remarks>
    /// False for a session that has ended, and for a DRIVEN one: a driven session's process is spawned
    /// without an input stream at all, because it was given its whole target at once and has nobody to
    /// take turns with. That is a structural answer, not a policy — there is no stream to write to.
    /// </remarks>
    public bool Send(string sessionId, string message)
    {
        Entry? entry;
        lock (_gate)
        {
            if (!_running.TryGetValue(sessionId, out entry)) return false;
        }

        try
        {
            var input = entry.Process.StandardInput;
            lock (entry)
            {
                input.WriteLine(message);
                input.Flush();
            }

            return true;
        }
        catch (Exception error) when (error is InvalidOperationException or IOException or ObjectDisposedException)
        {
            // Exited between the lookup and the write, or spawned with no input stream. Either way the
            // message went nowhere, and the caller is told rather than left to assume it landed.
            return false;
        }
    }

    /// <summary>
    /// Close a chat's input — the person has finished talking, and the harness should wind up.
    /// </summary>
    /// <remarks>
    /// Ending the conversation rather than killing it: a harness given end-of-input finishes what it
    /// was saying and exits on its own, which is a `completed` record. `Stop` is the other verb, and
    /// it means something different — the person cut it off.
    /// </remarks>
    public bool CloseInput(string sessionId)
    {
        Entry? entry;
        lock (_gate)
        {
            if (!_running.TryGetValue(sessionId, out entry)) return false;
        }

        try
        {
            entry.Process.StandardInput.Close();
            return true;
        }
        catch (Exception error) when (error is InvalidOperationException or IOException or ObjectDisposedException)
        {
            return false;
        }
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
