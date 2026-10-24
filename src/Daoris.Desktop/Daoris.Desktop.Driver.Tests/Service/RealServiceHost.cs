using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The service's own HTTP host (<c>daoris-knowledge-http</c>), built by this suite on first use and started in a process of its own, over a home
/// and a store under a scratch folder: what a driver test reaches the real doors through (KEEPSIZE1b2). Every variable of this machine's
/// Daoris is taken off its environment and the host is handed its own, so it never reaches the home of an install running here; this
/// process's environment is read and never set.
/// </summary>
/// <remarks>
/// The port is free when it is drawn and not when the host binds it (REALHOSTPORT1's lesson), so an answer is taken for the host's own only
/// where it names the home minted for it before it started. The host is started without a window, a console or an agent: it is the
/// service, which opens records and keeps no process of the driver's.
/// </remarks>
internal sealed class RealServiceHost : IDisposable
{
    private readonly Process _process;
    private readonly Queue<string> _heard = new();

    private RealServiceHost(Process process, string url, string home)
    {
        _process = process;
        Url = url;
        Home = home;
    }

    public string Url { get; }

    /// <summary>The home the host runs over, under the scratch folder it was given.</summary>
    public string Home { get; }

    private static readonly SemaphoreSlim Building = new(1, 1);
    private static string? _built;

    /// <summary>
    /// This suite's own copy of the host, built on first use and kept for the run (KEEPSIZE1b3). The test project does not reference the
    /// host's project, since the merge tool lets gates of two components run beside each other and two builds of one project fight over
    /// its <c>obj</c> folder; the copy is built with <c>--artifacts-path</c> under this project's own <c>obj</c>, so every project of that
    /// build keeps its <c>bin</c> and <c>obj</c> there and shares nothing with the service gate's. The folder is the same on every run, so a
    /// second run builds incrementally, and the host is started from it and never from the service tree's <c>bin</c>.
    /// </summary>
    internal static async Task<string> HostDllAsync()
    {
        await Building.WaitAsync();
        try
        {
            if (_built is not null && File.Exists(_built)) return _built;

            // This test's build is <desktop>/<tests>/bin/<configuration>/<framework>/.
            var build = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var configuration = build.Parent!.Name;
            var tests = build.Parent.Parent!.Parent!;
            var project = Path.Combine(tests.Parent!.Parent!.FullName, "Daoris.Service", "Daoris.Service.Http", "Daoris.Service.Http.csproj");
            var artifacts = Path.Combine(tests.FullName, "obj", "service-host");
            await BuildAsync(project, configuration, artifacts);

            var bin = Path.Combine(artifacts, "bin");
            var dlls = Directory.Exists(bin) ? Directory.GetFiles(bin, "daoris-knowledge-http.dll", SearchOption.AllDirectories) : [];
            _built = dlls.Length == 1
                ? dlls[0]
                : throw new FileNotFoundException($"the build of the service's host under {artifacts} left {dlls.Length} copies of daoris-knowledge-http.dll, not one");
            return _built;
        }
        finally
        {
            Building.Release();
        }
    }

    /// <summary><c>dotnet build</c> of the host's project into <paramref name="artifacts"/>: no shell, no window, and its last lines in the failure.</summary>
    internal static async Task BuildAsync(string project, string configuration, string artifacts)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in new[]
                 {
                     "build", project, "--configuration", configuration, "--artifacts-path", artifacts,
                     "--nologo", "--verbosity", "minimal", "--disable-build-servers", "-p:UseSharedCompilation=false",
                 })
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)!;
        var tail = new Queue<string>();
        void Keep(string? line)
        {
            if (line is null) return;
            lock (tail)
            {
                tail.Enqueue(line);
                if (tail.Count > 40) tail.Dequeue();
            }
        }

        process.OutputDataReceived += (_, line) => Keep(line.Data);
        process.ErrorDataReceived += (_, line) => Keep(line.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.StandardInput.Close();

        using var limit = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        try
        {
            await process.WaitForExitAsync(limit.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(10_000);
            string said;
            lock (tail) said = string.Join('\n', tail);
            throw new TimeoutException($"building the service's host took over ten minutes; its last lines:\n{said}");
        }

        // The asynchronous readers finish with the streams: wait for them before the lines are read.
        process.WaitForExit();
        if (process.ExitCode == 0) return;
        string last;
        lock (tail) last = string.Join('\n', tail);
        throw new InvalidOperationException($"building the service's host ({project}) exited {process.ExitCode}; its last lines:\n{last}");
    }

    /// <summary>Start the host over <paramref name="scratch"/> and wait until it answers as itself.</summary>
    public static async Task<RealServiceHost> StartAsync(string scratch)
    {
        var home = Path.Combine(scratch, "home");
        var repositories = Path.Combine(scratch, "repositories");
        Directory.CreateDirectory(home);
        Directory.CreateDirectory(repositories);
        var id = HomeIdentity.Ensure(home) ?? throw new InvalidOperationException("the scratch home took no id");
        var url = $"http://127.0.0.1:{FreePort()}";

        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = scratch,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        info.ArgumentList.Add(await HostDllAsync());
        foreach (var name in info.Environment.Keys.Where(key => key.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            info.Environment.Remove(name);
        }

        info.Environment["DAORIS_HOME"] = home;
        info.Environment["DAORIS_KNOWLEDGE_DB"] = Path.Combine(scratch, "index", "knowledge.db");
        info.Environment["DAORIS_KNOWLEDGE_ROOT"] = repositories;
        info.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        info.Environment["ASPNETCORE_URLS"] = url;

        var host = new RealServiceHost(Process.Start(info)!, url, home);
        host._process.OutputDataReceived += (_, line) => host.Hear("out", line.Data);
        host._process.ErrorDataReceived += (_, line) => host.Hear("err", line.Data);
        host._process.BeginOutputReadLine();
        host._process.BeginErrorReadLine();

        var started = Stopwatch.StartNew();
        while (started.Elapsed < TimeSpan.FromSeconds(90))
        {
            var status = await Task.Run(() => RunningService.Status(url));
            if (status?.Home == id) return host;
            if (host._process.HasExited)
            {
                var code = host._process.ExitCode;
                var said = host.Said();
                host.Dispose();
                throw new InvalidOperationException($"the service's host exited ({code}) before it answered:\n{said}");
            }

            await Task.Delay(200);
        }

        var last = host.Said();
        host.Dispose();
        throw new TimeoutException($"the service's host never answered at {url} as the home minted for it:\n{last}");
    }

    private void Hear(string stream, string? line)
    {
        if (line is null) return;
        lock (_heard)
        {
            _heard.Enqueue($"{stream} {line}");
            if (_heard.Count > 100) _heard.Dequeue();
        }
    }

    /// <summary>The last lines the host printed, for a failure that names where it sat.</summary>
    public string Said()
    {
        lock (_heard) return string.Join('\n', _heard);
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    /// <summary>Killed with its tree: the store it holds open is the scratch folder's, which goes next.</summary>
    public void Dispose()
    {
        try
        {
            if (!_process.HasExited) _process.Kill(entireProcessTree: true);
            _process.WaitForExit(10_000);
        }
        catch (InvalidOperationException)
        {
            // Already reaped.
        }

        _process.Dispose();
    }
}
