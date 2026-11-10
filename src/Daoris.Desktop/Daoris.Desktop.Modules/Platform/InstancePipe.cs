using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The running Daoris answers on a pipe: a terminal's verb is the instance's client,
/// and its request runs through the dispatcher every window's page uses, so the route is the window's press: the same handler, the
/// same middleware (<see cref="RefusalLog"/>, <see cref="UiStallWatch"/>, <see cref="OffTheWindow"/>, <see cref="StatePublisher"/>),
/// the same answer, under way in every window and heard by every window as it happens.
/// </summary>
/// <remarks>
/// <para><b>The pipe</b>: <see cref="PipeName"/>'s name, this Windows user's alone (owned by the current user's SID at either
/// elevation, and an ACL granting that SID and denying the network's: <see cref="Security"/>), made as the first
/// instance of its name, so where the name is already held this instance serves none and says so (<c>pipe.refused</c>) rather
/// than share it. Windows only: elsewhere nothing is served.</para>
///
/// <para><b>The wire</b>: UTF-8 lines of JSON. The hello first (<c>DAORIS.PIPE</c> <c>HELLO</c>), answered with this home's id,
/// the build and the process, or refused <see cref="WrongProtocol"/>, <see cref="WrongHome"/> or <see cref="Stopping"/>; anything
/// before it closes the connection. Then the kit's request envelopes, several at once, each answered with its response envelope
/// by id, a route's progress forwarded to the connection that sent it as the kit's notification envelope; <c>ROUTES</c> and
/// <c>RESULT</c> are the pipe's own.</para>
///
/// <para><b>The census decides what runs</b> (<see cref="PipeCensus"/>): an open route runs; a person's is shown to the
/// window (<see cref="Confirmations"/>) and runs only on the person's <b>Confirm</b>, however long it waits,
/// the terminal told it waits (<see cref="Confirming"/>) and, refused, changed, withdrawn or lost, how it ended; each waiting press is
/// kept in a record of its own under <c>data/pipe-waiting/</c> and brought back after a restart (<see cref="Restore"/>);
/// a window's is refused
/// <see cref="WindowOnly"/>; a module the census does not name <see cref="Unclassed"/>. Nothing refused reaches the dispatcher.</para>
///
/// <para><b>A request's id is durable</b> (<see cref="PipeJournal"/>): a press runs on the instance's lifetime, not the
/// connection's, so a lost terminal cancels nothing; an id seen before is answered from its line and never run twice, and
/// <c>RESULT</c> says <c>running</c>, <c>answered</c> with the answer, or <c>unknown</c>. Each id is bound to its route, its payload
/// and the <c>key</c> its sender sent beside the envelope (<see cref="PipeBinding"/>): sent again with any of them changed it is
/// refused <see cref="IdTaken"/>, and <c>RESULT</c> answers only that key. A press whose first line the journal cannot write is
/// refused <see cref="Unrecorded"/> and never dispatched.</para>
///
/// <para><b>Each connection is bounded</b> (<see cref="PipeLimits"/>): presses under way past <see cref="PipeLimits.InFlight"/> are
/// answered <see cref="Busy"/>, and lines sent past <see cref="PipeLimits.Queued"/> ahead of the instance close it.</para>
///
/// <para><b>Stopping</b>: from <see cref="BeginStopping"/>, or once the update applies (<see cref="Applying"/>), every new
/// hello and every new press is refused <see cref="Stopping"/> and every Confirm takes nothing; from <see cref="BeginStopping"/> a line
/// waiting on the person's Confirm is answered <see cref="Waiting"/>, the press kept in its record, and
/// <see cref="Drain"/> lets the presses under way answer; the loop then takes its address away and lets the home go.</para>
///
/// <para><b>An update's drain is not yet its close</b>: while it only drains (<see cref="Draining"/>), which can be hours, a
/// hello is answered and a press the census says the drain waits for (<see cref="PipeCensus.WaitsForDrain"/>) is refused
/// <see cref="Stopping"/>, <c>why: update</c>, or, confirmed, takes nothing and waits; every other press, a read, a stop, a take-back
/// or an answer, runs as at any time.</para>
///
/// <para>A request that came over the pipe is said as the terminal's (<see cref="PipeDoor"/>): the refusal log and the pipe's own
/// refusals write <c>door: terminal</c>.</para>
///
/// <para><b>Its parts and their state.</b> The presses and connections under way are <see cref="PipeState"/>'s, under its one gate; the
/// waiting presses' records are <see cref="PipeWaitingKeep"/>'s, under a lock of their own; neither is taken inside the other, and the
/// confirmations' lock only inside the gate. The listening is <see cref="PipeListener"/>'s, a connection <see cref="PipeConnection"/>'s,
/// a request's admission <see cref="PipeAdmission"/>'s, a person's press from its reservation to its ending <see cref="PipeAsking"/>'s,
/// <c>CANCEL</c>, <c>RESULT</c> and <c>WITHDRAW</c> <see cref="PipeTakeBacks"/>'s, the restart's <see cref="PipeRestore"/>'s, and a
/// terminal's hold of a conversation, <c>HOLD</c> and <c>LET_GO</c>, <see cref="PipeHolding"/>'s, and a terminal's heavy run,
/// <c>RUN_HEAVY</c> and <c>RUN_STOP</c>, <see cref="PipeHeavyRuns"/>'s. This type keeps the wire's words, the serve, the close and the run.</para>
/// </remarks>
public sealed class InstancePipe : IDisposable
{
    /// <summary>The pipe's own module on the wire: its hello, its census and how a request ended.</summary>
    public const string Module = "DAORIS.PIPE";

    /// <summary>The kit's request tracker's module, whose <c>CANCEL</c> a terminal's Ctrl+C sends for a press of its own.</summary>
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
    /// A route the census makes the person's, as an older instance refused it: kept as a word of the wire, which a
    /// client still reads from such an instance. This one asks the window instead.
    /// </summary>
    public const string ConfirmInWindow = "confirm-in-window";

    /// <summary>The notification that tells a terminal its press waits for the person in the window, naming its request.</summary>
    public const string Confirming = "CONFIRMING";

    /// <summary>The person refused the press in the window: nothing ran.</summary>
    public const string Refused = "refused";

    /// <summary>
    /// An older instance ended the press by its time: nothing ran. Kept as a word of the wire, as
    /// <see cref="ConfirmInWindow"/> is; this one makes it no more, since a terminal's press waits until the person answers.
    /// </summary>
    public const string Expired = "expired";

    /// <summary>The person confirmed it and a ground it is asked on has moved: nothing ran.</summary>
    public const string Changed = "changed";

    /// <summary>Its terminal took it back by the pipe's <c>WITHDRAW</c> before the person answered it: nothing ran.</summary>
    public const string Withdrawn = "withdrawn";

    /// <summary>A setting the press sets is already as it asks: nothing is asked, and the world is as asked.</summary>
    public const string Already = "already";

    public const string NotAllowed = "not-allowed";

    /// <summary>A person's press whose route names no grounds: Daoris cannot wait for the person on it, so nothing is asked.</summary>
    public const string Ungrounded = "ungrounded";

    /// <summary>A person's press whose grounds could not be read at its ask, or not within their bound: nothing is asked.</summary>
    public const string UnreadGrounds = "unread-grounds";

    /// <summary>As many of the person's presses as may wait already do: this one is not asked.</summary>
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
    /// The close's answer to a line waiting on a press, <c>why: close</c>: the press
    /// still waits, kept in its record across the restart.
    /// </summary>
    public const string Waiting = "waiting";

    /// <summary>
    /// A press not kept across a restart: one carrying a key at the close, or a record the restore could not bring back.
    /// Nothing ran.
    /// </summary>
    public const string Lost = "lost";

    private readonly IEventBus _bus;
    private readonly string _version;
    private readonly PipeAdmission _admission;
    private readonly PipeRestore _restore;
    private PipeListener? _listener;
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
    /// <param name="standing">The roles' judge: this home's where none is named; a test hands its own.</param>
    public InstancePipe(
        IMessageDispatcher dispatcher, IEventBus bus, string home, MachineLog? log = null, PipeCensus? census = null,
        string? version = null, PipeLimits? limits = null, TimeProvider? clock = null, StandingConfirmation? standing = null)
    {
        Dispatcher = dispatcher;
        _bus = bus;
        Home = home;
        Log = log ?? MachineLog.None;
        Census = census ?? PipeCensus.Daoris;
        _version = version ?? typeof(InstancePipe).Assembly.GetName().Version?.ToString() ?? "0";
        Limits = limits ?? new PipeLimits();
        Clock = clock ?? TimeProvider.System;
        Confirmations = new PipeConfirmations(bus, Log, clock, Limits.Waiting);
        // A press's record keeps when it is first shown, and a Confirm while this closes or drains takes nothing.
        Keep = new PipeWaitingKeep(new PipeWaiting(DaorisHome.Entry(home, HomeEntry.PipeWaiting)));
        Asking = new PipeAsking(this);
        TakeBacks = new PipeTakeBacks(this);
        StewardAllows = new StewardAllows(this);
        Holding = new PipeHolding(this, bus);
        HeavyRuns = new PipeHeavyRuns(this);
        Delegate = new PipeDelegate(this);
        _admission = new PipeAdmission(this);
        _restore = new PipeRestore(this);
        Confirmations.ShownFirst = Keep.Shown;
        // An update's drain holds only the presses that would add to what it waits on; its close holds all.
        Confirmations.Holding = (module, route) => Holds(module, route);
        // Each ending is written to the press's record before it is told or run.
        Confirmations.Persisting = Asking.Persisted;
        StandingConfirmation = standing ?? new StandingConfirmation(home, Log, clock: clock);
        if (StandingConfirmation.Caller is StandingCaller look) look.OwnPipe = () => Name;
    }

    /// <summary>
    /// The roles' judge: the terminals that hold a role this run, the role's acts judged and the steward's record and desk.
    /// </summary>
    public StandingConfirmation StandingConfirmation { get; }

    /// <summary>
    /// The person's presses a terminal sent, waiting for the window: the bridge's <c>PERSON_CONFIRMATIONS</c> reads them and
    /// its <c>PERSON_CONFIRM</c> answers one, which a terminal never may.
    /// </summary>
    public PipeConfirmations Confirmations { get; }

    /// <summary>
    /// What a person's press's grounds are read from: the loop's readers, handed as the
    /// standing confirmation's are. None reads nothing, so every press that names a ground is refused <see cref="UnreadGrounds"/>.
    /// </summary>
    public IPipeGroundWorld? Grounds { get; set; }

    /// <summary>The sessions Daoris started, as a proved press reads them, the loop's; none refuses every proof.</summary>
    public Func<string, PipeDelegateSession?>? Delegates { get; set; }

    internal PipeDelegate Delegate { get; }

    internal void TellDriverState() => StatePublisher.Publish(_bus, StateScopes.Driver);

    internal PipeState State { get; } = new();

    internal PipeWaitingKeep Keep { get; }

    internal PipeAsking Asking { get; }

    internal PipeTakeBacks TakeBacks { get; }

    /// <summary>A steward's act outside its grants, offered to the allow supervisor before the person's card.</summary>
    public StewardAllows StewardAllows { get; }

    /// <summary>A terminal's hold of a conversation: the window's <c>SESSION_HOLDS</c> reads it and <c>SESSION_LET_GO</c> ends one.</summary>
    public PipeHolding Holding { get; }

    /// <summary>The heavy runs terminals asked for, each held to its connection.</summary>
    internal PipeHeavyRuns HeavyRuns { get; }

    internal IMessageDispatcher Dispatcher { get; }

    /// <summary>Where a request's caller's parents are read, so a hook's or a helper's gits take the place their git lends (<see cref="PipeGitCaller"/>).</summary>
    internal IProcessTable Processes { get; init; } = new SystemProcesses();

    internal string Home { get; }

    internal MachineLog Log { get; }

    internal PipeCensus Census { get; }

    internal PipeLimits Limits { get; }

    internal TimeProvider Clock { get; }

    /// <summary>The instance's lifetime, which every press runs on and the close cancels.</summary>
    internal CancellationTokenSource Lifetime { get; } = new();

    /// <summary>
    /// Bring back the presses that waited when Daoris last stopped: once, before
    /// <see cref="Serve"/>, on the home's lease, which makes this instance the one writer of its records.
    /// </summary>
    /// <remarks>
    /// Each record in <c>asked</c> order: a name that is no confirmation's id is removed; a newer Daoris's record is left, said
    /// once; one <c>begun</c> answers <c>interrupted</c> and one <c>ended</c> its ending, journaled where the journal lacks it, and is
    /// removed; one carrying a key is lost; one waiting is judged again as <see cref="Accept"/> judges a press, from its module, route and
    /// payload alone (the census's <c>Person</c>, its binding, its shape, a grounds row, an id the journal does not hold, the bound), its
    /// card made from its payload and never read from the record, and waits again; one that fails or does not read is lost, journaled
    /// where its terminal's id reads, removed and never asked. The person is told once: a notice for the lost, one for a newer Daoris's,
    /// and one balloon for those waiting again.
    /// </remarks>
    public void Restore() => _restore.Run();

    /// <summary>
    /// Whether an update drains this instance, its applying included: the install's <see cref="InstallUpdater.Draining"/>.
    /// While it only drains, a press that would add to what it waits on waits for the restart and the rest go on.
    /// </summary>
    public Func<bool>? Draining { get; set; }

    /// <summary>
    /// Whether the update is past its drain and the application closes for it: the install's
    /// <see cref="InstallUpdater.Applying"/>. A stop's beginning, as <see cref="BeginStopping"/> is: every new hello and press refused,
    /// every Confirm held. A press let through the drain and still under way is the close's to wait for, as any begun press is.
    /// </summary>
    public Func<bool>? Applying { get; set; }

    /// <summary>The pipe's name once served, or null.</summary>
    public string? Name { get; private set; }

    /// <summary>Whether the stop has begun or an update drains or applies: what a press may be held for.</summary>
    public bool IsStopping => Closing || Draining?.Invoke() == true;

    /// <summary>Whether every new hello and press is refused and every Confirm held: the stop has begun, or the update applies.</summary>
    internal bool Closing => Stopped || Applying?.Invoke() == true;

    /// <summary>Whether the stop has begun (<see cref="BeginStopping"/>), the update's applying aside.</summary>
    internal bool Stopped => Volatile.Read(ref _stopping) == 1;

    /// <summary>
    /// Whether a press of <paramref name="module"/>'s <paramref name="route"/>, classed <paramref name="class"/>, waits for the restart rather
    /// than run: every one while the stop has begun or the update applies; while it only drains, those the census says the
    /// drain waits for (<see cref="PipeCensus.WaitsForDrain"/>).
    /// </summary>
    internal bool Holds(string? module, string? route, PipeClass? @class = PipeClass.Person) =>
        Closing || (Draining?.Invoke() == true && Census.WaitsForDrain(module, route, @class));

    /// <summary>
    /// Serve the pipe, and the name it is served under; null where none is served: not Windows, a home with no id, or a name
    /// another process already holds (said in the machine log, <c>pipe.refused</c>). Once: a second call answers the first's name.
    /// </summary>
    public string? Serve()
    {
        if (!OperatingSystem.IsWindows()) return null;
        lock (State.Gate)
        {
            if (Name is not null || Volatile.Read(ref _disposed) == 1) return Name;
            if (HomeIdentity.Ensure(Home, out var unread) is not { } id)
            {
                Log.Warn("pipe.refused", ("why", "home"), ("unread", unread));
                return null;
            }

            using var self = Process.GetCurrentProcess();
            var name = PipeName.For(id, self.SessionId);
            NamedPipeServerStream first;
            try
            {
                first = PipeListener.Create(name, Limits.Connections, first: true);
            }
            catch (Exception error) when (error is UnauthorizedAccessException or IOException)
            {
                // The first instance of its name or none: a name already held is never shared.
                Log.Warn("pipe.refused", ("why", "held"));
                return null;
            }

            _homeId = id;
            // The journal's folder is the home's `pipe` entry, asked of the table as every home path is.
            // Made by the restore where it ran first, which journals what it brought back or lost.
            State.Journal ??= new PipeJournal(DaorisHome.Entry(Home, HomeEntry.Pipe), Limits.Kept);
            _listener = new PipeListener(this, name);
            _progress = _bus.Subscribe("*", IpcRequestEvents.Updated, Progressed);
            Holding.Listen();
            Name = name;
            var listener = _listener;
            _accepting = Task.Run(() => listener.AcceptAsync(first));
            Log.Info("pipe.served", ("connections", Limits.Connections));
            return name;
        }
    }

    /// <summary>
    /// The stop begins: every new hello and every new press is refused <see cref="Stopping"/> from here. What waits for the window
    /// is not ended: each line waiting on one is answered <see cref="Waiting"/>,
    /// <c>why: close</c>, and the press leaves what is under way, so the drain is not spent on it, its record kept for the next start; a
    /// Confirm meanwhile takes nothing. A press carrying a key keeps no payload to wait on, so it ends <see cref="Lost"/>, said to its
    /// terminal and the person. Once: a second call does nothing more.
    /// </summary>
    public void BeginStopping()
    {
        if (Interlocked.Exchange(ref _stopping, 1) == 1) return;
        (string Id, string Confirmation, TaskCompletionSource<string>? Line)[] waiting;
        PipeReserved[] reserved;
        lock (State.Gate)
        {
            waiting = [.. State.WaitingFor.Select(each => (each.Key, each.Value, State.Lines.GetValueOrDefault(each.Key)))];
            reserved = [.. State.Reserved.Values];
            State.Reserved.Clear();
        }

        // A press whose grounds are still being read is never asked, so the close ends it `stopping` at once,
        // journaled, and the drain is not spent on its read.
        foreach (var each in reserved)
        {
            each.Reading.Cancel();
            Confirmations.Let(each.Place);
            Asking.EndReserved(each, RefuseStopping(each.Request));
        }

        StewardAllows.Close();
        Holding.CloseAsks();
        foreach (var (id, confirmation, line) in waiting) KeepAtClose(id, confirmation, line);
    }

    /// <summary>
    /// The close's step for one press waiting for the person: its line answered <see cref="Waiting"/>, <c>why: close</c>, and the
    /// press out of what is under way, its record kept; one carrying a key is lost.
    /// </summary>
    internal void KeepAtClose(string id, string confirmation, TaskCompletionSource<string>? line)
    {
        // A press confirmed and written `begun` is under way, not waiting: it stays in what the close's drain waits for, and its
        // line hears its run. One taken since the close's look at what waits has left the waiting already.
        if (Keep.Begun(confirmation)) return;
        lock (State.Gate)
        {
            if (State.WaitingFor.GetValueOrDefault(id) != confirmation) return;
        }

        if (Keep.Keyed(confirmation))
        {
            Confirmations.Lose(confirmation);
            return;
        }

        lock (State.Gate)
        {
            State.Running.Remove(id);
            State.Owners.Remove(id);
            State.Lines.Remove(id);
        }

        Confirmations.WaitedOn(confirmation, false);
        line?.TrySetResult(IpcJson.Serialize(IpcResponse.CreateError(id, Waiting, new Dictionary<string, string> { ["why"] = "close" },
            $"Daoris is closing; this still waits for your confirmation (request {id}).")));
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
            // The stop goes on, and the machine log names how many presses it left, so a terminal told `nothing is done`
            // by a closing Daoris can be read against what the instance knew. Presses not yet answered, not lines not yet written.
            int left;
            lock (State.Gate)
            {
                left = State.Running.Count;
            }

            Log.Warn("pipe.drained", ("left", left));
        }

        return drained;
    }

    private bool DrainWithin(TimeSpan within)
    {
        var until = DateTime.UtcNow + within;
        Task[] running;
        lock (State.Gate)
        {
            running = [.. State.Running.Values.Select(each => each.Answer)];
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
            lock (State.Gate)
            {
                answering = State.Connections.Any(each => each.Answering);
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
        Lifetime.Cancel();
        _progress?.Dispose();
        Holding.Dispose();
        PipeConnection[] open;
        lock (State.Gate)
        {
            open = [.. State.Connections];
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

        Lifetime.Dispose();
    }

    /// <summary>The pipe's security: the driver's rule for this user's pipes, <see cref="UserPipe.Security"/>.</summary>
    [SupportedOSPlatform("windows")]
    public static PipeSecurity Security() => UserPipe.Security();

    /// <summary>A connection ended: its place freed, its presses' cards told no terminal waits on them now, and its hold ended.</summary>
    internal void Ended(PipeConnection connection)
    {
        _listener?.Ended(connection);
        Holding.ConnectionEnded(connection);
        HeavyRuns.ConnectionEnded(connection);
    }

    /// <summary>A request on a connection that said its hello: <see cref="PipeAdmission.Accept"/>.</summary>
    /// <param name="hold">That it is sent with <c>hold: true</c> beside its envelope.</param>
    internal (string? Now, Task<string>? Later) Accept(
        IpcRequest request, string? key, PipeConnection connection, bool hold = false, PipeDelegateClaim? claim = null) =>
        _admission.Accept(request, key, connection, hold, claim);

    /// <summary>The kit's tracker told a request's progress: forwarded to the connection that sent it, and to nobody else.</summary>
    private Task Progressed(EventMessage message)
    {
        if (message.Payload is not IpcRequestStatus status) return Task.CompletedTask;
        PipeConnection? owner;
        lock (State.Gate)
        {
            State.Owners.TryGetValue(status.Id, out owner);
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
    internal IpcResponse Hello(IpcRequest hello, PipeConnection connection)
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
        // A drain takes a hello, since a take-back or a stop comes after one; the close it ends in does not.
        if (Closing) return RefuseStopping(hello);
        // A fresh nonce for this connection alone, which a holder's proof is bound to; an older verb ignores it.
        return IpcResponse.CreateSuccess(hello.Id, new
        {
            protocol = Protocol, home = _homeId, version = _version, pid = Environment.ProcessId, nonce = connection.DrawNonce(),
        });
    }

    /// <summary>
    /// A read of the driver's own, called in process through the dispatcher every window uses, with <paramref name="payload"/> as a
    /// window would send it: its answer's data. Only a route the census leaves open runs,
    /// so a ground's read never presses anything; a read told by event is asked again until it answers what it read. A refusal
    /// throws, which the grounds take as a read that failed.
    /// </summary>
    public async Task<JsonElement> ReadAsync(string module, string route, object? payload, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToNode(payload ?? new { }, IpcJson.Options) as System.Text.Json.Nodes.JsonObject ?? [];
        // A read told by event answers what it held, one ask behind; a payload of its own holds nothing, so what it answers is
        // what its work read now.
        if (string.Equals(module, DriverModule.Name, StringComparison.OrdinalIgnoreCase) && DriverModule.ByEventRoutes.Contains(route))
        {
            body["grounds"] = Guid.NewGuid().ToString("N");
        }

        var element = JsonSerializer.SerializeToElement(body, IpcJson.Options);
        if (Census.Of(module, route, element) != PipeClass.Open) throw new InvalidOperationException($"{route} is not a read.");
        var request = new IpcRequest { Id = $"grounds-{Guid.NewGuid():N}", Module = module, Type = route, Payload = element };
        while (true)
        {
            var response = await Dispatcher.DispatchAsync(request, cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// A press, run on the instance's lifetime and never the connection's, its lines kept either side. One whose first
    /// line cannot be written is refused and never dispatched, so no id runs that the journal does not hold.
    /// </summary>
    /// <param name="role">The role act the judge's role method covered: run inside <see cref="PipeDoor.EnterRole"/>.</param>
    /// <param name="begun">Told once the journal's first line is written or failed, before anything runs.</param>
    /// <param name="client">The connection's client, which an open route that counts its caller reads (<see cref="PipeDoor.Client"/>).</param>
    internal async Task RunAsync(
        IpcRequest request, string id, PipeBinding binding, TaskCompletionSource<string> answered,
        RoleJudgement? role = null, Action? begun = null, PipeClientProcess? client = null, string? worker = null)
    {
        string text;
        try
        {
            var journaled = State.Journal!.Begin(id, binding);
            begun?.Invoke();
            if (!journaled)
            {
                text = IpcJson.Serialize(Refuse(request, Unrecorded,
                    "Daoris could not keep this request in its journal, so it was not run; run it again."));
            }
            else
            {
                IpcResponse response;
                using (role is not null ? PipeDoor.EnterRole(role, client) : worker is not null ? PipeDoor.EnterWorker(worker, client) : PipeDoor.Enter(client))
                using (PipeGitCaller.Enter(Processes, client, Environment.ProcessId))
                {
                    response = await Dispatcher.DispatchAsync(request, Lifetime.Token).ConfigureAwait(false);
                }

                text = IpcJson.Serialize(response);
                State.Journal.End(id, binding, text);
            }
        }
        catch (Exception error)
        {
            // The dispatcher never throws; nothing here may leave the press unanswered.
            text = IpcJson.Serialize(IpcResponse.CreateError(id, IpcErrorCodes.UnknownError, null, error.GetType().Name));
        }

        lock (State.Gate)
        {
            State.Running.Remove(id);
            State.Owners.Remove(id);
        }

        answered.TrySetResult(text);
    }

    /// <summary>A key as a request carries it: a secret of 16 to 128 characters only its sender holds.</summary>
    internal static bool IsKey(string? key) => key is { Length: >= 16 and <= 128 };

    internal static string? Field(IpcRequest request, string name) =>
        request.Payload is { ValueKind: JsonValueKind.Object } payload && payload.TryGetProperty(name, out var named)
        && named.ValueKind == JsonValueKind.String ? named.GetString() : null;

    internal IpcResponse RefuseTaken(IpcRequest request) =>
        Refuse(request, IdTaken, "this id was sent with another request or another key; send yours with an id of its own.");

    internal IpcResponse RefuseStopping(IpcRequest request) =>
        Refuse(request, Stopping, "Daoris is closing; run this again once it is back.",
            ("why", Stopped ? "close" : "update"));

    /// <summary>A refusal the pipe answers itself, said in the machine log as the refusal log says one, as the terminal's.</summary>
    internal IpcResponse Refuse(IpcRequest request, string code, string message, params (string Key, string Value)[] parameters)
    {
        Log.Info("refused", ("code", code), ("request", $"{request.Module}.{request.Type}"), ("door", PipeDoor.Terminal));
        return IpcResponse.CreateError(
            request.Id, code, parameters.Length == 0 ? null : parameters.ToDictionary(each => each.Key, each => each.Value), message);
    }
}
