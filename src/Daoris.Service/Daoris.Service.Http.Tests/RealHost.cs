using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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

    /// <summary>
    /// Start the host and wait until it answers. <paramref name="starter"/> writes on its input first, as the shell writes
    /// the person key before the host can answer anyone (PERSONDOOR1a).
    /// </summary>
    public static async Task<RealHost> StartAsync(
        string scratch, IReadOnlyDictionary<string, string?> environment, Action<StreamWriter>? starter = null)
    {
        var repositories = Path.Combine(scratch, "repositories");
        Directory.CreateDirectory(HomeOf(scratch));
        Directory.CreateDirectory(repositories);
        var url = $"http://127.0.0.1:{FreePort()}";

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
        };
        foreach (var (name, value) in environment) variables[name] = value;
        foreach (var (name, value) in variables)
        {
            if (value is null) start.Environment.Remove(name);
            else start.Environment[name] = value;
        }

        var host = new RealHost(Process.Start(start)!, url, scratch);
        // Drained, so a host that writes more than a pipe holds is never blocked on its own output.
        host.Process.OutputDataReceived += (_, _) => { };
        host.Process.ErrorDataReceived += (_, _) => { };
        host.Process.BeginOutputReadLine();
        host.Process.BeginErrorReadLine();
        if (starter is not null)
        {
            starter(host.Process.StandardInput);
            host.Process.StandardInput.Flush();
        }

        for (var attempt = 0; attempt < 150; attempt++)
        {
            if (await AnswersAsync(url)) return host;
            if (host.Process.HasExited)
            {
                var exit = host.Process.ExitCode;
                host.Dispose();
                throw new InvalidOperationException($"the host exited ({exit}) before it answered");
            }

            await Task.Delay(200);
        }

        host.Dispose();
        throw new TimeoutException($"the host never answered at {url}");
    }

    /// <summary>Every line the host's machine log holds, across its files.</summary>
    public IReadOnlyList<string> LogLines()
    {
        var folder = Path.Combine(Home, MachineLog.Folder);
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

    public static async Task<bool> AnswersAsync(string url)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        try
        {
            using var response = await client.GetAsync($"{url}/api/status");
            return response.StatusCode == HttpStatusCode.OK;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            return false;
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
