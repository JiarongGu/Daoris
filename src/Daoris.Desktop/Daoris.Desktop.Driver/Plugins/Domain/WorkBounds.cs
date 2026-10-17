namespace Daoris.Driver;

/// <summary>The work wire's bounds (FABRIC-PERFORM1a; the fabric events design §C4), each before anything a plugin sends is believed.</summary>
public static class WorkBounds
{
    /// <summary>A frame longer than this, in UTF-8 bytes, is dropped unread.</summary>
    public const int FrameBytes = 64 * 1024;

    /// <summary>How many events are taken from one plugin in a minute; the rest are dropped.</summary>
    public const int EventsPerMinute = 60;

    /// <summary>How many events wait in one plugin's queue; past it the oldest is dropped first.</summary>
    public const int EventsWaiting = 256;

    /// <summary>How many requests (<c>work/perform</c>, <c>work/reconcile</c>) one plugin's process has in flight at once.</summary>
    public const int CallsInFlight = 4;

    /// <summary>The window of the rate and of a refusal's line: one minute.</summary>
    public static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    /// <summary>The waits before a restart after an exit Daoris did not ask for: 1, 5 and 15 minutes.</summary>
    public static readonly IReadOnlyList<TimeSpan> Backoff = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)];

    /// <summary>How many such exits within <see cref="ExitWindow"/> mark the plugin unsound until the person tries again.</summary>
    public const int ExitsUnsound = 3;

    public static readonly TimeSpan ExitWindow = TimeSpan.FromHours(1);
}

/// <summary>
/// The codes a frame from a plugin is not taken with (FABRIC-PERFORM1a, §C4), each a <c>plugin.step</c> line with no words. Beside them
/// stand §C4's belief codes (<c>not-named</c>, <c>not-declared</c>, <c>not-watched</c>, <c>stale</c>, <c>late</c>), judged by
/// <see cref="WorkBelief"/> (FABRIC-PERFORM1c).
/// </summary>
public static class WorkRefusals
{
    /// <summary>§C4's second rule: the frame names another step than its attempt's, or its node is no longer the sender's to reach.</summary>
    public const string NotNamed = "not-named";

    /// <summary>§C4's second rule: the step is no longer declared by its plugin, or no longer included by the cycle governing its unit.</summary>
    public const string NotDeclared = "not-declared";

    /// <summary>§C4's third rule: the unit is not one Daoris tells about, or the frame names another repository or branch than it.</summary>
    public const string NotWatched = "not-watched";

    /// <summary>§C4's fourth rule: the frame is bound to another commit than its work's, or the work has moved past its attempt's revision.</summary>
    public const string Stale = "stale";

    /// <summary>§C4's fifth rule: another outcome is already kept for that attempt, so this one came late.</summary>
    public const string Late = "late";

    /// <summary>A frame over <see cref="WorkBounds.FrameBytes"/>, dropped unread.</summary>
    public const string TooLong = "too-long";

    /// <summary>An event past <see cref="WorkBounds.EventsPerMinute"/>.</summary>
    public const string TooFast = "too-fast";

    /// <summary>The oldest waiting event, dropped for a newer one past <see cref="WorkBounds.EventsWaiting"/>.</summary>
    public const string QueueFull = "queue-full";

    /// <summary>An event not of <see cref="WorkStepFrame"/>'s shape.</summary>
    public const string Shape = "shape";

    /// <summary>An event read from a process that is no longer the plugin's (§C4's first rule): a generation since restarted or stopped.</summary>
    public const string NotSender = "not-sender";
}

/// <summary>
/// At most <see cref="WorkBounds.EventsPerMinute"/> in any minute (§C4), and a resync's replay beside them, on the caller's clock.
/// </summary>
/// <remarks>
/// A resync asks for one event per awaited step at once (FABRIC-PERFORM1a, round 2). Held to the 60 alone, a replay of 61 in a stable order
/// lost its last at every resync, which asked for the next: the minute after one admits as many more as the steps it named.
/// </remarks>
public sealed class EventRate
{
    private readonly Queue<DateTimeOffset> _taken = new();
    private int _replay;
    private DateTimeOffset _replayUntil;

    /// <summary>A resync told now: until <paramref name="until"/>, <paramref name="steps"/> more are taken beside the minute's bound.</summary>
    public void Allow(int steps, DateTimeOffset until) => (_replay, _replayUntil) = (steps, until);

    /// <summary>Whether one more is taken now: fewer than the bound were taken in the minute before it, or a resync's replay has room.</summary>
    public bool Admit(DateTimeOffset now)
    {
        while (_taken.Count > 0 && now - _taken.Peek() >= WorkBounds.Minute) _taken.Dequeue();
        var bound = WorkBounds.EventsPerMinute + (now < _replayUntil ? _replay : 0);
        if (_taken.Count >= bound) return false;
        _taken.Enqueue(now);
        return true;
    }
}

/// <summary>A refusal's line at most once a minute per code, with how many it stands for (§C4). Pure: the caller's clock and its writer.</summary>
public sealed class RefusalTally
{
    // Each code's last line and the refusals counted since it.
    private readonly Dictionary<string, (DateTimeOffset Written, int Held)> _codes = new(StringComparer.Ordinal);

    /// <summary>A refusal now: the count to write where its code's minute allows, else null, and it is counted for the next line.</summary>
    public int? Refused(string code, DateTimeOffset now)
    {
        var known = _codes.TryGetValue(code, out var tally);
        if (known && now - tally.Written < WorkBounds.Minute)
        {
            _codes[code] = (tally.Written, tally.Held + 1);
            return null;
        }

        _codes[code] = (now, 0);
        return tally.Held + 1;
    }

    /// <summary>The counts held whose minute has passed, each to be written now as one line.</summary>
    public IReadOnlyList<(string Code, int Count)> Due(DateTimeOffset now)
    {
        var due = new List<(string Code, int Count)>();
        foreach (var (code, tally) in _codes.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToList())
        {
            if (tally.Held == 0 || now - tally.Written < WorkBounds.Minute) continue;
            due.Add((code, tally.Held));
            _codes[code] = (now, 0);
        }

        return due;
    }
}

/// <summary>
/// When a plugin's supervised process may start again (FABRIC-PERFORM1a, §C4): an exit Daoris did not ask for waits 1, 5 and then 15
/// minutes, and three within an hour mark the plugin unsound until the person's <i>Try again</i>. An exit after Daoris's own
/// <c>shutdown</c> is never told here. Pure: the caller's clock.
/// </summary>
/// <remarks>
/// The wait grows with each such exit since the process last stayed up an hour (FABRIC-PERFORM1a's choice): exits an hour apart are not
/// unsound, and wait 15 minutes from the third on. A start that fails counts as an exit, so a plugin that cannot start is not started at
/// every look. The record is the loop's own, as its record of the plugin's health is (D119 §2): a new loop starts it afresh.
/// </remarks>
public sealed class WorkRestarts
{
    private readonly List<DateTimeOffset> _exits = [];
    private DateTimeOffset? _upSince;
    private int _run;

    /// <summary>Whether three unasked exits came within an hour: not started again until <see cref="TryAgain"/>.</summary>
    public bool Unsound { get; private set; }

    /// <summary>When the next start may be, after an unasked exit; null where none is waited for.</summary>
    public DateTimeOffset? NotBefore { get; private set; }

    public void Started(DateTimeOffset at) => _upSince = at;

    /// <summary>An exit Daoris did not ask for, or a start that failed: the wait before the next start, or null where it is now unsound.</summary>
    public TimeSpan? Exited(DateTimeOffset at)
    {
        if (_upSince is { } up && at - up >= WorkBounds.ExitWindow) _run = 0;
        _upSince = null;
        _exits.RemoveAll(exit => at - exit >= WorkBounds.ExitWindow);
        _exits.Add(at);
        _run += 1;
        if (_exits.Count >= WorkBounds.ExitsUnsound)
        {
            Unsound = true;
            NotBefore = null;
            return null;
        }

        var wait = WorkBounds.Backoff[Math.Min(_run, WorkBounds.Backoff.Count) - 1];
        NotBefore = at + wait;
        return wait;
    }

    public bool MayStart(DateTimeOffset now) => !Unsound && (NotBefore is not { } after || now >= after);

    /// <summary>The person's <i>Try again</i> (D119), or the plugin switched off, updated or removed: its record starts afresh.</summary>
    public void TryAgain()
    {
        _exits.Clear();
        _run = 0;
        _upSince = null;
        Unsound = false;
        NotBefore = null;
    }
}
