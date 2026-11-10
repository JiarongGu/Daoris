using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The hook wire: JSON-RPC 2.0, one frame per line, over a plugin process's stdio — the
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
    // A frame longer than this, in UTF-8 bytes, is dropped unread. A process's stdout is read with the bound
    // HookProcess.FrameLimit names for its plugin; null reads whole lines, as a test's stand-in that gives only lines is read.
    int? frameLimit = null,
    // The clock the grace of a shutdown is read on: the machine's, or a test's that it moves itself.
    TimeProvider? clock = null) : IHookChannel
{
    /// <summary>The wire version this host speaks. Stated in `initialize`; a plugin answering another is refused.</summary>
    public const int ProtocolVersion = 1;

    /// <summary>
    /// The longest frame read from a plugin that speaks no work point: far past any answer its points read — a
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

    // Where `work/step` goes (null: nowhere), and the requests in flight at once.
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
    /// patience here, however short a patience it gives the points' calls.</param>
    /// <param name="events">For a plugin on <see cref="HookPoints.Event"/>: the run, the grammar, the last <c>seq</c> minted and the build.</param>
    public async Task InitializeAsync(
        string home, string data, IReadOnlyList<string> declared, CancellationToken ct, TimeSpan? patience = null, WorkEventHandshake? events = null)
    {
        _pump ??= PumpAsync(_stopping.Token);

        object parameters = events is null
            ? new { protocolVersion = ProtocolVersion, plugin, home, data, points = declared }
            : new { protocolVersion = ProtocolVersion, plugin, home, data, points = declared, events = new { run = events.Run, grammar = events.Grammar, seq = events.Seq, build = events.Build } };
        var answer = await RequestAsync("initialize", parameters, ct, patience).ConfigureAwait(false);

        // 🔴 Every read checks the shape first: a property read on the wrong kind throws
        // InvalidOperationException, which is not a DriverException, so it would escape the tick's catch and
        // kill every tick without naming the plugin.
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

                // The work points are this build's too, spoken only by the lease's loop; the system points, by a system's apply and plan.
                if (!HookPoints.All.Contains(name, StringComparer.Ordinal) && !HookPoints.Work.Contains(name, StringComparer.Ordinal)
                    && !HookPoints.System.Contains(name, StringComparer.Ordinal))
                {
                    // A point is additive, so a plugin that grew one keeps the points this build has; refusing it whole
                    // would make an older build lose the plugin's landing. Said once, and never asked there.
                    onLine?.Invoke(
                        $"plugin `{plugin}` listens on `{name}`, which is not a point this build has "
                        + $"({string.Join(", ", HookPoints.All)}): it is not asked there.");
                    continue;
                }

                if (!points.Contains(name, StringComparer.Ordinal)) points.Add(name);
            }
        }

        Points = points;

        // The capabilities it names that this build knows; one it grew is passed over, as a point is.
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

    /// <summary>An observation: the answer is waited for, as <see cref="EndedAsync"/>'s is, and never read.</summary>
    public async Task ChangedAsync(object payload, CancellationToken ct) =>
        await RequestAsync($"hook/{HookPoints.StateChanged}", payload, ct).ConfigureAwait(false);

    /// <summary>One <c>work/event</c> frame: the answer is waited for, so the next waits on it, and never read.</summary>
    public async Task EventAsync(object frame, CancellationToken ct) =>
        await RequestAsync($"hook/{HookPoints.Event}", frame, ct).ConfigureAwait(false);

    /// <summary>
    /// The query's frame, and its answer read by shape (<see cref="PullRequestAnswers.Read"/>): a
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

    /// <summary>A system point's request, asked only where the plugin listens; <see cref="SystemWire"/> reads its answer by shape.</summary>
    public async Task<JsonElement> SystemAsync(string point, object payload, CancellationToken ct)
    {
        if (!HookPoints.System.Contains(point, StringComparer.Ordinal) || !Points.Contains(point, StringComparer.Ordinal))
        {
            throw Unreadable($"plugin `{plugin}` does not listen on `{point}`, so it is not asked there.");
        }

        return await InFlightAsync($"hook/{point}", payload, ct).ConfigureAwait(false);
    }

    public IReadOnlyList<string> Capabilities { get; private set; } = [];

    /// <summary>The calls sent and not yet answered.</summary>
    internal int Waiting => _pending.Count;

    public void Listen(Action<JsonElement?>? steps) => _steps = steps;

    /// <summary>The awaited set: its answer is waited for, as an observation's is, and never read.</summary>
    public async Task WatchAsync(WorkWatchFrame frame, CancellationToken ct) =>
        await RequestAsync(WorkWire.WatchMethod, WorkWire.Watch(frame), ct).ConfigureAwait(false);

    /// <summary>One attempt, its answer the receipt of this invocation and nothing else: anything other is unreadable.</summary>
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

    /// <summary>What became of one attempt, read by shape (<see cref="WorkWire.ReadReconcileAnswer"/>): anything else is unreadable.</summary>
    public async Task<WorkReconcileAnswer> ReconcileStepAsync(WorkReconcileCall call, CancellationToken ct)
    {
        var answer = await InFlightAsync(WorkWire.ReconcileMethod, WorkWire.Reconcile(call), ct).ConfigureAwait(false);
        return WorkWire.ReadReconcileAnswer(answer) ?? throw Unreadable(
            $"plugin `{plugin}` answered {Raw(answer)}, which is not what became of an attempt — "
            + "`{ \"state\": \"done\" | \"not-started\" | \"running\" | \"failed\" | \"unknown\", \"outcome\": … }`, done and failed with their outcome.");
    }

    /// <summary>One receipt's removal, read by shape (<see cref="WorkWire.ReadCleanupAnswer"/>): anything else is unreadable.</summary>
    public async Task<WorkCleanupAnswer> CleanupAsync(WorkCleanupCall call, CancellationToken ct)
    {
        var answer = await InFlightAsync(WorkWire.CleanupMethod, WorkWire.Cleanup(call), ct).ConfigureAwait(false);
        return WorkWire.ReadCleanupAnswer(answer) ?? throw Unreadable(
            $"plugin `{plugin}` answered {Raw(answer)}, which is not what became of a resource — "
            + "`{ \"disposition\": \"removed\" | \"already-gone\" | \"retained\" | \"failed\", \"proof\": …, \"message\": … }`.");
    }

    /// <summary>
    /// A request among at most <see cref="WorkBounds.CallsInFlight"/>: one past them waits the peer's patience for a place,
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
        using var grace = new CancellationTokenSource(HookProcess.Grace, clock ?? TimeProvider.System);
        await ShutdownAsync(grace.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Told to go, the write of <c>shutdown</c> given up when <paramref name="ct"/> is: a plugin that stops
    /// reading its stdin fills the pipe, and a write with no bound would hold whoever disposed it, and the process, for ever. Nothing is read after.
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
        // The wire's end fails what is pending after it stops being alive, so a call registered across that moment asks again here.
        if (!_alive)
        {
            _pending.TryRemove(id, out _);
            throw Marked($"plugin `{plugin}` is not running.", PluginEvents.Exited);
        }

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
        catch (OperationCanceledException)
        {
            _pending.TryRemove(id, out _);
            throw;
        }
        catch (IOException error)
        {
            _pending.TryRemove(id, out _);
            _alive = false;
            throw Marked($"plugin `{plugin}` stopped answering: {error.Message}", PluginEvents.Exited);
        }
    }

    /// <summary>A failure of the wire, marked with its kind for the machine log; the sentence is unchanged.</summary>
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
            var write = outgoing.WriteLineAsync(frame.ToJsonString());
            try
            {
                await write.WaitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!write.IsCompleted)
            {
                // The writer still holds the abandoned line and refuses the next with InvalidOperationException: the wire is gone.
                _alive = false;
                throw;
            }

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
                        // An answer too long to read fails its call now, as the plugin's unreadable answer, rather than
                        // waiting out its patience to say only that it is late.
                        if (read.Id is { } answered && !read.Method)
                        {
                            Fail(answered, Unreadable(
                                $"plugin `{plugin}` answered with a frame over {Size(limit!.Value)}, which is not read: an answer is at most that long."));
                            continue;
                        }

                        // Dropped unread. The lease's loop counts it and asks the plugin to send again what it
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

                // A JSON value that is not an object, and an object that is neither an answer nor a request, are not
                // frames either; nor is an answer whose id is no number, such as the `null` of a JSON-RPC parse error.
                // A bare `42` or such an id would throw InvalidOperationException from a read below, which the catch
                // does not name, and the wire would end.
                var hasMethod = frame.ValueKind == JsonValueKind.Object && frame.TryGetProperty("method", out _);
                if (frame.ValueKind != JsonValueKind.Object
                    || (!hasMethod && !(frame.TryGetProperty("id", out var idValue) && idValue.ValueKind == JsonValueKind.Number)))
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
                    // The one notification this wire takes, `work/step`, from a process that declared it and that
                    // the lease's loop listens to. Its sender is this process, whatever the frame says; what is believed of it is not decided here.
                    steps(frame.TryGetProperty("params", out var @params) ? @params : default(JsonElement));
                }
                // A request FROM a plugin is not a thing this wire has: there is nothing a plugin may
                // ask the host for, by design — so it is neither answered nor errored, and a
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
