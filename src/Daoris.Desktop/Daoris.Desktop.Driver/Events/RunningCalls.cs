using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>The call a live session's agent is inside (WAITSAY1b): its title, and when this process first heard of it.</summary>
/// <param name="Title">
/// The newest title the call's events gave it, as the record keeps it; for a call that waits, the request's own where the record
/// named none, and empty where neither did.
/// </param>
/// <param name="Since">
/// The stamp of the first event of the call this process heard, by the record's clock; for a call that waits, when its request was
/// held.
/// </param>
public sealed record RunningCall(string Title, DateTimeOffset Since)
{
    /// <summary>
    /// Whether the call waits on the person's answer to its permission request (AUTONOMY1b3b): held for them under the repository's
    /// asking permission prompts (<see cref="HeldPermissions"/>), so the session stands still until they answer or the bound refuses it.
    /// </summary>
    public bool Waiting { get; init; }

    /// <summary>
    /// Whether Daoris told the session this call ran past its pipeline's <c>callMinutes</c> (FABRIC-PIPELINE1c, <see cref="LongCalls"/>):
    /// the rail says so beside its time. Held in memory with the call, as the call is.
    /// </summary>
    public bool Told { get; init; }
}

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
/// <para><b>A call held for the person waits</b> (AUTONOMY1b3b): while the session holds a tool's permission request for the person
/// (<see cref="HeldPermissions"/>, AUTONOMY1b3a), its newest hold is the call answered, marked <see cref="RunningCall.Waiting"/> and
/// timed from the hold, so the rail says the session waits on the person, not that it runs. A hold writes no call's event, and the
/// protocol does not order a call's announcement before its request, so the hold is the call: named as the record named its call where
/// that call is open here, else by the request's own title. The registry's <see cref="HeldPermissions.Changed"/> moves it as
/// <see cref="SessionEvents.Evented"/> moves a call.</para>
///
/// <para><b>A call told it ran long</b> (FABRIC-PIPELINE1c): once Daoris told the session that a call ran past its pipeline's bound
/// (<see cref="MarkTold"/>), that call answers <see cref="RunningCall.Told"/> while it runs, and the mark goes with it.</para>
///
/// <para>Bounded: each record's watch holds the calls of at most <see cref="Remembered"/> sessions, the oldest call's session
/// dropped past it. The holds are the registry's, which holds each only while its session's agent waits.</para>
/// </remarks>
public sealed class RunningCalls
{
    /// <summary>How many sessions' open calls one record's watch holds.</summary>
    private const int Remembered = 1024;

    private static readonly ConditionalWeakTable<SessionEvents, RunningCalls> Watches = new();

    private readonly object _gate = new();

    // Each session's open calls, in the order they began.
    private readonly Dictionary<string, List<Open>> _open = new(StringComparer.OrdinalIgnoreCase);

    // AUTONOMY1b3b: each session's newest request held for the person, as the registry last told it.
    private readonly Dictionary<string, HeldPermission> _waiting = new(StringComparer.OrdinalIgnoreCase);

    // FABRIC-PIPELINE1c: each session's calls Daoris told ran long, by when each began, gone with the session's open calls.
    private readonly Dictionary<string, HashSet<DateTimeOffset>> _told = new(StringComparer.OrdinalIgnoreCase);

    private readonly HeldPermissions _held;

    private readonly int _remembered;

    private RunningCalls(SessionEvents events, int remembered)
    {
        _remembered = remembered;
        _held = HeldPermissions.Of(events);
        events.Evented += Heard;
        _held.Changed += Held;
        // A request held before this watch began is read now (oldest first, so each session keeps its newest); one held from here on
        // is told, and read again under the lock, so either way it is marked.
        lock (_gate)
        {
            foreach (var hold in _held.Now()) _waiting[hold.Session] = hold;
        }
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
            foreach (var session in _open.Keys.Concat(_waiting.Keys))
            {
                if (!now.ContainsKey(session) && CurrentOf(session) is { } call) now[session] = call;
            }

            return now;
        }
    }

    /// <summary>
    /// Mark <paramref name="call"/>, the one <paramref name="session"/>'s agent is inside, as told it ran past its bound
    /// (FABRIC-PIPELINE1c): told as a move where it is the call running, so the rail says it; a call no longer open marks nothing.
    /// </summary>
    public void MarkTold(string session, RunningCall call)
    {
        bool moved;
        lock (_gate)
        {
            if (_open.GetValueOrDefault(session)?.Exists(open => open.Since == call.Since) != true) return;
            var before = CurrentOf(session);
            if (!_told.TryGetValue(session, out var told)) _told[session] = told = [];
            told.Add(call.Since);
            moved = CurrentOf(session) != before;
        }

        if (moved) Tell(session);
    }

    private RunningCall? CurrentOf(string session)
    {
        var calls = _open.GetValueOrDefault(session);
        var call = _waiting.TryGetValue(session, out var hold) ? Waits(hold, calls) : calls is null ? null : Current(calls);
        return call is { Waiting: false } && _told.GetValueOrDefault(session)?.Contains(call.Since) == true ? call with { Told = true } : call;
    }

    /// <summary>
    /// The call <paramref name="hold"/> waits on (AUTONOMY1b3b): named as the record named its call where that call is open here, else
    /// by the request's own title, else by none; timed from when it was held.
    /// </summary>
    private static RunningCall Waits(HeldPermission hold, List<Open>? calls)
    {
        var named = hold.Call is { } id ? calls?.Find(call => call.Id == id)?.Title : null;
        return new RunningCall(named is { Length: > 0 } ? named : hold.Title ?? "", hold.Since) { Waiting = true };
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
            if (!_open.ContainsKey(session)) _told.Remove(session);
            moved = CurrentOf(session) != before;
        }

        // Told outside the lock (WAITSAY1c1): a hearer that reads the calls again on another thread is never held behind it.
        if (moved) Tell(session);
        foreach (var each in forgotten ?? []) Tell(each);
    }

    /// <summary>
    /// The registry's word that what <paramref name="session"/> holds for the person moved (AUTONOMY1b3b): its holds read again and
    /// judged as an event is, told after the lock.
    /// </summary>
    private void Held(string session)
    {
        bool moved;
        lock (_gate)
        {
            var before = CurrentOf(session);
            // Read under this watch's lock, so two moves told on two threads land as the registry last holds them, whichever is told
            // first. The registry tells only after its own lock is let go, so this is the one order the two locks are taken in.
            if (_held.Held(session) is [.., var newest]) _waiting[session] = newest;
            else _waiting.Remove(session);
            moved = CurrentOf(session) != before;
        }

        if (moved) Tell(session);
    }

    private void Called(string session, string id, SessionEvent e, ref List<string>? forgotten)
    {
        var open = e.Status is null or "pending" or "in_progress";
        if (!_open.TryGetValue(session, out var calls))
        {
            if (!open) return;
            _open[session] = [new Open(id, e.Title, e.At)];
            // Bounded once its call is held (WAITSAY1c1), so the session just begun is weighed by its call, never by an empty list
            // that reads as the oldest.
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
            _told.Remove(session);
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
