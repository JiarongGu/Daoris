namespace Daoris.Driver;

/// <summary>One line a session said, and where it sits in that session's stream.</summary>
/// <param name="Sequence">
/// Monotonic within a session, starting at 1. It is how a reader asks for "what I have not seen"
/// without the buffer having to remember who is reading — and how a subscriber that joined mid-stream
/// discovers it missed something.
/// </param>
public sealed record ConsoleLine(long Sequence, string Text);

/// <param name="Lines">The lines newer than what was asked for, oldest first.</param>
/// <param name="Sequence">The newest sequence this session has produced — what to ask after next.</param>
/// <param name="Live">Whether more is coming. False once the session's process has ended.</param>
/// <param name="Dropped">
/// How many lines fell out of the ring before this read — stated rather than implied, because a
/// console that silently skipped output would be a worse lie than one that said nothing at all.
/// </param>
public sealed record ConsoleTail(
    IReadOnlyList<ConsoleLine> Lines, long Sequence, bool Live, long Dropped);

/// <summary>
/// What a session is saying, as it says it (D49 §2) — the transcript capture, teed into memory.
/// </summary>
/// <remarks>
/// <para><b>The file stays the record; this is the window.</b> The transcript on disk is unbounded and
/// durable (D46 §4); this holds the last few hundred lines per session so a person can watch one
/// without the shell reading a growing file. Losing it costs nothing — the file is still there.</para>
///
/// <para><b>It never reaches the service.</b> Output is transcript-class material: it can carry
/// machine paths, and a transcript never leaves the machine that produced it (D47 §4). So this lives
/// beside <see cref="SessionProcesses"/>, in the driver, read only over the shell's own bridge — there
/// is no HTTP surface onto it, which is a structural guarantee rather than a policed one.</para>
///
/// <para><b>Bounded twice</b>: lines per session, and sessions retained. A live session is never
/// evicted; an ended one is kept only long enough that a person who was watching can still read how
/// it finished.</para>
/// </remarks>
public sealed class SessionOutput
{
    /// <summary>Lines kept per session. Enough to read a screen's worth of history, not a log file.</summary>
    public const int LinesPerSession = 500;

    /// <summary>Sessions whose buffers are kept. Live ones always; ended ones while there is room.</summary>
    public const int SessionsRetained = 16;

    private sealed class Buffer
    {
        public readonly Queue<ConsoleLine> Lines = new();
        public long Sequence;
        public long Dropped;
        public bool Live = true;
        public long Touched;
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Buffer> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private long _clock;

    /// <summary>
    /// Raised for every line, as it arrives. The shell turns this into its IPC event; nothing else
    /// subscribes, and a handler that throws would take the capture pump down with it — so the raise
    /// is guarded and a broken subscriber costs its own lines, never the transcript.
    /// </summary>
    public event Action<string, ConsoleLine>? Lined;

    /// <summary>Record a line. Called from the capture pump, which owns no lock of its own.</summary>
    public void Append(string sessionId, string text)
    {
        ConsoleLine line;
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionId, out var buffer))
            {
                buffer = new Buffer();
                _sessions[sessionId] = buffer;
                Evict();
            }

            buffer.Sequence += 1;
            buffer.Touched = ++_clock;
            buffer.Live = true;
            line = new ConsoleLine(buffer.Sequence, text);
            buffer.Lines.Enqueue(line);
            while (buffer.Lines.Count > LinesPerSession)
            {
                buffer.Lines.Dequeue();
                buffer.Dropped += 1;
            }
        }

        try
        {
            Lined?.Invoke(sessionId, line);
        }
        catch (Exception)
        {
            // A subscriber's failure is its own. The line is already in the buffer and the transcript,
            // and a console that could kill a session's capture would be worse than no console.
        }
    }

    /// <summary>
    /// What this session has said since <paramref name="after"/> — 0 for everything held.
    /// </summary>
    /// <remarks>
    /// A session nobody has buffered answers an empty, not-live tail rather than nothing at all: "this
    /// session produced no console here" is a real answer for a record fed from another machine, and a
    /// caller should not have to tell that apart from a failure.
    /// </remarks>
    public ConsoleTail Tail(string sessionId, long after = 0)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionId, out var buffer))
            {
                return new([], 0, Live: false, Dropped: 0);
            }

            var lines = buffer.Lines.Where(line => line.Sequence > after).ToList();
            return new(lines, buffer.Sequence, buffer.Live, buffer.Dropped);
        }
    }

    /// <summary>Mark a session's stream ended. Its buffer stays readable until room is needed.</summary>
    public void Close(string sessionId)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(sessionId, out var buffer)) buffer.Live = false;
        }
    }

    /// <summary>Sessions with a buffer here — what "there is a console to show" means.</summary>
    public IReadOnlyList<string> Buffered
    {
        get
        {
            lock (_gate) return [.. _sessions.Keys];
        }
    }

    /// <summary>
    /// Make room, never at a live session's expense: the oldest ENDED buffer goes first. With every
    /// retained session still live nothing is evicted — a machine running sixteen at once has a
    /// different problem, and dropping the console of a session someone is watching would be the
    /// wrong answer to it.
    /// </summary>
    private void Evict()
    {
        while (_sessions.Count > SessionsRetained)
        {
            var oldest = _sessions
                .Where(entry => !entry.Value.Live)
                .OrderBy(entry => entry.Value.Touched)
                .Select(entry => entry.Key)
                .FirstOrDefault();

            if (oldest is null) return;
            _sessions.Remove(oldest);
        }
    }
}
