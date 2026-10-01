using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What the person runs, and how long each part of it takes, into the machine log (LOG1b, D94): the
/// session events of the design's catalogue, from what the service client and the conversation record
/// already say.
/// </summary>
/// <remarks>
/// <para><b>Two sources, no new door.</b> Every session on this machine is opened and moved through the
/// <see cref="ServiceClient"/>, which says so once the ledger said yes; every conversation event lands in
/// <see cref="SessionEvents"/>, stamped as it is written. This class only listens to both.</para>
///
/// <para><b>The lines</b> (<c>docs/2026-09-30-machine-log-design.md</c> §4):
/// <c>session.started</c> {session, kind, adapter, repository} as a record opens;
/// <c>session.opened</c> {session, adapter, openMs}, the open to the session's first prompt in its record;
/// <c>turn.answered</c> {session, firstAnswerMs}, a prompt to the first thing back (a message, a thought
/// or a tool call); <c>turn.ended</c> {session, stopReason, turnMs}, a prompt to its turn's end;
/// <c>session.ended</c> {session, state, seconds} as the record reaches a closed state; and
/// <c>permission.refused</c> {session, adapter, tool, kind, by} for each call the record says was refused
/// (UNBLOCK5, D122 §3.10).</para>
///
/// <para><b>An ask is a refused call, once.</b> Nothing Daoris starts has a person at the prompt, so every
/// permission a harness would have asked for is refused (D52), and the record marks the call
/// <c>refused</c>: the protocol door when the agent reports a call it refused as failed (HELP4), the native
/// door from the harness's own <c>permission_denied</c> report. A call is counted the first time, however
/// many updates repeat it. <c>kind</c> is ACP's tool kind, from the refusal or else from the call's first
/// event; <c>tool</c> and <c>by</c> are the wire's own identifiers where it gave them (only the native door
/// does), and null where it did not.</para>
///
/// <para><b>Never anyone's words</b>: an event's text, a tool's input, a note the driver wrote on a move
/// are read for their kind and their stamp, and never written. A name that is not an identifier is written
/// as null, so a wire that put a command or a sentence where a name goes cannot pass it through. What
/// cannot be timed (an open this process never saw, a turn's end with no prompt before it) is written as
/// null, never as zero.</para>
/// </remarks>
public sealed class SessionLog : IDisposable
{
    /// <summary>The ledger's closed states, in their public spelling: a record in one has ended.</summary>
    public static readonly IReadOnlySet<string> Closed =
        new HashSet<string>(["completed", "declined", "stood-down", "failed", "stopped"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What a name in a line may be (D94 §4): an identifier such as <c>Bash</c>,
    /// <c>mcp__server__tool</c>, <c>execute</c> or <c>asyncAgent</c>, never a phrase.
    /// </summary>
    private static readonly Regex Identifier = new("^[A-Za-z][A-Za-z0-9_.:-]{0,119}$", RegexOptions.CultureInvariant);

    private readonly MachineLog _log;
    private readonly ServiceClient _service;
    private readonly SessionEvents _events;
    private readonly Func<DateTimeOffset> _clock;
    private readonly object _gate = new();
    private readonly Dictionary<string, Watched> _watched = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <param name="clock">What time it is; the system's by default. An event is timed by its own stamp.</param>
    public SessionLog(MachineLog log, ServiceClient service, SessionEvents events, Func<DateTimeOffset>? clock = null)
    {
        _log = log;
        _service = service;
        _events = events;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        service.Opened += OnOpened;
        service.Moved += OnMoved;
        events.Evented += OnEvented;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _watched.Clear();
        }

        _service.Opened -= OnOpened;
        _service.Moved -= OnMoved;
        _events.Evented -= OnEvented;
    }

    /// <summary>What is known about one session: when it opened, on what, and where its turn stands.</summary>
    private sealed class Watched
    {
        public DateTimeOffset? Opened { get; init; }

        public string? Adapter { get; init; }

        /// <summary>Whether its first prompt has been seen — the open's wait ends there, once.</summary>
        public bool Asked { get; set; }

        /// <summary>When the turn in flight began, or null between turns.</summary>
        public DateTimeOffset? TurnFrom { get; set; }

        /// <summary>Whether the turn in flight has had its first answer.</summary>
        public bool Answered { get; set; }

        /// <summary>Each call's kind as it was announced: a refusal that arrives as a bare update names none.</summary>
        public Dictionary<string, string> Kinds { get; } = new(StringComparer.Ordinal);

        /// <summary>The calls already counted as refused, so a repeated update is not a second ask.</summary>
        public HashSet<string> Refused { get; } = new(StringComparer.Ordinal);
    }

    private void OnOpened(SessionOpened opened)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _watched[opened.Session] = new Watched { Opened = _clock(), Adapter = opened.Adapter };
        }

        _log.Info("session.started",
            ("session", opened.Session), ("kind", opened.Kind), ("adapter", opened.Adapter), ("repository", opened.Repository));
    }

    private void OnMoved(SessionMoved moved)
    {
        if (!Closed.Contains(moved.State)) return;

        Watched? known;
        lock (_gate)
        {
            if (_disposed) return;
            _watched.Remove(moved.Session, out known);
        }

        long? seconds = known?.Opened is { } opened ? (long)Math.Max(0, (_clock() - opened).TotalSeconds) : null;
        _log.Info("session.ended", ("session", moved.Session), ("state", moved.State), ("seconds", seconds));
    }

    private void OnEvented(string session, SessionEvent e)
    {
        var at = e.At == default ? _clock() : e.At;
        List<(string Event, (string Key, object? Value)[] Data)> lines = [];
        lock (_gate)
        {
            if (_disposed) return;
            if (!_watched.TryGetValue(session, out var watched))
            {
                // A session this process never saw open still has turns worth timing; only its open is unknown.
                watched = new Watched();
                _watched[session] = watched;
            }

            switch (e.Kind)
            {
                case SessionEventKind.User:
                    if (!watched.Asked && watched.Opened is { } opened)
                    {
                        lines.Add(("session.opened", [("session", session), ("adapter", watched.Adapter), ("openMs", Ms(at - opened))]));
                    }

                    watched.Asked = true;
                    // A turn already in flight keeps its start: a prompt the driver sends as two lines of
                    // the record (its target, then the person's answer) is one turn.
                    if (watched.TurnFrom is null)
                    {
                        watched.TurnFrom = at;
                        watched.Answered = false;
                    }

                    break;

                case SessionEventKind.Message or SessionEventKind.Thought or SessionEventKind.Tool:
                    if (watched.TurnFrom is { } asked && !watched.Answered)
                    {
                        watched.Answered = true;
                        lines.Add(("turn.answered", [("session", session), ("firstAnswerMs", Ms(at - asked))]));
                    }

                    if (e.Kind == SessionEventKind.Tool && Refusal(session, watched, e) is { } refusal) lines.Add(refusal);
                    break;

                case SessionEventKind.Turn:
                    long? turnMs = watched.TurnFrom is { } began ? Ms(at - began) : null;
                    lines.Add(("turn.ended", [("session", session), ("stopReason", e.StopReason), ("turnMs", turnMs)]));
                    watched.TurnFrom = null;
                    watched.Answered = false;
                    break;
            }
        }

        // Written outside the gate: the log has its own, and a slow disk should not hold the next event.
        foreach (var (name, data) in lines) _log.Write("info", name, data);
    }

    /// <summary>
    /// A tool event read for an ask (UNBLOCK5): its kind remembered, and the line for a call refused for the
    /// first time. Called under the gate.
    /// </summary>
    private static (string Event, (string Key, object? Value)[] Data)? Refusal(string session, Watched watched, SessionEvent e)
    {
        if (e.Id is { } call && e.ToolKind is { Length: > 0 } announced) watched.Kinds.TryAdd(call, announced);
        if (e.Status != "refused") return null;
        // A refusal with no id cannot be told from the next one, so each is its own ask.
        if (e.Id is { } id && !watched.Refused.Add(id)) return null;

        var kind = e.ToolKind ?? (e.Id is { } known && watched.Kinds.TryGetValue(known, out var first) ? first : null);
        return ("permission.refused",
        [
            ("session", session), ("adapter", watched.Adapter), ("tool", Name(e.ToolName)), ("kind", Name(kind)),
            ("by", Name(e.RefusedBy)),
        ]);
    }

    /// <summary>An identifier as written, or null for anything that is not one (D94 §5).</summary>
    private static string? Name(string? value) => value is not null && Identifier.IsMatch(value) ? value : null;

    /// <summary>Whole milliseconds, to the nearest: a stamp's arithmetic can land a tick short of one.</summary>
    private static long Ms(TimeSpan span) => (long)Math.Round(Math.Max(0, span.TotalMilliseconds));
}
