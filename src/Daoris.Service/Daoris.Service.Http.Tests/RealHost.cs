using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Daoris.Knowledge;
using Daoris.Knowledge.Http;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// The host's own build, beside these tests', started in a process of its own as the shell starts it: input redirected,
/// on a free loopback port, over a machine of its own under <paramref name="Scratch"/>, with every variable it reads set
/// or cleared. What the in-process host cannot show is shown here: the real standard input (LOG2a, PERSONDOOR1a) and a
/// clean stop.
/// </summary>
public sealed class RealHost(Process process, string url, string scratch) : IDisposable
{
    public Process Process { get; } = process;

    public string Url { get; } = url;

    public string Scratch { get; } = scratch;

    public string Home => HomeOf(Scratch);

    private static string HomeOf(string scratch) => Path.Combine(scratch, "home");

    /// <summary>The id of the home this host was started over, minted before it starts: what it answers at its status.</summary>
    public string HomeId => HomeIdentity.Read(Home, out _)!;

    /// <summary>
    /// Start the host and wait until it answers as itself. <paramref name="starter"/> writes on its input first, as the shell
    /// writes the person key before the host can answer anyone (PERSONDOOR1a).
    /// </summary>
    /// <remarks>
    /// REALHOSTPORT1: the port is free when it is drawn, not when the host binds it, so whatever answers there may be another
    /// host that took it between. Only an answer naming this host's own home, minted under its scratch before it started, is
    /// taken for it; an answer naming another is said if the host then exits or never answers.
    /// </remarks>
    public static async Task<RealHost> StartAsync(
        string scratch, IReadOnlyDictionary<string, string?> environment, Action<StreamWriter>? starter = null)
    {
        var host = Launch(scratch, environment);
        if (starter is not null)
        {
            starter(host.Process.StandardInput);
            host.Process.StandardInput.Flush();
        }

        var own = host.HomeId;
        string? other = null;
        for (var attempt = 0; attempt < Poll.Patience.TotalMilliseconds / 200; attempt++)
        {
            var home = await HomeAnsweredAsync(host.Url);
            if (home == own) return host;
            if (home is not null) other = home;
            if (host.Process.HasExited)
            {
                var exit = host.Process.ExitCode;
                host.Dispose();
                throw new InvalidOperationException($"the host exited ({exit}) before it answered{Elsewhere(other)}");
            }

            await Task.Delay(200);
        }

        host.Dispose();
        throw new TimeoutException($"the host never answered at {host.Url}{Elsewhere(other)}");
    }

    private static string Elsewhere(string? other) =>
        other is null ? "" : $"; another host answered at its port, over home {(other.Length == 0 ? "(none)" : other)}";

    /// <summary>
    /// Start the host and return at once, without waiting for it to answer: for a host that is not to answer (HOSTSTART1).
    /// Its standard error is kept, in <see cref="Said"/>, and its output drained.
    /// </summary>
    public static RealHost Launch(string scratch, IReadOnlyDictionary<string, string?> environment)
    {
        var repositories = Path.Combine(scratch, "repositories");
        Directory.CreateDirectory(HomeOf(scratch));
        Directory.CreateDirectory(repositories);
        // Minted here, before the host starts, so its status names a home only this host was started over (REALHOSTPORT1).
        if (HomeIdentity.Ensure(HomeOf(scratch), out var unread) is null) throw new InvalidOperationException(unread);
        // An address the test names is the host's; otherwise a port free as it is drawn.
        var url = environment.GetValueOrDefault("ASPNETCORE_URLS") ?? $"http://127.0.0.1:{FreePort()}";

        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = scratch,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "daoris-knowledge-http.dll"));
        var variables = new Dictionary<string, string?>
        {
            [InputEndStop.Variable] = null,
            [PersonKey.InputVariable] = null,
            [DaorisHome.Variable] = HomeOf(scratch),
            [ServiceOptions.DatabaseVariable] = Path.Combine(scratch, "index", "knowledge.db"),
            [ServiceOptions.RootVariable] = repositories,
            [ServiceOptions.ModelVariable] = null,
            [ServiceOptions.UrlVariable] = null,
            [Access.ModeVariable] = null,
            [Access.WorkspaceVariable] = null,
            [RemoteConfig.UrlVariable] = null,
            [RemoteConfig.KeyVariable] = null,
            [RemoteConfig.WorkspaceVariable] = null,
            [RemoteConfig.PathVariable] = null,
            [RuleProposalBox.HomeVariable] = null,
            [IntakeScope.AskVariable] = null,
            [IntakeScope.SessionVariable] = null,
            ["DAORIS_WEB_ORIGIN"] = null,
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["ASPNETCORE_URLS"] = url,
            // The host's own lifetime says when its stop began and ended ("Hosting stopping", "Hosting stopped"), so a stop
            // that misses its bound names the step it sat in (FLAKE2).
            ["Logging__LogLevel__Microsoft.Extensions.Hosting"] = "Debug",
        };
        foreach (var (name, value) in environment) variables[name] = value;
        foreach (var (name, value) in variables)
        {
            if (value is null) start.Environment.Remove(name);
            else start.Environment[name] = value;
        }

        var host = new RealHost(Process.Start(start)!, url, scratch);
        // Drained, so a host that writes more than a pipe holds is never blocked on its own output.
        host.Process.OutputDataReceived += (_, line) =>
        {
            if (line.Data is not null) host.Hear("out", line.Data);
        };
        host.Process.ErrorDataReceived += (_, line) =>
        {
            if (line.Data is null) return;
            lock (host._said) host._said.AppendLine(line.Data);
            host.Hear("err", line.Data);
        };
        host.Process.BeginOutputReadLine();
        host.Process.BeginErrorReadLine();
        return host;
    }

    private readonly System.Text.StringBuilder _said = new();

    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private readonly Queue<string> _heard = new();

    /// <summary>A line the host printed, kept with when it arrived; the last <c>200</c> are kept.</summary>
    private void Hear(string stream, string line)
    {
        lock (_heard)
        {
            _heard.Enqueue($"{_clock.ElapsedMilliseconds,7} ms {stream} {line}");
            if (_heard.Count > 200) _heard.Dequeue();
        }
    }

    /// <summary>
    /// Stop the host as the shell does (LOG2a), by closing its input, and wait <paramref name="within"/> for it to exit. Null
    /// when it did; otherwise what a reader of the failure needs to tell where it sat (FLAKE2): whether its lifetime began
    /// to stop (the log's <c>app.stopped</c>), and the last lines it printed, its own lifetime's among them, with their times.
    /// </summary>
    public string? StopByInput(TimeSpan within)
    {
        Hear("test", "input closed");
        Process.StandardInput.Close();
        if (Process.WaitForExit(within)) return null;

        var stopping = LogLines().Any(line => line.Contains("\"event\":\"app.stopped\"", StringComparison.Ordinal));
        string[] heard;
        lock (_heard) heard = [.. _heard];
        return $"the host did not stop within {within.TotalSeconds:0} s of its input closing; its log "
            + (stopping ? "has app.stopped, so its lifetime began to stop" : "has no app.stopped, so its stop was never asked")
            + ". What it printed last:\n" + string.Join('\n', heard.TakeLast(40));
    }

    /// <summary>What the host printed on its standard error so far.</summary>
    public string Said
    {
        get
        {
            lock (_said) return _said.ToString();
        }
    }

    /// <summary>Every line the host's machine log holds, across its files.</summary>
    public IReadOnlyList<string> LogLines()
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return Directory.EnumerateFiles(folder, "*.host.jsonl")
            .SelectMany(path =>
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            })
            .ToList();
    }

    public static async Task<bool> AnswersAsync(string url) => await HomeAnsweredAsync(url) is not null;

    /// <summary>
    /// The home a host at <paramref name="url"/> names in its status: null where nothing answers it, empty where an answer
    /// names none.
    /// </summary>
    private static async Task<string?> HomeAnsweredAsync(string url)
    {
        using var client = new HttpClient { Timeout = Poll.Patience };
        try
        {
            using var response = await client.GetAsync($"{url}/api/status");
            if (response.StatusCode != HttpStatusCode.OK) return null;
            using var status = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return status.RootElement.ValueKind == JsonValueKind.Object
                && status.RootElement.TryGetProperty("home", out var home) && home.ValueKind == JsonValueKind.String
                ? home.GetString()!
                : "";
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
        catch (JsonException)
        {
            return "";
        }
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    /// <summary>Killed with its tree if a test leaves it running.</summary>
    public void Dispose()
    {
        try
        {
            if (!Process.HasExited) Process.Kill(entireProcessTree: true);
            Process.WaitForExit(5_000);
        }
        catch (InvalidOperationException)
        {
            // Already reaped.
        }

        Process.Dispose();
    }
}
