using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// A hook process: the plugin's own program, started in its install folder with the home, its data
/// folder and its id in the environment, and its stdout the wire. Registrations are effects (D64 §4,
/// rule 3): everything the plugin contributes exists exactly as long as this process does.
/// </summary>
/// <remarks>
/// The real <see cref="IHookChannel"/> behind the module's port (STRUCT3a): a <see cref="HookPeer"/> over a started
/// process's stdio. Another module starts one through <see cref="HookChannels"/>.
/// </remarks>
public sealed class HookProcess : IHookChannel
{
    private readonly Process _process;
    private readonly HookPeer _peer;
    private readonly Task _stderr;
    private readonly TimeProvider _clock;

    private HookProcess(Process process, HookPeer peer, Task stderr, TimeProvider clock)
    {
        _process = process;
        _peer = peer;
        _stderr = stderr;
        _clock = clock;
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
    /// <remarks>
    /// <para>🔴 <b>Its first word is resolved, never handed to the system bare</b> (TOOLS5, D121 §2.4): a name a tool
    /// answers for (<c>node</c>, <c>npm</c>, <c>git</c>, <c>gh</c>, <c>az</c>…) is the file Tools resolves, and any
    /// other is found on the child's <c>PATH</c> as an agent's is — both through the shim rule the agents' doors use,
    /// since a bare name started with no shell finds only an <c>.exe</c>. Its environment is the tools', so a landing
    /// plugin's own <c>git push</c> is the git the driver's fetch ran.</para>
    /// </remarks>
    /// <exception cref="DriverException">No hooks, or the tool its first word names cannot run as the person chose.</exception>
    public static ProcessStartInfo StartInfo(PluginEntry plugin, string home)
    {
        var hooks = plugin.Manifest.Hooks
            ?? throw new DriverException($"plugin `{plugin.Manifest.Id}` declares no hooks.");

        var tools = Tools.Read(home);
        var first = hooks.Command[0];
        if (Tools.ResolveCommand(tools, home, first) is { } tool)
        {
            if (tool.Refused)
            {
                throw new DriverException($"plugin `{plugin.Manifest.Id}`'s hook process was not started — `{first}`: {tool.Problem}");
            }

            first = tool.File ?? first;
        }

        var info = new ProcessStartInfo
        {
            FileName = first,
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
        Tools.Hand(info, tools, home);
        // GITAUTH1b (the git-auth design §6.7): the landing's grant goes to the one plugin process started for its push, and no other.
        if (Granting.Value is { } grant) info.Environment[GitRouting.GrantVariable] = grant;
        // What Windows can start, by the agents' resolver and on the child's own PATH: a `.cmd` (an `az`, an `npm`)
        // is held to the shim rule about its arguments rather than started bare and not found (USE1f).
        info.FileName = HarnessActions.WindowsShim(
            info.FileName, info.ArgumentList, info.Environment.TryGetValue(Tools.PathVariable, out var path) ? path : null);
        return info;
    }

    /// <summary>
    /// The landing's grant for the plugin process a landing starts (GITAUTH1b; <see cref="GitGrants"/>), set by
    /// <see cref="LandingPlugins"/> around that one start, so only its process carries <see cref="GitRouting.GrantVariable"/>.
    /// </summary>
    internal static readonly AsyncLocal<string?> Granting = new();

    /// <summary>
    /// The longest frame read from the plugin's stdout (FABRIC-PERFORM1a, round 2): a plugin that speaks a work point is held to the work
    /// wire's <see cref="WorkBounds.FrameBytes"/> on every frame, and any other to <see cref="HookPeer.AnswerBytes"/>, which no answer at the
    /// older points reaches. Stated once, so the kit's trial reads a plugin exactly as the driver does.
    /// </summary>
    public static int FrameLimit(PluginEntry plugin) => WorkPlugins.Speaks(plugin) ? WorkBounds.FrameBytes : HookPeer.AnswerBytes;

    /// <summary>Start the plugin's program and complete the handshake, or throw with the reason.</summary>
    /// <param name="clock">The clock its <see cref="Grace"/> is read on: the machine's unless a test hands its own (TESTCLOCK2).</param>
    public static async Task<HookProcess> StartAsync(
        PluginEntry plugin, string home, Action<string> onLine, CancellationToken ct, TimeSpan? patience = null, TimeProvider? clock = null)
    {
        var info = StartInfo(plugin, home);
        var hooks = plugin.Manifest.Hooks!;
        Directory.CreateDirectory(plugin.Data);

        Process process;
        try
        {
            // Through the start gate (PROCSTART2): a plugin is started at a press's point too. A hook's program serves the
            // loop's points, so its start is the background's (PARALLEL1d), and its caller's token ends its wait for a place
            // as it ends the handshake below (PARALLEL1e).
            process = await ProcessStarts.Shared.StartAsync(info, StartClass.Background, ct).ConfigureAwait(false)
                ?? throw new DriverException($"plugin `{plugin.Manifest.Id}`'s hook process did not start.");
        }
        catch (System.ComponentModel.Win32Exception error)
        {
            throw new DriverException(
                $"plugin `{plugin.Manifest.Id}`'s hook process could not start — `{hooks.Command[0]}`: {error.Message}");
        }

        var stderr = RelayAsync(process.StandardError, onLine);
        // FABRIC-PERFORM1a (§C4): every frame a plugin's process writes is bounded, so none can grow the driver's memory past it; the work
        // wire's 64 KB holds a plugin that speaks it, and round 2 gave every other the answers' bound, which none of theirs reaches.
        clock ??= TimeProvider.System;
        var peer = new HookPeer(process.StandardOutput, process.StandardInput, plugin.Manifest.Id, onLine, patience, FrameLimit(plugin), clock);
        var started = new HookProcess(process, peer, stderr, clock);
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

    public Task<PullRequestState> StateAsync(object payload, CancellationToken ct) => _peer.StateAsync(payload, ct);

    public Task ChangedAsync(object payload, CancellationToken ct) => _peer.ChangedAsync(payload, ct);

    public IReadOnlyList<string> Capabilities => _peer.Capabilities;

    public void Listen(Action<System.Text.Json.JsonElement?>? steps) => _peer.Listen(steps);

    public Task WatchAsync(WorkWatchFrame frame, CancellationToken ct) => _peer.WatchAsync(frame, ct);

    public Task PerformAsync(WorkPerformCall call, CancellationToken ct) => _peer.PerformAsync(call, ct);

    public Task<WorkReconcileAnswer> ReconcileStepAsync(WorkReconcileCall call, CancellationToken ct) => _peer.ReconcileStepAsync(call, ct);

    public Task<WorkCleanupAnswer> CleanupAsync(WorkCleanupCall call, CancellationToken ct) => _peer.CleanupAsync(call, ct);

    /// <summary>Shutdown said, a moment given, and then the process is ended — a plugin that will not leave is left no choice.</summary>
    /// <remarks>
    /// One grace bounds it all (FABRIC-PERFORM1a, round 2): the write of <c>shutdown</c> too, which waited with no bound on a plugin that
    /// stopped reading its stdin, so its process outlived the lease and the loop that owned it.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        using var grace = new CancellationTokenSource(Grace, _clock);
        await _peer.ShutdownAsync(grace.Token).ConfigureAwait(false);
        try
        {
            if (!_process.HasExited)
            {
                try
                {
                    await _process.WaitForExitAsync(grace.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    ChildProcess.KillTree(_process);
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

    /// <summary>
    /// The longest stderr line relayed whole, in UTF-8 bytes (PLUGSTDERR1, §C4): past any sentence a plugin says to a person, and still a bound
    /// on what one line can hold. A longer line is relayed clipped to it, marked <see cref="Clipped"/>, and the next line as it comes.
    /// </summary>
    public const int StderrBytes = 8 * 1024;

    /// <summary>What ends a clipped stderr line, so a reader sees it was cut rather than ended.</summary>
    public const string Clipped = " …";

    /// <summary>A plugin's stderr, line by line, each under <see cref="StderrBytes"/>: no line it writes can grow the driver's memory past it.</summary>
    private static async Task RelayAsync(TextReader stderr, Action<string> onLine)
    {
        try
        {
            var lines = new BoundedLines(stderr, StderrBytes, clip: true);
            while (await lines.ReadAsync(CancellationToken.None).ConfigureAwait(false) is { } line)
            {
                if (line.Text is { Length: > 0 } text) onLine(line.Clipped ? text + Clipped : text);
            }
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException)
        {
        }
    }
}
