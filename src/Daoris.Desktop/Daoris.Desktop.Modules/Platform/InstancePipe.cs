using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>The pipe's bounds (the terminal RPC design §4.1): the design's choices, none measured; a test narrows them.</summary>
public sealed class PipeLimits
{
    /// <summary>
    /// How many connections at once: more than <see cref="Waiting"/> (CONFIRMWAIT1a-i, the confirm-wait design §9.2), since for a verb's
    /// first thirty minutes each press waiting on its line holds one, and the pipe's own requests (<c>RESULT</c>, <c>WITHDRAW</c>, a
    /// hello) need room beside the ten.
    /// </summary>
    public int Connections { get; init; } = 16;

    /// <summary>The longest line read, in bytes: a connection that sends a longer one is closed.</summary>
    public int LineBytes { get; init; } = 1024 * 1024;

    /// <summary>How long a connection has to say its hello.</summary>
    public TimeSpan Hello { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>How long a connection with nothing under way may say nothing before it is closed.</summary>
    public TimeSpan Idle { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>How long a request's lines are kept (§6.1).</summary>
    public TimeSpan Kept { get; init; } = TimeSpan.FromHours(24);

    /// <summary>How many of a connection's requests may be under way at once: past it, one is answered <see cref="InstancePipe.Busy"/>.</summary>
    public int InFlight { get; init; } = 16;

    /// <summary>
    /// How many lines a connection may send ahead of the instance's taking them: past it the connection is closed, since lines
    /// pile up only where a terminal sends on without reading what it was answered, and an answer to them could not be read either.
    /// </summary>
    public int Queued { get; init; } = 64;

    /// <summary>
    /// How many of the person's presses may wait for the window at once (CONFIRMWAIT1a-i's ten, the confirm-wait design §9.2; RPC1c's
    /// five lasted at most twelve minutes): past it, one is refused. Fewer than <see cref="Connections"/>, which a test holds.
    /// </summary>
    public int Waiting { get; init; } = PipeConfirmations.DefaultBound;
}

/// <summary>
/// The running Daoris answers on a pipe (RPC1a, D171 point 3, the terminal RPC design): a terminal's verb is the instance's client,
/// and its request runs through the dispatcher every window's page uses, so the route is the window's press: the same handler, the
/// same middleware (<see cref="RefusalLog"/>, <see cref="UiStallWatch"/>, <see cref="OffTheWindow"/>, <see cref="StatePublisher"/>),
/// the same answer, under way in every window and heard by every window as it happens.
/// </summary>
/// <remarks>
/// <para><b>The pipe</b> (§4.1): <see cref="PipeName"/>'s name, this Windows user's alone (owned by the current user's SID at either
/// elevation, PIPEOWNER1, and an ACL granting that SID and denying the network's: <see cref="Security"/>), made as the first
/// instance of its name, so where the name is already held this instance serves none and says so (<c>pipe.refused</c>) rather
/// than share it. Windows only: elsewhere nothing is served (§11).</para>
///
/// <para><b>The wire</b> (§6): UTF-8 lines of JSON. The hello first (<c>DAORIS.PIPE</c> <c>HELLO</c>), answered with this home's id,
/// the build and the process, or refused <see cref="WrongProtocol"/>, <see cref="WrongHome"/> or <see cref="Stopping"/>; anything
/// before it closes the connection. Then the kit's request envelopes, several at once, each answered with its response envelope
/// by id, a route's progress forwarded to the connection that sent it as the kit's notification envelope; <c>ROUTES</c> and
/// <c>RESULT</c> are the pipe's own.</para>
///
/// <para><b>The census decides what runs</b> (§4.2, <see cref="PipeCensus"/>): an open route runs; a person's is shown to the
/// window (<see cref="Confirmations"/>, RPC1c) and runs only on the person's <b>Confirm</b>, however long it waits (CONFIRMWAIT1a-i),
/// the terminal told it waits (<see cref="Confirming"/>) and, refused, changed, withdrawn or lost, how it ended; each waiting press is
/// kept in a record of its own under <c>data/pipe-waiting/</c> and brought back after a restart (<see cref="Restore"/>,
/// CONFIRMWAIT1a-ii); a window's is refused
/// <see cref="WindowOnly"/>; a module the census does not name <see cref="Unclassed"/>. Nothing refused reaches the dispatcher.</para>
///
/// <para><b>A request's id is durable</b> (§6.1, <see cref="PipeJournal"/>): a press runs on the instance's lifetime, not the
/// connection's, so a lost terminal cancels nothing; an id seen before is answered from its line and never run twice, and
/// <c>RESULT</c> says <c>running</c>, <c>answered</c> with the answer, or <c>unknown</c>. Each id is bound to its route, its payload
/// and the <c>key</c> its sender sent beside the envelope (<see cref="PipeBinding"/>): sent again with any of them changed it is
/// refused <see cref="IdTaken"/>, and <c>RESULT</c> answers only that key. A press whose first line the journal cannot write is
/// refused <see cref="Unrecorded"/> and never dispatched.</para>
///
/// <para><b>Each connection is bounded</b> (<see cref="PipeLimits"/>): presses under way past <see cref="PipeLimits.InFlight"/> are
/// answered <see cref="Busy"/>, and lines sent past <see cref="PipeLimits.Queued"/> ahead of the instance close it.</para>
///
/// <para><b>Stopping</b> (§6.3): from <see cref="BeginStopping"/>, or while the update drains (<see cref="Draining"/>), every new
/// hello and every new press is refused <see cref="Stopping"/>, a line waiting on the person's Confirm is answered
/// <see cref="Waiting"/>, the press kept in its record (CONFIRMWAIT1a-ii), and <see cref="Drain"/> lets the presses under way answer;
/// the loop then takes its address away and lets the home go.</para>
///
/// <para>A request that came over the pipe is said as the terminal's (<see cref="PipeDoor"/>): the refusal log and the pipe's own
/// refusals write <c>door: terminal</c>.</para>
/// </remarks>
public sealed class InstancePipe : IDisposable
{
    /// <summary>The pipe's own module on the wire: its hello, its census and how a request ended.</summary>
    public const string Module = "DAORIS.PIPE";

    /// <summary>The kit's request tracker's module, whose <c>CANCEL</c> a terminal's Ctrl+C sends for a press of its own (RPC1h, §6.2).</summary>
    public const string RequestsModule = "SHENORA.REQUESTS";

    /// <summary>The wire's version, which the hello names; another is refused <see cref="WrongProtocol"/>.</summary>
    public const int Protocol = 1;

    /// <summary>The hello named another version of the wire.</summary>
    public const string WrongProtocol = "protocol";

    /// <summary>The hello named another home's id.</summary>
    public const string WrongHome = "home";

    /// <summary>The instance is closing or updating: its parameter <c>why</c> says which (<c>close</c>, <c>update</c>).</summary>
    public const string Stopping = "stopping";

    /// <summary>
    /// A route the census makes the person's, as an instance from before RPC1c refused it: kept as a word of the wire, which a
    /// client still reads from such an instance. This one asks the window instead.
    /// </summary>
    public const string ConfirmInWindow = "confirm-in-window";

    /// <summary>The notification that tells a terminal its press waits for the person in the window (RPC1c), naming its request.</summary>
    public const string Confirming = "CONFIRMING";

    /// <summary>
    /// The notification that tells a terminal its press runs under the person's standing confirmation, with no card (TERMTRUST1a, D201
    /// point 7, the terminal-trust design §5 step 4), naming its request.
    /// </summary>
    public const string Standing = "STANDING";

    /// <summary>The person refused the press in the window: nothing ran.</summary>
    public const string Refused = "refused";

    /// <summary>
    /// An instance from before CONFIRMWAIT1a-i ended the press by its time: nothing ran. Kept as a word of the wire, as
    /// <see cref="ConfirmInWindow"/> is; this one makes it no more, since a terminal's press waits until the person answers.
    /// </summary>
    public const string Expired = "expired";

    /// <summary>The person confirmed it and a ground it was asked on had moved (the confirm-wait design §5): nothing ran.</summary>
    public const string Changed = "changed";

    /// <summary>Its terminal took it back by the pipe's <c>WITHDRAW</c> before the person answered it (§4.3): nothing ran.</summary>
    public const string Withdrawn = "withdrawn";

    /// <summary>A setting the press sets is already as it asks (§5.1): nothing was asked, and the world is as asked.</summary>
    public const string Already = "already";

    /// <summary>A person's press whose route names no grounds (§5.1): Daoris cannot wait for the person on it, so nothing was asked.</summary>
    public const string Ungrounded = "ungrounded";

    /// <summary>A person's press whose grounds could not be read at its ask, or not within their bound (§5.2): nothing was asked.</summary>
    public const string UnreadGrounds = "unread-grounds";

    /// <summary>As many of the person's presses as may wait already do (D156 §5.2's code): this one was not asked.</summary>
    public const string Full = "confirmations-full";

    /// <summary>A person's press whose values the window could not show whole: not asked.</summary>
    public const string Shape = "shape";

    /// <summary>A route only a window may press.</summary>
    public const string WindowOnly = "window-only";

    /// <summary>A module the census does not name.</summary>
    public const string Unclassed = "unclassed";

    /// <summary>An id begun by an instance that stopped before it ended: what it did is not known, so it is not run again.</summary>
    public const string Interrupted = "interrupted";

    /// <summary>An id already bound to another request (another route, payload or key): refused, never answered for it.</summary>
    public const string IdTaken = "id-taken";

    /// <summary>A request, or a <c>RESULT</c>, sent without its key (16 to 128 characters), the secret only its sender holds.</summary>
    public const string KeyMissing = "key";

    /// <summary>A request whose first line the journal could not write: never run, so it is never run twice.</summary>
    public const string Unrecorded = "unrecorded";

    /// <summary>A connection with as many requests under way as it may have (<see cref="PipeLimits.InFlight"/>).</summary>
    public const string Busy = "busy";

    /// <summary>
    /// The close's answer to a line waiting on a press (CONFIRMWAIT1a-ii, the confirm-wait design §4.1, §7.2), <c>why: close</c>: the press
    /// still waits, kept in its record across the restart.
    /// </summary>
    public const string Waiting = "waiting";

    /// <summary>
    /// A press not kept across a restart (§7.1, §7.2): one carrying a key at the close, or a record the restore could not bring back.
    /// Nothing ran.
    /// </summary>
    public const string Lost = "lost";

    private readonly IMessageDispatcher _dispatcher;
    private readonly IEventBus _bus;
    private readonly string _home;
    private readonly MachineLog _log;
    private readonly PipeCensus _census;
    private readonly string _version;
    private readonly PipeLimits _limits;
    private readonly TimeProvider _clock;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (PipeBinding Binding, Task<string> Answer)> _running = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Connection> _owners = new(StringComparer.Ordinal);
    // RPC1c: a person's press waiting for the window, by its request's id, to the confirmation that shows it (a cancel takes it back).
    private readonly Dictionary<string, string> _waitingFor = new(StringComparer.Ordinal);
    // CONFIRMWAIT1a-i (§4.3): the ids the pipe's own WITHDRAW took back, answered `withdrawn`; the kit's CANCEL is answered as before.
    private readonly HashSet<string> _withdrawn = new(StringComparer.Ordinal);
    // CONFIRMWAIT1a-ii (§7.2): each waiting press's line answer by its request's id, which the close completes `waiting`.
    private readonly Dictionary<string, TaskCompletionSource<string>> _lines = new(StringComparer.Ordinal);
    // CONFIRMWAIT1a-ii (§7.1): each waiting press's record by its confirmation's id, rewritten as it is shown and as it ends, under its
    // own lock, never the pipe's, since it writes a file.
    private readonly PipeWaiting _records;
    private readonly Dictionary<string, PipeWaitingRecord> _kept = new(StringComparer.Ordinal);
    private readonly Lock _recording = new();
    private int _restored;
    private readonly HashSet<Connection> _connections = [];
    private SemaphoreSlim? _slots;
    private PipeJournal? _journal;
    private IDisposable? _progress;
    private Task? _accepting;
    private string? _homeId;
    private int _stopping;
    private int _disposed;

    /// <param name="dispatcher">The dispatcher every window's page uses: the pipe's requests run through it.</param>
    /// <param name="bus">The kit's bus, whose request tracker tells each route's progress.</param>
    /// <param name="home">The home this instance holds.</param>
    /// <param name="log">The machine log.</param>
    /// <param name="census">What runs (<see cref="PipeCensus.Daoris"/> where none is named; a test names its own).</param>
    /// <param name="version">The build, as the hello names it.</param>
    /// <param name="limits">The bounds (<see cref="PipeLimits"/>'s where none are named).</param>
    /// <param name="clock">The clock a confirmation's ask and showing are written by, and the oldest's age read by: the system's, or a test's.</param>
    /// <param name="standing">The person's standing confirmation (TERMTRUST1a): this home's where none is named; a test hands its own.</param>
    public InstancePipe(
        IMessageDispatcher dispatcher, IEventBus bus, string home, MachineLog? log = null, PipeCensus? census = null,
        string? version = null, PipeLimits? limits = null, TimeProvider? clock = null, StandingConfirmation? standing = null)
    {
        _dispatcher = dispatcher;
        _bus = bus;
        _home = home;
        _log = log ?? MachineLog.None;
        _census = census ?? PipeCensus.Daoris;
        _version = version ?? typeof(InstancePipe).Assembly.GetName().Version?.ToString() ?? "0";
        _limits = limits ?? new PipeLimits();
        _clock = clock ?? TimeProvider.System;
        Confirmations = new PipeConfirmations(bus, _log, clock, _limits.Waiting);
        // CONFIRMWAIT1a-ii (§7.1, §7.2): a press's record keeps when it was first shown, and a Confirm while this closes or drains takes nothing.
        _records = new PipeWaiting(DaorisHome.Entry(home, HomeEntry.PipeWaiting));
        Confirmations.ShownFirst = Shown;
        Confirmations.Holding = () => IsStopping;
        StandingConfirmation = standing ?? new StandingConfirmation(home, _log, clock: clock);
        // Every window is told a use (§5 step 4), which the line in Notifications counts; never a value, only the count and its moment.
        StandingConfirmation.Used ??= (seq, at) => _bus.Emit(StatePublisher.Module, StandingConfirmation.UsedEvent, new { Seq = seq, At = at });
        if (StandingConfirmation.Caller is StandingCaller look) look.OwnPipe = () => Name;
    }

    /// <summary>
    /// The person's standing confirmation (TERMTRUST1a, D201): read as the pipe is first served, judged before a person's press is shown
    /// to the window, granted and revoked only by the window's <c>SET_STANDING_CONFIRMATION</c>.
    /// </summary>
    public StandingConfirmation StandingConfirmation { get; }

    /// <summary>
    /// The person's presses a terminal sent, waiting for the window (RPC1c): the bridge's <c>PERSON_CONFIRMATIONS</c> reads them and
    /// its <c>PERSON_CONFIRM</c> answers one, which a terminal never may.
    /// </summary>
    public PipeConfirmations Confirmations { get; }

    /// <summary>
    /// What a person's press's grounds are read from (CONFIRMWAIT1a-i, the confirm-wait design §5.2): the loop's readers, handed as the
    /// standing confirmation's are. None reads nothing, so every press that names a ground is refused <see cref="UnreadGrounds"/>.
    /// </summary>
    public IPipeGroundWorld? Grounds { get; set; }

    /// <summary>
    /// Bring back the presses that waited when Daoris last stopped (CONFIRMWAIT1a-ii, the confirm-wait design §7.2): once, before
    /// <see cref="Serve"/>, on the home's lease, which makes this instance the one writer of its records.
    /// </summary>
    /// <remarks>
    /// Each record in <c>asked</c> order (§7.2): a name that is no confirmation's id is removed; a newer Daoris's record is left, said
    /// once; one <c>begun</c> answers <c>interrupted</c> and one <c>ended</c> its ending, journaled where the journal lacks it, and is
    /// removed; one carrying a key is lost; one waiting is judged again as <see cref="Accept"/> judges a press, from its module, route and
    /// payload alone (the census's <c>Person</c>, its binding, its shape, a grounds row, an id the journal does not hold, the bound), its
    /// card made from its payload and never read from the record, and waits again; one that fails or does not read is lost, journaled
    /// where its terminal's id reads, removed and never asked. The person is told once: a notice for the lost, one for a newer Daoris's,
    /// and one balloon for those waiting again.
    /// </remarks>
    public void Restore()
    {
        if (Interlocked.Exchange(ref _restored, 1) == 1) return;
        lock (_gate)
        {
            _journal ??= new PipeJournal(DaorisHome.Entry(_home, HomeEntry.Pipe), _limits.Kept);
        }

        int waiting = 0, lost = 0, newer = 0;
        var files = _records.Read().OrderBy(each => each.Record?.Asked ?? DateTimeOffset.MaxValue).ThenBy(each => each.Name, StringComparer.Ordinal).ToList();
        // CONFIRMWAIT1 (the confirm-wait design §15.2 item 7): each terminal id is reserved across the whole set before any card is told.
        // Records of one id that are the same press collapse into the first; records that differ are each lost.
        var byRequest = files
            .Where(each => each.Record is { State: PipeWaitingRecord.Waiting } record && record.Confirmation == each.Name && PipeWaiting.IsName(each.Name))
            .GroupBy(each => each.Record!.Request, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(each => each.Record!).ToList(), StringComparer.Ordinal);
        foreach (var file in files)
        {
            if (!PipeWaiting.IsName(file.Name))
            {
                PipeWaiting.Delete(file.Path);
                _log.Warn("pipe.restore", ("state", "misnamed"));
                continue;
            }

            if (file.Newer)
            {
                newer++;
                _log.Warn("pipe.restore", ("state", "newer"));
                continue;
            }

            if (file.Record is not { } record || record.Confirmation != file.Name)
            {
                lost++;
                PipeWaiting.Delete(file.Path);
                _log.Warn("pipe.restore", ("state", "unread"));
                continue;
            }

            switch (record.State)
            {
                case PipeWaitingRecord.Begun:
                    Journaled(record, IpcJson.Serialize(IpcResponse.CreateError(record.Request, Interrupted, null,
                        "Daoris stopped while this request was under way, so whether it was done is not known; it was not run again.")));
                    _records.Remove(record.Confirmation);
                    continue;
                case PipeWaitingRecord.Ended when record.Answer is { } answer:
                    Journaled(record, answer);
                    _records.Remove(record.Confirmation);
                    continue;
            }

            var twins = byRequest.GetValueOrDefault(record.Request) ?? [];
            if (twins.Count > 1 && twins.All(each => Same(each, twins[0])) && !ReferenceEquals(record, twins[0]))
            {
                // The same press kept twice: the first stands for it, and this file goes.
                _records.Remove(record.Confirmation);
                _log.Warn("pipe.restore", ("state", "duplicate"));
                continue;
            }

            var alone = twins.Count <= 1 || twins.All(each => Same(each, twins[0]));
            if (record.State == PipeWaitingRecord.Waiting && !record.Keyed && alone && Rejudged(record)) waiting++;
            else
            {
                lost++;
                Journaled(record, IpcJson.Serialize(IpcResponse.CreateError(record.Request, Lost, null, record.Keyed
                    ? "Daoris restarted while this waited, and it carries a key, so it does not wait across a restart; nothing was done. Send it again."
                    : "Daoris restarted while this waited and could not bring it back, so it was not done. Send it again.")));
                _records.Remove(record.Confirmation);
            }
        }

        _log.Info("pipe.restored", ("waiting", waiting), ("lost", lost), ("newer", newer));
        Confirmations.Restore(waiting, lost, newer);
    }

    /// <summary>
    /// A waiting record judged again as <see cref="Accept"/> judges a press (§7.2 step 6), and waiting again where it passes: its card
    /// made from its payload, its grounds the record's, no terminal on its line. False, and nothing asked, where it fails.
    /// </summary>
    private bool Rejudged(PipeWaitingRecord record)
    {
        if (record.Request is not { Length: > 0 and <= 128 } id || record.Payload is not { } text) return false;
        JsonElement? payload;
        try
        {
            payload = text.Length == 0 ? null : JsonDocument.Parse(text).RootElement.Clone();
        }
        catch (JsonException)
        {
            return false;
        }

        if (_census.Of(record.Module, record.Route, payload) != PipeClass.Person
            || record.Binding.Request != $"{record.Module}.{record.Route}"
            || record.Binding.Payload != PipeBinding.Fingerprint(text)
            || PipeConfirmations.FieldsOf(payload) is not ({ } fields, null)
            || _census.Targets.Row(record.Module, record.Route, payload) is not { } row)
        {
            return false;
        }

        // CONFIRMWAIT1 (§15.2 item 7): its grounds are what its row reads for its payload, by kind and subject, none missing and none
        // added, so a baseline that lacks a ground protects nothing it omitted.
        if (!row.Expected(payload).Select(each => $"{each.Kind}|{each.Subject}").Order(StringComparer.Ordinal)
                .SequenceEqual(record.Grounds.Select(each => $"{each.Kind}|{each.Subject}").Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            return false;
        }

        lock (_gate)
        {
            if (_running.ContainsKey(id) || _journal!.Find(id) is not null) return false;
        }

        if (Confirmations.Hold() is not { } place) return false;
        var request = new IpcRequest { Id = id, Module = record.Module, Type = record.Route, Payload = payload };
        var line = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_recording)
        {
            _kept[record.Confirmation] = record;
        }

        var asked = record.Grounds;
        var shown = Confirmations.Ask(
            place, record.Module, record.Route, fields, record.Asker,
            judge: row.Grounds.Count == 0 ? null : ct => JudgeAsync(row, payload, asked, ct), checkWithin: row.Grounds.Count == 0 ? null : row.Bound,
            @as: new PipeAsAsked(record.Confirmation, record.Asked, record.ShownAt, Restored: true, WaitedOn: false));
        lock (_gate)
        {
            _running[id] = (record.Binding, line.Task);
            _waitingFor[id] = shown.Shown.Id;
            _lines[id] = line;
        }

        _ = ConfirmThenRunAsync(request, id, record.Binding, line, shown.Ended, record.Confirmation);
        return true;
    }

    /// <summary>Whether two records of one terminal id keep the same press: its binding, route, payload, key and grounds alike.</summary>
    private static bool Same(PipeWaitingRecord one, PipeWaitingRecord other) =>
        one.Binding == other.Binding && one.Module == other.Module && one.Route == other.Route && one.Payload == other.Payload
        && one.Keyed == other.Keyed && one.Grounds.SequenceEqual(other.Grounds);

    /// <summary>
    /// <paramref name="record"/>'s ending journaled as <paramref name="answer"/> where its terminal's id reads and the journal holds no line
    /// for it (§7.2), so <c>RESULT</c> and the same id sent again say it.
    /// </summary>
    private void Journaled(PipeWaitingRecord record, string answer)
    {
        if (record.Request is not { Length: > 0 and <= 128 } id) return;
        lock (_gate)
        {
            if (_running.ContainsKey(id) || _journal!.Find(id) is not null) return;
        }

        if (_journal!.Begin(id, record.Binding)) _journal.End(id, record.Binding, answer);
    }

    /// <summary>A press's card shown for the first time (§7.1): its record keeps when.</summary>
    private void Shown(PipeConfirmation card)
    {
        lock (_recording)
        {
            if (!_kept.TryGetValue(card.Id, out var record)) return;
            _kept[card.Id] = record = record with { ShownAt = card.ShownAt };
            _records.Write(record);
        }
    }

    /// <summary>A press's record rewritten as it ends (§7.1's order): <c>begun</c>, or <c>ended</c> with its answer.</summary>
    private void Rewrite(string confirmation, string state, string? answer = null)
    {
        lock (_recording)
        {
            if (!_kept.TryGetValue(confirmation, out var record)) return;
            _kept[confirmation] = record = record with { State = state, Answer = answer };
            _records.Write(record);
        }
    }

    /// <summary>A press's record removed once its ending is journaled, or its run begun; one that cannot be is left, and the next start removes it.</summary>
    private void Forget(string confirmation)
    {
        lock (_recording)
        {
            _kept.Remove(confirmation);
            _records.Remove(confirmation);
        }
    }

    /// <summary>Whether a waiting press's record says it carries a key.</summary>
    private bool KeyedRecord(string confirmation)
    {
        lock (_recording)
        {
            return _kept.TryGetValue(confirmation, out var record) && record.Keyed;
        }
    }

    /// <summary>Whether an update drains this instance (UPDATE1, D139): a drain is a stop's beginning (§6.3).</summary>
    public Func<bool>? Draining { get; set; }

    /// <summary>The pipe's name once served, or null.</summary>
    public string? Name { get; private set; }

    /// <summary>Whether new hellos and presses are refused: the stop has begun, or an update drains.</summary>
    public bool IsStopping => Volatile.Read(ref _stopping) == 1 || Draining?.Invoke() == true;

    /// <summary>
    /// Serve the pipe, and the name it is served under; null where none is served: not Windows, a home with no id, or a name
    /// another process already holds (said in the machine log, <c>pipe.refused</c>). Once: a second call answers the first's name.
    /// </summary>
    public string? Serve()
    {
        if (!OperatingSystem.IsWindows()) return null;
        lock (_gate)
        {
            if (Name is not null || Volatile.Read(ref _disposed) == 1) return Name;
            if (HomeIdentity.Ensure(_home, out var unread) is not { } id)
            {
                _log.Warn("pipe.refused", ("why", "home"), ("unread", unread));
                return null;
            }

            using var self = Process.GetCurrentProcess();
            var name = PipeName.For(id, self.SessionId);
            NamedPipeServerStream first;
            try
            {
                first = Create(name, _limits.Connections, first: true);
            }
            catch (Exception error) when (error is UnauthorizedAccessException or IOException)
            {
                // The first instance of its name or none (§4.1): a name already held is never shared.
                _log.Warn("pipe.refused", ("why", "held"));
                return null;
            }

            _homeId = id;
            // TERMTRUST1a (§4.2): the standing confirmation's record is read before the pipe serves, and kept in memory.
            StandingConfirmation.Load();
            // The journal's folder is D172's `pipe` entry, asked of the table as every home path is (HOMELAYOUT2).
            // CONFIRMWAIT1a-ii (§7.2): made by the restore where it ran first, which journals what it brought back or lost.
            _journal ??= new PipeJournal(DaorisHome.Entry(_home, HomeEntry.Pipe), _limits.Kept);
            _slots = new SemaphoreSlim(_limits.Connections - 1, _limits.Connections);
            _progress = _bus.Subscribe("*", IpcRequestEvents.Updated, Progressed);
            Name = name;
            _accepting = Task.Run(() => AcceptAsync(first));
            _log.Info("pipe.served", ("connections", _limits.Connections));
            return name;
        }
    }

    /// <summary>
    /// The stop begins (§6.3): every new hello and every new press is refused <see cref="Stopping"/> from here. What waits for the window
    /// is not ended (CONFIRMWAIT1a-ii, the confirm-wait design §7.2): each line waiting on one is answered <see cref="Waiting"/>,
    /// <c>why: close</c>, and the press leaves what is under way, so the drain is not spent on it, its record kept for the next start; a
    /// Confirm meanwhile takes nothing. A press carrying a key keeps no payload to wait on, so it ends <see cref="Lost"/>, said to its
    /// terminal and the person. Once: a second call does nothing more.
    /// </summary>
    public void BeginStopping()
    {
        if (Interlocked.Exchange(ref _stopping, 1) == 1) return;
        (string Id, string Confirmation, TaskCompletionSource<string>? Line)[] waiting;
        lock (_gate)
        {
            waiting = [.. _waitingFor.Select(each => (each.Key, each.Value, _lines.GetValueOrDefault(each.Key)))];
        }

        foreach (var (id, confirmation, line) in waiting)
        {
            if (KeyedRecord(confirmation))
            {
                Confirmations.Lose(confirmation);
                continue;
            }

            lock (_gate)
            {
                _running.Remove(id);
                _owners.Remove(id);
                _lines.Remove(id);
            }

            Confirmations.WaitedOn(confirmation, false);
            line?.TrySetResult(IpcJson.Serialize(IpcResponse.CreateError(id, Waiting, new Dictionary<string, string> { ["why"] = "close" },
                $"Daoris is closing; this still waits for your confirmation (request {id}).")));
        }
    }

    /// <summary>
    /// Wait up to <paramref name="within"/> for the presses under way to answer, and each answer to be written to the terminal
    /// waiting on it: false where one had not.
    /// </summary>
    public bool Drain(TimeSpan within)
    {
        var drained = DrainWithin(within);
        if (!drained)
        {
            // RPC1i (§6.3): the stop goes on, and the machine log names how many presses it left, so a terminal told `nothing was done`
            // by a closing Daoris can be read against what the instance knew. Presses not yet answered, not lines not yet written.
            int left;
            lock (_gate)
            {
                left = _running.Count;
            }

            _log.Warn("pipe.drained", ("left", left));
        }

        return drained;
    }

    private bool DrainWithin(TimeSpan within)
    {
        var until = DateTime.UtcNow + within;
        Task[] running;
        lock (_gate)
        {
            running = [.. _running.Values.Select(each => each.Answer)];
        }

        try
        {
            if (!Task.WaitAll(running, within)) return false;
        }
        catch (AggregateException)
        {
            // An answer is a response, never a fault: nothing to wait for here.
        }

        while (true)
        {
            bool answering;
            lock (_gate)
            {
                answering = _connections.Any(each => each.Answering);
            }

            if (!answering) return true;
            if (DateTime.UtcNow >= until) return false;
            Thread.Sleep(10);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        BeginStopping();
        _lifetime.Cancel();
        _progress?.Dispose();
        Connection[] open;
        lock (_gate)
        {
            open = [.. _connections];
        }

        foreach (var connection in open) connection.Close();
        try
        {
            _accepting?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // The accepting loop ends on the lifetime's cancellation, as it should.
        }

        _lifetime.Dispose();
    }

    /// <summary>
    /// The pipe's security (§4.1): this user's alone, and nobody's from another machine. Owned by the user's SID (PIPEOWNER1), not
    /// left to the token's default owner, which for an elevated Daoris is the Administrators group: a terminal of the same user
    /// that is not elevated refused a pipe owned so, so the pipe is this user's at either elevation.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static PipeSecurity Security()
    {
        var security = new PipeSecurity();
        using (var identity = WindowsIdentity.GetCurrent())
        {
            security.SetOwner(identity.User!);
            security.AddAccessRule(new PipeAccessRule(identity.User!, PipeAccessRights.FullControl, AccessControlType.Allow));
        }

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        return security;
    }

    [SupportedOSPlatform("windows")]
    private static NamedPipeServerStream Create(string name, int connections, bool first) =>
        NamedPipeServerStreamAcl.Create(
            name, PipeDirection.InOut, connections, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | (first ? PipeOptions.FirstPipeInstance : PipeOptions.None), 0, 0, Security());

    private async Task AcceptAsync(NamedPipeServerStream first)
    {
        var listening = first;
        var token = _lifetime.Token;
        while (true)
        {
            try
            {
                await listening.WaitForConnectionAsync(token).ConfigureAwait(false);
            }
            catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException)
            {
                await listening.DisposeAsync().ConfigureAwait(false);
                return;
            }
            catch (IOException)
            {
                // A client gone before it was accepted: this instance is spent, and the next one listens.
                await listening.DisposeAsync().ConfigureAwait(false);
                listening = await NextAsync(token, acquired: true).ConfigureAwait(false);
                if (listening is null) return;
                continue;
            }

            var connection = new Connection(this, listening);
            lock (_gate)
            {
                _connections.Add(connection);
            }

            _ = connection.RunAsync();
            listening = await NextAsync(token, acquired: false).ConfigureAwait(false);
            if (listening is null) return;
        }
    }

    /// <summary>The next instance to listen on, once a connection's place is free; null once the instance ends.</summary>
    private async Task<NamedPipeServerStream?> NextAsync(CancellationToken token, bool acquired)
    {
        while (true)
        {
            try
            {
                if (!acquired) await _slots!.WaitAsync(token).ConfigureAwait(false);
                acquired = true;
                if (!OperatingSystem.IsWindows()) return null;
                return Create(Name!, _limits.Connections, first: false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Every instance the name allows is open this moment: looked at again shortly.
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }
        }
    }

    private void Ended(Connection connection)
    {
        string[] unwaited;
        lock (_gate)
        {
            if (!_connections.Remove(connection)) return;
            // CONFIRMWAIT1a-i (§8.1): a press whose terminal's line ended waits on, and its card says no terminal waits on it now.
            unwaited = [.. _waitingFor.Where(each => _owners.GetValueOrDefault(each.Key) == connection).Select(each => each.Value)];
        }

        foreach (var confirmation in unwaited) Confirmations.WaitedOn(confirmation, false);

        try
        {
            _slots?.Release();
        }
        catch (Exception error) when (error is SemaphoreFullException or ObjectDisposedException)
        {
            // The instance is ending: there is no next connection to make room for.
        }
    }

    /// <summary>The kit's tracker told a request's progress: forwarded to the connection that sent it, and to nobody else (§6).</summary>
    private Task Progressed(EventMessage message)
    {
        if (message.Payload is not IpcRequestStatus status) return Task.CompletedTask;
        Connection? owner;
        lock (_gate)
        {
            _owners.TryGetValue(status.Id, out owner);
        }

        if (owner is null) return Task.CompletedTask;
        var told = JsonSerializer.Serialize(
            new
            {
                category = IpcCategories.Notification,
                id = Guid.NewGuid().ToString("N"),
                payload = new[] { new { module = message.Module, type = message.Type, payload = (object)status } },
                timestamp = DateTimeOffset.UtcNow,
            },
            IpcJson.Options);
        _ = owner.WriteAsync(told);
        return Task.CompletedTask;
    }

    /// <summary>The hello's answer, or its refusal.</summary>
    private IpcResponse Hello(IpcRequest hello, Connection connection)
    {
        var payload = hello.Payload;
        var protocol = payload is { ValueKind: JsonValueKind.Object } p && p.TryGetProperty("protocol", out var named)
            && named.TryGetInt32(out var number) ? number : 0;
        if (protocol != Protocol)
        {
            return Refuse(hello, WrongProtocol, $"this Daoris speaks the pipe's version {Protocol}, and was asked another.");
        }

        var home = payload is { ValueKind: JsonValueKind.Object } q && q.TryGetProperty("home", out var id)
            && id.ValueKind == JsonValueKind.String ? id.GetString() : null;
        if (home != _homeId) return Refuse(hello, WrongHome, "this Daoris runs on another home.");
        if (IsStopping) return RefuseStopping(hello);
        // SUPERVISOR1a-ii (D208 §2.2): a fresh nonce for this connection alone, which a holder's proof is bound to; an older verb ignores it.
        return IpcResponse.CreateSuccess(hello.Id, new
        {
            protocol = Protocol, home = _homeId, version = _version, pid = Environment.ProcessId, nonce = connection.DrawNonce(),
        });
    }

    /// <summary>
    /// A request on a connection that said its hello: answered now (the pipe's own, a refusal, an id answered before), or later,
    /// once the press it begins or joins answers. A press is admitted only where the connection has room for one more under way.
    /// </summary>
    private (string? Now, Task<string>? Later) Accept(IpcRequest request, string? key, Connection connection)
    {
        if (string.Equals(request.Module, Module, StringComparison.OrdinalIgnoreCase))
        {
            return (IpcJson.Serialize(request.Type switch
            {
                "HELLO" => Hello(request, connection),
                "ROUTES" => IpcResponse.CreateSuccess(request.Id, new { routes = _census.Routes.Select(Listed) }),
                "RESULT" => Result(request),
                "WITHDRAW" => Withdraw(request),
                _ => IpcResponse.CreateError(request.Id, IpcErrorCodes.NoRoute, null, $"the pipe has no route {request.Type}."),
            }), null);
        }

        // RPC1h (§6.2): a terminal's Ctrl+C, taken for its own press alone. Before the census, which names no kit module, and before
        // the stop's refusal, which a press under way outlives: a cancel is how a terminal ends one while the instance closes.
        if (string.Equals(request.Module, RequestsModule, StringComparison.OrdinalIgnoreCase) && request.Type == "CANCEL")
        {
            return (null, CancelAsync(request, key));
        }

        if (IsStopping) return (IpcJson.Serialize(RefuseStopping(request)), null);
        var @class = _census.Of(request.Module, request.Type, request.Payload);
        switch (@class)
        {
            case null:
                return (IpcJson.Serialize(Refuse(request, Unclassed, "this route is not answered over the pipe.")), null);
            case PipeClass.Window:
                return (IpcJson.Serialize(Refuse(request, WindowOnly, "only Daoris's window does this.")), null);
        }

        if (request.Id is not { Length: > 0 and <= 128 } id)
        {
            return (IpcJson.Serialize(IpcResponse.CreateError(request.Id ?? "", IpcErrorCodes.InvalidPayloadValue, null, "a request names its id.")), null);
        }

        if (!IsKey(key)) return (IpcJson.Serialize(Refuse(request, KeyMissing, "a request carries a key of its own, 16 to 128 characters.")), null);
        if (!connection.HasRoom) return (IpcJson.Serialize(Refuse(request, Busy, "this terminal has as much under way as Daoris takes from one; send it again once one answers.")), null);

        // SUPERVISOR1a-ii (D208 §2.2, the supervisor design §11 row 1a-ii): a role's act is judged by the judge's own role method, before
        // the open path and before any card, under the same lock and caller look, then by the holder's proof over this connection's
        // nonce, which this act uses up. Anything it does not cover is refused, logged, never asked.
        RoleJudgement? role = null;
        if (@class == PipeClass.Role)
        {
            role = StandingConfirmation.JudgeRole(request.Module, request.Type, request.Payload, connection.Client, connection.TakeNonce());
            if (!role.Covered)
            {
                // STEWARDROLE1a-i (D209 §5.2): a role's act's refusal is `role.refused`; the supervisor's routes keep their own line.
                if (request.Type == RoleRoutes.Act) _log.Info("role.refused", ("why", role.Why ?? ""), ("door", PipeDoor.Terminal));
                else _log.Info("supervisor.refused", ("why", role.Why ?? ""), ("door", PipeDoor.Terminal));
                return (IpcJson.Serialize(Refuse(request, role.Why ?? Unclassed, RoleRefusal(role.Why))), null);
            }
        }

        // RPC1c: a person's press is shown to the window with every value it carries, so one the card could not show whole is not asked.
        IReadOnlyList<PipeConfirmationField>? fields = null;
        if (@class == PipeClass.Person)
        {
            var (shown, unshown) = PipeConfirmations.FieldsOf(request.Payload);
            if (unshown is not null) return (IpcJson.Serialize(Refuse(request, Shape, $"{Capitalized(unshown)} Nothing was asked.")), null);
            fields = shown;
        }

        var binding = new PipeBinding(
            $"{request.Module}.{request.Type}",
            PipeBinding.Fingerprint(request.Payload is { ValueKind: not JsonValueKind.Undefined } payload ? payload.GetRawText() : ""),
            PipeBinding.Fingerprint(key!));
        Task<string> answer;
        TaskCompletionSource<string>? mine = null;
        lock (_gate)
        {
            if (_running.TryGetValue(id, out var running))
            {
                if (running.Binding != binding) return (IpcJson.Serialize(RefuseTaken(request)), null);
                // Sent again while it runs, by the key that sent it: the one press's answer, and its progress to this connection.
                _owners[id] = connection;
                answer = running.Answer;
                // CONFIRMWAIT1a-i (§8.1): a terminal waits on its line for it again. The confirmations' lock is only ever taken inside
                // this one, never around it.
                if (_waitingFor.TryGetValue(id, out var waiting)) Confirmations.WaitedOn(waiting, true);
            }
            else if (_journal!.Find(id) is { } seen)
            {
                if (seen.Binding != binding) return (IpcJson.Serialize(RefuseTaken(request)), null);
                return (seen.State == PipeJournal.Seen.Answered
                    ? seen.Answer!
                    : IpcJson.Serialize(IpcResponse.CreateError(id, Interrupted, null,
                        "Daoris stopped while this request was under way, so whether it was done is not known; it was not run again.")), null);
            }
            else
            {
                mine = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                answer = mine.Task;
                _running[id] = (binding, answer);
                _owners[id] = connection;
            }
        }

        if (mine is null) return (null, answer);
        if (fields is null)
        {
            _ = RunAsync(request, id, binding, mine, role: role);
            return (null, answer);
        }

        // TERMTRUST1a (D201, the terminal-trust design §5 step 3): the person's standing confirmation is judged first, under its lock, and a
        // press it covers is admitted there, once: run as a confirmed press is, said to the log and the terminal, with no card. A revoke
        // after the admission does not recall it; a press it does not cover asks as before, saying why where the grant names it.
        var judged = StandingConfirmation.Judge(request.Module, request.Type, request.Payload, connection.Client);
        if (judged.Covered)
        {
            _log.Info(PipeConfirmations.LogEvent, ("state", "standing"), ("route", $"{request.Module}.{request.Type}"), ("door", PipeDoor.Terminal));
            _ = connection.WriteAsync(Told(Standing, new { id }));
            _ = RunAsync(request, id, binding, mine, standing: true);
            return (null, answer);
        }

        var why = judged.Said ? judged.Why : null;

        // CONFIRMWAIT1a-i (the confirm-wait design §5.2): after the standing judgement, so a press it admits is never refused here, a
        // person's press whose route names no grounds is refused at once, no read made; then a place among the ten is held, told to no
        // window yet, so one past them is refused before any read. The grounds are read in the press's own task, outside every lock.
        if (_census.Targets.Row(request.Module, request.Type, request.Payload) is not { } row)
        {
            return (Settled(request, id, mine, Refuse(request, Ungrounded,
                "Daoris does not know what this press rests on, so it cannot wait for the person; nothing was asked.")), null);
        }

        if (Confirmations.Hold() is not { } place)
        {
            return (Settled(request, id, mine, Refuse(request, Full, FullSentence())), null);
        }

        _ = AskThenRunAsync(request, id, binding, mine, place, row, fields, connection, why);
        return (null, answer);
    }

    /// <summary>
    /// A read of the driver's own, called in process through the dispatcher every window uses, with <paramref name="payload"/> as a
    /// window would send it (CONFIRMWAIT1a-i, the confirm-wait design §5.2): its answer's data. Only a route the census leaves open runs,
    /// so a ground's read never presses anything; a read told by event (NOBLOCK1) is asked again until it answers what it read. A refusal
    /// throws, which the grounds take as a read that failed.
    /// </summary>
    public async Task<JsonElement> ReadAsync(string module, string route, object? payload, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToNode(payload ?? new { }, IpcJson.Options) as System.Text.Json.Nodes.JsonObject ?? [];
        // A read told by event answers what it held, one ask behind (NOBLOCK1); a payload of its own holds nothing, so what it answers is
        // what its work read now.
        if (string.Equals(module, DriverModule.Name, StringComparison.OrdinalIgnoreCase) && DriverModule.ByEventRoutes.Contains(route))
        {
            body["grounds"] = Guid.NewGuid().ToString("N");
        }

        var element = JsonSerializer.SerializeToElement(body, IpcJson.Options);
        if (_census.Of(module, route, element) != PipeClass.Open) throw new InvalidOperationException($"{route} is not a read.");
        var request = new IpcRequest { Id = $"grounds-{Guid.NewGuid():N}", Module = module, Type = route, Payload = element };
        while (true)
        {
            var response = await _dispatcher.DispatchAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.Success) throw new InvalidOperationException($"{route} refused: {response.Error?.Code}");
            var data = JsonSerializer.SerializeToElement(response.Data, IpcJson.Options);
            if (data is { ValueKind: JsonValueKind.Object } held && held.TryGetProperty("pending", out var pending) && pending.ValueKind == JsonValueKind.True)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
                continue;
            }

            return data;
        }
    }

    /// <summary>The refusal of a press whose id was bound and never asked: its place let go, and what the terminal is answered.</summary>
    private string Settled(IpcRequest request, string id, TaskCompletionSource<string> mine, IpcResponse refusal)
    {
        var text = IpcJson.Serialize(refusal);
        lock (_gate)
        {
            _running.Remove(id);
            _owners.Remove(id);
        }

        mine.SetResult(text);
        return text;
    }

    /// <summary>The bound's refusal (§9.3), naming how long the oldest has waited, so the asker can withdraw its own or the person answer.</summary>
    private string FullSentence()
    {
        var bound = Confirmations.Bound == PipeConfirmations.DefaultBound
            ? "Ten"
            : Confirmations.Bound.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var oldest = Confirmations.Oldest() is { } asked ? $", the oldest asked {Ago(_clock.GetUtcNow() - asked)}" : "";
        return $"{bound} presses already wait for the person's confirmation{oldest}; this one was not asked. Nothing was kept.";
    }

    /// <summary>A wait's length in the terminal's words: <c>2 hours ago</c>.</summary>
    private static string Ago(TimeSpan waited) => waited switch
    {
        { TotalMinutes: < 1 } => "just now",
        { TotalHours: < 1 } => Count((int)waited.TotalMinutes, "minute"),
        { TotalDays: < 1 } => Count((int)waited.TotalHours, "hour"),
        _ => Count((int)waited.TotalDays, "day"),
    };

    private static string Count(int count, string unit) => count == 1 ? $"1 {unit} ago" : $"{count} {unit}s ago";

    /// <summary>
    /// A person's press on its held place (§5.2): its grounds read outside every lock, bounded; one that does not read, or reads that its
    /// setting is already as it asks, lets the place go and is answered so, nothing asked. Otherwise its card is shown, its terminal told
    /// it waits, and it runs once the person confirms it with its grounds still holding.
    /// </summary>
    private async Task AskThenRunAsync(
        IpcRequest request, string id, PipeBinding binding, TaskCompletionSource<string> mine, PipePlace place, PipeGroundRow row,
        IReadOnlyList<PipeConfirmationField> fields, Connection connection, string? why)
    {
        IReadOnlyList<PipeReading> asked;
        try
        {
            asked = await row.ReadAsync(Grounds, request.Payload, _clock, _lifetime.Token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Confirmations.Let(place);
            _log.Info("pipe.grounds", ("state", "unread"), ("route", $"{request.Module}.{request.Type}"), ("error", error.GetType().Name));
            Settled(request, id, mine, Refuse(request, UnreadGrounds,
                "Daoris could not read what this press rests on just now, so it was not asked; nothing was kept. Send it again."));
            return;
        }

        if (row.Already(asked, request.Payload))
        {
            Confirmations.Let(place);
            Settled(request, id, mine, Refuse(request, Already, "This is already set as this asks; nothing was asked."));
            return;
        }

        if (IsStopping)
        {
            Confirmations.Let(place);
            Settled(request, id, mine, RefuseStopping(request));
            return;
        }

        // CONFIRMWAIT1a-ii (§7.1): its record is written before the window is asked and the terminal told, named by the confirmation's own
        // id; a press carrying a key keeps no payload and no grounds in it. One that cannot be written is not asked.
        var keyed = PipeCensus.Keyed(request.Module, request.Type);
        var record = new PipeWaitingRecord(
            Guid.NewGuid().ToString("N"), id, binding, request.Module, request.Type,
            keyed ? null : request.Payload is { ValueKind: not JsonValueKind.Undefined } sent ? sent.GetRawText() : "",
            keyed ? [] : asked, connection.Asker, _clock.GetUtcNow(), null, keyed, PipeWaitingRecord.Waiting);
        lock (_recording)
        {
            if (_records.Write(record)) _kept[record.Confirmation] = record;
            else record = null;
        }

        if (record is null)
        {
            Confirmations.Let(place);
            Settled(request, id, mine, Refuse(request, Unrecorded,
                "Daoris could not keep this press while it waits for the person, so it was not asked; nothing was kept. Send it again."));
            return;
        }

        // RPC1c (D156 §4.2): the person's press waits for the window, which shows it naming this terminal, and runs on Confirm alone,
        // its grounds read again then (§5.2). The id is bound before the window is asked, so the same id sent again meanwhile joins this
        // one and asks nothing.
        var payload = request.Payload;
        var shown = Confirmations.Ask(
            place, request.Module, request.Type, fields, connection.Asker, why,
            judge: row.Grounds.Count == 0 ? null : ct => JudgeAsync(row, payload, asked, ct), checkWithin: row.Grounds.Count == 0 ? null : row.Bound,
            @as: new PipeAsAsked(record.Confirmation, record.Asked, Keyed: keyed));
        lock (_gate)
        {
            _waitingFor[id] = shown.Shown.Id;
            _lines[id] = mine;
        }

        await connection.WriteAsync(Waits(id, why)).ConfigureAwait(false);
        await ConfirmThenRunAsync(request, id, binding, mine, shown.Ended, record.Confirmation).ConfigureAwait(false);
    }

    /// <summary>The grounds read again at the Confirm, outside every lock, and compared with the ask's (§5.2): a failed read is unread.</summary>
    private async Task<PipeJudgement> JudgeAsync(PipeGroundRow row, JsonElement? payload, IReadOnlyList<PipeReading> asked, CancellationToken ct)
    {
        IReadOnlyList<PipeReading> now;
        try
        {
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
            now = await row.ReadAsync(Grounds, payload, _clock, lifetime.Token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            _log.Info("pipe.grounds", ("state", "unread-at-confirm"), ("error", error.GetType().Name));
            return new PipeJudgement(PipeJudged.Unread);
        }

        return PipeGroundRow.Judge(asked, now) is { } change ? new PipeJudgement(PipeJudged.Changed, change) : PipeJudgement.Holding;
    }

    /// <summary>
    /// The notification that tells the terminal its press waits for the person in the window (RPC1c), naming its request; no time, since
    /// none ends it (CONFIRMWAIT1a-i). With <c>standing</c>, why the person's standing confirmation, on and naming the press, did not cover
    /// it (TERMTRUST1a, §5 step 5), a field an older terminal ignores.
    /// </summary>
    private static string Waits(string id, string? standing) => standing is null
        ? Told(Confirming, new { id })
        : Told(Confirming, new { id, standing });

    /// <summary>A notification of the pipe's own to the terminal that sent a press.</summary>
    private static string Told(string type, object payload) => JsonSerializer.Serialize(
        new
        {
            category = IpcCategories.Notification,
            id = Guid.NewGuid().ToString("N"),
            payload = new[] { new { module = Module, type, payload } },
            timestamp = DateTimeOffset.UtcNow,
        },
        IpcJson.Options);

    /// <summary>
    /// A person's press once the window has answered (RPC1c): run, once, where the person confirmed it and the instance is not
    /// closing; otherwise answered with how it ended and kept as its ending, so <c>RESULT</c> and the same id sent again say it.
    /// CONFIRMWAIT1a-ii (§7.1): its record says what happened at each step. Confirmed, it is rewritten <c>begun</c>, then the journal's
    /// first line, then it is removed, then the run; ended otherwise, it is rewritten <c>ended</c> with its answer, then the journal's line,
    /// then it is removed, even where that line failed.
    /// </summary>
    private async Task ConfirmThenRunAsync(
        IpcRequest request, string id, PipeBinding binding, TaskCompletionSource<string> answered, Task<PipeEnding> ended, string confirmation)
    {
        var ending = await ended.ConfigureAwait(false);
        bool byWithdraw;
        lock (_gate)
        {
            _waitingFor.Remove(id);
            _lines.Remove(id);
            byWithdraw = _withdrawn.Remove(id);
        }

        if (ending.End == PipeConfirmationEnd.Confirmed && !IsStopping)
        {
            Rewrite(confirmation, PipeWaitingRecord.Begun);
            await RunAsync(request, id, binding, answered, begun: () => Forget(confirmation)).ConfigureAwait(false);
            return;
        }

        var response = ending switch
        {
            { End: PipeConfirmationEnd.Refused } => IpcResponse.CreateError(id, Refused, null,
                "The person refused this in Daoris's window. Nothing was done."),
            // CONFIRMWAIT1a-i (§5): confirmed, and a ground it was asked on had moved; said with its reason's code and values.
            { End: PipeConfirmationEnd.Changed, Change: { } change } => IpcResponse.CreateError(id, Changed,
                new Dictionary<string, string>(change.Values) { ["ground"] = change.Reason },
                $"The person confirmed this in Daoris's window, but {change.Sentence}. Nothing was done."),
            { End: PipeConfirmationEnd.Withdrawn } when byWithdraw => IpcResponse.CreateError(id, Withdrawn, null,
                "This terminal took its request back before the person answered it. Nothing was done."),
            // CONFIRMWAIT1a-ii (§7.1): a press carrying a key keeps no payload to wait on across the close.
            { End: PipeConfirmationEnd.Lost } => IpcResponse.CreateError(id, Lost, null,
                "Daoris closed while this waited, and it carries a key, so it does not wait across a restart; nothing was done. Send it again once Daoris is back."),
            _ when IsStopping => RefuseStopping(request),
            _ => IpcResponse.CreateError(id, IpcErrorCodes.OperationCancelled, null,
                "This terminal took its request back before the person answered it. Nothing was done."),
        };
        var text = IpcJson.Serialize(response);
        Rewrite(confirmation, PipeWaitingRecord.Ended, text);
        try
        {
            if (_journal!.Begin(id, binding)) _journal.End(id, binding, text);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Not kept: the terminal still hears it now, and RESULT says it does not know, which is so.
        }

        // Removed even where the journal's line failed: the record already said it ended, so no restart asks it again.
        Forget(confirmation);
        lock (_gate)
        {
            _running.Remove(id);
            _owners.Remove(id);
        }

        // The close may have answered its line already (`waiting`): this ending is still kept, and RESULT says it.
        answered.TrySetResult(text);
    }

    private static string Capitalized(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>A role's act refused at the door (SUPERVISOR1a-ii), in the terminal's words, by its code. Nothing was kept.</summary>
    private static string RoleRefusal(string? why) => why switch
    {
        SupervisorWhy.NotOffered => "This Daoris does not offer this role's appointment yet. Nothing was kept.",
        SupervisorWhy.NotAppointed => "No appointment of this role is in force: the owner appoints it in the window. Nothing was done.",
        RoleWhy.NotRole => "That is the owner's to do; the role's appointment does not cover it. Nothing was done.",
        RoleWhy.NotGranted => "The owner's appointment of this role does not name that act. Nothing was done.",
        RoleWhy.HolderConfig =>
            "This act did not come from the role's terminal as Daoris started it, on Daoris's own configuration; its terminal is ended. Nothing was done.",
        SupervisorWhy.NotHolder => "This terminal did not prove the supervisor's key for this connection and this answer. Nothing was kept.",
        SupervisorWhy.Dormant => "The supervisor was appointed before Daoris restarted: the owner presses Appoint again. Nothing was kept.",
        SupervisorWhy.AppointmentChanged => "The appointment this answer names is not the one in force: read the question again. Nothing was kept.",
        SupervisorWhy.Form => "The answer is not the supervisor's form: an id, the question, the generation, the proof, one choice and 1 to 5 reasons, each with its source. Nothing was kept.",
        StandingWhy.InAgentJob => "An agent Daoris runs does not answer as the supervisor. Nothing was kept.",
        StandingWhy.Untold => "Daoris could not tell this terminal is outside every agent it runs. Nothing was kept.",
        _ => "No appointment covers this. Nothing was kept.",
    };

    /// <summary>A route as <c>ROUTES</c> lists it: a route classed by its payload says its rule beside its class.</summary>
    private static object Listed(PipeRoute route) => new
    {
        module = route.Module,
        route = route.Route,
        @class = route.Class,
        when = route.When is { } rule ? new { field = rule.Field, values = rule.Values, @class = rule.Class } : null,
    };

    /// <summary>A key as a request carries it: a secret of 16 to 128 characters only its sender holds.</summary>
    private static bool IsKey(string? key) => key is { Length: >= 16 and <= 128 };

    /// <summary>
    /// A press, run on the instance's lifetime and never the connection's (§6.1), its lines kept either side. One whose first
    /// line cannot be written is refused and never dispatched, so no id runs that the journal does not hold.
    /// </summary>
    /// <param name="standing">That the person's standing confirmation admitted it (TERMTRUST1a): run inside <see cref="PipeDoor.EnterStanding"/>.</param>
    /// <param name="role">The role act the judge's role method covered (SUPERVISOR1a-ii): run inside <see cref="PipeDoor.EnterRole"/>.</param>
    /// <param name="begun">Told once the journal's first line is written or failed, before anything runs (CONFIRMWAIT1a-ii, §7.1).</param>
    private async Task RunAsync(
        IpcRequest request, string id, PipeBinding binding, TaskCompletionSource<string> answered, bool standing = false,
        RoleJudgement? role = null, Action? begun = null)
    {
        string text;
        try
        {
            var journaled = _journal!.Begin(id, binding);
            begun?.Invoke();
            if (!journaled)
            {
                text = IpcJson.Serialize(Refuse(request, Unrecorded,
                    "Daoris could not keep this request in its journal, so it was not run; run it again."));
            }
            else
            {
                IpcResponse response;
                using (role is not null ? PipeDoor.EnterRole(role) : standing ? PipeDoor.EnterStanding() : PipeDoor.Enter())
                {
                    response = await _dispatcher.DispatchAsync(request, _lifetime.Token).ConfigureAwait(false);
                }

                text = IpcJson.Serialize(response);
                _journal.End(id, binding, text);
            }
        }
        catch (Exception error)
        {
            // The dispatcher never throws; nothing here may leave the press unanswered.
            text = IpcJson.Serialize(IpcResponse.CreateError(id, IpcErrorCodes.UnknownError, null, error.GetType().Name));
        }

        lock (_gate)
        {
            _running.Remove(id);
            _owners.Remove(id);
        }

        answered.TrySetResult(text);
    }

    /// <summary>
    /// The kit's <c>CANCEL { requestId }</c> from a terminal (RPC1h, §6.2), for a press this pipe began and with the key that sent it:
    /// passed to the dispatcher's own <c>SHENORA.REQUESTS</c> module, which aborts the token the press runs under, so the press ends as
    /// a cancelled one and <c>RESULT</c> says so. A press that has ended answers <c>cancelled: false</c> and nothing runs; an id this pipe
    /// did not begin (a window's, an id nobody sent) or one begun with another key is refused as an id bound elsewhere is, and
    /// nothing is cancelled. The cancel carries its own key, a secret only its sender holds, as a press does.
    /// </summary>
    private async Task<string> CancelAsync(IpcRequest request, string? key)
    {
        var target = Field(request, "requestId");
        if (string.IsNullOrEmpty(target))
        {
            return IpcJson.Serialize(IpcResponse.CreateError(request.Id, IpcErrorCodes.MissingPayloadValue, null, "CANCEL names the request's id (requestId)."));
        }

        if (!IsKey(key)) return IpcJson.Serialize(Refuse(request, KeyMissing, "a request carries a key of its own, 16 to 128 characters."));
        var holder = PipeBinding.Fingerprint(key!);
        string? waiting = null;
        lock (_gate)
        {
            if (_running.TryGetValue(target, out var running))
            {
                if (running.Binding.Key != holder) return IpcJson.Serialize(RefuseTaken(request));
                waiting = _waitingFor.GetValueOrDefault(target);
            }
            else if (_journal?.Find(target) is { } seen && seen.Binding.Key == holder)
            {
                return IpcJson.Serialize(IpcResponse.CreateSuccess(request.Id, new { cancelled = false, state = "ended" }));
            }
            else
            {
                return IpcJson.Serialize(RefuseTaken(request));
            }
        }

        _log.Info("pipe.cancelled", ("request", target));
        // RPC1c: a press still waiting for the window is taken back from it, its card gone and nothing run. One the person answered
        // meanwhile is under way, and the dispatcher's cancel reaches it as any press's does.
        if (waiting is not null && Confirmations.Withdraw(waiting) == PipeWithdrawal.Withdrawn)
        {
            return IpcJson.Serialize(IpcResponse.CreateSuccess(request.Id, new { cancelled = true, state = "withdrawn" }));
        }

        var answer = await _dispatcher.DispatchAsync(request, _lifetime.Token).ConfigureAwait(false);
        return IpcJson.Serialize(answer);
    }

    /// <summary>
    /// <c>RESULT { id, key }</c>: how the press stands, told only to the key that sent it, whichever connection asks. <c>waiting</c> while
    /// its card waits for the person (CONFIRMWAIT1a-i, the confirm-wait design §4.2), with when it was asked, whether a terminal waits on
    /// its line for it, and when a window first showed it; <c>running</c> while its Confirm is judged and once it runs; then
    /// <c>answered</c> with its ending, or <c>unknown</c>.
    /// </summary>
    private IpcResponse Result(IpcRequest request)
    {
        var id = Field(request, "id");
        if (string.IsNullOrEmpty(id)) return IpcResponse.CreateError(request.Id, IpcErrorCodes.MissingPayloadValue, null, "RESULT names the request's id.");
        var key = Field(request, "key");
        if (!IsKey(key)) return Refuse(request, KeyMissing, "RESULT carries the key its request was sent with.");
        var holder = PipeBinding.Fingerprint(key!);
        lock (_gate)
        {
            if (_running.TryGetValue(id, out var running))
            {
                if (running.Binding.Key != holder) return RefuseTaken(request);
                if (WaitingCard(id) is { } card)
                {
                    return IpcResponse.CreateSuccess(request.Id, new
                    {
                        state = "waiting", asked = card.Asked, waitedOn = card.WaitedOn, shownAt = card.ShownAt,
                    });
                }

                return IpcResponse.CreateSuccess(request.Id, new { state = "running" });
            }

            return _journal!.Find(id) switch
            {
                { } seen when seen.Binding.Key != holder => RefuseTaken(request),
                { State: PipeJournal.Seen.Answered, Answer: { } answer } =>
                    IpcResponse.CreateSuccess(request.Id, new { state = "answered", answer = JsonDocument.Parse(answer).RootElement.Clone() }),
                // CONFIRMWAIT1 (the confirm-wait design §15.3 item 10): begun and never ended, it stopped while under way, as WITHDRAW says.
                { State: PipeJournal.Seen.Interrupted } => IpcResponse.CreateSuccess(request.Id, new
                {
                    state = "answered",
                    answer = JsonSerializer.SerializeToElement(InterruptedAnswer(id), IpcJson.Options),
                }),
                _ => IpcResponse.CreateSuccess(request.Id, new { state = "unknown" }),
            };
        }
    }

    /// <summary>The card of a press still waiting for the person and not being judged, by its request's id; null otherwise. Under the lock.</summary>
    private PipeConfirmation? WaitingCard(string id) =>
        _waitingFor.TryGetValue(id, out var confirmation)
            ? Confirmations.Now().Waiting.FirstOrDefault(each => each.Id == confirmation && !each.Checking)
            : null;

    /// <summary>
    /// <c>WITHDRAW { id, key }</c> (CONFIRMWAIT1a-i, the confirm-wait design §4.3): the asker takes back a press of its own that still
    /// waits for the person, under the confirmations' lock, where a Confirm is taken too, so the first of the two wins. Answered
    /// <c>withdrawn</c>; <c>running</c> where the person confirmed it meanwhile, its grounds being judged or it running, and nothing is
    /// cancelled; <c>ended</c> with its answer; another key's, or an id the pipe does not know, refused <see cref="IdTaken"/>. Never the
    /// kit's <c>CANCEL</c>, which reaches a confirmed press and aborts it.
    /// </summary>
    private IpcResponse Withdraw(IpcRequest request)
    {
        var id = Field(request, "id");
        if (string.IsNullOrEmpty(id)) return IpcResponse.CreateError(request.Id, IpcErrorCodes.MissingPayloadValue, null, "WITHDRAW names the request's id.");
        var key = Field(request, "key");
        if (!IsKey(key)) return Refuse(request, KeyMissing, "WITHDRAW carries the key its request was sent with.");
        var holder = PipeBinding.Fingerprint(key!);
        string? waiting = null;
        lock (_gate)
        {
            if (_running.TryGetValue(id, out var running))
            {
                if (running.Binding.Key != holder) return RefuseTaken(request);
                waiting = _waitingFor.GetValueOrDefault(id);
                // Marked before the confirmations are asked, so the press's ending says it was this route's.
                if (waiting is not null) _withdrawn.Add(id);
            }
            else if (_journal?.Find(id) is { } seen && seen.Binding.Key == holder)
            {
                return IpcResponse.CreateSuccess(request.Id, new
                {
                    state = "ended",
                    answer = seen.State == PipeJournal.Seen.Answered && seen.Answer is { } answer
                        ? JsonDocument.Parse(answer).RootElement.Clone()
                        : JsonSerializer.SerializeToElement(InterruptedAnswer(id), IpcJson.Options),
                });
            }
            else
            {
                return RefuseTaken(request);
            }
        }

        if (waiting is not null && Confirmations.Withdraw(waiting) == PipeWithdrawal.Withdrawn)
        {
            _log.Info("pipe.withdrawn", ("request", id));
            return IpcResponse.CreateSuccess(request.Id, new { state = "withdrawn" });
        }

        lock (_gate)
        {
            _withdrawn.Remove(id);
        }

        return IpcResponse.CreateSuccess(request.Id, new { state = "running" });
    }

    /// <summary>The answer of a press begun by an instance that stopped before it ended: whether it was done is not known.</summary>
    private static IpcResponse InterruptedAnswer(string id) => IpcResponse.CreateError(id, Interrupted, null,
        "Daoris stopped while this request was under way, so whether it was done is not known; it was not run again.");

    private static string? Field(IpcRequest request, string name) =>
        request.Payload is { ValueKind: JsonValueKind.Object } payload && payload.TryGetProperty(name, out var named)
        && named.ValueKind == JsonValueKind.String ? named.GetString() : null;

    private IpcResponse RefuseTaken(IpcRequest request) =>
        Refuse(request, IdTaken, "this id was sent with another request or another key; send yours with an id of its own.");

    private IpcResponse RefuseStopping(IpcRequest request) =>
        Refuse(request, Stopping, "Daoris is closing; run this again once it is back.",
            ("why", Volatile.Read(ref _stopping) == 1 ? "close" : "update"));

    /// <summary>A refusal the pipe answers itself, said in the machine log as the refusal log says one, as the terminal's (§8).</summary>
    private IpcResponse Refuse(IpcRequest request, string code, string message, params (string Key, string Value)[] parameters)
    {
        _log.Info("refused", ("code", code), ("request", $"{request.Module}.{request.Type}"), ("door", PipeDoor.Terminal));
        return IpcResponse.CreateError(
            request.Id, code, parameters.Length == 0 ? null : parameters.ToDictionary(each => each.Key, each => each.Value), message);
    }

    /// <summary>
    /// One terminal's connection: its hello, then its requests. What is answered at once is written in order before the next line
    /// is taken, so a terminal that sends on without reading holds its own lines back; a press is answered as it ends, at most
    /// <see cref="PipeLimits.InFlight"/> at once, and lines sent past <see cref="PipeLimits.Queued"/> close the connection.
    /// </summary>
    private sealed class Connection(InstancePipe pipe, NamedPipeServerStream stream)
    {
        private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
        private readonly SemaphoreSlim _writing = new(1, 1);
        private readonly Channel<string> _lines = Channel.CreateBounded<string>(new BoundedChannelOptions(Math.Max(1, pipe._limits.Queued))
        {
            SingleReader = true,
            SingleWriter = true,
        });

        private int _underWay;
        private int _closed;

        /// <summary>
        /// The terminal at the far end, as the system names the pipe's client (RPC1c): what the window's card names as the asker,
        /// read once as the connection is taken, never from what the terminal says of itself.
        /// </summary>
        public PipeAsker Asker { get; } = AskerOf(stream);

        /// <summary>
        /// The terminal's process, held by a handle opened as the connection is taken and kept for its life (TERMTRUST1a, the
        /// terminal-trust design §3.3 point 3): what the standing confirmation's judge asks the jobs about.
        /// </summary>
        public PipeClientProcess Client { get; } = PipeClientProcess.Of(stream);

        // SUPERVISOR1a-ii (D208 §2.2): this connection's hello's nonce and when it was drawn, in memory for this connection alone.
        private readonly Lock _nonceGate = new();
        private string? _nonce;
        private long _nonceAt;

        /// <summary>How long a hello's nonce may be used: forgotten at its first use, at the connection's end or after this.</summary>
        public static readonly TimeSpan NonceLife = TimeSpan.FromSeconds(60);

        /// <summary>A fresh nonce for this connection's hello, replacing any it drew before.</summary>
        public string DrawNonce()
        {
            lock (_nonceGate)
            {
                _nonce = SupervisorProof.Nonce();
                _nonceAt = Environment.TickCount64;
                return _nonce;
            }
        }

        /// <summary>The hello's nonce for one act, forgotten as it is taken: null where none was drawn, it was used, or its minute passed.</summary>
        public string? TakeNonce()
        {
            lock (_nonceGate)
            {
                var nonce = _nonce;
                var fresh = nonce is not null && Environment.TickCount64 - _nonceAt <= (long)NonceLife.TotalMilliseconds;
                _nonce = null;
                return fresh ? nonce : null;
            }
        }

        /// <summary>Whether a press this connection sent has not been answered yet.</summary>
        public bool Answering => Volatile.Read(ref _underWay) > 0;

        /// <summary>Whether this connection may have one more press under way.</summary>
        public bool HasRoom => Volatile.Read(ref _underWay) < pipe._limits.InFlight;

        public async Task RunAsync()
        {
            try
            {
                _ = ReadAsync();
                // The hello first, within its bound: anything else closes the connection unanswered (§6).
                if (await NextAsync(pipe._limits.Hello, waitWhileBusy: false).ConfigureAwait(false) is not { } first
                    || Parse(first) is not ({ } hello, _)
                    || !string.Equals(hello.Module, Module, StringComparison.OrdinalIgnoreCase) || hello.Type != "HELLO")
                {
                    return;
                }

                var answered = pipe.Hello(hello, this);
                await WriteAsync(IpcJson.Serialize(answered)).ConfigureAwait(false);
                if (!answered.Success) return;

                while (await NextAsync(pipe._limits.Idle, waitWhileBusy: true).ConfigureAwait(false) is { } line)
                {
                    if (Parse(line) is not ({ } request, var key)) return;
                    var (now, later) = pipe.Accept(request, key, this);
                    if (now is not null)
                    {
                        await WriteAsync(now).ConfigureAwait(false);
                    }
                    else
                    {
                        // Counted from here, on this loop alone, so the room asked above is the room taken.
                        Interlocked.Increment(ref _underWay);
                        _ = AnswerLaterAsync(later!);
                    }
                }
            }
            finally
            {
                Close();
                pipe.Ended(this);
            }
        }

        private async Task AnswerLaterAsync(Task<string> answer)
        {
            try
            {
                await WriteAsync(await answer.ConfigureAwait(false)).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref _underWay);
            }
        }

        /// <summary>The next line, or null: the connection closed, a line too long, or nothing said within <paramref name="within"/>.</summary>
        private async Task<string?> NextAsync(TimeSpan within, bool waitWhileBusy)
        {
            while (true)
            {
                // A connection closed under it (it ran past its lines' bound, or the instance ends) takes no more of them.
                if (Volatile.Read(ref _closed) == 1) return null;
                using var bound = CancellationTokenSource.CreateLinkedTokenSource(pipe._lifetime.Token);
                bound.CancelAfter(within);
                try
                {
                    return await _lines.Reader.ReadAsync(bound.Token).ConfigureAwait(false);
                }
                catch (ChannelClosedException)
                {
                    return null;
                }
                catch (OperationCanceledException) when (!pipe._lifetime.IsCancellationRequested)
                {
                    // Idle is a connection with nothing under way: one waiting on a long press is not.
                    if (waitWhileBusy && Volatile.Read(ref _underWay) > 0) continue;
                    return null;
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }
        }

        /// <summary>Reads the connection's bytes into lines, bounded in length; ends the lines where it closes or a line runs over.</summary>
        private async Task ReadAsync()
        {
            var buffer = new byte[64 * 1024];
            using var line = new MemoryStream();
            try
            {
                while (true)
                {
                    var read = await stream.ReadAsync(buffer, pipe._lifetime.Token).ConfigureAwait(false);
                    if (read == 0) break;
                    var start = 0;
                    for (var at = 0; at < read; at++)
                    {
                        if (buffer[at] != (byte)'\n') continue;
                        line.Write(buffer, start, at - start);
                        start = at + 1;
                        if (line.Length > pipe._limits.LineBytes) return;
                        // Lines past the bound pile up only where nothing it was answered is read: the connection ends.
                        if (!_lines.Writer.TryWrite(Utf8.GetString(line.GetBuffer(), 0, (int)line.Length).TrimEnd('\r')))
                        {
                            Close();
                            return;
                        }

                        line.SetLength(0);
                    }

                    line.Write(buffer, start, read - start);
                    if (line.Length > pipe._limits.LineBytes) return;
                }
            }
            catch (Exception error) when (error is IOException or OperationCanceledException or ObjectDisposedException)
            {
                // Closed under it: the lines end here.
            }
            finally
            {
                _lines.Writer.TryComplete();
            }
        }

        /// <summary>
        /// The kit's envelope on a line, and the <c>key</c> beside it (its sender's secret, which binds the id, a second agent's
        /// review of RPC1a); null where the line is no request.
        /// </summary>
        private static (IpcRequest Request, string? Key)? Parse(string line)
        {
            try
            {
                if (IpcJson.Deserialize<IpcRequest>(line) is not { Module: { Length: > 0 }, Type: { Length: > 0 } } request) return null;
                using var document = JsonDocument.Parse(line);
                var key = document.RootElement.TryGetProperty("key", out var named) && named.ValueKind == JsonValueKind.String
                    ? named.GetString()
                    : null;
                return (request, key);
            }
            catch (Exception error) when (error is JsonException or NotSupportedException or ArgumentException or InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>One line to the terminal, whole; nothing where it has gone.</summary>
        public async Task WriteAsync(string json)
        {
            if (Volatile.Read(ref _closed) == 1) return;
            var bytes = Utf8.GetBytes(json + "\n");
            try
            {
                await _writing.WaitAsync().ConfigureAwait(false);
                try
                {
                    await stream.WriteAsync(bytes).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);
                }
                finally
                {
                    _writing.Release();
                }
            }
            catch (Exception error) when (error is IOException or ObjectDisposedException or InvalidOperationException)
            {
                // The terminal has gone: its press runs on, and RESULT says how it ended (§6.1).
            }
        }

        /// <summary>
        /// Closes this end. Not a disconnect, which drops what the terminal has not read yet: a refusal written just before still
        /// reaches it, and it then reads the end.
        /// </summary>
        public void Close()
        {
            if (Interlocked.Exchange(ref _closed, 1) == 1) return;
            TakeNonce();
            stream.Dispose();
            Client.Dispose();
        }

        private static PipeAsker AskerOf(NamedPipeServerStream stream)
        {
            if (!OperatingSystem.IsWindows()) return new PipeAsker(null, null);
            try
            {
                if (!GetNamedPipeClientProcessId(stream.SafePipeHandle, out var id)) return new PipeAsker(null, null);
                var pid = (int)id;
                try
                {
                    using var process = Process.GetProcessById(pid);
                    return new PipeAsker(process.ProcessName, pid);
                }
                catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Gone already, or not ours to read: the card names its process id alone.
                    return new PipeAsker(null, pid);
                }
            }
            catch (Exception error) when (error is InvalidOperationException or ObjectDisposedException or EntryPointNotFoundException)
            {
                return new PipeAsker(null, null);
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GetNamedPipeClientProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint clientProcessId);
    }
}
