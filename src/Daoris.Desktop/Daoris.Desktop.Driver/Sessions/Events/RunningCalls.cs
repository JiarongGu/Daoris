using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>The call a live session's agent is inside (WAITSAY1b): its title, and when this process first heard of it.</summary>
/// <param name="Title">The newest title the call's events gave it, as the record keeps it.</param>
/// <param name="Since">The stamp of the first event of the call this process heard, by the record's clock.</param>
public sealed record RunningCall(string Title, DateTimeOffset Since);

/// <summary>
/// The call each live session's agent is inside, heard from its record's events as they are written (WAITSAY1b, D152's UX7c
/// amendment): what the rail says as *running `npm test` for 8 min* of a session whose events it does not hold, a call begun
/// before the window opened among them.
/// </summary>
/// <remarks>
/// <para><b>A live fact, held in memory alone.</b> Nothing is written: the record already holds every event this reads, and a
/// reader of it after a restart reads the calls from the events themselves (the page's <c>runningCall</c>). A second reader
/// of the same record is told nothing here (<see cref="Of"/>), since only the one this process appends to hears its events.</para>
///
/// <para><b>Open until an end.</b> A call's event with no status, <c>pending</c> or <c>in_progress</c> begins it or keeps it
/// running; any other status ends it: <c>completed</c>, <c>failed</c>, a steer's <c>cut</c> (STEER2b) and a refusal
/// (<c>refused</c>, D52), which waits on nobody. A turn's end ends every call it left open, and so does the ask a session
/// takes, which begins the next turn: the page reads the last turn's calls alone, and a process that died mid-call left one
/// open with no end.</para>
///
/// <para><b>The newest open call with a title is the one running</b>, as the page's <c>runningCall</c> reads the last tool
/// block: calls run side by side, and when the newest ends the one still open beside it is running. A call this process heard
/// only updates of is named once an update names it, and runs from the first it heard.</para>
///
/// <para>Bounded: each record's watch holds the calls of at most <see cref="Remembered"/> sessions, the oldest call's session
/// dropped past it.</para>
/// </remarks>
public sealed class RunningCalls
{
    /// <summary>How many sessions' open calls one record's watch holds.</summary>
    private const int Remembered = 1024;

    private static readonly ConditionalWeakTable<SessionEvents, RunningCalls> Watches = new();

    private readonly object _gate = new();

    // Each session's open calls, in the order they began.
    private readonly Dictionary<string, List<Open>> _open = new(StringComparer.OrdinalIgnoreCase);

    private RunningCalls(SessionEvents events) => events.Evented += Heard;

    /// <summary>Watch a record for its calls, once whoever asks: the record handed back, as <see cref="UsageTurns.Watched"/> does.</summary>
    public static SessionEvents Watched(SessionEvents events)
    {
        _ = Of(events);
        return events;
    }

    /// <summary>The record's watch, begun now where nothing began it: it hears only what is appended from now on.</summary>
    public static RunningCalls Of(SessionEvents events) => Watches.GetValue(events, each => new RunningCalls(each));

    /// <summary>The call the session's agent is inside, or null where none is open or none open was named.</summary>
    public RunningCall? Calling(string session)
    {
        lock (_gate)
        {
            return _open.TryGetValue(session, out var calls) ? Current(calls) : null;
        }
    }

    /// <summary>Every session's running call, by session: what one look at the sessions reads (<see cref="SessionLook.Calls"/>).</summary>
    public IReadOnlyDictionary<string, RunningCall> Now()
    {
        lock (_gate)
        {
            var now = new Dictionary<string, RunningCall>(StringComparer.OrdinalIgnoreCase);
            foreach (var (session, calls) in _open)
            {
                if (Current(calls) is { } call) now[session] = call;
            }

            return now;
        }
    }

    private static RunningCall? Current(List<Open> calls)
    {
        for (var at = calls.Count - 1; at >= 0; at--)
        {
            if (calls[at].Title is { Length: > 0 } title) return new RunningCall(title, calls[at].Since);
        }

        return null;
    }

    private void Heard(string session, SessionEvent e)
    {
        switch (e.Kind)
        {
            case SessionEventKind.Tool when e.Id is { } id:
                lock (_gate) Called(session, id, e);
                return;
            // A turn's end, and the ask that begins the next: the words shown while they wait (STEER1) are no ask.
            case SessionEventKind.Turn:
            case SessionEventKind.User when e.Reaches is null:
                lock (_gate) _open.Remove(session);
                return;
        }
    }

    private void Called(string session, string id, SessionEvent e)
    {
        var open = e.Status is null or "pending" or "in_progress";
        if (!_open.TryGetValue(session, out var calls))
        {
            if (!open) return;
            calls = [];
            _open[session] = calls;
            Bound();
        }

        var at = calls.FindIndex(call => call.Id == id);
        if (!open)
        {
            if (at >= 0) calls.RemoveAt(at);
            if (calls.Count == 0) _open.Remove(session);
            return;
        }

        if (at < 0) calls.Add(new Open(id, e.Title, e.At));
        else if (e.Title is { Length: > 0 } title) calls[at] = calls[at] with { Title = title };
    }

    /// <summary>Past the bound, the session whose oldest open call began first is forgotten.</summary>
    private void Bound()
    {
        while (_open.Count > Remembered)
        {
            _open.Remove(_open.MinBy(pair => pair.Value.Count == 0 ? DateTimeOffset.MinValue : pair.Value[0].Since).Key);
        }
    }

    private sealed record Open(string Id, string? Title, DateTimeOffset Since);
}
