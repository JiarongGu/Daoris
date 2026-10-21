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
/// state?, route?, act? }</c>, picked to <see cref="Fields"/>. No field says what to announce: an arrival is an event
/// (<see cref="ArrivedType"/>). A conversation's answer is the kind <c>answered</c> (NOTIFY2g), which the page lists in
/// <i>Messages</i>.</para>
///
/// <para><b><i>Messages</i>, keyed by ask</b> (NOTIFY2g, <see cref="Messages"/>, judged by <see cref="AskMessages"/>): every live ask's
/// channel and every teammate's, read through the service as the person reads them, again only where the change stream's channel or
/// asks moved, a press declared to the asks, or <see cref="ChannelsDeadline"/> passed; an urgent message to everyone said once as
/// <see cref="AskMessages.ArrivedType"/>, while the switch is on.</para>
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
/// is said already by its own <c>SESSION_ATTENTION</c>, so it is not said again; a press's unanswered end has no other voice, and is.
/// With the headless loop driving, this re-read is the only way its notice reaches the windows.</para>
///
/// <para><b>Desktop-only</b>: machine-local, never on a wire to a shared host (D47, D182 point 2).</para>
/// </remarks>
public sealed class NoticesStateSlice
{
    /// <summary>The slice's name, in the page's words.</summary>
    public const string Name = "notices";

    /// <summary>Its partition of notices, keyed by id.</summary>
    public const string News = "news";

    /// <summary>Its partition of <i>Messages</i>, keyed by ask (NOTIFY2g, <see cref="AskMessages"/>).</summary>
    public const string Messages = "messages";

    /// <summary>
    /// How long the channels' read stands with nothing heard (NOTIFY2g): a move the slice did not hear (a mark moved at the service's
    /// door by no press here) is read within it.
    /// </summary>
    public static readonly TimeSpan ChannelsDeadline = TimeSpan.FromSeconds(30);

    /// <summary>The change stream's families that move an ask's channel as the person reads it, or which asks are live.</summary>
    public static readonly IReadOnlyList<string> ChannelFamilies = ["channel", "asks"];

    /// <summary>The event a notice's arrival is said by, its fields the partition's.</summary>
    public const string ArrivedType = "NOTICE";

    /// <summary>The fields a notice is pushed with: the page's <c>Notice</c> type, which its test reads.</summary>
    public static readonly IReadOnlyList<string> Fields =
        ["id", "kind", "at", "read", "workspace", "session", "quest", "repository", "state", "route", "act"];

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
    private readonly Func<CancellationToken, Task<IReadOnlyList<AskChannel>?>>? _channels;
    private readonly AskMessages _messages = new();
    private HashSet<string>? _known;
    private JsonObject? _asks;
    private DateTimeOffset? _asksRead;
    private int _asksDue = 1;
    private bool _asksFailing;

    /// <param name="home">The home whose record is read and written.</param>
    /// <param name="bus">Where the slice pushes and an arrival is said.</param>
    /// <param name="log">The machine log, where a refused write and a failed judgement are said.</param>
    /// <param name="clock">The clock a notice's moment and its week are read by: the system's, or a test's.</param>
    /// <param name="notify">Whether the person is told (the notify switch), read at each arrival; told where none is given.</param>
    /// <param name="channels">
    /// Every live ask's channel and every teammate's, as the person reads them (NOTIFY2g); null where the service does not answer yet,
    /// which keeps what was pushed. None given, the slice has no <see cref="Messages"/> partition.
    /// </param>
    /// <param name="hears">Where the channels are heard moving (<see cref="ChannelsMoved"/>), beside the record's own wakes.</param>
    public NoticesStateSlice(
        string home, IEventBus bus, MachineLog? log = null, TimeProvider? clock = null, Func<bool>? notify = null,
        Func<CancellationToken, Task<IReadOnlyList<AskChannel>?>>? channels = null, IReadOnlyList<Func<Action, IDisposable>>? hears = null)
    {
        _home = home;
        _bus = bus;
        _log = log;
        _clock = clock ?? TimeProvider.System;
        _notify = notify ?? (() => true);
        _channels = channels;
        Slice = new(Name, bus, JudgeAsync)
        {
            Keyed = true,
            Hears = hears ?? [],
            Failed = error => log?.Warn("state.slice", ("slice", Name), ("message", error.Message)),
        };
    }

    /// <summary>
    /// The loop's own: its home, its log, its notify switch read from the machine's choices at each arrival, and the channels read
    /// through its service, heard moving by the change stream's <see cref="ChannelFamilies"/>, a press declared to the asks (a post, a
    /// mark) and the service coming up.
    /// </summary>
    public static NoticesStateSlice Of(DriverLoop loop, IEventBus bus)
    {
        NoticesStateSlice? slice = null;
        slice = new(loop.Home, bus, loop.Log, notify: () => Notifying(loop.ConfigPath), channels: ct => ChannelsAsync(loop, ct), hears:
        [
            wake => QuestsStateSlice.HearLedger(loop, ChannelFamilies, () => slice!.ChannelsMoved()),
            wake => bus.Subscribe(StateSlice.Module, StatePublisher.Type, message =>
            {
                if (message.Payload is StateChanged changed && changed.Scopes.Contains("asks")) slice!.ChannelsMoved();
                return Task.CompletedTask;
            }),
            wake => bus.Subscribe(StateSlice.Module, "DRIVER_READY", _ =>
            {
                slice!.ChannelsMoved();
                return Task.CompletedTask;
            }),
        ]);
        return slice;
    }

    /// <summary>
    /// Every live ask's channel and every teammate's, as the person reads them through <paramref name="loop"/>'s service: null until it
    /// answers. An ask whose channel the host does not hold is passed over.
    /// </summary>
    private static async Task<IReadOnlyList<AskChannel>?> ChannelsAsync(DriverLoop loop, CancellationToken ct)
    {
        if (loop.Service is not { } service) return null;
        var channels = new List<AskChannel>();
        foreach (var ask in await service.AsksAsync(ct).ConfigureAwait(false))
        {
            if (await service.ChannelAsync(ask.Id, null, ct).ConfigureAwait(false) is { } view) channels.Add(new(ask.Id, ask.Workspace, false, view));
        }

        foreach (var ask in await service.ElsewhereAsksAsync(ct).ConfigureAwait(false))
        {
            if (await service.ChannelAsync(ask.Id, null, ct).ConfigureAwait(false) is { } view) channels.Add(new(ask.Id, ask.Workspace, true, view));
        }

        return channels;
    }

    /// <summary>An ask's channel moved, or which asks are live: the next judgement reads the channels again, and is asked for now.</summary>
    public void ChannelsMoved()
    {
        Interlocked.Exchange(ref _asksDue, 1);
        Slice.Wake();
    }

    /// <summary>
    /// A conversation's answer kept as a notice (NOTIFY2g, §2.4), <paramref name="item"/> an <see cref="AttentionKind.Answered"/>, whatever
    /// the switch says, renewed unread at each later answer. Said already by its own <c>SESSION_ATTENTION</c> where the switch is on, so
    /// never said again as an arrival.
    /// </summary>
    public void Answered(AttentionEvent item)
    {
        if (Notices.Of(item, _clock.GetUtcNow()) is not { } notice) return;
        Said(notice.Id);
        if (!Written(Notices.Renew(_home, notice, _clock.GetUtcNow())))
        {
            lock (_gate) _said.Remove(notice.Id);
        }
    }

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
    /// pushed stands. Each id not read before is an arrival, said once, but on the first read. Beside it <i>Messages</i>
    /// (<see cref="Messages"/>), its channels read again only where they moved or past <see cref="ChannelsDeadline"/>.
    /// </summary>
    public async Task<IReadOnlyList<SliceJudged>?> JudgeAsync(CancellationToken ct = default)
    {
        var news = Judge();
        if (news is null) return null;

        var asks = await AsksAsync(ct).ConfigureAwait(false);
        return asks is null ? news : [.. news, new SliceJudged(Messages, asks)];
    }

    /// <summary><i>Done and failed</i>'s partition alone, judged from the record (<see cref="JudgeAsync"/>'s first half).</summary>
    public IReadOnlyList<SliceJudged>? Judge()
    {
        var read = Notices.Read(_home, _clock.GetUtcNow());
        if (read is null) return null;

        var part = new JsonObject();
        foreach (var notice in read) part[notice.Id] = Picked(notice);
        foreach (var notice in Arrived(read)) _ = _bus.EmitAsync(StateSlice.Module, ArrivedType, Picked(notice));
        return [new SliceJudged(News, part)];
    }

    /// <summary>
    /// <i>Messages</i> as last read, read again where the channels moved or the deadline passed: null while none was read, which names no
    /// partition. A read the service did not answer, or that failed, keeps the last one, said once in the log, and is asked again at the
    /// next judgement. Each urgent message to everyone that arrived is said, while the switch is on.
    /// </summary>
    private async Task<JsonObject?> AsksAsync(CancellationToken ct)
    {
        if (_channels is null) return null;
        var now = _clock.GetUtcNow();
        if (Interlocked.Exchange(ref _asksDue, 0) == 0 && _asksRead is { } read && now - read < ChannelsDeadline) return _asks;

        IReadOnlyList<AskChannel>? channels;
        try
        {
            channels = await _channels(ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // Said once until a read succeeds again, as a slice's own failure is.
            if (!_asksFailing) _log?.Warn("state.slice", ("slice", Name), ("partition", Messages), ("message", error.Message));
            _asksFailing = true;
            Interlocked.Exchange(ref _asksDue, 1);
            return _asks;
        }

        if (channels is null)
        {
            Interlocked.Exchange(ref _asksDue, 1);
            return _asks;
        }

        _asksFailing = false;
        var (partition, arrived) = _messages.Judge(channels);
        _asks = partition;
        _asksRead = now;
        if (arrived.Count > 0 && _notify())
        {
            foreach (var message in arrived) _ = _bus.EmitAsync(StateSlice.Module, AskMessages.ArrivedType, message);
        }

        return partition;
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
                if (known.Contains(notice.Id) || _said.Remove(notice.Id)) continue;
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
