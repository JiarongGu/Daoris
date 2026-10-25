using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>What a running driven session is, for its watch: whose (a quest's or an intake's), its quest, and the agent it runs on.</summary>
/// <param name="Kind"><see cref="SessionWatch.Quest"/> or <see cref="SessionWatch.Intake"/>.</param>
/// <param name="Quest">The quest it works, or null for an intake.</param>
/// <param name="Adapter">The adapter its harness runs under, as the machine log names it.</param>
public sealed record WatchedRun(string Kind, string? Quest, string Adapter)
{
    /// <summary>
    /// Whether no turn runs and it waits on its own background work (state C, the session-watch design §1.4): read at each look, its
    /// quiet then judged by whether that work moves, on longer bounds. Null where its door holds no such wait.
    /// </summary>
    public Func<bool>? WaitsOnOwnWork { get; init; }

    /// <summary>The folder it works in, whose repository's heavy-run slot it may wait for (§1.3); null where none was named.</summary>
    public string? Tree { get; init; }
}

/// <summary>
/// A session's place at its repository's heavy-run slot (HEAVYSLOT1, the session-watch design §1.3): it waits behind a holder, named by
/// the holder's session where its environment named one, else null (a run from a terminal).
/// </summary>
public sealed record HeavyWait(string? Holder);

/// <summary>What the job's accounting reads for a session now: whether its root joined, and its CPU, cumulative.</summary>
public sealed record JobRead(bool Holds, TimeSpan? Cpu);

/// <summary>A row the person is brought (the session-watch design §2.3, §5.2): a quiet session, or a busy one nothing is heard from.</summary>
/// <param name="Session">The session.</param>
/// <param name="Kind"><see cref="SessionWatch.Quest"/> or <see cref="SessionWatch.Intake"/>.</param>
/// <param name="Quest">Its quest, or null for an intake.</param>
/// <param name="Minutes">How long nothing was heard from it, in whole minutes, when the row came.</param>
/// <param name="Busy">
/// The <i>busy, nothing heard</i> row: something it runs keeps the machine busy, so it is never offered <i>End and carry on</i>.
/// </param>
/// <param name="Since">When it was last heard, by the wall clock, for the row's time.</param>
public sealed record QuietRow(string Session, string Kind, string? Quest, int Minutes, bool Busy, DateTimeOffset Since)
{
    /// <summary>
    /// It waits for its repository's heavy-run slot (§1.3): brought to the person all the same past three times its bound, since the
    /// holder may be a run from a terminal with no session to help. The holder's session, or empty for a run from a terminal; null where
    /// it waits for no slot.
    /// </summary>
    public string? Heavy { get; init; }
}

/// <summary>One step the watcher took this look, written as a coded note, a console line and <c>session.quiet</c>.</summary>
/// <param name="State">The state it was judged in: <c>A</c> (a turn, its door's own) or <c>C</c> (waiting on its own work).</param>
/// <param name="Step"><see cref="SessionWatch.Marked"/>, <see cref="SessionWatch.Brought"/>, <see cref="SessionWatch.Ended"/>, or <c>over</c>.</param>
/// <param name="Outcome">Where an episode ends: <c>heard</c>, <c>busy</c>, <c>moving</c>, <c>waits</c>, <c>ended</c>, <c>left</c>, <c>pressed</c>; else <c>open</c>.</param>
public sealed record QuietStep(string Session, string Kind, string Adapter, string State, int Minutes, string Step, string Outcome, int Episode)
{
    /// <summary>The call its agent was inside when it was marked, as the record named it (§5.1's <c>{{what}}</c>); null where none was.</summary>
    public string? Call { get; init; }

    /// <summary>The background work it waited on in state C when it was marked; empty where none.</summary>
    public IReadOnlyList<string> Works { get; init; } = [];
}

/// <summary>
/// The watcher (SESSWATCH1a, D200, the session-watch design §1, §2): a running driven session that says nothing and runs nothing for its
/// bound is marked quiet, and brought to the person; Daoris ends nothing quiet by itself but a dead process (state E) and a headless run's
/// <c>--quiet-end</c>. A manager's look in on the team, never a kill switch.
/// </summary>
/// <remarks>
/// <para><b>One per shell, held beside <see cref="RunningCalls"/></b>: one per record (<see cref="Of"/>), so every look's driver and the
/// headless host read the same. Each run registers while it lasts (<see cref="Watch"/>); its captures stamp each line they read
/// (<see cref="Hearing"/>), the time alone, in memory.</para>
///
/// <para><b>Busy by window</b> (§1.2): the job's CPU is cumulative, so busy is the CPU between the newest read and the newest one at least
/// <see cref="DefaultWindow"/> before it, over that time, above <see cref="IdleFloor"/> a minute. A job is read only for a session unheard for half
/// its bound, so a session heard costs a look no system call. Where the root did not join (or off Windows) busy is unknown, which marks
/// and brings the row as quiet does, and never ends anything.</para>
///
/// <para><b>Waits</b> (§1.3): a call held for the person's answer to its permission request is never quiet.</para>
///
/// <para><b>Steps</b> (§2): at <see cref="Quiet"/> unheard and not busy (three times it in state C), the mark; at three times it (five
/// in state C), the row: <i>quiet</i>, or <i>busy, nothing heard</i> where it was busy. State C's moving work keeps it working. Each
/// episode lasts from its first step to its session heard again, busy, waiting, ended, pressed or left, and its end is written too.</para>
///
/// <para><b>The clock stops in sleep</b> (<see cref="QuietClock"/>); where it does not, a gap between looks longer than three paces and
/// at least two minutes is not counted.</para>
/// </remarks>
public sealed class SessionWatch
{
    /// <summary>The quiet bound Q's default (§1.5, §3): ten minutes, longer than the longest foreground command Claude Code allows.</summary>
    public static readonly TimeSpan DefaultQuiet = TimeSpan.FromMinutes(10);

    /// <summary>The idle floor (§1.5): 0.2 seconds of CPU a minute, a third of a percent of one core.</summary>
    public static readonly TimeSpan IdleFloor = TimeSpan.FromMilliseconds(200);

    /// <summary>The shortest window busy is read over (§1.2), a minute.</summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(1);

    /// <summary>The shortest window busy is read over: <see cref="DefaultWindow"/>, shorter only in a test on a real process.</summary>
    public TimeSpan Window { get; set; } = DefaultWindow;

    /// <summary>A quest's session: marked, brought to the person, and ended by the person's press or a headless <c>--quiet-end</c>.</summary>
    public const string Quest = "quest";

    /// <summary>An intake: marked and brought to the person, with <i>Stop</i> and <i>Leave it running</i>; never carried on.</summary>
    public const string Intake = "intake";

    /// <summary>The step that marks a session quiet.</summary>
    public const string Marked = "mark";

    /// <summary>The step that brings it to the person (§2.3).</summary>
    public const string Brought = "person";

    /// <summary>The step that ends its run, under a headless <c>--quiet-end</c> alone in this build.</summary>
    public const string Ended = "end";

    private static readonly ConditionalWeakTable<SessionEvents, SessionWatch> Watches = new();

    private readonly ConcurrentDictionary<string, Watched> _watched = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private readonly SessionEvents _events;
    private readonly RunningCalls _calls;
    private int _episodes;
    private TimeSpan? _lastLook;

    private SessionWatch(SessionEvents events)
    {
        _events = events;
        _calls = RunningCalls.Of(events);
    }

    /// <summary>The machine log each step's <c>session.quiet</c> line goes to; the look hands its own.</summary>
    public MachineLog? Log { get; set; }

    /// <summary>The record's watcher, begun now where nothing began it.</summary>
    public static SessionWatch Of(SessionEvents events) => Watches.GetValue(events, each => new SessionWatch(each));

    /// <summary>The clock quiet is counted on; a test hands one it moves.</summary>
    public QuietClock Clock { get; set; } = QuietClock.System;

    /// <summary>Q, how long a session may say nothing before Daoris looks in (§1.5): <see cref="DefaultQuiet"/> until SESSWATCH1c sets it.</summary>
    public TimeSpan Quiet { get; set; } = DefaultQuiet;

    /// <summary>
    /// A headless run's <c>--quiet-end</c> (§2): a session unheard this long, busy or not, is ended and carried on; null, every shell's,
    /// ends nothing.
    /// </summary>
    public TimeSpan? EndAfter { get; set; }

    /// <summary>Told a session whose mark or row moved, after the watcher's lock is let go: what wakes the pages' slices.</summary>
    public event Action<string>? Changed;

    /// <summary>Register a run while it lasts; disposing ends its episode, if one is open, as <c>ended</c>.</summary>
    public WatchHandle Watch(string session, WatchedRun run)
    {
        var watched = new Watched(session, run, Clock.Now.Ticks);
        _watched[session] = watched;
        return new WatchHandle(this, watched);
    }

    /// <summary>
    /// Where a session's door learns whether it waits on its own background work (state C), once its door holds the session that
    /// knows: the protocol door's (<see cref="AcpSession.WaitsOnOwnWork"/>).
    /// </summary>
    /// <param name="works">The names of the background work it waits on, which its mark's note names (§5.1); none where null.</param>
    public void OwnWork(string session, Func<bool> waits, Func<IReadOnlyList<string>>? works = null)
    {
        if (!_watched.TryGetValue(session, out var watched)) return;
        watched.OwnWork = waits;
        watched.Works = works;
    }

    /// <summary>
    /// Where a session's door says it waits between a transient episode's tries (AGENTERRORS1a, D210 §5.1, amending D200): on Daoris's own
    /// timer, never its agent, so the watch reads it as a call held for the person's permission is read, a wait that is not the agent's,
    /// opening no episode and ending nothing, a headless <c>--quiet-end</c> included.
    /// </summary>
    public void Transient(string session, Func<bool> waits)
    {
        if (_watched.TryGetValue(session, out var watched)) watched.Transient = waits;
    }

    /// <summary>Stamp a session heard now: one in-memory write, from any reading thread.</summary>
    public void Heard(string session)
    {
        if (_watched.TryGetValue(session, out var watched)) Volatile.Write(ref watched.HeardTicks, Clock.Now.Ticks);
    }

    /// <summary>A capture's stream, stamping <paramref name="session"/> heard at each line it reads.</summary>
    public TextReader Hearing(TextReader reader, string session) => new HeardReader(reader, () => Heard(session));

    /// <summary>The row a session brings the person now, or null.</summary>
    public QuietRow? RowOf(string session)
    {
        lock (_gate) return _watched.TryGetValue(session, out var watched) ? watched.Episode?.Row : null;
    }

    /// <summary>Every row brought to the person now, oldest heard first.</summary>
    public IReadOnlyList<QuietRow> Rows()
    {
        lock (_gate)
        {
            return [.. _watched.Values.Select(watched => watched.Episode?.Row).OfType<QuietRow>().OrderBy(row => row.Since)];
        }
    }

    /// <summary>When a quiet session was last heard, by the wall clock, while it is marked quiet; null while it is not.</summary>
    public DateTimeOffset? QuietSince(string session)
    {
        lock (_gate)
        {
            return _watched.TryGetValue(session, out var watched) && watched.Episode is { Marked: true } ? watched.Episode.Since : null;
        }
    }

    /// <summary>
    /// Every session marked quiet now, with when it was last heard by the wall clock (§5.1): what the rail and the head say as
    /// <i>quiet · 14 min</i> while its episode lasts, a busy row's session among them once it is brought to the person.
    /// </summary>
    public IReadOnlyDictionary<string, DateTimeOffset> QuietNow()
    {
        lock (_gate)
        {
            return _watched.Values
                .Where(watched => watched.Episode is { Marked: true } or { Row: not null })
                .ToDictionary(watched => watched.Session, watched => watched.Episode!.Since, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>The minutes a headless <c>--quiet-end</c> ended a run after, read by its conclusion; null where the watcher ended nothing.</summary>
    public int? EndedMinutes(string session) =>
        _watched.TryGetValue(session, out var watched) ? watched.EndedMinutes : null;

    /// <summary>
    /// The person's <i>Leave it running</i> (§2.3): the watcher stands down for this session until it is heard again, and its row goes.
    /// False where it brings no row.
    /// </summary>
    public bool Leave(string session) => Close(session, "left");

    /// <summary>The person pressed the row's end (<i>End and carry on</i>, <i>Stop</i>, <i>Hand over…</i>): its episode ends <c>pressed</c>.</summary>
    public bool Pressed(string session) => Close(session, "pressed");

    private bool Close(string session, string outcome)
    {
        QuietStep? step;
        lock (_gate)
        {
            if (!_watched.TryGetValue(session, out var watched) || watched.Episode is not { } episode) return false;
            step = Over(watched, episode, outcome, Clock.Now);
            if (outcome == "left") watched.LeftAt = Volatile.Read(ref watched.HeardTicks);
        }

        Write(step);
        Changed?.Invoke(session);
        return true;
    }

    /// <summary>
    /// One look over every running session (§2): the steps taken now, each to be written by the caller. Reads a job only for a session
    /// unheard for half its bound.
    /// </summary>
    /// <param name="jobs">The job's accounting for a session, or null where it runs no process here.</param>
    /// <param name="pace">The watch's pace, for the gap rule where the clock runs on in sleep.</param>
    /// <param name="heavy">
    /// Where a session's tree waits for its repository's heavy-run slot (§1.3), read from the slot's records only for a session unheard for
    /// half its bound; null reads none.
    /// </param>
    public IReadOnlyList<QuietStep> Look(Func<string, JobRead?> jobs, TimeSpan pace, Func<string, string?, HeavyWait?>? heavy = null)
    {
        var steps = new List<QuietStep>();
        var now = Clock.Now;
        lock (_gate)
        {
            Gap(now, pace);
            foreach (var watched in _watched.Values) Judge(watched, now, jobs, heavy, steps);
        }

        foreach (var step in steps) Write(step);
        foreach (var session in steps.Select(step => step.Session).Distinct(StringComparer.OrdinalIgnoreCase)) Changed?.Invoke(session);
        return steps;
    }

    /// <summary>
    /// A step written where SESSWATCH1d reads it (§2): <c>session.quiet</c> to the machine log, every step and every episode's end, and
    /// on the record the mark's coded note and the person's <i>Leave it running</i>'s.
    /// </summary>
    private void Write(QuietStep step)
    {
        Log?.Info(
            "session.quiet", ("session", step.Session), ("adapter", step.Adapter), ("kind", step.Kind), ("state", step.State),
            ("minutes", step.Minutes), ("step", step.Step), ("outcome", step.Outcome), ("episode", step.Episode));
        var noted = step switch
        {
            { Step: Marked, Call: { } call } => Noted.Of(
                NoteCodes.WatchQuietCall, $"Nothing came from its agent for {step.Minutes} minutes, while `{call}` ran. Daoris looked in.",
                ("minutes", step.Minutes), ("call", call)),
            { Step: Marked, Works.Count: > 0 } => Noted.Of(
                NoteCodes.WatchQuietWork,
                $"Nothing came from its agent for {step.Minutes} minutes, while its background work {string.Join(", ", step.Works.Select(work => $"`{work}`"))} showed no progress. Daoris looked in.",
                ("minutes", step.Minutes), ("works", step.Works)),
            { Step: Marked } => Noted.Of(NoteCodes.WatchQuiet, $"Nothing came from its agent for {step.Minutes} minutes. Daoris looked in.", ("minutes", step.Minutes)),
            { Outcome: "left" } => Noted.Of(NoteCodes.WatchLeft, "You left it running. Daoris looks in again once it is heard from."),
            _ => null,
        };
        if (noted is not null)
        {
            _events.Keep(step.Session, new SessionEvent { Kind = SessionEventKind.Note, Text = noted.Note, Parts = noted.Parts }, say: null);
        }
    }

    // Off a clock that stops in sleep, a gap between looks longer than three paces and at least two minutes is not counted (§1.5).
    private void Gap(TimeSpan now, TimeSpan pace)
    {
        var last = _lastLook;
        _lastLook = now;
        if (Clock.StopsInSleep || last is not { } before) return;
        var gap = now - before;
        var limit = TimeSpan.FromTicks(Math.Max(pace.Ticks * 3, TimeSpan.FromMinutes(2).Ticks));
        if (gap <= limit) return;
        foreach (var watched in _watched.Values)
        {
            var heard = Volatile.Read(ref watched.HeardTicks);
            Interlocked.CompareExchange(ref watched.HeardTicks, heard + gap.Ticks, heard);
            watched.Reads.Clear();
        }
    }

    private void Judge(Watched watched, TimeSpan now, Func<string, JobRead?> jobs, Func<string, string?, HeavyWait?>? heavy, List<QuietStep> steps)
    {
        if (watched.Gone) return;
        var heard = Volatile.Read(ref watched.HeardTicks);
        var unheard = now - TimeSpan.FromTicks(heard);
        var waitsOnWork = watched.OwnWork?.Invoke() == true;
        var markAt = waitsOnWork ? Quiet * 3 : Quiet;
        var bringAt = waitsOnWork ? Quiet * 5 : Quiet * 3;

        // Heard again since its episode began, or since the person left it running: the episode is over, the stand-down too.
        if (watched.Episode is { } heardOver && heard > heardOver.HeardAt) steps.Add(Over(watched, heardOver, "heard", now));
        if (watched.LeftAt is { } left && heard > left) watched.LeftAt = null;

        var state = waitsOnWork ? "C" : "A";
        var minutes = (int)unheard.TotalMinutes;

        // A call held for the person's answer to its permission request is their wait, never the agent's (§1.3).
        if (_calls.Calling(watched.Session) is { Waiting: true })
        {
            if (watched.Episode is { } waiting) steps.Add(Over(watched, waiting, "waits", now));
            return;
        }

        // AGENTERRORS1a: a run waiting between a transient episode's tries waits on Daoris's own timer, bounded by the episode itself.
        if (watched.Transient?.Invoke() == true)
        {
            if (watched.Episode is { } retrying) steps.Add(Over(watched, retrying, "waits", now));
            return;
        }

        // A headless run's --quiet-end: unheard that long, busy or not, the run is ended and carried on (§2), on its own bound, which
        // may be shorter than Q's.
        if (EndAfter is { } endAfter && unheard >= endAfter && watched.EndedMinutes is null)
        {
            var episode = watched.Episode ??= Begin(watched, heard, now, unheard, busy: false);
            watched.EndedMinutes = minutes;
            steps.Add(new QuietStep(watched.Session, watched.Run.Kind, watched.Run.Adapter, state, minutes, Ended, "ended", episode.Number));
            watched.End.Cancel();
            return;
        }

        if (unheard < markAt / 2)
        {
            watched.Reads.Clear();
            return;
        }

        // A call waiting for the heavy-run slot behind another run is a wait (§1.3), until three times its bound, when the person is
        // brought it all the same: the holder may be a run from a terminal with no session to help.
        if (heavy?.Invoke(watched.Session, watched.Run.Tree) is { } waits)
        {
            if (watched.Episode is { Marked: true, Row: null } marked) steps.Add(Over(watched, marked, "waits", now));
            if (unheard < bringAt || watched.LeftAt is not null || watched.Episode?.Row is not null) return;
            var waiting = watched.Episode ??= Begin(watched, heard, now, unheard, busy: false);
            waiting.Row = new QuietRow(watched.Session, watched.Run.Kind, watched.Run.Quest, minutes, Busy: false, waiting.Since)
            {
                Heavy = waits.Holder ?? "",
            };
            steps.Add(new QuietStep(watched.Session, watched.Run.Kind, watched.Run.Adapter, "B", minutes, Brought, "open", waiting.Number));
            return;
        }

        var busy = Busy(watched, now, jobs);

        if (watched.LeftAt is not null) return;

        // State C's moving work keeps it working (§1.2), and its count starts again from it: its quiet is three times its bound with no
        // moving work (§1.5), never since the turn's last line.
        if (waitsOnWork && busy == true)
        {
            if (watched.Episode is { } moving) steps.Add(Over(watched, moving, "moving", now));
            Interlocked.CompareExchange(ref watched.HeardTicks, now.Ticks, heard);
            return;
        }

        if (unheard >= bringAt && watched.Episode?.Row is null)
        {
            var episode = watched.Episode ??= Begin(watched, heard, now, unheard, busy: busy == true);
            // Busy now, or never marked quiet: the busy row, offered no end (§2.3).
            var busyRow = busy == true || !episode.Marked;
            episode.Row = new QuietRow(watched.Session, watched.Run.Kind, watched.Run.Quest, minutes, busyRow, episode.Since);
            steps.Add(new QuietStep(watched.Session, watched.Run.Kind, watched.Run.Adapter, state, minutes, Brought, "open", episode.Number));
            return;
        }

        if (unheard >= markAt && busy != true && watched.Episode is null)
        {
            var episode = watched.Episode = Begin(watched, heard, now, unheard, busy: false);
            episode.Marked = true;
            // What it was doing as it went quiet (§5.1): the call its agent was inside, or in state C the work that showed nothing.
            steps.Add(new QuietStep(watched.Session, watched.Run.Kind, watched.Run.Adapter, state, minutes, Marked, "open", episode.Number)
            {
                Call = waitsOnWork ? null : _calls.Calling(watched.Session) is { Waiting: false, Title.Length: > 0 } call ? call.Title : null,
                Works = waitsOnWork ? watched.Works?.Invoke() ?? [] : [],
            });
            return;
        }

        // Busy again before the person was brought: the quiet is over, and three times its bound unheard still brings the busy row.
        if (watched.Episode is { Marked: true, Row: null } over && busy == true) steps.Add(Over(watched, over, "busy", now));
    }

    private Episode Begin(Watched watched, long heard, TimeSpan now, TimeSpan unheard, bool busy) =>
        new(Interlocked.Increment(ref _episodes), heard, DateTimeOffset.UtcNow - unheard) { Busy = busy };

    private QuietStep Over(Watched watched, Episode episode, string outcome, TimeSpan now)
    {
        watched.Episode = null;
        var minutes = (int)(now - TimeSpan.FromTicks(episode.HeardAt)).TotalMinutes;
        return new QuietStep(
            watched.Session, watched.Run.Kind, watched.Run.Adapter, watched.OwnWork?.Invoke() == true ? "C" : "A", minutes, "over",
            outcome, episode.Number);
    }

    // Busy by window (§1.2): the CPU between the newest read and the newest at least a window before it, over their time.
    private bool? Busy(Watched watched, TimeSpan now, Func<string, JobRead?> jobs)
    {
        if (jobs(watched.Session) is not { Holds: true, Cpu: { } cpu }) return null;
        watched.Reads.Add((now, cpu));
        var reference = -1;
        for (var at = watched.Reads.Count - 2; at >= 0; at--)
        {
            if (now - watched.Reads[at].At >= Window)
            {
                reference = at;
                break;
            }
        }

        // Only the window's start so far: past work never counts, and a first read proves nothing either way.
        if (reference < 0) return false;
        var (since, before) = watched.Reads[reference];
        if (reference > 0) watched.Reads.RemoveRange(0, reference);
        var used = cpu - before;
        // CPU a minute over the window, against the floor's 0.2 s a minute.
        return used.Ticks * TimeSpan.TicksPerMinute / Math.Max(1, (now - since).Ticks) > IdleFloor.Ticks;
    }

    private void Forget(Watched watched)
    {
        QuietStep? step = null;
        lock (_gate)
        {
            watched.Gone = true;
            if (watched.Episode is { } episode) step = Over(watched, episode, "ended", Clock.Now);
            if (_watched.TryGetValue(watched.Session, out var now) && ReferenceEquals(now, watched)) _watched.TryRemove(watched.Session, out _);
        }

        if (step is not null) Write(step);
        Changed?.Invoke(watched.Session);
    }

    /// <summary>A run's registration: its end, should the watcher end it, and its forgetting once disposed.</summary>
    public sealed class WatchHandle : IDisposable
    {
        private readonly SessionWatch _owner;
        private readonly Watched _watched;
        private int _disposed;

        internal WatchHandle(SessionWatch owner, Watched watched)
        {
            _owner = owner;
            _watched = watched;
        }

        /// <summary>Cancelled where the watcher ends the run (a headless <c>--quiet-end</c>): the run's own wait reads it.</summary>
        public CancellationToken Ending => _watched.End.Token;

        /// <summary>The minutes the watcher ended the run after, or null where it did not.</summary>
        public int? EndedMinutes => _watched.EndedMinutes;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _owner.Forget(_watched);
            _watched.End.Dispose();
        }
    }

    internal sealed class Watched(string session, WatchedRun run, long heard)
    {
        public readonly string Session = session;
        public readonly WatchedRun Run = run;
        public Func<bool>? OwnWork = run.WaitsOnOwnWork;
        public Func<IReadOnlyList<string>>? Works;
        public Func<bool>? Transient;
        public long HeardTicks = heard;
        public readonly List<(TimeSpan At, TimeSpan Cpu)> Reads = [];
        public readonly CancellationTokenSource End = new();
        public Episode? Episode;
        public long? LeftAt;
        public int? EndedMinutes;
        public bool Gone;
    }

    internal sealed class Episode(int number, long heardAt, DateTimeOffset since)
    {
        public int Number { get; } = number;
        public long HeardAt { get; } = heardAt;
        public DateTimeOffset Since { get; } = since;
        public bool Marked { get; set; }
        public bool Busy { get; set; }
        public QuietRow? Row { get; set; }
    }
}
