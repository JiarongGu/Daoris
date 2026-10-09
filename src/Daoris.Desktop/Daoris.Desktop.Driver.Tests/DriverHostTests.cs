using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// `daoris-driver` itself (DRV8a, D104): run with no verb to read its usage, it started a headless loop on
/// the install's home, and that loop took a quest two seconds before the desktop's own.
/// </summary>
/// <remarks>
/// <para>The host as built, started as a person starts it, against a stand-in service on a loopback port
/// that counts what reaches it — "starts nothing" is a claim about the process, so the process is what is
/// asked. Every <c>DAORIS_</c> variable is dropped from its environment and the home is a scratch folder:
/// no run here can reach this machine's own home or service.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class DriverHostTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-driver-host-" + Guid.NewGuid().ToString("N")[..8]);

    private string LockFile => Path.Combine(_home, "driver.lock");

    public DriverHostTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>🔴 No verb: the usage, exit 2 — and nothing reached the service, and no lock was taken.</summary>
    [Fact]
    public async Task A_bare_invocation_prints_the_usage_and_starts_nothing()
    {
        await using var service = CountingService.Start();

        var (code, said) = await RunAsync(service);

        Assert.Equal(2, code);
        Assert.Contains("usage: daoris-driver", said);
        Assert.Contains("drive [--once | --until-idle] [--share]", said);
        Assert.Equal(0, service.Requests);
        Assert.False(File.Exists(LockFile));
    }

    /// <summary>A word nobody answers used to fall through to the loop too. Now it is the usage, and says so.</summary>
    [Fact]
    public async Task A_word_nobody_answers_is_the_usage_and_starts_nothing()
    {
        await using var service = CountingService.Start();

        var (code, said) = await RunAsync(service, "watch");

        Assert.Equal(2, code);
        Assert.Contains("no verb `watch`", said);
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

        var (_, said) = await RunAsync(service, "--once");

        Assert.True(service.Requests > 0, said);
        Assert.False(File.Exists(LockFile), "the loop kept the home after it ended");
    }

    private void Plant(string kind, int pid, long started) =>
        File.WriteAllText(LockFile, new JsonObject
        {
            ["kind"] = kind, ["pid"] = pid, ["started"] = started, ["since"] = "2026-09-30T08:02:13+00:00",
        }.ToJsonString());

    private static long OurStart()
    {
        using var self = Process.GetCurrentProcess();
        return self.StartTime.ToUniversalTime().Ticks;
    }

    /// <summary>The host as built, beside this test's own build: its exit code, and everything it said.</summary>
    private async Task<(int Code, string Said)> RunAsync(CountingService service, params string[] args)
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
