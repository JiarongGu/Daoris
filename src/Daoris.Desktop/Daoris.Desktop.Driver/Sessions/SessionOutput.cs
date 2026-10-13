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

/// <summary>The kinds a <see cref="SessionStream"/> can be (CONSOLE2).</summary>
public static class SessionStreamKind
{
    /// <summary>A sub-session the harness spawned, streaming as a session of its own.</summary>
    public const string Subagent = "subagent";

    /// <summary>Background work the session started, such as a dev server, whose output is a file.</summary>
    public const string Task = "task";
}

/// <summary>
/// Something a session is running beside itself (CONSOLE2): a subagent, or background work.
/// </summary>
/// <param name="Id">Unique within its session: <c>subagent/&lt;id&gt;</c> or <c>task/&lt;id&gt;</c>, the wire's id.</param>
/// <param name="Kind">A <see cref="SessionStreamKind"/>.</param>
/// <param name="Name">What the harness called it.</param>
/// <param name="CanStop">
/// Whether its harness said it can be stopped (CONSOLE3a) — a task's <c>canStop</c>, in the wire's word.
/// </param>
public sealed record SessionStream(string Id, string Kind, string Name, bool CanStop = false)
{
    /// <summary>
    /// How a stream ended when its session ended first: the harness never said, and nothing it
    /// started outlives the session (ORPHAN1's job object).
    /// </summary>
    public const string SessionEnded = "session-ended";
}

/// <summary>
/// One session's streams, kept under that session (CONSOLE2) — what a door hands the wire so a
/// subagent's lines and a task's output land beside the session rather than in it.
/// </summary>
public sealed class SessionStreams(SessionOutput output, string sessionId)
{
    public void Open(SessionStream stream) => output.Open(sessionId, stream);

    public void Line(string streamId, string text) => output.Append(SessionOutput.Key(sessionId, streamId), text);

    public void End(string streamId, string? state) => output.End(sessionId, streamId, state);
}

/// <summary>One stream under a session, as a reader lists them.</summary>
/// <param name="Key">What to tail it by — <see cref="SessionOutput.Key"/>.</param>
/// <param name="State">How it ended, in the wire's word, or null while it runs.</param>
/// <param name="CanStop">Whether a person can stop it now (CONSOLE3a): its harness said so, and it runs.</param>
public sealed record StreamState(string Key, string Kind, string Name, bool Live, string? State, bool CanStop = false);

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
///
/// <para><b>A session's streams are buffers of their own</b> (CONSOLE2): each subagent and each
/// background task it runs, under <see cref="Key"/>, so any reader tails one exactly as it tails a
/// session. They ride with their session: they are not counted as sessions retained, they leave
/// when it is evicted, and its end is theirs.</para>
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

        /// <summary>The session a stream belongs to, and what it is — null for a session's own buffer.</summary>
        public string? Parent;
        public SessionStream? Stream;
        public string? State;

        /// <summary>When a stream was opened, which is the order a reader lists them in.</summary>
        public long Opened;
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Buffer> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private long _clock;

    /// <summary>What a session's stream is tailed by: the session's id, then the stream's own.</summary>
    public static string Key(string sessionId, string streamId) => $"{sessionId}/{streamId}";

    /// <summary>
    /// Begin a stream under a session. Opening one already open changes nothing, so an announcement
    /// the wire repeats does not reset what was said.
    /// </summary>
    public void Open(string sessionId, SessionStream stream)
    {
        lock (_gate)
        {
            var key = Key(sessionId, stream.Id);
            if (_sessions.ContainsKey(key)) return;
            Ensure(sessionId);
            _clock += 1;
            _sessions[key] = new Buffer { Parent = sessionId, Stream = stream, Touched = _clock, Opened = _clock };
        }

        Heard(sessionId);
    }

    /// <summary>
    /// Raised with a session's id when one of its streams opens or ends (CONSOLE2c) — how a page's
    /// console grows a tab while the session runs. Guarded as <see cref="Lined"/> is.
    /// </summary>
    public event Action<string>? Streamed;

    private void Heard(string sessionId)
    {
        try
        {
            Streamed?.Invoke(sessionId);
        }
        catch (Exception)
        {
            // A subscriber's failure is its own, as a line's is.
        }
    }

    /// <summary>
    /// End a stream, in the wire's word. The last ending is the one kept: a task that finished on its
    /// own is said <c>stopped</c> and then <c>completed</c> (CONSOLE2a).
    /// </summary>
    public void End(string sessionId, string streamId, string? state)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(Key(sessionId, streamId), out var buffer) || buffer.Parent is null) return;
            buffer.Live = false;
            buffer.State = state ?? buffer.State;
        }

        Heard(sessionId);
    }

    /// <summary>The streams a session has opened, oldest first.</summary>
    public IReadOnlyList<StreamState> Streams(string sessionId)
    {
        lock (_gate)
        {
            return [.. _sessions
                .Where(entry => entry.Value.Parent is { } parent && parent.Equals(sessionId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.Value.Opened)
                .Select(entry => new StreamState(
                    entry.Key, entry.Value.Stream!.Kind, entry.Value.Stream.Name, entry.Value.Live, entry.Value.State,
                    entry.Value.Live && entry.Value.Stream.CanStop))];
        }
    }

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
            // A session that speaks again is live again. A stream that ended stays ended: a line its
            // file still held is the last of it, never a second life.
            if (buffer.Parent is null) buffer.Live = true;
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
    /// <summary>
    /// Whether a session's console, or a stream's, still runs — what a batch of its lines says beside
    /// them, so a line written after it closed never reads as it running again.
    /// </summary>
    public bool IsLive(string sessionId)
    {
        lock (_gate) return _sessions.TryGetValue(sessionId, out var buffer) && buffer.Live;
    }

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
    /// <remarks>Its streams end with it: nothing a session started is still talking once it has ended.</remarks>
    public void Close(string sessionId)
    {
        var ended = false;
        lock (_gate)
        {
            if (_sessions.TryGetValue(sessionId, out var buffer)) buffer.Live = false;
            foreach (var stream in StreamsOf(sessionId).Where(stream => stream.Live))
            {
                stream.Live = false;
                ended = true;
            }
        }

        if (ended) Heard(sessionId);
    }

    /// <summary>Sessions with a buffer here — what "there is a console to show" means. Their streams are not listed.</summary>
    public IReadOnlyList<string> Buffered
    {
        get
        {
            lock (_gate) return [.. _sessions.Where(entry => entry.Value.Parent is null).Select(entry => entry.Key)];
        }
    }

    /// <summary>A session's own buffer, made when a stream arrives before the session has said anything.</summary>
    private void Ensure(string sessionId)
    {
        if (_sessions.ContainsKey(sessionId)) return;
        _sessions[sessionId] = new Buffer { Touched = ++_clock };
        Evict();
    }

    private IEnumerable<Buffer> StreamsOf(string sessionId) => _sessions.Values
        .Where(buffer => buffer.Parent is { } parent && parent.Equals(sessionId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Make room, never at a live session's expense: the oldest ENDED buffer goes first. With every
    /// retained session still live nothing is evicted — a machine running sixteen at once has a
    /// different problem, and dropping the console of a session someone is watching would be the
    /// wrong answer to it. Only sessions count, and a session's streams leave with it.
    /// </summary>
    private void Evict()
    {
        while (_sessions.Count(entry => entry.Value.Parent is null) > SessionsRetained)
        {
            var oldest = _sessions
                .Where(entry => entry.Value.Parent is null && !entry.Value.Live)
                .OrderBy(entry => entry.Value.Touched)
                .Select(entry => entry.Key)
                .FirstOrDefault();

            if (oldest is null) return;
            _sessions.Remove(oldest);
            foreach (var key in _sessions.Where(entry => entry.Value.Parent is { } parent
                         && parent.Equals(oldest, StringComparison.OrdinalIgnoreCase)).Select(entry => entry.Key).ToList())
            {
                _sessions.Remove(key);
            }
        }
    }
}
