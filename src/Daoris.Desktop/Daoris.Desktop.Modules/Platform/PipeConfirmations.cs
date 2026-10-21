using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// The terminal that asks, as the operating system names the pipe's client (RPC1c): its process's name and id, never a word the
/// asker wrote. Null where the system would not say.
/// </summary>
public sealed record PipeAsker(string? Process, int? Pid);

/// <summary>
/// One value a press carries, as the card shows it (RPC1c, as PERSONDOOR1b's card shows a body's): <see cref="Name"/> its JSON
/// pointer (<c>/ids/0</c>; <c>~0</c> and <c>~1</c> escape a name's <c>~</c> and <c>/</c>), <see cref="Type"/> its JSON type, so a
/// string <c>"3"</c> and a number <c>3</c> read apart, and <see cref="Value"/> a string as it is and anything else as its JSON.
/// </summary>
public sealed record PipeConfirmationField(string Name, string Type, string Value);

/// <summary>
/// A person's press a terminal sent over the pipe, waiting for the window (RPC1c): shown by its module and route, the names the
/// census and the bridge give it, every value its payload carries, and the terminal that asks. Never its request's id or key.
/// <see cref="ShownAt"/> is when a window first said its card was in front of the person (CONFIRMSEEN1), null until then;
/// <see cref="Expires"/> is the end of its wait to be found until then, and of its two minutes from then.
/// </summary>
public sealed record PipeConfirmation(
    string Id, string Module, string Route, IReadOnlyList<PipeConfirmationField> Fields, PipeAsker Asker,
    DateTimeOffset Asked, DateTimeOffset Expires, DateTimeOffset? ShownAt = null);

/// <summary>What waits, told whole at each change, numbered so a window keeps the newest telling (as <c>UNDER_WAY</c> is).</summary>
public sealed record PipeConfirmationsNow(long Seq, IReadOnlyList<PipeConfirmation> Waiting);

/// <summary>How a confirmation ended: only <see cref="Confirmed"/> runs its press.</summary>
public enum PipeConfirmationEnd
{
    /// <summary>The person pressed <b>Confirm</b> in the window within its two minutes.</summary>
    Confirmed,

    /// <summary>The person pressed <b>Refuse</b>.</summary>
    Refused,

    /// <summary>
    /// Its two minutes from when it was shown passed with no answer, or no window showed it within its wait to be found; or an answer,
    /// or a word that its card is shown, came at or past its time (NOTIFY2a).
    /// </summary>
    Expired,

    /// <summary>Taken back: its terminal cancelled it, or the instance began to close.</summary>
    Withdrawn,
}

/// <summary>
/// The window's confirmations of the person's presses a terminal sent (RPC1c, D156 point 4, the person-door design §4.2, the
/// terminal RPC design §4.2): held in memory, never in the store, at most <see cref="Bound"/> waiting at once, each for its two
/// minutes, each answered once. The pipe's connection is what a confirmation answers, so there is no secret and no second
/// request: the press the terminal sent runs on <b>Confirm</b>, and on nothing else.
/// </summary>
/// <remarks>
/// <para><b>Told to every window</b>: each change raises <c>DAORIS</c> <see cref="Event"/> with what waits, whole, as
/// <c>UNDER_WAY</c> is told, and the bridge's <c>PERSON_CONFIRMATIONS</c> reads it for a window that opens mid-wait.</para>
///
/// <para><b>Said by codes</b> (design §5.1): <see cref="LogEvent"/> with the state (<c>asked</c>, <c>shown</c>, <c>confirmed</c>,
/// <c>refused</c>, <c>expired</c>, <c>withdrawn</c>), the route and the terminal's door. Never a value, an id or the asker.</para>
///
/// <para><b>Its two minutes are counted from when it is shown</b> (CONFIRMSEEN1): five presses once waited for a card nobody was
/// looking at and expired, logged <c>asked</c> then <c>expired</c> and nothing said where the person looks. A press is asked with
/// a wait to be found (<see cref="FindWithin"/>), in which the person is told where they look: a row of <i>What needs you</i>, which
/// the badge counts (<see cref="NeedsAPerson"/>), and <see cref="AttentionType"/>, the balloon while no window of this application
/// is in front. The window says when its card is in front of the person (<see cref="Show"/>, the bridge's
/// <c>PERSON_CONFIRM_SHOWN</c>); from then its two minutes count, once.</para>
///
/// <para><b>The bound it states is the bound kept</b> (NOTIFY2a, D196 point 4, the notification design §3.4): an answer, or a word that
/// its card is shown, at or past its time ends it <see cref="PipeConfirmationEnd.Expired"/> and runs nothing, the clock read first,
/// whether or not its timer has fired; a pool under load runs the timer late.</para>
///
/// <para><b>It is not an approval surface for agents</b> (design §4.2, D52): the card's default is no, and a session that asks
/// is answered only by the person, in the window.</para>
/// </remarks>
public sealed class PipeConfirmations : IDisposable
{
    /// <summary>The event that tells every window what waits.</summary>
    public const string Event = "CONFIRMATIONS";

    /// <summary>The machine log's event for a confirmation's step, the HTTP host's own name for it.</summary>
    public const string LogEvent = "person.confirmation";

    /// <summary>
    /// The event the application's notifier turns into a balloon while nobody looks at its windows (CONFIRMSEEN1): the one a
    /// session's attention is told by, in its shape, of the kind <see cref="AttentionKind"/>.
    /// </summary>
    public const string AttentionType = "SESSION_ATTENTION";

    /// <summary>The kind of attention a press waiting for the window is told as.</summary>
    public const string AttentionKind = "Confirming";

    /// <summary>The most values a card shows: past it, a press is refused rather than shown in part (the service's own bound).</summary>
    public const int FieldLimit = 256;

    private readonly IEventBus? _bus;
    private readonly MachineLog _log;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _within;
    private readonly TimeSpan _find;
    private readonly Lock _gate = new();
    private readonly List<Held> _waiting = [];
    private long _seq;

    /// <param name="bus">Where every window is told; none tells nobody.</param>
    /// <param name="log">The machine log.</param>
    /// <param name="clock">The clock the two minutes are read by: the system's, or a test's.</param>
    /// <param name="bound">How many may wait at once (the design's five).</param>
    /// <param name="within">How long each waits once shown (the design's two minutes).</param>
    /// <param name="find">How long each waits to be shown (<see cref="FindWithin"/>), never less than <paramref name="within"/>.</param>
    public PipeConfirmations(
        IEventBus? bus, MachineLog log, TimeProvider? clock = null, int bound = 5, TimeSpan? within = null, TimeSpan? find = null)
    {
        _bus = bus;
        _log = log;
        _clock = clock ?? TimeProvider.System;
        Bound = bound;
        _within = within ?? TimeSpan.FromMinutes(2);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_within, TimeSpan.Zero, nameof(within));
        _find = find ?? FindWithin;
        ArgumentOutOfRangeException.ThrowIfLessThan(_find, _within, nameof(find));
    }

    /// <summary>
    /// How long a press waits for a window to show it before it expires unseen (CONFIRMSEEN1): ten minutes, long enough for a person
    /// told by the balloon and the badge to come to the window, and well inside the terminal's own wait for its answer (thirty).
    /// </summary>
    public static readonly TimeSpan FindWithin = TimeSpan.FromMinutes(10);

    /// <summary>How many may wait at once.</summary>
    public int Bound { get; }

    /// <summary>What waits now, oldest first.</summary>
    public PipeConfirmationsNow Now()
    {
        lock (_gate) return Telling();
    }

    /// <summary>
    /// Show the window <paramref name="route"/> of <paramref name="module"/> with <paramref name="fields"/>, asked by
    /// <paramref name="asker"/>: the confirmation, and how it ends; null where <see cref="Bound"/> already wait, and nothing is asked.
    /// </summary>
    public (PipeConfirmation Shown, Task<PipeConfirmationEnd> Ended)? Ask(
        string module, string route, IReadOnlyList<PipeConfirmationField> fields, PipeAsker asker)
    {
        Held held;
        PipeConfirmationsNow told;
        lock (_gate)
        {
            if (_waiting.Count >= Bound) return null;
            var asked = _clock.GetUtcNow();
            held = new Held(new PipeConfirmation(Guid.NewGuid().ToString("N"), module, route, fields, asker, asked, asked + _find));
            // Its timer, its ask's line and its place are made together under the lock, so a confirmation anyone can see already has its
            // wait to be found counting from its ask, and its end can be said only after its ask. The timer's first firing is that wait
            // away (never zero), so it finds the confirmation held.
            var id = held.Shown.Id;
            held.Timer = _clock.CreateTimer(_ => Expire(id), null, _find, Timeout.InfiniteTimeSpan);
            Write("asked", held.Shown);
            _waiting.Add(held);
            _seq++;
            told = Telling();
        }

        Tell(told);
        Attend(held.Shown);
        return (held.Shown, held.Ended.Task);
    }

    /// <summary>
    /// A window says <paramref name="id"/>'s card is in front of the person (CONFIRMSEEN1): the first time, its two minutes count
    /// from now, logged <c>shown</c> and told to every window; true where it waits, shown now or before, false where it no longer waits.
    /// Said at or past its time, its ten minutes or its two, it ends expired and is false (NOTIFY2a): a word never gives it more time.
    /// </summary>
    public bool Show(string id)
    {
        Held? held;
        PipeConfirmationsNow told;
        bool over;
        lock (_gate)
        {
            held = _waiting.Find(each => each.Shown.Id == id);
            if (held is null) return false;
            var now = _clock.GetUtcNow();
            // NOTIFY2a (D196 point 4): the clock read first, before the early return for a card shown before. A late timer had not
            // ended it, so a word past its ten minutes gave it two more, and one past a shown card's two found it still waiting.
            over = now >= held.Shown.Expires;
            if (over)
            {
                told = Take(held);
            }
            else
            {
                if (held.Shown.ShownAt is not null) return true;
                // Never later than its wait to be found would have ended: shown in its last seconds, it still has its two minutes.
                var shown = held.Shown = held.Shown with { ShownAt = now, Expires = now + _within };
                held.Timer?.Change(_within, Timeout.InfiniteTimeSpan);
                Write("shown", shown);
                _seq++;
                told = Telling();
            }
        }

        if (over)
        {
            Settle(held, PipeConfirmationEnd.Expired, told);
            return false;
        }

        Tell(told);
        return true;
    }

    /// <summary>
    /// The person's answer in the window: true where it was waiting and is now answered, false where it no longer waits. One at or
    /// past its time ends it expired, and is false (NOTIFY2a): the bound the card states is the bound kept, whenever the timer fires.
    /// </summary>
    public bool Answer(string id, bool confirm) => End(id, confirm ? PipeConfirmationEnd.Confirmed : PipeConfirmationEnd.Refused);

    /// <summary>Take one back (its terminal cancelled it): true where it was waiting.</summary>
    public bool Withdraw(string id) => End(id, PipeConfirmationEnd.Withdrawn);

    /// <summary>Take back every one that waits: the instance closes, and none of them runs.</summary>
    public void WithdrawAll()
    {
        string[] ids;
        lock (_gate) ids = [.. _waiting.Select(each => each.Shown.Id)];
        foreach (var id in ids) End(id, PipeConfirmationEnd.Withdrawn);
    }

    public void Dispose() => WithdrawAll();

    /// <summary>
    /// Every value <paramref name="payload"/> carries, by its JSON pointer, or the sentence that refuses it: a name said twice in
    /// one object as the bridge reads names (ignoring case), which the card would show once and the handler might read as the other,
    /// or more than <see cref="FieldLimit"/> values.
    /// </summary>
    public static (IReadOnlyList<PipeConfirmationField>? Fields, string? Refusal) FieldsOf(JsonElement? payload)
    {
        var into = new List<PipeConfirmationField>();
        if (payload is not { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } given) return (into, null);
        var refusal = Walk(given, "", into);
        return refusal is null ? (into, null) : (null, refusal);
    }

    private static string? Walk(JsonElement element, string pointer, List<PipeConfirmationField> into)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var any = false;
                foreach (var property in element.EnumerateObject())
                {
                    any = true;
                    var at = $"{pointer}/{Escape(property.Name)}";
                    if (!named.Add(property.Name)) return $"`{at}` is named twice, as the press reads names, and the card would show one of them.";
                    if (Walk(property.Value, at, into) is { } refusal) return refusal;
                }

                return any || pointer.Length == 0 ? null : Leaf(element, pointer, into);
            case JsonValueKind.Array:
                var count = 0;
                foreach (var value in element.EnumerateArray())
                {
                    if (Walk(value, $"{pointer}/{count++}", into) is { } refusal) return refusal;
                }

                return count > 0 ? null : Leaf(element, pointer, into);
            default:
                return Leaf(element, pointer, into);
        }
    }

    private static string? Leaf(JsonElement element, string pointer, List<PipeConfirmationField> into)
    {
        if (into.Count >= FieldLimit) return $"the press carries more than {FieldLimit} values, more than the window shows.";
        into.Add(new PipeConfirmationField(
            pointer.Length == 0 ? "/" : pointer,
            element.ValueKind switch
            {
                JsonValueKind.String => "string",
                JsonValueKind.Number => "number",
                JsonValueKind.True or JsonValueKind.False => "boolean",
                JsonValueKind.Null => "null",
                JsonValueKind.Array => "array",
                _ => "object",
            },
            element.ValueKind == JsonValueKind.String ? element.GetString()! : element.GetRawText()));
        return null;
    }

    // A JSON pointer's escapes (RFC 6901), as the HTTP host's card names a body's values: an empty name and one holding a slash are
    // each their own pointer (PERSONDOOR1b's review found a value whose empty name vanished from its path).
    private static string Escape(string name) => name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    /// <summary>The timer's firing: ends one whose time is over, never one shown since the firing was due, whose two minutes run.</summary>
    private void Expire(string id) => End(id, PipeConfirmationEnd.Expired, due: true);

    /// <summary>
    /// Ends one that waits, once: whichever of the person, the clock and the terminal comes first. True where it ended as
    /// <paramref name="end"/>; an answer at or past its time ends it <see cref="PipeConfirmationEnd.Expired"/> and is false.
    /// </summary>
    private bool End(string id, PipeConfirmationEnd end, bool due = false)
    {
        Held? held;
        PipeConfirmationsNow told;
        var ended = end;
        lock (_gate)
        {
            held = _waiting.Find(each => each.Shown.Id == id);
            if (held is null) return false;
            // The clock read for every answer as for the timer (NOTIFY2a, D196 point 4): only the timer's firing read it, so a Confirm
            // taken while a late timer had not fired ran past the bound the card states. A terminal's withdrawal ends it as it says.
            var over = _clock.GetUtcNow() >= held.Shown.Expires;
            if (due && !over) return false;
            if (over && end is PipeConfirmationEnd.Confirmed or PipeConfirmationEnd.Refused) ended = PipeConfirmationEnd.Expired;
            told = Take(held);
        }

        Settle(held, ended, told);
        return ended == end;
    }

    /// <summary>Takes <paramref name="held"/> from what waits, under the lock: what every window is told next.</summary>
    private PipeConfirmationsNow Take(Held held)
    {
        _waiting.Remove(held);
        _seq++;
        return Telling();
    }

    /// <summary>After the lock: its timer stopped, its line written, every window told, and its press answered by how it ended.</summary>
    private void Settle(Held held, PipeConfirmationEnd end, PipeConfirmationsNow told)
    {
        held.Timer?.Dispose();
        Write(end.ToString().ToLowerInvariant(), held.Shown);
        Tell(told);
        held.Ended.TrySetResult(end);
    }

    private PipeConfirmationsNow Telling() => new(_seq, [.. _waiting.Select(each => each.Shown)]);

    private void Tell(PipeConfirmationsNow told) => _bus?.Emit(StatePublisher.Module, Event, told);

    /// <summary>
    /// The person told where they look (CONFIRMSEEN1), in the shape the application's notifier reads: a balloon while none of its
    /// windows is in front, quiet while one is, whose card already shows it. The terminal as the system names it, never the route's
    /// constant (NOTIFY2a: a person reading 中文 met <c>SESSION_INPUT</c> in English), a value or a word the asker wrote; no session,
    /// so the balloon's click brings the window forward alone. <c>Id</c>, a field added, is the press's own, which the window's
    /// announcement keys on (the notification design §3.3), so an event told twice is said once.
    /// </summary>
    private void Attend(PipeConfirmation confirmation) => _bus?.Emit(StatePublisher.Module, AttentionType, new
    {
        Kind = AttentionKind,
        Id = confirmation.Id,
        Session = (string?)null,
        Headline = "A terminal's press waits for you in Daoris's window",
        Detail = $"Sent by {Named(confirmation.Asker)}. Open Daoris to confirm or refuse it.",
    });

    private static string Named(PipeAsker asker) => asker switch
    {
        { Process: { Length: > 0 } process, Pid: { } pid } => $"{process} ({pid})",
        { Pid: { } pid } => $"process {pid}",
        _ => "a terminal",
    };

    // Codes only (design §5.1): what happened, the route, and the door it came in by.
    private void Write(string state, PipeConfirmation confirmation) =>
        _log.Info(LogEvent, ("state", state), ("route", $"{confirmation.Module}.{confirmation.Route}"), ("door", PipeDoor.Terminal));

    private sealed class Held(PipeConfirmation shown)
    {
        /// <summary>The confirmation as the windows see it; replaced, under the lock, when it is first shown.</summary>
        public PipeConfirmation Shown { get; set; } = shown;

        public TaskCompletionSource<PipeConfirmationEnd> Ended { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ITimer? Timer { get; set; }
    }
}
