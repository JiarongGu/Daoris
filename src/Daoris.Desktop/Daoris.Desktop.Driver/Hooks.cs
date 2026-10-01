using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>A plugin's answer at a decision point (D64 §4): allow, or hold with the sentence a person reads.</summary>
public sealed record HookDecision(bool Allowed, string? Reason = null)
{
    public static readonly HookDecision Allow = new(true);

    public static HookDecision Hold(string reason) => new(false, reason);
}

/// <summary>The points a plugin may listen on. Named here once; a manifest naming another is refused at `initialize`.</summary>
public static class HookPoints
{
    /// <summary>A decision: one consideration the planner marked <i>Start</i>. The first hold in catalogue order ends the waterfall.</summary>
    public const string QuestConsider = "quest/consider";

    /// <summary>An observation: what a tick concluded. Contained — nothing a plugin says here changes anything.</summary>
    public const string SessionEnded = "session/ended";

    /// <summary>
    /// An act, after Daoris's own (WSR4, D100): a branch rule's landing has made its branch, and the plugin
    /// the rule names pushes it and opens the pull request for its platform. Spoken by the landing, never
    /// by the loop, and it can undo nothing: a failure here leaves the branch.
    /// </summary>
    public const string Land = "work/land";

    public static readonly IReadOnlyList<string> All = [QuestConsider, SessionEnded, Land];

    /// <summary>The points the driver loop asks at — a plugin that speaks on none of them is not kept running beside it.</summary>
    public static readonly IReadOnlyList<string> Loop = [QuestConsider, SessionEnded];
}

/// <summary>
/// What each point is sent, built in one place (PLUG8): the loop and the landing send these, and the
/// plugin kit's sample frames are built by the same functions, so a sample cannot say what the driver
/// does not. Every name is spelled here rather than left to a serializer's policy.
/// </summary>
public static class HookFrames
{
    /// <summary><see cref="HookPoints.QuestConsider"/>: one planned start.</summary>
    public static object Consider(Consideration start) => new
    {
        quest = new { id = start.Quest.Id, title = start.Quest.Title, from = start.Quest.From, to = start.Quest.To },
        repository = start.Quest.To,
        workspace = start.Workspace,
        root = start.Root,
    };

    /// <summary><see cref="HookPoints.SessionEnded"/>: what a tick concluded.</summary>
    public static object Ended(SessionEnded ended) => new
    {
        session = ended.Session,
        quest = ended.Quest,
        repository = ended.Repository,
        state = ended.State,
        adapter = ended.Adapter,
        account = ended.Account,
        byPerson = ended.ByPerson,
        note = ended.Note,
    };

    /// <summary><see cref="HookPoints.Land"/>: the branch a landing just made (D100).</summary>
    public static object Land(LandingFrame frame) => new
    {
        repository = frame.Repository,
        workspace = frame.Workspace,
        root = frame.Root,
        branch = frame.Branch,
        @base = frame.Base,
        title = frame.Title,
        quest = frame.Quest is { } quest ? new { id = quest, title = frame.Title } : null,
        session = frame.Session,
        commits = frame.Commits.Select(commit => new { sha = commit.Sha, subject = commit.Subject }).ToArray(),
    };
}

/// <summary>
/// One plugin's side of the wire, as the host sees it — what a process gives once it speaks, and what a
/// test can hand in without one.
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
    TimeSpan? patience = null) : IHookChannel
{
    /// <summary>The wire version this host speaks. Stated in `initialize`; a plugin answering another is refused.</summary>
    public const int ProtocolVersion = 1;

    /// <summary>How long a call waits for its answer unless the caller says otherwise — the loop's points wait this long.</summary>
    public static readonly TimeSpan DefaultPatience = TimeSpan.FromSeconds(10);

    private readonly TimeSpan _patience = patience ?? DefaultPatience;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _stopping = new();
    private Task? _pump;
    private int _nextId;
    private volatile bool _alive = true;

    public IReadOnlyList<string> Points { get; private set; } = [];

    public bool Alive => _alive;

    /// <summary>
    /// The handshake: what the host is, where the plugin is, and which points it may listen on. The
    /// plugin answers with its version and the points it actually listens on — a subset of what its
    /// manifest declared, because a manifest is a claim and the process is the fact.
    /// </summary>
    public async Task InitializeAsync(
        string home, string data, IReadOnlyList<string> declared, CancellationToken ct)
    {
        _pump ??= PumpAsync(_stopping.Token);

        var answer = await RequestAsync(
            "initialize",
            new { protocolVersion = ProtocolVersion, plugin, home, data, points = declared },
            ct).ConfigureAwait(false);

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

                if (!HookPoints.All.Contains(name, StringComparer.Ordinal))
                {
                    throw Unreadable(
                        $"plugin `{plugin}` listens on `{name}`, which is not a point this build has "
                        + $"({string.Join(", ", HookPoints.All)}).");
                }

                points.Add(name);
            }
        }

        Points = points;
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

    /// <summary>Told to go, politely; whoever owns the process then makes sure it did.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await NotifyAsync("shutdown").ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // Already gone, which is the state being asked for.
        }

        _stopping.Cancel();
        _alive = false;
        if (_pump is { } pump) _ = pump.ContinueWith(static t => t.Exception, TaskScheduler.Default);
    }

    private async Task<JsonElement> RequestAsync(string method, object? parameters, CancellationToken ct)
    {
        if (!_alive) throw Marked($"plugin `{plugin}` is not running.", PluginEvents.Exited);

        var id = Interlocked.Increment(ref _nextId);
        var waiting = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = waiting;

        var frame = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method };
        if (parameters is not null) frame["params"] = JsonSerializer.SerializeToNode(parameters);

        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bounded.CancelAfter(_patience);
        try
        {
            await SendAsync(frame, bounded.Token).ConfigureAwait(false);
            return await waiting.Task.WaitAsync(bounded.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _pending.TryRemove(id, out _);
            throw Marked($"plugin `{plugin}` did not answer `{method}` within {_patience.TotalSeconds:0}s.", PluginEvents.Late);
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

    private Task NotifyAsync(string method) =>
        SendAsync(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = method }, CancellationToken.None);

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

    private async Task PumpAsync(CancellationToken ct)
    {
        try
        {
            while (await incoming.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
            {
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

                if (frame.TryGetProperty("id", out var id) && !hasMethod)
                {
                    Complete(id, frame);
                }
                // A request FROM a plugin is not a thing this wire has: there is nothing a plugin may
                // ask the host for, by design (D64 §7) — so it is neither answered nor errored, and a
                // plugin that waits on one waits on itself. A notification is noise, and is ignored.
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

    private static string Truncate(string text) => text.Length <= 80 ? text : text[..77] + "…";
}

/// <summary>
/// A hook process: the plugin's own program, started in its install folder with the home, its data
/// folder and its id in the environment, and its stdout the wire. Registrations are effects (D64 §4,
/// rule 3): everything the plugin contributes exists exactly as long as this process does.
/// </summary>
public sealed class HookProcess : IHookChannel
{
    private readonly Process _process;
    private readonly HookPeer _peer;
    private readonly Task _stderr;

    private HookProcess(Process process, HookPeer peer, Task stderr)
    {
        _process = process;
        _peer = peer;
        _stderr = stderr;
    }

    public IReadOnlyList<string> Points => _peer.Points;

    public bool Alive => _peer.Alive && !_process.HasExited;

    public int? ExitCode
    {
        get
        {
            try
            {
                return _process.HasExited ? _process.ExitCode : null;
            }
            catch (InvalidOperationException)
            {
                // Disposed, or never started: there is no code to say.
                return null;
            }
        }
    }

    /// <summary>How long a plugin has to leave once it is told `shutdown`, before it is ended.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(2);

    /// <summary>UTF-8 with no byte-order mark: a mark ahead of the first frame is not JSON.</summary>
    private static readonly System.Text.Encoding Utf8 = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// What a plugin's program is started with: in its install folder, stdio redirected, and its id, its
    /// folders and the home in the environment. Stated once, so the plugin kit's try (PLUG8) starts a
    /// plugin exactly as the driver does.
    /// </summary>
    public static ProcessStartInfo StartInfo(PluginEntry plugin, string home)
    {
        var hooks = plugin.Manifest.Hooks
            ?? throw new DriverException($"plugin `{plugin.Manifest.Id}` declares no hooks.");

        var info = new ProcessStartInfo
        {
            FileName = hooks.Command[0],
            WorkingDirectory = plugin.Folder,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // The wire is UTF-8 both ways: left to the default, the streams take the console's code page,
            // and on a Chinese-locale machine a plugin's em dash read back as `鈥?` (PLUG8, seen on the window).
            StandardInputEncoding = Utf8,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8,
            CreateNoWindow = true,
        };
        foreach (var argument in hooks.Command.Skip(1)) info.ArgumentList.Add(argument);
        // Where it is and what it keeps, told rather than guessed (Yaorin's lesson; D64 §3).
        info.Environment["DAORIS_PLUGIN_ID"] = plugin.Manifest.Id;
        info.Environment["DAORIS_PLUGIN_FOLDER"] = plugin.Folder;
        info.Environment["DAORIS_PLUGIN_DATA"] = plugin.Data;
        info.Environment[DaorisHome.Variable] = home;
        return info;
    }

    /// <summary>Start the plugin's program and complete the handshake, or throw with the reason.</summary>
    public static async Task<HookProcess> StartAsync(
        PluginEntry plugin, string home, Action<string> onLine, CancellationToken ct, TimeSpan? patience = null)
    {
        var info = StartInfo(plugin, home);
        var hooks = plugin.Manifest.Hooks!;
        Directory.CreateDirectory(plugin.Data);

        Process process;
        try
        {
            process = Process.Start(info)
                ?? throw new DriverException($"plugin `{plugin.Manifest.Id}`'s hook process did not start.");
        }
        catch (System.ComponentModel.Win32Exception error)
        {
            throw new DriverException(
                $"plugin `{plugin.Manifest.Id}`'s hook process could not start — `{hooks.Command[0]}`: {error.Message}");
        }

        var stderr = RelayAsync(process.StandardError, onLine);
        var peer = new HookPeer(process.StandardOutput, process.StandardInput, plugin.Manifest.Id, onLine, patience);
        var started = new HookProcess(process, peer, stderr);
        try
        {
            await peer.InitializeAsync(home, plugin.Data, hooks.Points, ct).ConfigureAwait(false);
        }
        catch
        {
            await started.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return started;
    }

    public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => _peer.ConsiderAsync(payload, ct);

    public Task EndedAsync(object payload, CancellationToken ct) => _peer.EndedAsync(payload, ct);

    public Task<PluginLanding> LandAsync(object payload, CancellationToken ct) => _peer.LandAsync(payload, ct);

    /// <summary>Shutdown said, a moment given, and then the process is ended — a plugin that will not leave is left no choice.</summary>
    public async ValueTask DisposeAsync()
    {
        await _peer.DisposeAsync().ConfigureAwait(false);
        try
        {
            if (!_process.HasExited)
            {
                using var grace = new CancellationTokenSource(Grace);
                try
                {
                    await _process.WaitForExitAsync(grace.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }

        _ = _stderr.ContinueWith(static t => t.Exception, TaskScheduler.Default);
        _process.Dispose();
    }

    private static async Task RelayAsync(StreamReader stderr, Action<string> onLine)
    {
        try
        {
            while (await stderr.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (line.Length > 0) onLine(line);
            }
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException)
        {
        }
    }
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
    PluginHealth? health = null) : IAsyncDisposable
{
    /// <summary>A running plugin's process, with what it was started from: what a stop's reason is judged against.</summary>
    private sealed record Held(string Signature, string Version, DateTimeOffset? Installed, IHookChannel Channel);

    private readonly Dictionary<string, Held> _running = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _reconciling = new(1, 1);

    // Stopped by a removal or an update (StopAsync) before the catalogue says which: written at the next look.
    private readonly Dictionary<string, Held> _stopped = new(StringComparer.Ordinal);

    // The plugins whose last start failed, by the kind it failed with: a failure repeated at every look is one line.
    private readonly Dictionary<string, string> _unstarted = new(StringComparer.Ordinal);

    private readonly Func<PluginEntry, Action<string>, CancellationToken, Task<IHookChannel>> _start =
        start ?? (async (plugin, onLine, ct) => await HookProcess.StartAsync(plugin, home, onLine, ct).ConfigureAwait(false));

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
            // Only a plugin the loop has something to ask: one that speaks at a landing alone is started
            // by the landing, for that landing (D100), and a process kept beside the ticks would idle.
            var wanted = catalog.Contributing
                .Where(p => p.Manifest.Hooks is { } hooks && hooks.Points.Any(point => HookPoints.Loop.Contains(point, StringComparer.Ordinal)))
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
                await held.Channel.DisposeAsync().ConfigureAwait(false);
                lines.Add(wasAlive
                    ? $"plugin  {id}: stopped."
                    : $"plugin  {id}: its hook process had exited — it will be started again if it is still enabled.");
                if (wasAlive) Log.Stopped(id, Why(id, held, catalog), PluginEvents.ByLoop);
                else Log.Failed(id, PluginEvents.AtProcess, PluginEvents.Exited, code, ms: null, PluginEvents.ByLoop);
            }

            // A plugin whose start failed and that the loop no longer keeps: its record ends as a stop's would.
            foreach (var id in _unstarted.Keys.Where(id => !wanted.ContainsKey(id)).ToList())
            {
                _unstarted.Remove(id);
                Log.Stopped(id, Why(id, held: null, catalog), PluginEvents.ByLoop);
            }

            foreach (var plugin in wanted.Values.OrderBy(p => p.Manifest.Id, StringComparer.Ordinal))
            {
                bool running;
                lock (_running) running = _running.ContainsKey(plugin.Manifest.Id);
                if (running) continue;

                var id = plugin.Manifest.Id;
                var took = Stopwatch.StartNew();
                try
                {
                    var channel = await _start(plugin, line => Say(id, line), ct).ConfigureAwait(false);
                    lock (_running) _running[id] = new Held(Signature(plugin), plugin.Manifest.Version, InstalledAt(plugin.Folder), channel);
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
                }
            }
        }
        finally
        {
            _reconciling.Release();
        }

        return lines;
    }

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

    /// <summary>When a plugin's install folder was made: an update replaces the folder whole (D103).</summary>
    private static DateTimeOffset? InstalledAt(string folder)
    {
        try
        {
            return Directory.Exists(folder) ? Directory.GetCreationTimeUtc(folder) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

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

            await held.Channel.DisposeAsync().ConfigureAwait(false);
            return true;
        }
        finally
        {
            _reconciling.Release();
        }
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

    /// <summary>A call that failed, as its kind: a plain refusal from a channel is the plugin's error.</summary>
    private void CallFailed(string id, IHookChannel channel, string point, DriverException error, Stopwatch took)
    {
        var kind = PluginFailures.KindOf(error, PluginEvents.Errored);
        Log.Failed(id, point, kind, kind == PluginEvents.Exited ? channel.ExitCode : null, took.ElapsedMilliseconds, PluginEvents.ByLoop);
    }

    /// <summary>Every process stopped with the loop, and said so: a stop a removal or an update asked for, by what the catalogue says now.</summary>
    public async ValueTask DisposeAsync()
    {
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
