using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// `daoris-driver` itself: run with no verb it prints its usage and starts no headless loop on
/// the install's home.
/// </summary>
/// <remarks>
/// <para>The host as built, started as a person starts it, against a stand-in service on a loopback port
/// that counts what reaches it — "starts nothing" is a claim about the process, so the process is what is
/// asked. Every <c>DAORIS_</c> variable is dropped from its environment and the home is a scratch folder in the repository's
/// gitignored <c>_fixtures/</c>, never OS temp: no run here can reach this machine's own home or service.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class DriverHostTests : IDisposable
{
    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "driver-host", Guid.NewGuid().ToString("N")[..8]);

    // `DAORIS_HOME`, and a folder beside it that a retired driver-file variable names.
    private readonly string _home;
    private readonly string _elsewhere;

    // The home's lease, which covers every writer.
    private string LockFile => LeaseIn(_home);

    private static string LeaseIn(string home) => DaorisHome.Entry(home, HomeEntry.HomeLease);

    public DriverHostTests()
    {
        _home = Path.Combine(_scratch, "home");
        _elsewhere = Path.Combine(_scratch, "elsewhere");
        Directory.CreateDirectory(_home);
    }

    public void Dispose() => GitSeed.Discard(_scratch);

    /// <summary>🔴 No verb: the usage, exit 2 — and nothing reaches the service, and no lock is taken.</summary>
    [Fact]
    public async Task A_bare_invocation_prints_the_usage_and_starts_nothing()
    {
        await using var service = CountingService.Start();

        var (code, said) = await RunAsync(service);

        Assert.Equal(2, code);
        Assert.Contains("usage: daoris <command>", said);
        Assert.Contains("drive [--once | --until-idle] [--quiet-end <minutes>] [--share]", said);
        Assert.DoesNotContain("daoris-driver ", said);
        Assert.Equal(0, service.Requests);
        Assert.False(File.Exists(LockFile));
    }

    /// <summary>A word no group owns is the CLI's; a build that carries no CLI says so, exit 2, and starts nothing.</summary>
    [Fact]
    public async Task A_word_no_group_owns_is_handed_to_the_cli_and_starts_nothing()
    {
        await using var service = CountingService.Start();

        var (code, said) = await RunAsync(service, "watch");

        Assert.Equal(2, code);
        Assert.Contains("`watch` is no group of this program", said);
        Assert.Equal(0, service.Requests);
    }

    /// <summary>
    /// 🔴 A loop on a home another live driver holds is refused naming it — which door, its process and
    /// since when — as a refusal (exit 1), before anything reaches the service.
    /// </summary>
    [Fact]
    public async Task A_loop_on_a_home_a_live_driver_holds_is_refused_naming_it_before_the_service_is_reached()
    {
        await using var service = CountingService.Start();
        Plant("desktop", Environment.ProcessId, OurStart());
        var planted = File.ReadAllText(LockFile);

        var (code, said) = await RunAsync(service, "drive", "--once");

        Assert.Equal(1, code);
        Assert.Contains("the desktop", said);
        Assert.Contains($"pid {Environment.ProcessId}", said);
        Assert.Contains("--share", said);
        Assert.Equal(0, service.Requests);
        Assert.Equal(planted, File.ReadAllText(LockFile));
    }

    /// <summary>`--share` runs beside it on purpose, says so, and leaves the other driver's lock where it is.</summary>
    [Fact]
    public async Task Sharing_runs_the_loop_beside_a_live_driver()
    {
        await using var service = CountingService.Start();
        Plant("desktop", Environment.ProcessId, OurStart());
        var planted = File.ReadAllText(LockFile);

        var (_, said) = await RunAsync(service, "drive", "--once", "--share");

        Assert.Contains("running beside the desktop", said);
        Assert.True(service.Requests > 0, said);
        Assert.Equal(planted, File.ReadAllText(LockFile));
    }

    /// <summary>A lock whose process is gone never blocks: the loop runs, and lets the home go when it ends.</summary>
    [Fact]
    public async Task A_stale_lock_does_not_block_the_loop()
    {
        await using var service = CountingService.Start();
        Plant("desktop", int.MaxValue, OurStart());

        var (_, said) = await RunAsync(service, "drive", "--once");

        Assert.True(service.Requests > 0, said);
        Assert.False(File.Exists(LockFile), "the loop kept the home after it ended");
    }

    /// <summary>
    /// 🔴 A verb that changes the home holds its lease while it acts alone, and lets it go as it ends, whatever its
    /// answer; beside a running desktop it acts holding nothing and leaving the desktop's lease as it is.
    /// </summary>
    [Fact]
    public async Task A_verb_holds_the_home_for_its_act_alone_and_acts_beside_a_running_desktop()
    {
        await using var service = CountingService.Start();
        var package = Path.Combine(_home, "no-such-plugin.nupkg");

        await RunAsync(service, "plugin", "install", package);
        Assert.False(File.Exists(LockFile), "the verb kept the home after it ended");
        Assert.True(File.Exists(HomeIdentity.PathIn(_home)), "the lease records the home's id, minted where it had none");

        Plant("desktop", Environment.ProcessId, OurStart());
        var planted = File.ReadAllText(LockFile);
        await RunAsync(service, "plugin", "install", package);
        Assert.Equal(planted, File.ReadAllText(LockFile));
    }

    private void Plant(string kind, int pid, long started) => Plant(LockFile, kind, pid, started);

    private static void Plant(string lease, string kind, int pid, long started) =>
        File.WriteAllText(lease, new JsonObject
        {
            ["kind"] = kind, ["pid"] = pid, ["started"] = started, ["since"] = "2026-09-30T08:02:13+00:00",
        }.ToJsonString());

    private static long OurStart()
    {
        using var self = Process.GetCurrentProcess();
        return self.StartTime.ToUniversalTime().Ticks;
    }

    /// <summary>The host as built, beside this test's own build: its exit code, and everything it said.</summary>
    private Task<(int Code, string Said)> RunAsync(CountingService service, params string[] args) =>
        RunAsync(service, new Dictionary<string, string>(), args);

    /// <summary>The same, with <paramref name="named"/> laid over the hermetic environment.</summary>
    private async Task<(int Code, string Said)> RunAsync(
        CountingService service, IReadOnlyDictionary<string, string> named, params string[] args)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = _home,
        };
        info.ArgumentList.Add(HostDll());
        foreach (var arg in args) info.ArgumentList.Add(arg);

        // Hermetic: nothing of this machine's Daoris reaches the run — its home, its remotes, its key.
        foreach (var name in info.Environment.Keys.Where(key => key.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            info.Environment.Remove(name);
        }

        info.Environment["DAORIS_HOME"] = _home;
        info.Environment["DAORIS_SERVICE_URL"] = service.Url;
        foreach (var (name, value) in named) info.Environment[name] = value;

        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try
        {
            await process.WaitForExitAsync(bound.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"daoris-driver {string.Join(' ', args)} did not end: {await output}{await error}");
        }

        return (process.ExitCode, await output + await error);
    }

    /// <summary>The host's build beside this test's own; <c>DriverStartFailureTests</c> starts it too.</summary>
    internal static string HostDll()
    {
        // This test's build is <tests>/bin/<configuration>/<framework>/; the host's sits the same way.
        var build = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var framework = build.Name;
        var configuration = build.Parent!.Name;
        var desktop = build.Parent.Parent!.Parent!.Parent!.FullName;
        var dll = Path.Combine(desktop, "Daoris.Desktop.Driver.Host", "bin", configuration, framework, "daoris-driver.dll");
        return File.Exists(dll) ? dll : throw new FileNotFoundException("the host is built before these tests; it was not found", dll);
    }

    /// <summary>A service on a loopback port that counts every request and answers the tick's reads empty.</summary>
    private sealed class CountingService : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly Task _serving;
        private int _requests;

        public string Url { get; }

        public int Requests => Volatile.Read(ref _requests);

        private CountingService(HttpListener listener, string url)
        {
            _listener = listener;
            Url = url.TrimEnd('/');
            _serving = ServeAsync();
        }

        public static CountingService Start()
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var url = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            return new CountingService(listener, url);
        }

        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await _listener.GetContextAsync(); }
                catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or InvalidOperationException) { return; }

                Interlocked.Increment(ref _requests);
                var read = context.Request.HttpMethod == "GET"
                           && context.Request.Url!.AbsolutePath is "/api/quests" or "/api/registry" or "/api/sessions";
                var bytes = Encoding.UTF8.GetBytes(read ? "[]" : """{"error":"the stand-in answers only the tick's reads"}""");
                context.Response.StatusCode = read ? 200 : 404;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            _listener.Close();
            try { await _serving; } catch (ObjectDisposedException) { }
        }
    }
}
