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
    /// <summary>How many connections at once.</summary>
    public int Connections { get; init; } = 8;

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

    /// <summary>How many of the person's presses may wait for the window at once (D156 §4.2's five): past it, one is refused.</summary>
    public int Waiting { get; init; } = 5;

    /// <summary>How long one waits for the person's answer in the window (D156 §4.2's two minutes).</summary>
    public TimeSpan Confirm { get; init; } = TimeSpan.FromMinutes(2);
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
/// window (<see cref="Confirmations"/>, RPC1c) and runs only on the person's <b>Confirm</b> within two minutes, the terminal told
/// it waits (<see cref="Confirming"/>) and, refused, expired or withdrawn, how it ended; a window's is refused
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
/// hello and every new press is refused <see cref="Stopping"/>, and <see cref="Drain"/> lets the presses under way answer; the
/// loop then takes its address away and lets the home go.</para>
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

    /// <summary>The person refused the press in the window: nothing ran.</summary>
    public const string Refused = "refused";

    /// <summary>Nobody answered the press in the window within its two minutes: nothing ran.</summary>
    public const string Expired = "expired";

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

    private readonly IMessageDispatcher _dispatcher;
    private readonly IEventBus _bus;
    private readonly string _home;
    private readonly MachineLog _log;
    private readonly PipeCensus _census;
    private readonly string _version;
    private readonly PipeLimits _limits;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (PipeBinding Binding, Task<string> Answer)> _running = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Connection> _owners = new(StringComparer.Ordinal);
    // RPC1c: a person's press waiting for the window, by its request's id, to the confirmation that shows it (a cancel takes it back).
    private readonly Dictionary<string, string> _waitingFor = new(StringComparer.Ordinal);
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
    /// <param name="clock">The clock a confirmation's two minutes are read by: the system's, or a test's.</param>
    public InstancePipe(
        IMessageDispatcher dispatcher, IEventBus bus, string home, MachineLog? log = null, PipeCensus? census = null,
        string? version = null, PipeLimits? limits = null, TimeProvider? clock = null)
    {
        _dispatcher = dispatcher;
        _bus = bus;
        _home = home;
        _log = log ?? MachineLog.None;
        _census = census ?? PipeCensus.Daoris;
        _version = version ?? typeof(InstancePipe).Assembly.GetName().Version?.ToString() ?? "0";
        _limits = limits ?? new PipeLimits();
        Confirmations = new PipeConfirmations(bus, _log, clock, _limits.Waiting, _limits.Confirm);
    }

    /// <summary>
    /// The person's presses a terminal sent, waiting for the window (RPC1c): the bridge's <c>PERSON_CONFIRMATIONS</c> reads them and
    /// its <c>PERSON_CONFIRM</c> answers one, which a terminal never may.
    /// </summary>
    public PipeConfirmations Confirmations { get; }

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
            // The journal's folder is D172's `pipe` entry, asked of the table as every home path is (HOMELAYOUT2).
            _journal = new PipeJournal(DaorisHome.Entry(_home, HomeEntry.Pipe), _limits.Kept);
            _slots = new SemaphoreSlim(_limits.Connections - 1, _limits.Connections);
            _progress = _bus.Subscribe("*", IpcRequestEvents.Updated, Progressed);
            Name = name;
            _accepting = Task.Run(() => AcceptAsync(first));
            _log.Info("pipe.served", ("connections", _limits.Connections));
            return name;
        }
    }

    /// <summary>
    /// The stop begins (§6.3): every new hello and every new press is refused <see cref="Stopping"/> from here, and what waits for
    /// the window is taken back and answered <see cref="Stopping"/>, so nothing the person has not yet confirmed runs as Daoris closes.
    /// </summary>
    public void BeginStopping()
    {
        Interlocked.Exchange(ref _stopping, 1);
        Confirmations.WithdrawAll();
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
        lock (_gate)
        {
            if (!_connections.Remove(connection)) return;
        }

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
    private IpcResponse Hello(IpcRequest hello)
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
        return IpcResponse.CreateSuccess(hello.Id, new { protocol = Protocol, home = _homeId, version = _version, pid = Environment.ProcessId });
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
                "HELLO" => Hello(request),
                "ROUTES" => IpcResponse.CreateSuccess(request.Id, new { routes = _census.Routes.Select(Listed) }),
                "RESULT" => Result(request),
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
            _ = RunAsync(request, id, binding, mine);
            return (null, answer);
        }

        // RPC1c (D156 §4.2): the person's press waits for the window, which shows it naming this terminal, and runs on Confirm alone.
        // The id is bound above before the window is asked, so the same id sent again meanwhile joins this one and asks nothing.
        if (Confirmations.Ask(request.Module, request.Type, fields, connection.Asker) is not { } asked)
        {
            var bound = Confirmations.Bound == 5 ? "Five" : Confirmations.Bound.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var full = IpcJson.Serialize(Refuse(request, Full,
                $"{bound} acts already wait for the person's confirmation, so this one was not asked. Nothing was kept."));
            lock (_gate)
            {
                _running.Remove(id);
                _owners.Remove(id);
            }

            mine.SetResult(full);
            return (full, null);
        }

        lock (_gate)
        {
            _waitingFor[id] = asked.Shown.Id;
        }

        _ = connection.WriteAsync(Waits(id, asked.Shown));
        _ = ConfirmThenRunAsync(request, id, binding, mine, asked.Ended);
        return (null, answer);
    }

    /// <summary>The notification that tells the terminal its press waits for the person in the window, and until when (RPC1c).</summary>
    private static string Waits(string id, PipeConfirmation shown) => JsonSerializer.Serialize(
        new
        {
            category = IpcCategories.Notification,
            id = Guid.NewGuid().ToString("N"),
            payload = new[] { new { module = Module, type = Confirming, payload = new { id, expires = shown.Expires } } },
            timestamp = DateTimeOffset.UtcNow,
        },
        IpcJson.Options);

    /// <summary>
    /// A person's press once the window has answered (RPC1c): run, once, where the person confirmed it and the instance is not
    /// closing; otherwise answered with how it ended and kept as its ending, so <c>RESULT</c> and the same id sent again say it.
    /// </summary>
    private async Task ConfirmThenRunAsync(
        IpcRequest request, string id, PipeBinding binding, TaskCompletionSource<string> answered, Task<PipeConfirmationEnd> ended)
    {
        var end = await ended.ConfigureAwait(false);
        lock (_gate)
        {
            _waitingFor.Remove(id);
        }

        if (end == PipeConfirmationEnd.Confirmed && !IsStopping)
        {
            await RunAsync(request, id, binding, answered).ConfigureAwait(false);
            return;
        }

        var response = end switch
        {
            PipeConfirmationEnd.Refused => IpcResponse.CreateError(id, Refused, null,
                "The person refused this in Daoris's window. Nothing was done."),
            PipeConfirmationEnd.Expired => IpcResponse.CreateError(id, Expired, null,
                "Nobody confirmed this in Daoris's window within two minutes. Nothing was done."),
            _ when IsStopping => RefuseStopping(request),
            _ => IpcResponse.CreateError(id, IpcErrorCodes.OperationCancelled, null,
                "This terminal took its request back before the person answered it. Nothing was done."),
        };
        var text = IpcJson.Serialize(response);
        try
        {
            if (_journal!.Begin(id, binding)) _journal.End(id, binding, text);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Not kept: the terminal still hears it now, and RESULT says it does not know, which is so.
        }

        lock (_gate)
        {
            _running.Remove(id);
            _owners.Remove(id);
        }

        answered.SetResult(text);
    }

    private static string Capitalized(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

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
    private async Task RunAsync(IpcRequest request, string id, PipeBinding binding, TaskCompletionSource<string> answered)
    {
        string text;
        try
        {
            if (!_journal!.Begin(id, binding))
            {
                text = IpcJson.Serialize(Refuse(request, Unrecorded,
                    "Daoris could not keep this request in its journal, so it was not run; run it again."));
            }
            else
            {
                IpcResponse response;
                using (PipeDoor.Enter())
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

        answered.SetResult(text);
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
        if (waiting is not null && Confirmations.Withdraw(waiting))
        {
            return IpcJson.Serialize(IpcResponse.CreateSuccess(request.Id, new { cancelled = true, state = "withdrawn" }));
        }

        var answer = await _dispatcher.DispatchAsync(request, _lifetime.Token).ConfigureAwait(false);
        return IpcJson.Serialize(answer);
    }

    /// <summary><c>RESULT { id, key }</c>: how the press ended, told only to the key that sent it, whichever connection asks.</summary>
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
                return running.Binding.Key == holder ? IpcResponse.CreateSuccess(request.Id, new { state = "running" }) : RefuseTaken(request);
            }

            return _journal!.Find(id) switch
            {
                { } seen when seen.Binding.Key != holder => RefuseTaken(request),
                { State: PipeJournal.Seen.Answered, Answer: { } answer } =>
                    IpcResponse.CreateSuccess(request.Id, new { state = "answered", answer = JsonDocument.Parse(answer).RootElement.Clone() }),
                _ => IpcResponse.CreateSuccess(request.Id, new { state = "unknown" }),
            };
        }
    }

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

                var answered = pipe.Hello(hello);
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
            stream.Dispose();
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
