using System.Globalization;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// <i>Done and failed</i> as every window holds it (NOTIFY2f, D196 point 6; the notification design §2.2, §2.5): the notices of
/// <c>data/notices.json</c> with whether each is read, pushed from this process's read of the record, and each notice another process
/// wrote said once as it arrives.
/// </summary>
/// <remarks>
/// <para><b>One partition, keyed by notice</b> (<see cref="News"/>): <c>{ id, kind, at, read, workspace?, session?, quest?, repository?,
/// state?, route?, act?, code? }</c>, picked to <see cref="Fields"/>. No field says what to announce: an arrival is an event
/// (<see cref="ArrivedType"/>).</para>
///
/// <para><b>Read in every process, whoever drives</b>: at its start, at each change its home watch hears
/// (<see cref="HomeWatch.ForNotices"/>) and at the topic's deadline behind a missed one. A read that fails keeps what was pushed, and
/// waits for the next wake; a reader never sets a record aside, which <see cref="Notices"/>' writer alone does.</para>
///
/// <para><b>The writer pushes its own write</b> (<see cref="Record"/>, <see cref="MarkRead"/>, <see cref="Dismiss"/>): a stamp this
/// process marked wakes nothing on its watch. A write refused is logged (<c>notices.refused</c>) and dropped.</para>
///
/// <para><b>Arrivals</b> (§5): a notice id this host's read has not seen is said once, as <see cref="ArrivedType"/>, while the notify
/// switch is on; the first read is a baseline and says nothing, so a restart repeats nothing. A session's end this process recorded
/// is said already by its own <c>SESSION_ATTENTION</c>, so it is not said again; a press's unanswered end has no other voice, and is. A
/// look's news (<see cref="Notices.Look"/>, NOTIFY2h) is never said: the design's §5 lists it with a dot and nothing at the window.
/// With the headless loop driving, this re-read is the only way its notice reaches the windows.</para>
///
/// <para><b>Desktop-only</b>: machine-local, never on a wire to a shared host (D47, D182 point 2).</para>
/// </remarks>
public sealed class NoticesStateSlice
{
    /// <summary>The slice's name, in the page's words.</summary>
    public const string Name = "notices";

    /// <summary>Its partition of notices, keyed by id (2g adds <c>messages</c> beside it).</summary>
    public const string News = "news";

    /// <summary>The event a notice's arrival is said by, its fields the partition's.</summary>
    public const string ArrivedType = "NOTICE";

    /// <summary>The fields a notice is pushed with: the page's <c>Notice</c> type, which its test reads.</summary>
    public static readonly IReadOnlyList<string> Fields =
        ["id", "kind", "at", "read", "workspace", "session", "quest", "repository", "state", "route", "act", "code"];

    private const string Moment = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    // How many ids said by another event are kept: a session's end said and recorded here is matched within a judgement or two.
    private const int SaidBound = 256;

    private readonly string _home;
    private readonly IEventBus _bus;
    private readonly MachineLog? _log;
    private readonly TimeProvider _clock;
    private readonly Func<bool> _notify;
    private readonly Lock _gate = new();
    private readonly LinkedList<string> _said = new();
    private HashSet<string>? _known;

    /// <param name="home">The home whose record is read and written.</param>
    /// <param name="bus">Where the slice pushes and an arrival is said.</param>
    /// <param name="log">The machine log, where a refused write and a failed judgement are said.</param>
    /// <param name="clock">The clock a notice's moment and its week are read by: the system's, or a test's.</param>
    /// <param name="notify">Whether the person is told (the notify switch), read at each arrival; told where none is given.</param>
    public NoticesStateSlice(string home, IEventBus bus, MachineLog? log = null, TimeProvider? clock = null, Func<bool>? notify = null)
    {
        _home = home;
        _bus = bus;
        _log = log;
        _clock = clock ?? TimeProvider.System;
        _notify = notify ?? (() => true);
        Slice = new(Name, bus, _ => Task.FromResult(Judge()))
        {
            Keyed = true,
            Failed = error => log?.Warn("state.slice", ("slice", Name), ("message", error.Message)),
        };
    }

    /// <summary>The loop's own: its home, its log, and its notify switch read from the machine's choices at each arrival.</summary>
    public static NoticesStateSlice Of(DriverLoop loop, IEventBus bus) => new(loop.Home, bus, loop.Log, notify: () => Notifying(loop.ConfigPath));

    /// <summary>The slice the windows hold, which <see cref="DriverLoop.Slices"/> runs.</summary>
    public StateSlice Slice { get; }

    /// <summary>
    /// The notices a look's attention items keep, recorded whatever the switch says (§2.5): each session's end, said already by this
    /// process's own <c>SESSION_ATTENTION</c> where the switch is on, so never said again as an arrival.
    /// </summary>
    public void Keep(IEnumerable<AttentionEvent> items)
    {
        var now = _clock.GetUtcNow();
        foreach (var item in items)
        {
            if (Notices.Of(item, now) is { } notice) Record(notice, said: true);
        }
    }

    /// <summary>A press that ended unanswered, kept as a notice (§2.2): every window's, said as it arrives.</summary>
    public void Unanswered(PipeConfirmation press)
    {
        var route = press.Module == DriverModule.Name ? press.Route : $"{press.Module.Replace("DAORIS.", "", StringComparison.Ordinal)}.{press.Route}";
        Record(new Notice($"expired-{press.Id}", Notices.Expired, _clock.GetUtcNow(), Route: route), said: false);
    }

    /// <summary>
    /// <paramref name="notice"/> written to the record and pushed; <paramref name="said"/> where another event said it, so its arrival
    /// is not said again. A refusal is logged and dropped.
    /// </summary>
    public void Record(Notice notice, bool said)
    {
        if (said) Said(notice.Id);
        if (Written(Notices.Add(_home, notice, _clock.GetUtcNow()))) return;
        if (said)
        {
            lock (_gate) _said.Remove(notice.Id);
        }
    }

    /// <summary>The person's read of <paramref name="ids"/>: written and pushed. Null where written; else why not, logged.</summary>
    public string? MarkRead(IReadOnlyCollection<string> ids) => Refusal(Notices.MarkRead(_home, ids, _clock.GetUtcNow()));

    /// <summary>The person's dismissal of <paramref name="ids"/>: written and pushed. Null where written; else why not, logged.</summary>
    public string? Dismiss(IReadOnlyCollection<string> ids) => Refusal(Notices.Dismiss(_home, ids, _clock.GetUtcNow()));

    /// <summary>The notices of <paramref name="workspace"/>'s window, its own and every machine-wide one, as last read; every one for none.</summary>
    public IReadOnlyList<string> Scoped(string? workspace) =>
        [.. (Notices.Read(_home, _clock.GetUtcNow()) ?? [])
            .Where(notice => workspace is null || notice.Workspace is null || MainWindowScopes.Circle(notice.Workspace) == MainWindowScopes.Circle(workspace))
            .Select(notice => notice.Id)];

    private string? Refusal(string? why)
    {
        Written(why);
        return why;
    }

    /// <summary>Whether the write went in: pushed by its writer where it did, logged where it did not.</summary>
    private bool Written(string? why)
    {
        if (why is not null)
        {
            _log?.Warn("notices.refused", ("why", why));
            return false;
        }

        Slice.Wake();
        return true;
    }

    private void Said(string id)
    {
        lock (_gate)
        {
            _said.AddLast(id);
            while (_said.Count > SaidBound) _said.RemoveFirst();
        }
    }

    /// <summary>
    /// One judgement: the record read, each notice younger than a week under its id; null where the record does not read, so what was
    /// pushed stands. Each id not read before is an arrival, said once, but on the first read.
    /// </summary>
    public IReadOnlyList<SliceJudged>? Judge()
    {
        var read = Notices.Read(_home, _clock.GetUtcNow());
        if (read is null) return null;

        var part = new JsonObject();
        foreach (var notice in read) part[notice.Id] = Picked(notice);
        foreach (var notice in Arrived(read)) _ = _bus.EmitAsync(StateSlice.Module, ArrivedType, Picked(notice));
        return [new SliceJudged(News, part)];
    }

    /// <summary>The notices read now that no read before held, none at the first read, and none said already or with the switch off.</summary>
    private List<Notice> Arrived(IReadOnlyList<Notice> read)
    {
        List<Notice> arrived = [];
        lock (_gate)
        {
            var known = _known;
            _known = read.Select(notice => notice.Id).ToHashSet(StringComparer.Ordinal);
            if (known is null) return arrived;
            foreach (var notice in read)
            {
                // A look's news is listed and counted, never said aloud (NOTIFY2h, the design §5's table).
                if (known.Contains(notice.Id) || _said.Remove(notice.Id) || notice.Kind == Notices.Look) continue;
                arrived.Add(notice);
            }
        }

        return arrived.Count > 0 && _notify() ? arrived : [];
    }

    /// <summary>A notice as the partition and its arrival carry it: the allow-list's fields, an absent one left out.</summary>
    public static JsonObject Picked(Notice notice)
    {
        var picked = new JsonObject
        {
            ["id"] = notice.Id,
            ["kind"] = notice.Kind,
            ["at"] = notice.At.UtcDateTime.ToString(Moment, CultureInfo.InvariantCulture),
            ["read"] = notice.Read,
        };
        Optional("workspace", notice.Workspace);
        Optional("session", notice.Session);
        Optional("quest", notice.Quest);
        Optional("repository", notice.Repository);
        Optional("state", notice.State);
        Optional("route", notice.Route);
        Optional("act", notice.Act);
        Optional("code", notice.Code);
        return picked;

        void Optional(string name, string? value)
        {
            if (value is not null) picked[name] = value;
        }
    }

    /// <summary>The notify switch, read from the machine's choices; one that does not read errs toward saying it, as the shell's notifier does.</summary>
    private static bool Notifying(string configPath)
    {
        try
        {
            return DriverConfig.Load(configPath).Notify;
        }
        catch (DriverConfigUnreadableException)
        {
            return true;
        }
    }
}
