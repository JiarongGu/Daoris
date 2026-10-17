using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace Daoris.Driver;

/// <summary>
/// One plugin's side of the wire, as the host sees it — what a process gives once it speaks, and what a
/// test can hand in without one. The module's port (STRUCT3a): the hook set and the landing ask through it, a test's
/// fake and <see cref="HookPeer"/> speak it, and <see cref="HookProcess"/> is the real one.
/// </summary>
public interface IHookChannel : IAsyncDisposable
{
    /// <summary>The points the plugin actually listens on, as it answered `initialize`.</summary>
    IReadOnlyList<string> Points { get; }

    /// <summary>Whether the other side is still there. A channel that is not is not asked.</summary>
    bool Alive { get; }

    /// <summary>The process's exit code once it has exited, where there is a process to have one (PLUGUI1d).</summary>
    int? ExitCode => null;

    Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct);

    Task EndedAsync(object payload, CancellationToken ct);

    /// <summary>The landing's one frame (D100): the branch Daoris made, answered with what the plugin did with it.</summary>
    Task<PluginLanding> LandAsync(object payload, CancellationToken ct);

    /// <summary>
    /// A landed branch's pull request (PLUGHOOK1a, D148), answered with its state. A channel that has no such answer fails as
    /// the plugin's error, which keeps everything where it is.
    /// </summary>
    Task<PullRequestState> StateAsync(object payload, CancellationToken ct) =>
        Task.FromException<PullRequestState>(PluginFailures.Mark(
            new DriverException($"this channel does not answer `{HookPoints.State}`."), PluginEvents.Errored));

    /// <summary>
    /// What moved of what the driver holds (STATEHUB1g, D169 point 9), its answer not read. A channel that has no such answer fails
    /// as the plugin's error, which an observation contains.
    /// </summary>
    Task ChangedAsync(object payload, CancellationToken ct) =>
        Task.FromException(PluginFailures.Mark(
            new DriverException($"this channel does not answer `{HookPoints.StateChanged}`."), PluginEvents.Errored));

    /// <summary>The capabilities the plugin's handshake named that this build knows (FABRIC-PERFORM1a): <see cref="WorkWire.Capability"/>.</summary>
    IReadOnlyList<string> Capabilities => [];

    /// <summary>
    /// Where the plugin's <see cref="HookPoints.Step"/> events go (FABRIC-PERFORM1a, §C4): each event's params, or null for a frame dropped
    /// unread for its length. Only the lease's loop listens, on a process it started; a channel nobody listens to drops every event, as a
    /// landing's process does, and a channel with no events has nothing to give.
    /// </summary>
    void Listen(Action<JsonElement?>? steps)
    {
    }

    /// <summary>The awaited set (§C4), its answer waited for and never read. A channel that has no such answer fails as the plugin's error.</summary>
    Task WatchAsync(WorkWatchFrame frame, CancellationToken ct) =>
        Task.FromException(PluginFailures.Mark(
            new DriverException($"this channel does not answer `{HookPoints.Watch}`."), PluginEvents.Errored));

    /// <summary>One attempt (§D6), answered with its receipt; the outcome comes later as an event. Fails where receipt is not acknowledged.</summary>
    Task PerformAsync(WorkPerformCall call, CancellationToken ct) =>
        Task.FromException(PluginFailures.Mark(
            new DriverException($"this channel does not answer `{HookPoints.Perform}`."), PluginEvents.Errored));

    /// <summary>What became of one attempt (§D6). Fails where the answer is not one.</summary>
    Task<WorkReconcileAnswer> ReconcileStepAsync(WorkReconcileCall call, CancellationToken ct) =>
        Task.FromException<WorkReconcileAnswer>(PluginFailures.Mark(
            new DriverException($"this channel does not answer `{PluginSteps.ReconcilePoint}`."), PluginEvents.Errored));
}

/// <summary>
/// The hook wire (D64 §4): JSON-RPC 2.0, one frame per line, over a plugin process's stdio — the
/// framing <see cref="AcpSession"/> speaks, with the roles reversed. Daoris calls; the plugin answers.
/// </summary>
/// <remarks>
/// <para><b>It takes streams, not a process</b>, for the reason the ACP session does: every rule here
/// is provable against two in-memory streams and a fake plugin, and a class that owned a process could
/// not be tested without one.</para>
///
/// <para><b>A late, wrong or missing answer at a decision point is a hold</b> that names the plugin
/// and the failure — never a silent allow, because the driver spends real accounts and a policy that
/// failed open would be the one wrong result nobody sees. The caller renders the sentence; this class
/// only refuses to guess.</para>
/// </remarks>
public sealed class HookPeer(
    TextReader incoming,
    TextWriter outgoing,
    string plugin,
    Action<string>? onLine = null,
    TimeSpan? patience = null,
    // FABRIC-PERFORM1a (§C4): a frame longer than this, in UTF-8 bytes, is dropped unread. A process's stdout is read with the bound
    // HookProcess.FrameLimit names for its plugin; null reads whole lines, as a test's stand-in that gives only lines is read.
    int? frameLimit = null) : IHookChannel
{
    /// <summary>The wire version this host speaks. Stated in `initialize`; a plugin answering another is refused.</summary>
    public const int ProtocolVersion = 1;

    /// <summary>
    /// The longest frame read from a plugin that speaks no work point (FABRIC-PERFORM1a, round 2): far past any answer its points read — a
    /// decision, a landing's sentence and link, a pull request's state — so none is lost, and still a bound on what one line can hold.
    /// </summary>
    public const int AnswerBytes = 4 * 1024 * 1024;

    /// <summary>How long a call waits for its answer unless the caller says otherwise — the loop's points wait this long.</summary>
    public static readonly TimeSpan DefaultPatience = TimeSpan.FromSeconds(10);

    private readonly TimeSpan _patience = patience ?? DefaultPatience;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _stopping = new();
    private Task? _pump;
    private int _nextId;
    private volatile bool _alive = true;

    // FABRIC-PERFORM1a: where `work/step` goes (null: nowhere), and the requests in flight at once.
    private volatile Action<JsonElement?>? _steps;
    private readonly SemaphoreSlim _calls = new(WorkBounds.CallsInFlight, WorkBounds.CallsInFlight);

    public IReadOnlyList<string> Points { get; private set; } = [];

    public bool Alive => _alive;

    /// <summary>
    /// The handshake: what the host is, where the plugin is, and which points it may listen on. The
    /// plugin answers with its version and the points it actually listens on — a subset of what its
    /// manifest declared, because a manifest is a claim and the process is the fact.
    /// </summary>
    /// <param name="patience">How long the handshake waits, where it is not the peer's: the kit's trial waits its points' own
    /// patience here, however short a patience it gives the points' calls (PLUGKIT2).</param>
    public async Task InitializeAsync(
        string home, string data, IReadOnlyList<string> declared, CancellationToken ct, TimeSpan? patience = null)
    {
        _pump ??= PumpAsync(_stopping.Token);

        var answer = await RequestAsync(
            "initialize",
            new { protocolVersion = ProtocolVersion, plugin, home, data, points = declared },
            ct, patience).ConfigureAwait(false);

        // 🔴 Every read checks the shape first (REV3): a property read on the wrong kind throws
        // InvalidOperationException, which is not a DriverException, so it escaped the tick's catch and
        // killed every tick without naming the plugin.
        if (answer.ValueKind != JsonValueKind.Object)
        {
            throw Unreadable(
                $"plugin `{plugin}` answered the handshake with {Raw(answer)}, which is not one — "
                + $"`{{ \"protocolVersion\": {ProtocolVersion}, \"points\": [ … ] }}`.");
        }

        if (!answer.TryGetProperty("protocolVersion", out var version)
            || version.ValueKind != JsonValueKind.Number
            || !version.TryGetInt32(out var spoken) || spoken != ProtocolVersion)
        {
            throw Unreadable(
                $"plugin `{plugin}` speaks hook wire {(answer.TryGetProperty("protocolVersion", out var v) ? v.GetRawText() : "(none)")}; "
                + $"this build speaks {ProtocolVersion}.");
        }

        var points = new List<string>();
        if (answer.TryGetProperty("points", out var listened) && listened.ValueKind == JsonValueKind.Array)
        {
            foreach (var point in listened.EnumerateArray())
            {
                if (point.ValueKind != JsonValueKind.String) continue;
                var name = point.GetString()!;
                if (!declared.Contains(name, StringComparer.Ordinal))
                {
                    throw Unreadable(
                        $"plugin `{plugin}` listens on `{name}`, which its manifest does not declare — "
                        + "a manifest is what a person reads before enabling a plugin, so the process may not exceed it.");
                }

                // The work points (FABRIC-PERFORM1a) are this build's too, spoken only by the lease's loop.
                if (!HookPoints.All.Contains(name, StringComparer.Ordinal) && !HookPoints.Work.Contains(name, StringComparer.Ordinal))
                {
                    // PLUGHOOK1a (D148 point 8): a point is additive, so a plugin that grew one keeps the points this build has.
                    // Refusing it whole made an older build lose the plugin's landing. Said once, and never asked there.
                    onLine?.Invoke(
                        $"plugin `{plugin}` listens on `{name}`, which is not a point this build has "
                        + $"({string.Join(", ", HookPoints.All)}): it is not asked there.");
                    continue;
                }

                if (!points.Contains(name, StringComparer.Ordinal)) points.Add(name);
            }
        }

        Points = points;

        // FABRIC-PERFORM1a (§D6): the capabilities it names that this build knows; one it grew is passed over, as a point is.
        var capabilities = new List<string>();
        if (answer.TryGetProperty("capabilities", out var named) && named.ValueKind == JsonValueKind.Array)
        {
            foreach (var each in named.EnumerateArray())
            {
                if (each.ValueKind == JsonValueKind.String && each.GetString() == WorkWire.Capability && !capabilities.Contains(WorkWire.Capability))
                {
                    capabilities.Add(WorkWire.Capability);
                }
            }
        }

        Capabilities = capabilities;
    }

    public async Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct)
    {
        var answer = await RequestAsync($"hook/{HookPoints.QuestConsider}", payload, ct).ConfigureAwait(false);
        var kind = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("kind", out var k)
            && k.ValueKind == JsonValueKind.String
            ? k.GetString()
            : null;

        return kind switch
        {
            "allow" => HookDecision.Allow,
            "hold" => HookDecision.Hold(
                answer.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String
                && reason.GetString() is { Length: > 0 } why
                    ? why
                    : "no reason given"),
            _ => throw Unreadable(
                $"plugin `{plugin}` answered {Raw(answer)}, which is not a decision — "
                + "`{ \"kind\": \"allow\" }` or `{ \"kind\": \"hold\", \"reason\": \"…\" }`."),
        };
    }

    public async Task EndedAsync(object payload, CancellationToken ct) =>
        await RequestAsync($"hook/{HookPoints.SessionEnded}", payload, ct).ConfigureAwait(false);

    /// <summary>An observation (STATEHUB1g): the answer is waited for, as <see cref="EndedAsync"/>'s is, and never read.</summary>
    public async Task ChangedAsync(object payload, CancellationToken ct) =>
        await RequestAsync($"hook/{HookPoints.StateChanged}", payload, ct).ConfigureAwait(false);

    /// <summary>
    /// The landing's frame (D100), and its answer read by shape: <c>pushed</c> is required, a pull request
    /// is an absolute web address or absent, and the plugin's sentence is words or absent. Anything else
    /// is not an answer, and the landing says the plugin's step failed — never that the branch went.
    /// </summary>
    public async Task<PluginLanding> LandAsync(object payload, CancellationToken ct)
    {
        var answer = await RequestAsync($"hook/{HookPoints.Land}", payload, ct).ConfigureAwait(false);

        DriverException NotAnAnswer() => Unreadable(
            $"plugin `{plugin}` answered {Raw(answer)}, which is not a landing's answer — "
            + "`{ \"pushed\": true, \"pullRequest\": \"https://…\", \"message\": \"…\" }`, the last two optional.");

        if (answer.ValueKind != JsonValueKind.Object
            || !answer.TryGetProperty("pushed", out var pushed)
            || pushed.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw NotAnAnswer();
        }

        string? pullRequest = null;
        if (answer.TryGetProperty("pullRequest", out var opened) && opened.ValueKind != JsonValueKind.Null)
        {
            // Only a web page is said as a link: a pull request is somewhere a person goes to read.
            if (opened.ValueKind != JsonValueKind.String
                || !Uri.TryCreate(opened.GetString(), UriKind.Absolute, out var address)
                || address.Scheme is not ("https" or "http"))
            {
                throw NotAnAnswer();
            }

            pullRequest = opened.GetString();
        }

        var message = "no sentence given";
        if (answer.TryGetProperty("message", out var said) && said.ValueKind != JsonValueKind.Null)
        {
            if (said.ValueKind != JsonValueKind.String) throw NotAnAnswer();
            if (said.GetString() is { Length: > 0 } words) message = words.Trim();
        }

        return new PluginLanding(plugin, pushed.GetBoolean(), pullRequest, message);
    }

    /// <summary>
    /// The query's frame (PLUGHOOK1a, design §2.2), and its answer read by shape (<see cref="PullRequestAnswers.Read"/>): a
    /// state of the four, the full ids of both commits for a completed one, and the rest each its own shape or absent.
    /// Anything else is not an answer (<c>unreadable</c>), and never a state.
    /// </summary>
    public async Task<PullRequestState> StateAsync(object payload, CancellationToken ct)
    {
        var answer = await RequestAsync($"hook/{HookPoints.State}", payload, ct).ConfigureAwait(false);
        return PullRequestAnswers.Read(answer, plugin) ?? throw Unreadable(
            $"plugin `{plugin}` answered {Raw(answer)}, which is not a pull request's state — "
            + "`{ \"state\": \"open\" | \"completed\" | \"abandoned\" | \"unknown\", … }`, a completed one naming its "
            + "`mergeCommit` and `sourceCommit` in full.");
    }

    public IReadOnlyList<string> Capabilities { get; private set; } = [];

    public void Listen(Action<JsonElement?>? steps) => _steps = steps;

    /// <summary>The awaited set (§C4): its answer is waited for, as an observation's is, and never read.</summary>
    public async Task WatchAsync(WorkWatchFrame frame, CancellationToken ct) =>
        await RequestAsync(WorkWire.WatchMethod, WorkWire.Watch(frame), ct).ConfigureAwait(false);

    /// <summary>One attempt (§D6), its answer the receipt of this invocation and nothing else: anything other is unreadable.</summary>
    public async Task PerformAsync(WorkPerformCall call, CancellationToken ct)
    {
        var answer = await InFlightAsync(WorkWire.PerformMethod, WorkWire.Perform(call), ct).ConfigureAwait(false);
        if (!WorkWire.ReadPerformAnswer(answer, call.Invocation))
        {
            throw Unreadable(
                $"plugin `{plugin}` answered {Raw(answer)}, which does not acknowledge `{call.Invocation}` — "
                + $"`{{ \"invocation\": \"{call.Invocation}\" }}`.");
        }
    }

    /// <summary>What became of one attempt (§D6), read by shape (<see cref="WorkWire.ReadReconcileAnswer"/>): anything else is unreadable.</summary>
    public async Task<WorkReconcileAnswer> ReconcileStepAsync(WorkReconcileCall call, CancellationToken ct)
    {
        var answer = await InFlightAsync(WorkWire.ReconcileMethod, WorkWire.Reconcile(call), ct).ConfigureAwait(false);
        return WorkWire.ReadReconcileAnswer(answer) ?? throw Unreadable(
            $"plugin `{plugin}` answered {Raw(answer)}, which is not what became of an attempt — "
            + "`{ \"state\": \"done\" | \"not-started\" | \"running\" | \"failed\" | \"unknown\", \"outcome\": … }`, done and failed with their outcome.");
    }

    /// <summary>
    /// A request among at most <see cref="WorkBounds.CallsInFlight"/> (FABRIC-PERFORM1a): one past them waits the peer's patience for a place,
    /// and is late where none comes, so a plugin that answers nothing holds four attempts, never every one Daoris has.
    /// </summary>
    private async Task<JsonElement> InFlightAsync(string method, object parameters, CancellationToken ct)
    {
        if (!await _calls.WaitAsync(_patience, ct).ConfigureAwait(false))
        {
            throw Marked(
                $"plugin `{plugin}` has {WorkBounds.CallsInFlight} calls in flight, and `{method}` found no place within {_patience.TotalSeconds:0}s.",
                PluginEvents.Late);
        }

        try
        {
            return await RequestAsync(method, parameters, ct).ConfigureAwait(false);
        }
        finally
        {
            _calls.Release();
        }
    }

    /// <summary>Told to go, politely, within <see cref="HookProcess.Grace"/>; whoever owns the process then makes sure it did.</summary>
    public async ValueTask DisposeAsync()
    {
        using var grace = new CancellationTokenSource(HookProcess.Grace);
        await ShutdownAsync(grace.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Told to go, the write of <c>shutdown</c> given up when <paramref name="ct"/> is (FABRIC-PERFORM1a, round 2): a plugin that stopped
    /// reading its stdin filled the pipe, and a write with no bound held whoever disposed it, and the process, for ever. Nothing is read after.
    /// </summary>
    public async Task ShutdownAsync(CancellationToken ct)
    {
        try
        {
            await NotifyAsync("shutdown", ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or InvalidOperationException or OperationCanceledException)
        {
            // Already gone, which is the state being asked for — or not listening, which its owner's kill answers.
        }

        _stopping.Cancel();
        _alive = false;
        if (_pump is { } pump) _ = pump.ContinueWith(static t => t.Exception, TaskScheduler.Default);
    }

    private async Task<JsonElement> RequestAsync(string method, object? parameters, CancellationToken ct, TimeSpan? patience = null)
    {
        if (!_alive) throw Marked($"plugin `{plugin}` is not running.", PluginEvents.Exited);
        var waits = patience ?? _patience;

        var id = Interlocked.Increment(ref _nextId);
        var waiting = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = waiting;

        var frame = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method };
        if (parameters is not null) frame["params"] = JsonSerializer.SerializeToNode(parameters);

        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bounded.CancelAfter(waits);
        try
        {
            await SendAsync(frame, bounded.Token).ConfigureAwait(false);
            return await waiting.Task.WaitAsync(bounded.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _pending.TryRemove(id, out _);
            throw Marked($"plugin `{plugin}` did not answer `{method}` within {waits.TotalSeconds:0}s.", PluginEvents.Late);
        }
        catch (IOException error)
        {
            _pending.TryRemove(id, out _);
            _alive = false;
            throw Marked($"plugin `{plugin}` stopped answering: {error.Message}", PluginEvents.Exited);
        }
    }

    /// <summary>A failure of the wire, marked with its kind for the machine log (PLUGUI1d); the sentence is unchanged.</summary>
    private static DriverException Marked(string message, string kind) => PluginFailures.Mark(new DriverException(message), kind);

    private static DriverException Unreadable(string message) => Marked(message, PluginEvents.Unreadable);

    private Task NotifyAsync(string method, CancellationToken ct) =>
        SendAsync(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = method }, ct);

    private async Task SendAsync(JsonObject frame, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // One frame per line, written as a line: the same call the ACP wire makes, so a fake that
            // stands in for a plugin sees exactly what a process would.
            await outgoing.WriteLineAsync(frame.ToJsonString()).WaitAsync(ct).ConfigureAwait(false);
            await outgoing.FlushAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>A bound as a person reads it: 64 KB, 4 MB.</summary>
    private static string Size(int bytes) => bytes % (1024 * 1024) == 0 ? $"{bytes / (1024 * 1024)} MB" : $"{bytes / 1024} KB";

    private void Fail(int id, Exception error)
    {
        if (_pending.TryRemove(id, out var waiting)) waiting.TrySetException(error);
    }

    private async Task PumpAsync(CancellationToken ct)
    {
        var limit = frameLimit;
        var bounded = limit is { } bound ? new BoundedLines(incoming, bound) : null;
        try
        {
            while (true)
            {
                string line;
                if (bounded is null)
                {
                    if (await incoming.ReadLineAsync(ct).ConfigureAwait(false) is not { } whole) break;
                    line = whole;
                }
                else
                {
                    if (await bounded.ReadAsync(ct).ConfigureAwait(false) is not { } read) break;
                    if (read.Text is not { } text)
                    {
                        // FABRIC-PERFORM1a, round 2: an answer too long to read fails its call now, as the plugin's unreadable answer; it
                        // was left to wait out its patience, and a landing's took its whole patience to say only that it was late.
                        if (read.Id is { } answered && !read.Method)
                        {
                            Fail(answered, Unreadable(
                                $"plugin `{plugin}` answered with a frame over {Size(limit!.Value)}, which is not read: an answer is at most that long."));
                            continue;
                        }

                        // FABRIC-PERFORM1a (§C4): dropped unread. The lease's loop counts it and asks the plugin to send again what it
                        // knows; a process nobody listens to says so under the plugin's name.
                        if (_steps is { } listening) listening(null);
                        else onLine?.Invoke($"a frame longer than {Size(limit!.Value)} was dropped unread.");
                        continue;
                    }

                    line = text;
                }

                if (string.IsNullOrWhiteSpace(line)) continue;

                JsonElement frame;
                try
                {
                    frame = JsonDocument.Parse(line).RootElement.Clone();
                }
                catch (JsonException)
                {
                    // Not a frame: shown under the plugin's name rather than dropped, and the wire goes on.
                    onLine?.Invoke(line);
                    continue;
                }

                // PLUG8: a JSON value that is not an object, and an object that is neither an answer nor
                // a request, are not frames either. A bare `42` threw InvalidOperationException from the
                // property read below, which the catch does not name, and the wire ended.
                var hasMethod = frame.ValueKind == JsonValueKind.Object && frame.TryGetProperty("method", out _);
                if (frame.ValueKind != JsonValueKind.Object || (!hasMethod && !frame.TryGetProperty("id", out _)))
                {
                    onLine?.Invoke(line);
                    continue;
                }

                var hasId = frame.TryGetProperty("id", out var id);
                if (hasId && !hasMethod)
                {
                    Complete(id, frame);
                }
                else if (!hasId && _steps is { } steps && Points.Contains(HookPoints.Step, StringComparer.Ordinal)
                    && frame.TryGetProperty("method", out var method) && method.ValueKind == JsonValueKind.String
                    && method.GetString() == WorkWire.StepMethod)
                {
                    // FABRIC-PERFORM1a (§C4): the one notification this wire takes, `work/step`, from a process that declared it and that
                    // the lease's loop listens to. Its sender is this process, whatever the frame says; what is believed of it is not decided here.
                    steps(frame.TryGetProperty("params", out var @params) ? @params : default(JsonElement));
                }
                // A request FROM a plugin is not a thing this wire has: there is nothing a plugin may
                // ask the host for, by design (D64 §7) — so it is neither answered nor errored, and a
                // plugin that waits on one waits on itself. Any other notification is noise, and is ignored.
            }
        }
        catch (Exception error) when (error is OperationCanceledException or IOException or ObjectDisposedException)
        {
            // The wire ended; whoever is still waiting learns so below.
        }
        finally
        {
            _alive = false;
            foreach (var (_, waiting) in _pending)
            {
                waiting.TrySetException(Marked(
                    $"plugin `{plugin}` stopped answering — its process exited, or it is not speaking the hook wire on stdout.",
                    PluginEvents.Exited));
            }
            _pending.Clear();
        }
    }

    private void Complete(JsonElement id, JsonElement frame)
    {
        if (!id.TryGetInt32(out var key) || !_pending.TryRemove(key, out var waiting)) return;

        if (frame.TryGetProperty("error", out var error))
        {
            var message = error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString()
                : Raw(error);
            waiting.TrySetException(Marked($"plugin `{plugin}` refused the call: {message}", PluginEvents.Errored));
            return;
        }

        waiting.TrySetResult(frame.TryGetProperty("result", out var result) ? result.Clone() : default);
    }

    /// <summary>What an answer said, for a sentence — "(no result)" where it said nothing at all.</summary>
    private static string Raw(JsonElement answer) =>
        answer.ValueKind == JsonValueKind.Undefined ? "(no result)" : Truncate(answer.GetRawText());

    private static string Truncate(string text) => TextBounds.Clip(text, 80, keep: 77);
}

/// <summary>
/// A plugin's hook started and spoken to (D64 §4): the channel the hook set and the landing start through, so neither,
/// nor another module, names the process behind it (STRUCT3a).
/// </summary>
public static class HookChannels
{
    /// <summary>
    /// The plugin's program started in the home as the driver starts it, its handshake made:
    /// <see cref="HookProcess"/>, behind the port. Throws with the reason where it does not start or does not answer.
    /// </summary>
    public static async Task<IHookChannel> StartAsync(
        PluginEntry plugin, string home, Action<string> onLine, CancellationToken ct, TimeSpan? patience = null) =>
        await HookProcess.StartAsync(plugin, home, onLine, ct, patience).ConfigureAwait(false);
}

/// <summary>
/// The hook processes of the enabled plugins, shared across ticks the way the process registry is —
/// reconciled against the catalogue each tick, asked at the points, and stopped with the loop.
/// </summary>
/// <remarks>
/// <para><b>`quest/consider` is a waterfall that fails closed.</b> Plugins are asked in catalogue
/// order; the first hold ends it and its reason is the consideration's. A plugin that answers late,
/// wrongly or not at all is a hold too, naming the plugin and the failure — the person reads it,
/// disables the plugin, and the driver never stopped.</para>
///
/// <para><b>`session/ended` is contained.</b> A failure there is a line and the tick goes on.</para>
///
/// <para><b>`state/changed` is contained and off the path</b> (STATEHUB1g, D169 point 9): a plugin is a middleware in the event
/// pipeline only as an observer beside it. <see cref="Tell"/> writes the change to a queue and returns, and the set's one teller
/// tells it, so the press whose change it is has answered before any plugin hears, and no plugin can hold, change or refuse it.</para>
///
/// <para><b>Every line is the plugin's</b>, under `plugin:&lt;id&gt;` on the console — transcript-class
/// material that stays on the machine (D49 §2).</para>
///
/// <para><b>What the set did is kept without the plugin's words</b> (PLUGUI1d, D119 §4.2): each start, call,
/// failure and stop is a <c>plugin.*</c> line in the machine log and a word in the loop's record of the plugin's
/// health, through <see cref="Log"/>. A start that fails the same way at every look is one line until it
/// starts or fails differently.</para>
/// </remarks>
public sealed class HookSet(
    string home,
    SessionOutput? output = null,
    Func<PluginEntry, Action<string>, CancellationToken, Task<IHookChannel>>? start = null,
    // Where a plugin's word goes when there is no console buffer — the headless host prints it.
    Action<string, string>? say = null,
    // The process's machine log (PLUGUI1d), where what the set did with each plugin is written. Null writes none.
    MachineLog? log = null,
    // The loop's own record of each plugin's health (D119 §2). Null keeps none.
    PluginHealth? health = null,
    // FABRIC-PERFORM1a: whether this process holds the home's lease, asked at each look. Only the lease's loop starts a plugin for the
    // work wire, listens to its events and tells it what is awaited; a `--share` loop, which holds nothing, never does.
    Func<bool>? holdsHome = null,
    // The clock the work wire's waits and bounds are read on.
    Func<DateTimeOffset>? clock = null) : IAsyncDisposable
{
    /// <summary>A running plugin's process, with what it was started from: what a stop's reason is judged against.</summary>
    /// <param name="Work">Whether the plugin speaks the work wire (FABRIC-PERFORM1a): its starts are supervised by <see cref="WorkPlugins"/>.</param>
    private sealed record Held(string Signature, string Version, DateTimeOffset? Installed, IHookChannel Channel, bool Work = false);

    private readonly Dictionary<string, Held> _running = new(StringComparer.Ordinal);

    // FABRIC-PERFORM1a: the work wire's side, its lease and its clock. Its own writer of the same lines, since a field cannot read `Log`.
    private readonly Func<bool> _holdsHome = holdsHome ?? (() => HomeLease.HeldHere(home));
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly WorkPlugins _work = new(new PluginLog(log, health), clock ?? (() => DateTimeOffset.UtcNow));
    private readonly SemaphoreSlim _reconciling = new(1, 1);

    // Stopped by a removal or an update (StopAsync) before the catalogue says which: written at the next look.
    private readonly Dictionary<string, Held> _stopped = new(StringComparer.Ordinal);

    // The plugins whose last start failed, by the kind it failed with: a failure repeated at every look is one line.
    private readonly Dictionary<string, string> _unstarted = new(StringComparer.Ordinal);

    private readonly Func<PluginEntry, Action<string>, CancellationToken, Task<IHookChannel>> _start =
        start ?? ((plugin, onLine, ct) => HookChannels.StartAsync(plugin, home, onLine, ct));

    /// <summary>
    /// How many changes wait for the plugins at <see cref="HookPoints.StateChanged"/> before the oldest goes (STATEHUB1g): a plugin
    /// that answers late holds the next change by its patience at most, and nobody who tells waits on it.
    /// </summary>
    public const int ChangesWaiting = 64;

    // STATEHUB1g: the changes told, waiting for the one teller started at the first, so a tell writes here and does nothing else.
    private readonly Channel<StateChangedFrame> _changes = Channel.CreateBounded<StateChangedFrame>(
        new BoundedChannelOptions(ChangesWaiting) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    private readonly CancellationTokenSource _going = new();
    private readonly Lock _tellingGate = new();
    private Task? _telling;

    /// <summary>
    /// What the set does with each plugin, into the machine log and the loop's record (PLUGUI1d): also what the
    /// driver writes the servers it hands a session through, since the set is what it is given of the plugins.
    /// </summary>
    public PluginLog Log { get; } = new(log, health);

    /// <summary>The plugins whose process is up, by id, in catalogue order.</summary>
    public IReadOnlyList<string> Running
    {
        get { lock (_running) return _running.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(); }
    }

    /// <summary>
    /// Start what the catalogue now has and stop what it no longer has — a plugin disabled between
    /// ticks is stopped at the next one, one enabled is started, one whose manifest changed is
    /// restarted. Returns the lines worth a tick's report.
    /// </summary>
    public async Task<IReadOnlyList<string>> ReconcileAsync(PluginCatalog catalog, CancellationToken ct)
    {
        var lines = new List<string>();
        await _reconciling.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // FABRIC-PERFORM1a (§C4): the work wire is the lease's loop's alone, asked at each look. An exit nobody asked for, of a plugin
            // on it, is told before what is wanted is judged, so its wait holds from this look.
            var now = _clock();
            var holds = _holdsHome();
            List<string> exited;
            lock (_running) exited = [.. _running.Where(pair => pair.Value.Work && !pair.Value.Channel.Alive).Select(pair => pair.Key)];
            var waits = exited.ToDictionary(id => id, id => _work.Exited(id, now), StringComparer.Ordinal);
            _work.Keep(catalog.Contributing.Select(p => (p.Manifest.Id, Build(p))));

            // Only a plugin the loop has something to ask: one that speaks at a landing alone is started
            // by the landing, for that landing (D100), and a process kept beside the ticks would idle. One on the work wire is
            // started where this loop holds the lease and a unit awaits its steps, once its wait after an unasked exit is over.
            var wanted = catalog.Contributing
                .Where(p => p.Manifest.Hooks is { } hooks
                    && (hooks.Points.Any(point => HookPoints.Loop.Contains(point, StringComparer.Ordinal))
                        || (holds && WorkPlugins.Speaks(p) && _work.Awaits(p.Manifest.Id))))
                .Where(p => !WorkPlugins.Speaks(p) || _work.MayStart(p.Manifest.Id, now))
                .ToDictionary(p => p.Manifest.Id, p => p, StringComparer.Ordinal);

            // The stops a removal or an update asked for, said now that the catalogue says which it was.
            List<(string Id, Held Held)> asked;
            lock (_running)
            {
                asked = [.. _stopped.Select(pair => (pair.Key, pair.Value))];
                _stopped.Clear();
            }

            foreach (var (id, held) in asked) Log.Stopped(id, Why(id, held, catalog), PluginEvents.ByLoop);

            List<string> gone;
            lock (_running)
            {
                gone = _running.Keys.Where(id =>
                        !wanted.TryGetValue(id, out var plugin)
                        || _running[id].Signature != Signature(plugin)
                        || !_running[id].Channel.Alive)
                    .ToList();
            }

            foreach (var id in gone)
            {
                Held held;
                lock (_running)
                {
                    held = _running[id];
                    _running.Remove(id);
                }
                var wasAlive = held.Channel.Alive;
                var code = held.Channel.ExitCode;

                // FABRIC-PERFORM1a, round 2: one that exited after this look noted the exits is an unasked exit all the same, its wait from
                // now. It was removed uncounted and started again below, so a plugin crashing on what it was told never waited.
                if (held.Work && !wasAlive && !waits.ContainsKey(id)) waits[id] = _work.Exited(id, now);
                if (held.Work) _work.Stopped(id);
                await held.Channel.DisposeAsync().ConfigureAwait(false);
                var waited = waits.TryGetValue(id, out var wait);
                lines.Add(wasAlive ? $"plugin  {id}: stopped."
                    : waited ? Waiting(id, wait)
                    : $"plugin  {id}: its hook process had exited — it will be started again if it is still enabled.");
                if (wasAlive) Log.Stopped(id, WorkWhy(id, held, catalog, holds) ?? Why(id, held, catalog), PluginEvents.ByLoop);
                else Log.Failed(id, PluginEvents.AtProcess, PluginEvents.Exited, code, ms: null, PluginEvents.ByLoop);
                if (waited && wait is null) Log.Failed(id, PluginEvents.AtProcess, PluginEvents.Unsound, code: null, ms: null, PluginEvents.ByLoop);
            }

            // A plugin kept for another point while this loop does not hold the lease is neither told nor heard (§C4).
            if (!holds)
            {
                List<string> work;
                lock (_running) work = [.. _running.Where(pair => pair.Value.Work).Select(pair => pair.Key)];
                foreach (var id in work) _work.Stopped(id);
            }

            // A plugin whose start failed and that the loop no longer keeps: its record ends as a stop's would.
            foreach (var id in _unstarted.Keys.Where(id => !wanted.ContainsKey(id)).ToList())
            {
                _unstarted.Remove(id);
                Log.Stopped(id, Why(id, held: null, catalog), PluginEvents.ByLoop);
            }

            foreach (var plugin in wanted.Values.OrderBy(p => p.Manifest.Id, StringComparer.Ordinal))
            {
                Held? kept;
                lock (_running) _running.TryGetValue(plugin.Manifest.Id, out kept);
                if (kept is not null)
                {
                    // FABRIC-PERFORM1a, round 2: a process kept for its loop point while the lease was elsewhere is heard and told again
                    // once the lease is this loop's; it had stayed deaf until it happened to restart.
                    if (holds && kept.Work) _work.Reattach(plugin.Manifest.Id, kept.Channel);
                    continue;
                }

                var id = plugin.Manifest.Id;
                // Its wait asked again: an exit counted partway through this look holds it from this start.
                if (WorkPlugins.Speaks(plugin) && !_work.MayStart(id, now)) continue;
                var took = Stopwatch.StartNew();
                try
                {
                    var channel = await _start(plugin, line => Say(id, line), ct).ConfigureAwait(false);
                    // FABRIC-PERFORM1a: its next generation, heard and told only where this loop holds the lease.
                    var work = WorkPlugins.Speaks(plugin);
                    if (work) _work.Started(id, channel, listen: holds, now);
                    lock (_running) _running[id] = new Held(Signature(plugin), plugin.Manifest.Version, InstalledAt(plugin.Folder), channel, work);
                    lines.Add($"plugin  {id}: started, listening on {(channel.Points.Count > 0 ? string.Join(", ", channel.Points) : "nothing")}.");
                    _unstarted.Remove(id);
                    Log.Started(id, channel.Points, took.ElapsedMilliseconds, PluginEvents.ByLoop);
                }
                catch (DriverException error)
                {
                    // Logged and skipped, never fatal — and said where a person will read it.
                    Say(id, error.Message);
                    lines.Add($"plugin  {id}: could not start — {error.Message}");
                    var kind = PluginFailures.KindOf(error, PluginEvents.Unstartable);
                    if (_unstarted.GetValueOrDefault(id) != kind)
                    {
                        Log.Failed(id, PluginEvents.AtStart, kind, code: null, took.ElapsedMilliseconds, PluginEvents.ByLoop);
                    }

                    _unstarted[id] = kind;

                    // FABRIC-PERFORM1a: a start that failed waits as an unasked exit does, so it is not tried at every look.
                    if (WorkPlugins.Speaks(plugin) && _work.Exited(id, now) is null)
                    {
                        lines.Add(Waiting(id, null));
                        Log.Failed(id, PluginEvents.AtStart, PluginEvents.Unsound, code: null, ms: null, PluginEvents.ByLoop);
                    }
                }
            }

            _work.Look(now);
        }
        finally
        {
            _reconciling.Release();
        }

        return lines;
    }

    /// <summary>What a person reads of a work process's unasked exit (FABRIC-PERFORM1a): when it starts again, or that it does not.</summary>
    private static string Waiting(string id, TimeSpan? wait) => wait is { } after
        ? $"plugin  {id}: its hook process had exited — it is started again in {(after.TotalMinutes == 1 ? "1 minute" : $"{after.TotalMinutes:0} minutes")}."
        : $"plugin  {id}: its hook process exited {WorkBounds.ExitsUnsound} times within an hour — it is not started again until you try again.";

    /// <summary>
    /// Why a work process the catalogue still has, unchanged, was stopped (FABRIC-PERFORM1a): this loop no longer holds the home, or no unit
    /// awaits its steps. Null for any other plugin or reason, which <see cref="Why"/> says.
    /// </summary>
    private static string? WorkWhy(string id, Held held, PluginCatalog catalog, bool holds) =>
        held.Work && catalog.Contributing.FirstOrDefault(p => p.Manifest.Id == id) is { } entry && Signature(entry) == held.Signature
            ? holds ? PluginEvents.Idle : PluginEvents.NotHeld
            : null;

    /// <summary>
    /// Why a plugin's process was stopped, from what the catalogue now says of it: gone from it, switched off, a
    /// new install of it (another version, or its folder replaced), or anything else about it changed.
    /// </summary>
    private static string Why(string id, Held? held, PluginCatalog catalog)
    {
        var entry = catalog.Plugins.FirstOrDefault(p => string.Equals(p.Manifest.Id, id, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return PluginEvents.Removed;
        if (!entry.Enabled) return PluginEvents.Off;
        if (held is not null && (held.Version != entry.Manifest.Version || held.Installed != InstalledAt(entry.Folder)))
        {
            return PluginEvents.Updated;
        }

        return PluginEvents.Changed;
    }

    /// <summary>A plugin's build, as <see cref="Why"/> tells an update: its version and when its folder was made (FABRIC-PERFORM1a, round 2).</summary>
    private static string Build(PluginEntry plugin) => $"{plugin.Manifest.Version}\t{InstalledAt(plugin.Folder)?.UtcTicks}";

    /// <summary>When a plugin's install folder was made: an update replaces the folder whole (D103).</summary>
    private static DateTimeOffset? InstalledAt(string folder) => PluginFolders.MadeAt(folder);

    /// <summary>
    /// Stop one plugin's hook process now, rather than at the next tick — what removing a plugin needs
    /// first, because on Windows a running process holds its working directory (REV3).
    /// </summary>
    /// <returns>Whether a process was running to stop.</returns>
    /// <remarks>
    /// Its <c>plugin.stopped</c> line is written at the next look (PLUGUI1d), once the catalogue says whether the
    /// plugin was removed or updated: a stop asked for here cannot tell which, and the route asks for that look
    /// at once.
    /// </remarks>
    public async Task<bool> StopAsync(string id, CancellationToken ct = default)
    {
        await _reconciling.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Held? held;
            lock (_running)
            {
                if (!_running.Remove(id, out held)) return false;
                _stopped[id] = held;
            }

            if (held.Work) _work.Stopped(id);
            await held.Channel.DisposeAsync().ConfigureAwait(false);
            return true;
        }
        finally
        {
            _reconciling.Release();
        }
    }

    /// <summary>
    /// What each plugin's steps are awaited by, whole (FABRIC-PERFORM1a, §C4): a plugin not named awaits nothing. A changed set is told to a
    /// running process at once; one not running is started at the next look, where the lease is held here.
    /// </summary>
    public void Watch(IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>> awaited) => _work.Watch(awaited);

    /// <summary>
    /// The step events heard since the last take, each with its plugin and the generation of the process it came from, in the order they came.
    /// An event from a process since restarted or stopped is refused here (<see cref="WorkRefusals.NotSender"/>), as is one not of its shape.
    /// </summary>
    public IReadOnlyList<WorkStepHeard> TakeSteps() => _work.TakeSteps();

    /// <summary>The generation of the plugin's work process while one is up and listened to, else null.</summary>
    public int? GenerationOf(string plugin) => _work.GenerationOf(plugin);

    /// <summary>Whether three unasked exits within an hour have marked the plugin unsound, so the loop does not start it again.</summary>
    public bool IsUnsound(string plugin) => _work.IsUnsound(plugin);

    /// <summary>The person's <i>Try again</i> (D119): an unsound plugin is started again at the next look, its waits afresh.</summary>
    public void TryAgain(string plugin) => _work.TryAgain(plugin);

    /// <summary>
    /// One attempt sent to the plugin's current process (§D6), returning that process's generation once it acknowledged receipt. Refused
    /// where this loop does not hold the lease, the plugin's process is not up, or it did not name <c>work/perform</c> and its capability.
    /// </summary>
    /// <remarks>
    /// The answer is handed back only while the process that gave it is still the plugin's current generation (round 2): one stopped or
    /// replaced while the call was out is refused here, never returned as the new process's. <see cref="ReconcileStepAsync"/> is fenced alike.
    /// </remarks>
    public async Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct)
    {
        var (channel, generation) = Performer(plugin);
        await channel.PerformAsync(call, ct).ConfigureAwait(false);
        HandedBack(plugin, channel, generation, HookPoints.Perform);
        return generation;
    }

    /// <summary>What became of one attempt, asked of the plugin's current process (§D6), with that process's generation.</summary>
    public async Task<(WorkReconcileAnswer Answer, int Generation)> ReconcileStepAsync(string plugin, WorkReconcileCall call, CancellationToken ct)
    {
        var (channel, generation) = Performer(plugin);
        var answer = await channel.ReconcileStepAsync(call, ct).ConfigureAwait(false);
        HandedBack(plugin, channel, generation, PluginSteps.ReconcilePoint);
        return (answer, generation);
    }

    /// <summary>
    /// The generation fence (FABRIC-PERFORM1a, round 2): an answer is handed back while its process is the plugin's current one, and refused
    /// as its process's exit otherwise. An answer read just before a stop, or from stdout buffered past an exit, completed after the next
    /// start had been given the plugin's name.
    /// </summary>
    private void HandedBack(string plugin, IHookChannel channel, int generation, string point)
    {
        if (_work.IsCurrent(plugin, channel, generation)) return;
        throw PluginFailures.Mark(
            new DriverException(
                $"plugin `{plugin}` answered `{point}` from its process of generation {generation}, which has since stopped: the answer is not taken."),
            PluginEvents.Exited);
    }

    /// <summary>The plugin's current work process, where this loop may ask it to perform: refused with the reason otherwise.</summary>
    private (IHookChannel Channel, int Generation) Performer(string plugin)
    {
        if (!_holdsHome())
        {
            throw new DriverException($"plugin `{plugin}` is asked to perform only by the loop that holds this home, and this process does not hold it.");
        }

        var (channel, generation) = _work.Performer(plugin)
            ?? throw new DriverException($"plugin `{plugin}`'s work process is not running.");
        if (!channel.Points.Contains(HookPoints.Perform, StringComparer.Ordinal) || !channel.Capabilities.Contains(WorkWire.Capability, StringComparer.Ordinal))
        {
            throw new DriverException(
                $"plugin `{plugin}` did not name `{HookPoints.Perform}` and `{WorkWire.Capability}` at its handshake: it is never asked to perform.");
        }

        return (channel, generation);
    }

    /// <summary>The waterfall, for one planned start.</summary>
    public async Task<HookDecision> ConsiderAsync(Consideration start, CancellationToken ct)
    {
        var payload = HookFrames.Consider(start);

        foreach (var (id, channel) in Listening(HookPoints.QuestConsider))
        {
            HookDecision decision;
            var took = Stopwatch.StartNew();
            try
            {
                decision = await channel.ConsiderAsync(payload, ct).ConfigureAwait(false);
            }
            catch (DriverException error)
            {
                // 🔴 Fail closed, naming the plugin: the loop spends accounts, and a policy that
                // silently failed open is the one wrong result nobody would see.
                Say(id, error.Message);
                CallFailed(id, channel, HookPoints.QuestConsider, error, took);
                return HookDecision.Hold($"plugin `{id}` could not decide — {error.Message} `daoris plugin disable {id}` lets the quest go.");
            }

            // The decision's word, never its reason (D94 §5): the reason stays on the console, below.
            Log.Called(id, HookPoints.QuestConsider, decision.Allowed ? PluginEvents.Allow : PluginEvents.Hold, took.ElapsedMilliseconds);
            if (!decision.Allowed)
            {
                Say(id, $"holds #{start.Quest.Id} → {start.Quest.To}: {decision.Reason}");
                return HookDecision.Hold($"plugin `{id}` holds it: {decision.Reason}");
            }
        }

        return HookDecision.Allow;
    }

    /// <summary>The observation, fanned out and contained. Returns the lines a failure is worth.</summary>
    public async Task<IReadOnlyList<string>> EndedAsync(SessionEnded ended, CancellationToken ct)
    {
        var lines = new List<string>();
        var payload = HookFrames.Ended(ended);

        foreach (var (id, channel) in Listening(HookPoints.SessionEnded))
        {
            var took = Stopwatch.StartNew();
            try
            {
                await channel.EndedAsync(payload, ct).ConfigureAwait(false);
                Log.Called(id, HookPoints.SessionEnded, PluginEvents.Answered, took.ElapsedMilliseconds);
            }
            catch (DriverException error)
            {
                Say(id, error.Message);
                lines.Add($"plugin  {id}: {error.Message}");
                CallFailed(id, channel, HookPoints.SessionEnded, error, took);
            }
        }

        return lines;
    }

    /// <summary>
    /// Tell the plugins at <see cref="HookPoints.StateChanged"/> what moved (STATEHUB1g, D169 point 9), and return at once: the
    /// change waits here for the set's one teller, so whoever tells (the shell's bus, as a press answers) never waits on a plugin,
    /// and nothing a plugin says reaches back to it. Changes are told in the order they came; past <see cref="ChangesWaiting"/>
    /// waiting, the oldest goes. A set that has gone tells nothing more.
    /// </summary>
    public void Tell(StateChangedFrame changed)
    {
        if (!_changes.Writer.TryWrite(changed)) return;
        lock (_tellingGate) _telling ??= Task.Run(TellingAsync);
    }

    /// <summary>
    /// One change told to every plugin listening at <see cref="HookPoints.StateChanged"/>, all at once and contained: a plugin that
    /// fails is said on the console and in the machine log, and the others hear it all the same. Returns the lines a failure is worth.
    /// </summary>
    public async Task<IReadOnlyList<string>> ChangedAsync(StateChangedFrame changed, CancellationToken ct)
    {
        var payload = HookFrames.Changed(changed);
        var told = await Task.WhenAll(Listening(HookPoints.StateChanged).Select(async listener =>
        {
            var (id, channel) = listener;
            var took = Stopwatch.StartNew();
            try
            {
                await channel.ChangedAsync(payload, ct).ConfigureAwait(false);
                Log.Called(id, HookPoints.StateChanged, PluginEvents.Answered, took.ElapsedMilliseconds);
                return null;
            }
            catch (DriverException error)
            {
                Say(id, error.Message);
                CallFailed(id, channel, HookPoints.StateChanged, error, took);
                return $"plugin  {id}: {error.Message}";
            }
        })).ConfigureAwait(false);

        return [.. told.OfType<string>()];
    }

    /// <summary>The set's one teller (STATEHUB1g): each change in turn, until the set goes.</summary>
    private async Task TellingAsync()
    {
        try
        {
            await foreach (var changed in _changes.Reader.ReadAllAsync(_going.Token).ConfigureAwait(false))
            {
                try
                {
                    await ChangedAsync(changed, _going.Token).ConfigureAwait(false);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    // Contained (D64 §4): a call that failed some other way than the wire names (a channel stopped under it) is that
                    // plugin's, and the next change is told all the same. There is no tick here to catch it, so it is caught here.
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The set is going.
        }
    }

    /// <summary>A call that failed, as its kind: a plain refusal from a channel is the plugin's error.</summary>
    private void CallFailed(string id, IHookChannel channel, string point, DriverException error, Stopwatch took)
    {
        var kind = PluginFailures.KindOf(error, PluginEvents.Errored);
        Log.Failed(id, point, kind, kind == PluginEvents.Exited ? channel.ExitCode : null, took.ElapsedMilliseconds, PluginEvents.ByLoop);
    }

    /// <summary>Every process stopped with the loop, and said so: a stop a removal or an update asked for, by what the catalogue says now.</summary>
    public async ValueTask DisposeAsync()
    {
        // The changes waiting go with the set (STATEHUB1g): its teller is stopped before the processes it would tell, and so is the work
        // wire's (FABRIC-PERFORM1a): a frame not yet sent is not sent.
        _changes.Writer.TryComplete();
        await _going.CancelAsync().ConfigureAwait(false);
        _work.Dispose();
        Task? telling;
        lock (_tellingGate) telling = _telling;
        if (telling is not null) await telling.ConfigureAwait(false);

        List<(string Id, Held Held)> running;
        List<(string Id, Held Held)> asked;
        lock (_running)
        {
            running = [.. _running.Select(pair => (pair.Key, pair.Value))];
            asked = [.. _stopped.Select(pair => (pair.Key, pair.Value))];
            _running.Clear();
            _stopped.Clear();
        }

        foreach (var (id, held) in running)
        {
            await held.Channel.DisposeAsync().ConfigureAwait(false);
            Log.Stopped(id, PluginEvents.Ended, PluginEvents.ByLoop);
        }

        if (asked.Count > 0)
        {
            var catalog = PluginCatalog.Load(home);
            foreach (var (id, held) in asked) Log.Stopped(id, Why(id, held, catalog), PluginEvents.ByLoop);
        }
    }

    private List<(string Id, IHookChannel Channel)> Listening(string point)
    {
        lock (_running)
        {
            return _running
                .Where(pair => pair.Value.Channel.Alive && pair.Value.Channel.Points.Contains(point, StringComparer.Ordinal))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => (pair.Key, pair.Value.Channel))
                .ToList();
        }
    }

    private void Say(string id, string line)
    {
        if (output is not null) output.Append($"plugin:{id}", line);
        else say?.Invoke(id, line);
    }

    private static string Signature(PluginEntry plugin) =>
        $"{plugin.Folder}\t{plugin.Manifest.Version}\t{string.Join(" ", plugin.Manifest.Hooks!.Command)}\t{string.Join(",", plugin.Manifest.Hooks.Points)}";
}
