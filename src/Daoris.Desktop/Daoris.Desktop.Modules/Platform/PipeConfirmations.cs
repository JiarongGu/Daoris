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
/// <see cref="ShownAt"/> is when a window first said its card was in front of the person (CONFIRMSEEN1), null until then; it moves no
/// clock. <see cref="Expires"/> is always null (CONFIRMWAIT1a-i): a terminal's press waits until the person answers, and the field
/// stays for the page's card, which draws the service door's presses by the same shape. <see cref="Named"/> is the values its
/// sentence names by role (NOTIFY2b, <see cref="PipeWording"/>), a field added beside <see cref="Fields"/>, which still holds every
/// value. <see cref="WaitedOn"/> is whether a terminal still waits on its line for it (the confirm-wait design §8.1);
/// <see cref="Checking"/> that its Confirm is being judged against the grounds it was asked on (§5.2), its buttons replaced while it
/// is; <see cref="CheckWithin"/> the longest that judgement may take, in milliseconds, which the page's Confirm waits for (§5.2).
/// <see cref="Keyed"/> that it carries a key, so it is not kept across a restart (CONFIRMWAIT1a-ii, §7.1); <see cref="Restored"/> that it
/// was asked before Daoris restarted and waits again from its record, its <see cref="Asker"/> the record's word, never the system's (§8.1).
/// </summary>
public sealed record PipeConfirmation(
    string Id, string Module, string Route, IReadOnlyList<PipeConfirmationField> Fields, PipeAsker Asker,
    DateTimeOffset Asked, DateTimeOffset? Expires = null, DateTimeOffset? ShownAt = null, IReadOnlyList<PipeNamed>? Named = null,
    bool WaitedOn = true, bool Checking = false, int? CheckWithin = null, bool Keyed = false, bool Restored = false);

/// <summary>What waits, told whole at each change, numbered so a window keeps the newest telling (as <c>UNDER_WAY</c> is).</summary>
public sealed record PipeConfirmationsNow(long Seq, IReadOnlyList<PipeConfirmation> Waiting);

/// <summary>How a confirmation ended: only <see cref="Confirmed"/> runs its press.</summary>
public enum PipeConfirmationEnd
{
    /// <summary>The person pressed <b>Confirm</b> in the window, and every ground it was asked on still held (CONFIRMWAIT1a-i).</summary>
    Confirmed,

    /// <summary>The person pressed <b>Refuse</b>.</summary>
    Refused,

    /// <summary>The person pressed <b>Confirm</b>, and a ground it was asked on no longer held (the confirm-wait design §5): nothing ran.</summary>
    Changed,

    /// <summary>Taken back by its terminal: the pipe's <c>WITHDRAW</c>, or the kit's <c>CANCEL</c> from a terminal from before.</summary>
    Withdrawn,

    /// <summary>
    /// Not kept across a restart (CONFIRMWAIT1a-ii, the confirm-wait design §7.1, §7.2): a press carrying a key at the close, or a record
    /// the restore could not bring back. Nothing ran.
    /// </summary>
    Lost,
}

/// <summary>
/// What the pipe already made of a press it asks (CONFIRMWAIT1a-ii, the confirm-wait design §7.1): its id, minted before its record is
/// written; when it was asked and shown, where it is restored from its record; whether it carries a key; whether it is restored; and
/// whether a terminal waits on its line for it.
/// </summary>
public sealed record PipeAsAsked(
    string Id, DateTimeOffset? Asked = null, DateTimeOffset? ShownAt = null, bool Keyed = false, bool Restored = false, bool WaitedOn = true);

/// <summary>How a confirmation ended, and where a ground moved, which one and how (<see cref="PipeConfirmationEnd.Changed"/> alone).</summary>
public sealed record PipeEnding(PipeConfirmationEnd End, PipeGroundChange? Change = null);

/// <summary>What the grounds read at the Confirm said (the confirm-wait design §5.2).</summary>
public enum PipeJudged
{
    /// <summary>Every ground the press was asked on holds: it runs.</summary>
    Holds,

    /// <summary>A ground moved: the press ends <see cref="PipeConfirmationEnd.Changed"/> and runs nothing.</summary>
    Changed,

    /// <summary>A ground could not be read, or not within its bound: the press waits on, its buttons given back.</summary>
    Unread,
}

/// <summary>The judgement of a Confirm, with the ground that moved where one did.</summary>
public sealed record PipeJudgement(PipeJudged Judged, PipeGroundChange? Change = null)
{
    public static PipeJudgement Holding { get; } = new(PipeJudged.Holds);
}

/// <summary>How the person's answer in the window was taken (the bridge's <c>PERSON_CONFIRM</c>).</summary>
public enum PipeAnswered
{
    /// <summary>Taken: refused, or confirmed with its grounds holding, and running.</summary>
    Answered,

    /// <summary>It no longer waits (answered in another window, taken back), or it is being judged already: nothing was taken.</summary>
    Gone,

    /// <summary>Confirmed, and a ground had moved: it ended <see cref="PipeConfirmationEnd.Changed"/> and nothing ran.</summary>
    Changed,

    /// <summary>Confirmed, and its grounds could not be read just now: it still waits.</summary>
    Unread,

    /// <summary>Confirmed while Daoris closes or an update drains it (CONFIRMWAIT1a-ii, §7.2): nothing was taken, and it still waits.</summary>
    Draining,

    /// <summary>
    /// Its ending could not be written to its record (CONFIRMWAIT1, the confirm-wait design §15.2 item 8): nothing was taken, nothing ran,
    /// and it still waits, its buttons back.
    /// </summary>
    Unrecorded,
}

/// <summary>The person's answer as taken, with the ground that moved where it was <see cref="PipeAnswered.Changed"/>.</summary>
public sealed record PipeAnswer(PipeAnswered Outcome, PipeGroundChange? Change = null);

/// <summary>How a withdrawal by a press's asker was taken (the confirm-wait design §4.3).</summary>
public enum PipeWithdrawal
{
    /// <summary>It waited, and is taken back: its card is gone and nothing ran.</summary>
    Withdrawn,

    /// <summary>The person confirmed it meanwhile and its grounds are being judged: nothing is taken back.</summary>
    Checking,

    /// <summary>It no longer waits.</summary>
    Gone,

    /// <summary>Its ending could not be written to its record (§15.2 item 8): nothing is taken back, and it still waits.</summary>
    Unrecorded,
}

/// <summary>
/// A place among the <see cref="PipeConfirmations.Bound"/> held for a press whose grounds are being read (the confirm-wait design
/// §5.2): counted as waiting, told to no window, made the card by <see cref="PipeConfirmations.Ask(PipePlace, string, string, IReadOnlyList{PipeConfirmationField}, PipeAsker, string?, Func{CancellationToken, Task{PipeJudgement}}?, TimeSpan?)"/>
/// or given back by <see cref="PipeConfirmations.Let"/>, once.
/// </summary>
public sealed class PipePlace
{
    internal PipePlace()
    {
    }

    internal bool Spent { get; set; }
}

/// <summary>
/// The window's confirmations of the person's presses a terminal sent (RPC1c, D156 point 4, the person-door design §4.2, the
/// terminal RPC design §4.2): held in memory, never in the store (the pipe keeps each in a record of its own, CONFIRMWAIT1a-ii), at most <see cref="Bound"/> waiting at once, each answered once.
/// The pipe's connection is what a confirmation answers, so there is no secret and no second request: the press the terminal sent
/// runs on <b>Confirm</b>, its grounds still holding, and on nothing else.
/// </summary>
/// <remarks>
/// <para><b>Told to every window</b>: each change raises <c>DAORIS</c> <see cref="Event"/> with what waits, whole, as
/// <c>UNDER_WAY</c> is told, and the bridge's <c>PERSON_CONFIRMATIONS</c> reads it for a window that opens mid-wait.</para>
///
/// <para><b>Said by codes</b> (design §5.1): <see cref="LogEvent"/> with the state (<c>asked</c>, <c>shown</c>, <c>checking</c>,
/// <c>unread</c>, <c>draining</c>, <c>confirmed</c>, <c>refused</c>, <c>changed</c>, <c>withdrawn</c>, <c>lost</c>), the route and the terminal's
/// door. Never a value, an id or the asker.</para>
///
/// <para><b>No clock ends one</b> (CONFIRMWAIT1a-i, D156's CONFIRMWAIT1 note, the confirm-wait design §2): a press waits until the
/// person confirms or refuses it or its asker withdraws it; a close ends none of them (CONFIRMWAIT1a-ii: the pipe keeps each in its record
/// across the restart, and a Confirm while Daoris closes or drains takes nothing), but one carrying a key, which is lost. CONFIRMSEEN1's two minutes from its showing and its
/// ten to be found went with the reason the owner gave on 2026-10-24: they ended presses the person had not refused. The window's word
/// that its card is shown (<see cref="Show"/>) is still kept, once, and moves nothing. The person is told where they look: a row of
/// <i>What needs you</i> (<see cref="NeedsAPerson"/>) and <see cref="AttentionType"/>, one per press, which the balloon coalesces
/// (<see cref="SessionBalloon"/>).</para>
///
/// <para><b>A Confirm is judged before it runs</b> (§5.2): the press's judge, handed at its ask, reads its grounds again outside the
/// lock while its card stays, <see cref="PipeConfirmation.Checking"/>; then, under the lock, it is taken and ended confirmed or changed,
/// or, unread, given back its buttons.</para>
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

    /// <summary>How many may wait at once where none is named (the confirm-wait design §9.2): <see cref="PipeLimits.Waiting"/>'s.</summary>
    public const int DefaultBound = 10;

    private readonly IEventBus? _bus;
    private readonly MachineLog _log;
    private readonly TimeProvider _clock;
    private readonly Lock _gate = new();
    private readonly List<Held> _waiting = [];
    private int _places;
    private long _seq;

    /// <param name="bus">Where every window is told; none tells nobody.</param>
    /// <param name="log">The machine log.</param>
    /// <param name="clock">The clock a press's ask and showing are written by: the system's, or a test's.</param>
    /// <param name="bound">How many may wait at once (the design's ten).</param>
    public PipeConfirmations(IEventBus? bus, MachineLog log, TimeProvider? clock = null, int bound = DefaultBound)
    {
        _bus = bus;
        _log = log;
        _clock = clock ?? TimeProvider.System;
        ArgumentOutOfRangeException.ThrowIfLessThan(bound, 1, nameof(bound));
        Bound = bound;
    }

    /// <summary>How many may wait at once.</summary>
    public int Bound { get; }

    /// <summary>
    /// Told each press the person is told the end of in <i>Done and failed</i>, after every window was told it ended (NOTIFY2f,
    /// CONFIRMWAIT1a-i): one its asker took back after a window showed it (<see cref="PipeConfirmationEnd.Withdrawn"/>; one nobody showed
    /// goes with no notice, the confirm-wait design §6), and one lost as Daoris closed (<see cref="PipeConfirmationEnd.Lost"/>,
    /// CONFIRMWAIT1a-ii). None keeps nothing.
    /// </summary>
    public Action<PipeConfirmation, PipeConfirmationEnd>? Noticed { get; set; }

    /// <summary>Whether Daoris is closing or an update drains it (CONFIRMWAIT1a-ii, §7.2): a Confirm then takes nothing, and the press waits.</summary>
    public Func<bool>? Holding { get; set; }

    /// <summary>
    /// Asked to write a press's ending to its record before anyone is told it or it runs (CONFIRMWAIT1, the confirm-wait design §15.2 item
    /// 8): <c>begun</c> for a Confirm whose grounds hold, <c>ended</c> with its answer for any other. False where it could not be written,
    /// and the press waits on, nothing taken; none writes nothing and takes every ending.
    /// </summary>
    public Func<PipeConfirmation, PipeEnding, bool>? Persisting { get; set; }

    /// <summary>Told a press's card the first time a window shows it (CONFIRMWAIT1a-ii): the pipe keeps <see cref="PipeConfirmation.ShownAt"/> in its record.</summary>
    public Action<PipeConfirmation>? ShownFirst { get; set; }

    /// <summary>
    /// Told once after a restart's restore (CONFIRMWAIT1a-ii, §7.2 step 7) how many records it could not bring back and how many a newer
    /// Daoris wrote, where either is any: one notice each.
    /// </summary>
    public Action<int, int>? Restarted { get; set; }

    /// <summary>What waits now, oldest first.</summary>
    public PipeConfirmationsNow Now()
    {
        lock (_gate) return Telling();
    }

    /// <summary>When the oldest press that waits was asked, or null where none waits.</summary>
    public DateTimeOffset? Oldest()
    {
        lock (_gate) return _waiting.Count == 0 ? null : _waiting.Min(each => each.Shown.Asked);
    }

    /// <summary>
    /// A place for one more press while its grounds are read (the confirm-wait design §5.2): null where <see cref="Bound"/> already
    /// wait or are held, and nothing is asked. Told to no window.
    /// </summary>
    public PipePlace? Hold()
    {
        lock (_gate)
        {
            if (_waiting.Count + _places >= Bound) return null;
            _places++;
            return new PipePlace();
        }
    }

    /// <summary>A place held and never asked on, given back (its grounds refused it, or did not read). Once; a spent place is ignored.</summary>
    public void Let(PipePlace place)
    {
        lock (_gate)
        {
            if (place.Spent) return;
            place.Spent = true;
            _places--;
        }
    }

    /// <summary>
    /// Show the window <paramref name="route"/> of <paramref name="module"/> with <paramref name="fields"/>, asked by
    /// <paramref name="asker"/>: the confirmation, and how it ends; null where <see cref="Bound"/> already wait, and nothing is asked.
    /// </summary>
    /// <param name="standing">
    /// Why the person's standing confirmation, on and naming this press, did not cover it (TERMTRUST1a, the terminal-trust design §7.2):
    /// written on the <c>asked</c> line as <c>standing</c>; null while it is off or names no such press, which writes no field.
    /// </param>
    /// <param name="judge">Reads the press's grounds again at a Confirm (§5.2); none runs it on the Confirm alone.</param>
    /// <param name="checkWithin">The longest <paramref name="judge"/> may take, told to the window as <see cref="PipeConfirmation.CheckWithin"/>.</param>
    public (PipeConfirmation Shown, Task<PipeEnding> Ended)? Ask(
        string module, string route, IReadOnlyList<PipeConfirmationField> fields, PipeAsker asker, string? standing = null,
        Func<CancellationToken, Task<PipeJudgement>>? judge = null, TimeSpan? checkWithin = null) =>
        Hold() is { } place ? Ask(place, module, route, fields, asker, standing, judge, checkWithin) : null;

    /// <summary>The press asked on the <paramref name="place"/> held for it (<see cref="Hold"/>): its card, told to every window.</summary>
    /// <param name="as">
    /// What the pipe already made of it (CONFIRMWAIT1a-ii, §7.1): its id, minted before its record is written; when it was asked and shown,
    /// for one restored from its record; whether it carries a key; whether it is restored, which raises no balloon of its own (§7.2 step 7),
    /// and whether a terminal waits on its line. None where the confirmations mint it.
    /// </param>
    public (PipeConfirmation Shown, Task<PipeEnding> Ended) Ask(
        PipePlace place, string module, string route, IReadOnlyList<PipeConfirmationField> fields, PipeAsker asker, string? standing = null,
        Func<CancellationToken, Task<PipeJudgement>>? judge = null, TimeSpan? checkWithin = null, PipeAsAsked? @as = null)
    {
        Held held;
        PipeConfirmationsNow told;
        lock (_gate)
        {
            if (place.Spent) throw new InvalidOperationException("a place is asked on once.");
            place.Spent = true;
            _places--;
            held = new Held(new PipeConfirmation(
                @as?.Id ?? Guid.NewGuid().ToString("N"), module, route, fields, asker, @as?.Asked ?? _clock.GetUtcNow(),
                ShownAt: @as?.ShownAt,
                Named: PipeWording.Named(module, route, fields),
                WaitedOn: @as?.WaitedOn ?? true,
                CheckWithin: checkWithin is { } within ? (int)Math.Ceiling(within.TotalMilliseconds) : null,
                Keyed: @as?.Keyed ?? false,
                Restored: @as?.Restored ?? false), judge);
            // Its ask's line and its place are made together under the lock, so its end can be said only after its ask.
            Write("asked", held.Shown, standing);
            _waiting.Add(held);
            _seq++;
            told = Telling();
        }

        Tell(told);
        // A press restored from its record raises no balloon of its own: the restart says one for all of them (§7.2 step 7).
        if (!held.Shown.Restored) Attend(held.Shown);
        return (held.Shown, held.Ended.Task);
    }

    /// <summary>
    /// A window says <paramref name="id"/>'s card is in front of the person (CONFIRMSEEN1): the first time, kept as when it was shown,
    /// logged <c>shown</c> and told to every window; true where it waits, shown now or before, false where it no longer waits. It moves
    /// no clock (CONFIRMWAIT1a-i): a press waits until the person answers.
    /// </summary>
    public bool Show(string id)
    {
        PipeConfirmationsNow told;
        PipeConfirmation shown;
        lock (_gate)
        {
            var held = _waiting.Find(each => each.Shown.Id == id);
            if (held is null) return false;
            if (held.Shown.ShownAt is not null) return true;
            held.Shown = held.Shown with { ShownAt = _clock.GetUtcNow() };
            Write("shown", held.Shown);
            _seq++;
            told = Telling();
            shown = held.Shown;
        }

        Tell(told);
        ShownFirst?.Invoke(shown);
        return true;
    }

    /// <summary>
    /// Whether a terminal still waits on its line for <paramref name="id"/> (the confirm-wait design §8.1): re-told to every window where
    /// it changed. False where it no longer waits.
    /// </summary>
    public bool WaitedOn(string id, bool waited)
    {
        PipeConfirmationsNow told;
        lock (_gate)
        {
            var held = _waiting.Find(each => each.Shown.Id == id);
            if (held is null) return false;
            if (held.Shown.WaitedOn == waited) return true;
            held.Shown = held.Shown with { WaitedOn = waited };
            _seq++;
            told = Telling();
        }

        Tell(told);
        return true;
    }

    /// <summary>
    /// The person's answer in the window (§5.2). A refusal ends it at once. A Confirm marks it checking, told to every window, reads its
    /// grounds again outside every lock, then ends it confirmed (it runs) or changed (nothing runs), or, unread, gives its buttons back.
    /// One no longer waiting, or already being judged, is <see cref="PipeAnswered.Gone"/>.
    /// </summary>
    public async Task<PipeAnswer> AnswerAsync(string id, bool confirm, CancellationToken cancellationToken = default)
    {
        Held? held;
        PipeConfirmationsNow told;
        lock (_gate)
        {
            held = _waiting.Find(each => each.Shown.Id == id);
            if (held is null || held.Shown.Checking || held.Settling) return new PipeAnswer(PipeAnswered.Gone);
            // CONFIRMWAIT1a-ii (§7.2): Daoris closing or an update draining takes no Confirm, and the press waits on, kept across the restart.
            if (confirm && Holding?.Invoke() == true) return new PipeAnswer(PipeAnswered.Draining);
            if (!confirm || held.Judge is null)
            {
                held.Settling = true;
                told = Telling();
            }
            else
            {
                held.Shown = held.Shown with { Checking = true };
                Write("checking", held.Shown);
                _seq++;
                told = Telling();
            }
        }

        if (!confirm || held.Judge is null)
        {
            return Finish(held, new PipeEnding(confirm ? PipeConfirmationEnd.Confirmed : PipeConfirmationEnd.Refused), new PipeAnswer(PipeAnswered.Answered));
        }

        Tell(told);
        PipeJudgement judged;
        try
        {
            judged = await held.Judge(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // A judge that throws read nothing it could compare: the press waits on, as one whose read failed (§5.2).
            judged = new PipeJudgement(PipeJudged.Unread);
        }

        bool draining;
        lock (_gate)
        {
            // A close may have lost it meanwhile (a press carrying a key): its terminal was told so, and this answer takes nothing.
            if (!_waiting.Contains(held)) return new PipeAnswer(PipeAnswered.Gone);
            // A drain begun while it was judged: nothing is taken, and it waits on, its buttons back (§7.2).
            draining = Holding?.Invoke() == true;
            if (judged.Judged == PipeJudged.Unread || draining)
            {
                held.Shown = held.Shown with { Checking = false };
                Write(draining ? "draining" : "unread", held.Shown);
                _seq++;
                told = Telling();
            }
            else
            {
                held.Settling = true;
            }
        }

        if (draining)
        {
            Tell(told);
            return new PipeAnswer(PipeAnswered.Draining);
        }

        return judged.Judged switch
        {
            PipeJudged.Unread => Told(told, new PipeAnswer(PipeAnswered.Unread)),
            PipeJudged.Changed => Finish(held, new PipeEnding(PipeConfirmationEnd.Changed, judged.Change), new PipeAnswer(PipeAnswered.Changed, judged.Change)),
            _ => Finish(held, new PipeEnding(PipeConfirmationEnd.Confirmed), new PipeAnswer(PipeAnswered.Answered)),
        };
    }

    private PipeAnswer Told(PipeConfirmationsNow told, PipeAnswer answer)
    {
        Tell(told);
        return answer;
    }

    /// <summary>
    /// A press marked settling, ended as <paramref name="ending"/> once its record says so (§15.2 item 8): taken, told and answered; or,
    /// where the record could not be written, nothing taken, its buttons back, <see cref="PipeAnswered.Unrecorded"/>.
    /// </summary>
    private PipeAnswer Finish(Held held, PipeEnding ending, PipeAnswer taken)
    {
        PipeConfirmationsNow told;
        if (!Persisted(held, ending))
        {
            lock (_gate)
            {
                held.Settling = false;
                held.Shown = held.Shown with { Checking = false };
                Write("unrecorded", held.Shown);
                _seq++;
                told = Telling();
            }

            Tell(told);
            return new PipeAnswer(PipeAnswered.Unrecorded);
        }

        lock (_gate)
        {
            held.Settling = false;
            // A close may have lost it meanwhile (a press carrying a key): its terminal was told so, and this answer takes nothing.
            if (!_waiting.Contains(held)) return new PipeAnswer(PipeAnswered.Gone);
            told = Take(held);
        }

        Settle(held, ending, told);
        return taken;
    }

    /// <summary>The ending written to the press's record before it is told or run; true where nothing is asked to write it.</summary>
    private bool Persisted(Held held, PipeEnding ending)
    {
        try
        {
            return Persisting?.Invoke(held.Shown, ending) ?? true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Take one back for its terminal (the pipe's <c>WITHDRAW</c>, the kit's <c>CANCEL</c>): only while it still waits; one the person
    /// confirmed, whose grounds are being judged, is <see cref="PipeWithdrawal.Checking"/> and nothing is taken back (§4.3).
    /// </summary>
    public PipeWithdrawal Withdraw(string id)
    {
        Held? held;
        PipeConfirmationsNow told;
        lock (_gate)
        {
            held = _waiting.Find(each => each.Shown.Id == id);
            if (held is null) return PipeWithdrawal.Gone;
            if (held.Shown.Checking || held.Settling) return PipeWithdrawal.Checking;
            held.Settling = true;
        }

        // CONFIRMWAIT1 (§15.2 item 8): its record says it was taken back before anyone is told; where it cannot, it waits on.
        var ending = new PipeEnding(PipeConfirmationEnd.Withdrawn);
        if (!Persisted(held, ending))
        {
            lock (_gate) held.Settling = false;
            return PipeWithdrawal.Unrecorded;
        }

        lock (_gate)
        {
            held.Settling = false;
            if (!_waiting.Contains(held)) return PipeWithdrawal.Gone;
            told = Take(held);
        }

        Settle(held, ending, told);
        return PipeWithdrawal.Withdrawn;
    }

    /// <summary>
    /// Nothing waits on a clock, and a close ends nothing here (CONFIRMWAIT1a-ii, the confirm-wait design §7.2): what waits is kept in its
    /// record across the restart, and the pipe answers each line waiting on one.
    /// </summary>
    public void Dispose()
    {
    }

    /// <summary>
    /// End <paramref name="id"/> <see cref="PipeConfirmationEnd.Lost"/>, whatever it waits on (CONFIRMWAIT1a-ii, §7.2): a press carrying a key
    /// as Daoris closes. False where it no longer waits.
    /// </summary>
    public bool Lose(string id)
    {
        Held? held;
        PipeConfirmationsNow told;
        lock (_gate)
        {
            held = _waiting.Find(each => each.Shown.Id == id);
            if (held is null) return false;
            told = Take(held);
        }

        // Written where it can be; where it cannot, the keyed record ends lost at the next start all the same (§15.2 item 8).
        var ending = new PipeEnding(PipeConfirmationEnd.Lost);
        Persisted(held, ending);
        Settle(held, ending, told);
        return true;
    }

    /// <summary>
    /// The restore's one word to the person (CONFIRMWAIT1a-ii, §7.2 step 7): one notice for the presses it could not bring back and one
    /// for the records a newer Daoris wrote (<see cref="Restarted"/>), and one balloon for those waiting again while no window is in front,
    /// naming how many (<see cref="RestoredKind"/>). Nothing where there is nothing to say.
    /// </summary>
    public void Restore(int waiting, int lost, int newer)
    {
        if (lost > 0 || newer > 0) Restarted?.Invoke(lost, newer);
        if (waiting <= 0) return;
        _bus?.Emit(StatePublisher.Module, AttentionType, new
        {
            Kind = RestoredKind,
            Count = waiting,
            Session = (string?)null,
            Headline = "A terminal's presses still wait for you in Daoris's window",
            Detail = $"{waiting} were asked before Daoris restarted. Open Daoris to confirm or refuse them.",
        });
    }

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

    /// <summary>Takes <paramref name="held"/> from what waits, under the lock: what every window is told next.</summary>
    private PipeConfirmationsNow Take(Held held)
    {
        _waiting.Remove(held);
        _seq++;
        return Telling();
    }

    /// <summary>After the lock: its line written, every window told, the person's notice kept, and its press answered by how it ended.</summary>
    private void Settle(Held held, PipeEnding ending, PipeConfirmationsNow told)
    {
        Write(ending.End.ToString().ToLowerInvariant(), held.Shown);
        Tell(told);
        // A card nobody was shown goes with its row and no notice (§6), so an asker that loops ask-then-withdraw floods nothing; one lost
        // as Daoris closes is said (§7.2).
        if (ending.End == PipeConfirmationEnd.Lost || (ending.End == PipeConfirmationEnd.Withdrawn && held.Shown.ShownAt is not null))
        {
            Noticed?.Invoke(held.Shown, ending.End);
        }

        held.Ended.TrySetResult(ending);
    }

    private PipeConfirmationsNow Telling() => new(_seq, [.. _waiting.Select(each => each.Shown)]);

    private void Tell(PipeConfirmationsNow told) => _bus?.Emit(StatePublisher.Module, Event, told);

    /// <summary>
    /// The person told where they look (CONFIRMSEEN1), in the shape the application's notifier reads: a balloon while none of its
    /// windows is in front, quiet while one is, whose card already shows it. The terminal as the system names it, never the route's
    /// constant (NOTIFY2a: a person reading 中文 met <c>SESSION_INPUT</c> in English), a value or a word the asker wrote; no session,
    /// so the balloon's click brings the window forward alone. <c>Id</c>, a field added, is the press's own, which the window's
    /// announcement keys on (the notification design §3.3), so an event told twice is said once. <c>Module</c> and <c>Route</c>, fields
    /// added (NOTIFY2d), name its act for the notifier, which says it in the window's words by the route's catalogue key and never says
    /// the route itself. One per press (the confirm-wait design §8.2): the balloon coalesces, never the event.
    /// </summary>
    private void Attend(PipeConfirmation confirmation) => _bus?.Emit(StatePublisher.Module, AttentionType, new
    {
        Kind = AttentionKind,
        Id = confirmation.Id,
        confirmation.Module,
        confirmation.Route,
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
    private void Write(string state, PipeConfirmation confirmation, string? standing = null)
    {
        if (standing is null)
        {
            _log.Info(LogEvent, ("state", state), ("route", $"{confirmation.Module}.{confirmation.Route}"), ("door", PipeDoor.Terminal));
        }
        else
        {
            // TERMTRUST1a (the terminal-trust design §7.2): a press the standing confirmation names that asked anyway, and why, by code.
            _log.Info(
                LogEvent, ("state", state), ("route", $"{confirmation.Module}.{confirmation.Route}"), ("door", PipeDoor.Terminal),
                ("standing", standing));
        }
    }

    /// <summary>The kind of the restart's one balloon (§7.2 step 7): <see cref="BalloonFacts.Count"/> presses wait again.</summary>
    public const string RestoredKind = "ConfirmingRestored";

    private sealed class Held(PipeConfirmation shown, Func<CancellationToken, Task<PipeJudgement>>? judge)
    {
        /// <summary>The confirmation as the windows see it; replaced, under the lock, as it is shown, waited on or checked.</summary>
        public PipeConfirmation Shown { get; set; } = shown;

        public Func<CancellationToken, Task<PipeJudgement>>? Judge { get; } = judge;

        /// <summary>That its ending is being written to its record (§15.2 item 8): a second answer meanwhile is gone, a withdrawal checking.</summary>
        public bool Settling { get; set; }

        public TaskCompletionSource<PipeEnding> Ended { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
