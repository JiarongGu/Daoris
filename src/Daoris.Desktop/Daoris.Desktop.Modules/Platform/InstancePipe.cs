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
}

/// <summary>
/// The running Daoris answers on a pipe (RPC1a, D171 point 3, the terminal RPC design): a terminal's verb is the instance's client,
/// and its request runs through the dispatcher every window's page uses, so the route is the window's press: the same handler, the
/// same middleware (<see cref="RefusalLog"/>, <see cref="UiStallWatch"/>, <see cref="OffTheWindow"/>, <see cref="StatePublisher"/>),
/// the same answer, under way in every window and heard by every window as it happens.
/// </summary>
/// <remarks>
/// <para><b>The pipe</b> (§4.1): <see cref="PipeName"/>'s name, this Windows user's alone (an ACL granting the current user's SID and
/// denying the network's), made as the first instance of its name, so where the name is already held this instance serves none and
/// says so (<c>pipe.refused</c>) rather than share it. Windows only: elsewhere nothing is served (§11).</para>
///
/// <para><b>The wire</b> (§6): UTF-8 lines of JSON. The hello first (<c>DAORIS.PIPE</c> <c>HELLO</c>), answered with this home's id,
/// the build and the process, or refused <see cref="WrongProtocol"/>, <see cref="WrongHome"/> or <see cref="Stopping"/>; anything
/// before it closes the connection. Then the kit's request envelopes, several at once, each answered with its response envelope
/// by id, a route's progress forwarded to the connection that sent it as the kit's notification envelope; <c>ROUTES</c> and
/// <c>RESULT</c> are the pipe's own.</para>
///
/// <para><b>The census decides what runs</b> (§4.2, <see cref="PipeCensus"/>): an open route runs; a person's is refused
/// <see cref="ConfirmInWindow"/> until RPC1c confirms it in the window; a window's <see cref="WindowOnly"/>; a module the census
/// does not name <see cref="Unclassed"/>. Nothing refused reaches the dispatcher.</para>
///
/// <para><b>A request's id is durable</b> (§6.1, <see cref="PipeJournal"/>): a press runs on the instance's lifetime, not the
/// connection's, so a lost terminal cancels nothing; an id seen before is answered from its line and never run twice, and
/// <c>RESULT</c> says <c>running</c>, <c>answered</c> with the answer, or <c>unknown</c>.</para>
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

    /// <summary>The wire's version, which the hello names; another is refused <see cref="WrongProtocol"/>.</summary>
    public const int Protocol = 1;

    /// <summary>Where the request journal is kept under the home.</summary>
    public const string JournalFolder = "pipe";

    /// <summary>The hello named another version of the wire.</summary>
    public const string WrongProtocol = "protocol";

    /// <summary>The hello named another home's id.</summary>
    public const string WrongHome = "home";

    /// <summary>The instance is closing or updating: its parameter <c>why</c> says which (<c>close</c>, <c>update</c>).</summary>
    public const string Stopping = "stopping";

    /// <summary>A route the census makes the person's: confirmed in the window once RPC1c builds it, refused until then.</summary>
    public const string ConfirmInWindow = "confirm-in-window";

    /// <summary>A route only a window may press.</summary>
    public const string WindowOnly = "window-only";

    /// <summary>A module the census does not name.</summary>
    public const string Unclassed = "unclassed";

    /// <summary>An id begun by an instance that stopped before it ended: what it did is not known, so it is not run again.</summary>
    public const string Interrupted = "interrupted";

    private readonly IMessageDispatcher _dispatcher;
    private readonly IEventBus _bus;
    private readonly string _home;
    private readonly MachineLog _log;
    private readonly PipeCensus _census;
    private readonly string _version;
    private readonly PipeLimits _limits;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Task<string>> _running = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Connection> _owners = new(StringComparer.Ordinal);
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
    public InstancePipe(
        IMessageDispatcher dispatcher, IEventBus bus, string home, MachineLog? log = null, PipeCensus? census = null,
        string? version = null, PipeLimits? limits = null)
    {
        _dispatcher = dispatcher;
        _bus = bus;
        _home = home;
        _log = log ?? MachineLog.None;
        _census = census ?? PipeCensus.Daoris;
        _version = version ?? typeof(InstancePipe).Assembly.GetName().Version?.ToString() ?? "0";
        _limits = limits ?? new PipeLimits();
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
            _journal = new PipeJournal(Path.Combine(_home, JournalFolder), _limits.Kept);
            _slots = new SemaphoreSlim(_limits.Connections - 1, _limits.Connections);
            _progress = _bus.Subscribe("*", IpcRequestEvents.Updated, Progressed);
            Name = name;
            _accepting = Task.Run(() => AcceptAsync(first));
            _log.Info("pipe.served", ("connections", _limits.Connections));
            return name;
        }
    }

    /// <summary>The stop begins (§6.3): every new hello and every new press is refused <see cref="Stopping"/> from here.</summary>
    public void BeginStopping() => Interlocked.Exchange(ref _stopping, 1);

    /// <summary>
    /// Wait up to <paramref name="within"/> for the presses under way to answer, and each answer to be written to the terminal
    /// waiting on it: false where one had not.
    /// </summary>
    public bool Drain(TimeSpan within)
    {
        var until = DateTime.UtcNow + within;
        Task[] running;
        lock (_gate)
        {
            running = [.. _running.Values];
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

    [SupportedOSPlatform("windows")]
    private static NamedPipeServerStream Create(string name, int connections, bool first)
    {
        // This user's alone, and nobody's from another machine (§4.1).
        var security = new PipeSecurity();
        using (var identity = WindowsIdentity.GetCurrent())
        {
            security.AddAccessRule(new PipeAccessRule(identity.User!, PipeAccessRights.FullControl, AccessControlType.Allow));
        }

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        return NamedPipeServerStreamAcl.Create(
            name, PipeDirection.InOut, connections, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | (first ? PipeOptions.FirstPipeInstance : PipeOptions.None), 0, 0, security);
    }

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

    /// <summary>A request on a connection that said its hello: the pipe's own, or a route the census lets run.</summary>
    private async Task<string> AnswerAsync(IpcRequest request, Connection connection)
    {
        if (string.Equals(request.Module, Module, StringComparison.OrdinalIgnoreCase))
        {
            return IpcJson.Serialize(request.Type switch
            {
                "HELLO" => Hello(request),
                "ROUTES" => IpcResponse.CreateSuccess(request.Id, new
                {
                    routes = _census.Routes.Select(route => new { module = route.Module, route = route.Route, @class = route.Class }),
                }),
                "RESULT" => Result(request),
                _ => IpcResponse.CreateError(request.Id, IpcErrorCodes.NoRoute, null, $"the pipe has no route {request.Type}."),
            });
        }

        if (IsStopping) return IpcJson.Serialize(RefuseStopping(request));
        switch (_census.Of(request.Module, request.Type))
        {
            case null:
                return IpcJson.Serialize(Refuse(request, Unclassed, "this route is not answered over the pipe."));
            case PipeClass.Person:
                return IpcJson.Serialize(Refuse(request, ConfirmInWindow,
                    "this is the person's to do, and Daoris cannot tell whether a person or an agent ran this: do it in Daoris's window."));
            case PipeClass.Window:
                return IpcJson.Serialize(Refuse(request, WindowOnly, "only Daoris's window does this."));
        }

        if (request.Id is not { Length: > 0 and <= 128 } id)
        {
            return IpcJson.Serialize(IpcResponse.CreateError(request.Id ?? "", IpcErrorCodes.InvalidPayloadValue, null, "a request names its id."));
        }

        Task<string> answer;
        TaskCompletionSource<string>? mine = null;
        lock (_gate)
        {
            if (_running.TryGetValue(id, out var running))
            {
                // Sent again while it runs: the one press's answer, to the connection that asked last.
                _owners[id] = connection;
                answer = running;
            }
            else if (_journal!.Find(id) is { } seen)
            {
                return seen.State == PipeJournal.Seen.Answered
                    ? seen.Answer!
                    : IpcJson.Serialize(IpcResponse.CreateError(id, Interrupted, null,
                        "Daoris stopped while this request was under way, so whether it was done is not known; it was not run again."));
            }
            else
            {
                mine = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                _running[id] = answer = mine.Task;
                _owners[id] = connection;
            }
        }

        if (mine is not null) _ = RunAsync(request, id, mine);
        return await answer.ConfigureAwait(false);
    }

    /// <summary>A press, run on the instance's lifetime and never the connection's (§6.1), its lines kept either side.</summary>
    private async Task RunAsync(IpcRequest request, string id, TaskCompletionSource<string> answered)
    {
        string text;
        try
        {
            _journal!.Begin(id, $"{request.Module}.{request.Type}");
            IpcResponse response;
            using (PipeDoor.Enter())
            {
                response = await _dispatcher.DispatchAsync(request, _lifetime.Token).ConfigureAwait(false);
            }

            text = IpcJson.Serialize(response);
            _journal.End(id, text);
        }
        catch (Exception error)
        {
            // The dispatcher never throws; a journal that could not be kept must not leave the press unanswered.
            text = IpcJson.Serialize(IpcResponse.CreateError(id, IpcErrorCodes.UnknownError, null, error.GetType().Name));
        }

        lock (_gate)
        {
            _running.Remove(id);
            _owners.Remove(id);
        }

        answered.SetResult(text);
    }

    private IpcResponse Result(IpcRequest request)
    {
        var id = request.Payload is { ValueKind: JsonValueKind.Object } payload && payload.TryGetProperty("id", out var named)
            && named.ValueKind == JsonValueKind.String ? named.GetString() : null;
        if (string.IsNullOrEmpty(id)) return IpcResponse.CreateError(request.Id, IpcErrorCodes.MissingPayloadValue, null, "RESULT names the request's id.");
        lock (_gate)
        {
            if (_running.ContainsKey(id)) return IpcResponse.CreateSuccess(request.Id, new { state = "running" });
            return _journal!.Find(id) is { State: PipeJournal.Seen.Answered, Answer: { } answer }
                ? IpcResponse.CreateSuccess(request.Id, new { state = "answered", answer = JsonDocument.Parse(answer).RootElement.Clone() })
                : IpcResponse.CreateSuccess(request.Id, new { state = "unknown" });
        }
    }

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

    /// <summary>One terminal's connection: its hello, then its requests, each answered as it ends.</summary>
    private sealed class Connection(InstancePipe pipe, NamedPipeServerStream stream)
    {
        private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
        private readonly SemaphoreSlim _writing = new(1, 1);
        private readonly Channel<string?> _lines = Channel.CreateUnbounded<string?>(new UnboundedChannelOptions { SingleReader = true });
        private int _underWay;
        private int _closed;

        /// <summary>Whether a request on this connection has not been answered yet.</summary>
        public bool Answering => Volatile.Read(ref _underWay) > 0;

        public async Task RunAsync()
        {
            try
            {
                _ = ReadAsync();
                // The hello first, within its bound: anything else closes the connection unanswered (§6).
                if (await NextAsync(pipe._limits.Hello, waitWhileBusy: false).ConfigureAwait(false) is not { } first
                    || Parse(first) is not { } hello
                    || !string.Equals(hello.Module, Module, StringComparison.OrdinalIgnoreCase) || hello.Type != "HELLO")
                {
                    return;
                }

                var answered = pipe.Hello(hello);
                await WriteAsync(IpcJson.Serialize(answered)).ConfigureAwait(false);
                if (!answered.Success) return;

                while (await NextAsync(pipe._limits.Idle, waitWhileBusy: true).ConfigureAwait(false) is { } line)
                {
                    if (Parse(line) is not { } request) return;
                    _ = AnswerAsync(request);
                }
            }
            finally
            {
                Close();
                pipe.Ended(this);
            }
        }

        private async Task AnswerAsync(IpcRequest request)
        {
            Interlocked.Increment(ref _underWay);
            try
            {
                await WriteAsync(await pipe.AnswerAsync(request, this).ConfigureAwait(false)).ConfigureAwait(false);
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
                        _lines.Writer.TryWrite(Utf8.GetString(line.GetBuffer(), 0, (int)line.Length).TrimEnd('\r'));
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

        private static IpcRequest? Parse(string line)
        {
            try
            {
                return IpcJson.Deserialize<IpcRequest>(line) is { Module: { Length: > 0 }, Type: { Length: > 0 } } request ? request : null;
            }
            catch (Exception error) when (error is JsonException or NotSupportedException or ArgumentException)
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
    }
}
