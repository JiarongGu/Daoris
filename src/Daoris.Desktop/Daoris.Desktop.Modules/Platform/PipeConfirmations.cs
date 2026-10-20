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
/// </summary>
public sealed record PipeConfirmation(
    string Id, string Module, string Route, IReadOnlyList<PipeConfirmationField> Fields, PipeAsker Asker,
    DateTimeOffset Asked, DateTimeOffset Expires);

/// <summary>What waits, told whole at each change, numbered so a window keeps the newest telling (as <c>UNDER_WAY</c> is).</summary>
public sealed record PipeConfirmationsNow(long Seq, IReadOnlyList<PipeConfirmation> Waiting);

/// <summary>How a confirmation ended: only <see cref="Confirmed"/> runs its press.</summary>
public enum PipeConfirmationEnd
{
    /// <summary>The person pressed <b>Confirm</b> in the window within its two minutes.</summary>
    Confirmed,

    /// <summary>The person pressed <b>Refuse</b>.</summary>
    Refused,

    /// <summary>Its two minutes passed with no answer.</summary>
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
/// <para><b>Said by codes</b> (design §5.1): <see cref="LogEvent"/> with the state (<c>asked</c>, <c>confirmed</c>, <c>refused</c>,
/// <c>expired</c>, <c>withdrawn</c>), the route and the terminal's door. Never a value, an id or the asker.</para>
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

    /// <summary>The most values a card shows: past it, a press is refused rather than shown in part (the service's own bound).</summary>
    public const int FieldLimit = 256;

    private readonly IEventBus? _bus;
    private readonly MachineLog _log;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _within;
    private readonly Lock _gate = new();
    private readonly List<Held> _waiting = [];
    private long _seq;

    /// <param name="bus">Where every window is told; none tells nobody.</param>
    /// <param name="log">The machine log.</param>
    /// <param name="clock">The clock the two minutes are read by: the system's, or a test's.</param>
    /// <param name="bound">How many may wait at once (the design's five).</param>
    /// <param name="within">How long each waits (the design's two minutes).</param>
    public PipeConfirmations(IEventBus? bus, MachineLog log, TimeProvider? clock = null, int bound = 5, TimeSpan? within = null)
    {
        _bus = bus;
        _log = log;
        _clock = clock ?? TimeProvider.System;
        Bound = bound;
        _within = within ?? TimeSpan.FromMinutes(2);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_within, TimeSpan.Zero, nameof(within));
    }

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
            held = new Held(new PipeConfirmation(Guid.NewGuid().ToString("N"), module, route, fields, asker, asked, asked + _within));
            // Its timer, its ask's line and its place are made together under the lock, so a confirmation anyone can see already has its
            // two minutes counting from its ask, and its end can be said only after its ask. The timer's first firing is two minutes
            // away (the bound is never zero), so it finds the confirmation held.
            held.Timer = _clock.CreateTimer(_ => End(held.Shown.Id, PipeConfirmationEnd.Expired), null, _within, Timeout.InfiniteTimeSpan);
            Write("asked", held.Shown);
            _waiting.Add(held);
            _seq++;
            told = Telling();
        }

        Tell(told);
        return (held.Shown, held.Ended.Task);
    }

    /// <summary>The person's answer in the window: true where it was waiting and is now answered, false where it no longer waits.</summary>
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

    /// <summary>Ends one that waits, once: whichever of the person, the clock and the terminal comes first.</summary>
    private bool End(string id, PipeConfirmationEnd end)
    {
        Held? held;
        PipeConfirmationsNow told;
        lock (_gate)
        {
            held = _waiting.Find(each => each.Shown.Id == id);
            if (held is null) return false;
            _waiting.Remove(held);
            _seq++;
            told = Telling();
        }

        held.Timer?.Dispose();
        Write(end.ToString().ToLowerInvariant(), held.Shown);
        Tell(told);
        held.Ended.TrySetResult(end);
        return true;
    }

    private PipeConfirmationsNow Telling() => new(_seq, [.. _waiting.Select(each => each.Shown)]);

    private void Tell(PipeConfirmationsNow told) => _bus?.Emit(StatePublisher.Module, Event, told);

    // Codes only (design §5.1): what happened, the route, and the door it came in by.
    private void Write(string state, PipeConfirmation confirmation) =>
        _log.Info(LogEvent, ("state", state), ("route", $"{confirmation.Module}.{confirmation.Route}"), ("door", PipeDoor.Terminal));

    private sealed class Held(PipeConfirmation shown)
    {
        public PipeConfirmation Shown { get; } = shown;

        public TaskCompletionSource<PipeConfirmationEnd> Ended { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ITimer? Timer { get; set; }
    }
}
