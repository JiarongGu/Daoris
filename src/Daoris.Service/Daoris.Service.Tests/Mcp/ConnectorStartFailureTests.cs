using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// HOSTSTART2: the connector a session's harness starts left nothing of why in its machine log when its start threw, as
/// the HTTP host did before HOSTSTART1. Its entry point is async and holds its log in a <c>using</c>, so the log was closed
/// as the exception left the entry point, before the runtime raised it as unhandled, and the <c>error</c> line written for
/// an unhandled exception was dropped on a closed log. The real executable in a process of its own, since only a real
/// process raises an exception as unhandled.
/// </summary>
public sealed class ConnectorStartFailureTests : IDisposable
{
    /// <summary>A key in the connector's environment, which nothing a failed start says may repeat.</summary>
    private const string Key = "hoststart2-a-key-nobody-may-read";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-hoststart2-" + Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "home");

    public void Dispose()
    {
        for (var attempt = 0; attempt < 20 && Directory.Exists(_root); attempt++)
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
    }

    /// <summary>
    /// A store that cannot be opened ends the start before the connector serves: the index's folder is named under a
    /// file. Its standard output is the protocol's, so the failure is said on standard error and in the log, and the
    /// protocol's stream carries nothing; the remote's key in its environment is said nowhere.
    /// </summary>
    [Fact]
    public async Task A_connector_whose_store_cannot_open_writes_why_to_its_log_and_nothing_to_the_protocol()
    {
        Directory.CreateDirectory(Home);
        var repositories = Path.Combine(_root, "repositories");
        Directory.CreateDirectory(repositories);
        var file = Path.Combine(_root, "a-file");
        File.WriteAllText(file, "not a folder");

        var (code, printed, said) = await RunAsync(new Dictionary<string, string>
        {
            [DaorisHome.Variable] = Home,
            [ServiceOptions.DatabaseVariable] = Path.Combine(file, "index", "knowledge.db"),
            [ServiceOptions.RootVariable] = repositories,
            [RemoteConfig.UrlVariable] = "http://127.0.0.1:9",
            [RemoteConfig.KeyVariable] = Key,
        });

        Assert.NotEqual(0, code);
        Assert.Equal("", printed);
        Assert.Contains(typeof(IOException).FullName!, said);

        var logged = LogLines();
        var error = Assert.Single(logged, line => line.GetProperty("event").GetString() == "error");
        Assert.Equal("error", error.GetProperty("level").GetString());
        Assert.Equal("mcp", error.GetProperty("source").GetString());
        var data = error.GetProperty("data");
        Assert.Equal("start", data.GetProperty("where").GetString());
        Assert.Equal(typeof(IOException).FullName, data.GetProperty("type").GetString());
        Assert.True(data.GetProperty("terminating").GetBoolean());
        Assert.False(string.IsNullOrEmpty(data.GetProperty("message").GetString()));
        Assert.Contains(typeof(IOException).FullName!, data.GetProperty("stack").GetString());

        Assert.DoesNotContain(Key, said, StringComparison.Ordinal);
        Assert.All(logged, line => Assert.DoesNotContain(Key, line.GetRawText(), StringComparison.Ordinal));
    }

    /// <summary>
    /// The connector as built, beside these tests, started as a harness starts it, with nothing of this machine's Daoris
    /// in its environment: its exit code, its standard output and its standard error.
    /// </summary>
    private async Task<(int Code, string Printed, string Said)> RunAsync(IReadOnlyDictionary<string, string> environment)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "daoris-knowledge.dll"));
        foreach (var name in start.Environment.Keys.Where(key => key.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            start.Environment.Remove(name);
        }

        foreach (var (name, value) in environment) start.Environment[name] = value;

        using var process = Process.Start(start)!;
        // A start that served would wait on its input; closed, it would end, and the exit code would say it.
        process.StandardInput.Close();
        var printed = process.StandardOutput.ReadToEndAsync();
        var said = process.StandardError.ReadToEndAsync();
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await process.WaitForExitAsync(bound.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"the connector did not exit: {await said}");
        }

        return (process.ExitCode, await printed, await said);
    }

    /// <summary>Every line the connector's machine log holds, across its files.</summary>
    private IReadOnlyList<JsonElement> LogLines()
    {
        var folder = Path.Combine(Home, MachineLog.Folder);
        if (!Directory.Exists(folder)) return [];
        return Directory.EnumerateFiles(folder, "*.mcp.jsonl")
            .SelectMany(path => File.ReadAllText(path).Split('\n', StringSplitOptions.RemoveEmptyEntries))
            .Select(line => JsonDocument.Parse(line).RootElement)
            .ToList();
    }
}
