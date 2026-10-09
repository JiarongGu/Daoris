using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HOSTSTART2: the headless driver left nothing of why in its machine log when its start threw past its one catch, as
/// the HTTP host did before HOSTSTART1. Its entry point is async and holds its log in a <c>using</c>, so the log was closed
/// as the exception left the entry point, before the runtime raised it as unhandled, and the <c>error</c> line written for
/// an unhandled exception was dropped on a closed log. The host as built in a process of its own, since only a real
/// process raises an exception as unhandled, with nothing of this machine's Daoris in its environment. CONFIGREAD1 then
/// brought the start this found inside the one catch, so it is a sentence and exit 2; the watch on what still leaves the
/// entry point is held by <c>MachineLogTests</c>' rows.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class DriverStartFailureTests : IDisposable
{
    /// <summary>A key in the driver's environment, which nothing a failed start says may repeat.</summary>
    private const string Key = "hoststart2-a-key-nobody-may-read";

    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-hoststart2-" + Guid.NewGuid().ToString("N")[..8]);

    public DriverStartFailureTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        for (var attempt = 0; attempt < 20 && Directory.Exists(_home); attempt++)
        {
            try
            {
                Directory.Delete(_home, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
    }

    /// <summary>
    /// A <c>driver.json</c> that does not read ends the loop's start before its first look, as a sentence naming the file
    /// and where in it, and exit 2 (CONFIGREAD1, REV3): the parse threw past the host's catch, and the person read the
    /// parser's stack trace. The host's one catch writes it to its log, and the service's key in its environment is said
    /// nowhere.
    /// </summary>
    [Fact]
    public async Task A_loop_whose_choices_do_not_read_says_which_file_and_where_and_writes_it_to_its_log()
    {
        var path = Path.Combine(_home, "driver.json");
        File.WriteAllText(path, """{ "drivable": [""");

        var (code, said) = await RunAsync("drive", "--once");

        Assert.Equal(2, code);
        Assert.StartsWith($"driver: {path} is not readable JSON at line 1, byte 16 (", said);
        Assert.EndsWith("). Fix it, or delete it to start from nothing.", said.TrimEnd());
        Assert.DoesNotContain("System.Text.Json", said, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", said, StringComparison.Ordinal);

        var logged = LogLines();
        var error = Assert.Single(logged, line => line.GetProperty("event").GetString() == "error");
        Assert.Equal("error", error.GetProperty("level").GetString());
        Assert.Equal("driver", error.GetProperty("source").GetString());
        var data = error.GetProperty("data");
        Assert.Equal("the headless driver", data.GetProperty("where").GetString());
        Assert.Equal(typeof(DriverConfigUnreadableException).FullName, data.GetProperty("type").GetString());
        Assert.False(data.GetProperty("terminating").GetBoolean());
        Assert.Equal(said.TrimEnd()["driver: ".Length..], data.GetProperty("message").GetString());

        Assert.DoesNotContain(Key, said, StringComparison.Ordinal);
        Assert.All(logged, line => Assert.DoesNotContain(Key, line.GetRawText(), StringComparison.Ordinal));
    }

    /// <summary>The host as built, beside this test's own build: its exit code, and everything it said.</summary>
    private async Task<(int Code, string Said)> RunAsync(params string[] args)
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
        info.ArgumentList.Add(DriverHostTests.HostDll());
        foreach (var arg in args) info.ArgumentList.Add(arg);

        foreach (var name in info.Environment.Keys.Where(key => key.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            info.Environment.Remove(name);
        }

        info.Environment[DaorisHome.Variable] = _home;
        // Nothing listens there, and the start ends before anything would ask.
        info.Environment[ServiceClient.UrlVariable] = "http://127.0.0.1:9";
        info.Environment[ServiceClient.KeyVariable] = Key;

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

    /// <summary>Every line the driver's machine log holds, across its files.</summary>
    private IReadOnlyList<JsonElement> LogLines()
    {
        var folder = Path.Combine(_home, MachineLog.Folder);
        if (!Directory.Exists(folder)) return [];
        return Directory.EnumerateFiles(folder, "*.driver.jsonl")
            .SelectMany(path => File.ReadAllText(path).Split('\n', StringSplitOptions.RemoveEmptyEntries))
            .Select(line => JsonDocument.Parse(line).RootElement)
            .ToList();
    }
}
