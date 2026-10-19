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
/// <para><b>Told as it moves</b> (WAITSAY1c1): <see cref="Changed"/> names a session whenever what <see cref="Calling"/> answers of
/// it moved, judged under the watch's lock and told after it, so the pushed <c>calls</c> slice is woken by a call's start and end
/// and by nothing that leaves the call as it was.</para>
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

    private readonly int _remembered;

    private RunningCalls(SessionEvents events, int remembered)
    {
        _remembered = remembered;
        events.Evented += Heard;
    }

    /// <summary>
    /// A session whose running call moved (WAITSAY1c1): what <see cref="Calling"/> answers of it is not what it answered before the
    /// event, whatever moved it (a call's start or end, a title arriving, a turn's end or an ask clearing it, the bound forgetting
    /// it). Told on the appending thread after the watch's lock is let go; a hearer's failure is its own.
    /// </summary>
    public event Action<string>? Changed;

    /// <summary>Watch a record for its calls, once whoever asks: the record handed back, as <see cref="UsageTurns.Watched"/> does.</summary>
    public static SessionEvents Watched(SessionEvents events)
    {
        _ = Of(events);
        return events;
    }

    /// <summary>The record's watch, begun now where nothing began it: it hears only what is appended from now on.</summary>
    public static RunningCalls Of(SessionEvents events) => Of(events, Remembered);

    /// <summary>The record's watch, bounded at <paramref name="remembered"/> sessions where nothing began it: the bound's own test.</summary>
    internal static RunningCalls Of(SessionEvents events, int remembered) =>
        Watches.GetValue(events, each => new RunningCalls(each, remembered));

    /// <summary>The call the session's agent is inside, or null where none is open or none open was named.</summary>
    public RunningCall? Calling(string session)
    {
        lock (_gate)
        {
            return CurrentOf(session);
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

    private RunningCall? CurrentOf(string session) => _open.TryGetValue(session, out var calls) ? Current(calls) : null;

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
        // A call's event, a turn's end, and the ask that begins the next: the words shown while they wait (STEER1) are no ask.
        var call = e.Kind == SessionEventKind.Tool ? e.Id : null;
        var clears = e.Kind == SessionEventKind.Turn || (e.Kind == SessionEventKind.User && e.Reaches is null);
        if (call is null && !clears) return;

        bool moved;
        List<string>? forgotten = null;
        lock (_gate)
        {
            var before = CurrentOf(session);
            if (call is null) _open.Remove(session);
            else Called(session, call, e, ref forgotten);
            moved = CurrentOf(session) != before;
        }

        // Told outside the lock (WAITSAY1c1): a hearer that reads the calls again on another thread is never held behind it.
        if (moved) Tell(session);
        foreach (var each in forgotten ?? []) Tell(each);
    }

    private void Called(string session, string id, SessionEvent e, ref List<string>? forgotten)
    {
        var open = e.Status is null or "pending" or "in_progress";
        if (!_open.TryGetValue(session, out var calls))
        {
            if (!open) return;
            _open[session] = [new Open(id, e.Title, e.At)];
            // Bounded once its call is held (WAITSAY1c1): bounded before, its empty list read as the oldest, and the session just
            // begun was the one forgotten.
            Bound(session, ref forgotten);
            return;
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

    /// <summary>
    /// Past the bound, the session whose oldest open call began first is forgotten; one that had a call running is named in
    /// <paramref name="forgotten"/>, the session heard now left to its own comparison.
    /// </summary>
    private void Bound(string heard, ref List<string>? forgotten)
    {
        while (_open.Count > _remembered)
        {
            var (session, calls) = _open.MinBy(pair => pair.Value.Count == 0 ? DateTimeOffset.MinValue : pair.Value[0].Since);
            _open.Remove(session);
            if (!string.Equals(session, heard, StringComparison.OrdinalIgnoreCase) && Current(calls) is not null) (forgotten ??= []).Add(session);
        }
    }

    private void Tell(string session)
    {
        if (Changed is not { } changed) return;
        foreach (var hearer in changed.GetInvocationList().Cast<Action<string>>())
        {
            try
            {
                hearer(session);
            }
            catch (Exception)
            {
                // A hearer's failure is its own: the call is kept, and the other hearers and the record's other watchers still hear.
            }
        }
    }

    private sealed record Open(string Id, string? Title, DateTimeOffset Since);
}
