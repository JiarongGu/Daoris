using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The service's own HTTP host (<c>daoris-knowledge-http</c>), built before these tests and started in a process of its own, over a home
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

    /// <summary>The host's build beside this suite's own (<c>Daoris.Desktop.Driver.Tests</c> builds it first and never references it).</summary>
    internal static string HostDll()
    {
        // This test's build is <desktop>/<tests>/bin/<configuration>/<framework>/; the host's sits the same way under the service tree.
        var build = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var framework = build.Name;
        var configuration = build.Parent!.Name;
        var src = build.Parent.Parent!.Parent!.Parent!.Parent!.FullName;
        var dll = Path.Combine(src, "Daoris.Service", "Daoris.Service.Http", "bin", configuration, framework, "daoris-knowledge-http.dll");
        return File.Exists(dll) ? dll : throw new FileNotFoundException("the service's host is built before these tests; it was not found", dll);
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
        info.ArgumentList.Add(HostDll());
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
