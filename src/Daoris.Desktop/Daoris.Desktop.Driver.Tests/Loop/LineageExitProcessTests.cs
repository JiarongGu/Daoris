using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Over real stand-in processes: no <c>end</c> is written before its process's exit is read. A refused run's is
/// written once its kill's exit is read and before its capture's wait returns, a conversation's among them since its run is every
/// start's.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class LineageExitProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-lineage-exit-process-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly LineageTokens _tokens = new(Environment.ProcessId, DateTime.UtcNow);

    private readonly IDisposable _within;

    private readonly List<Process> _started = [];

    public LineageExitProcessTests()
    {
        Directory.CreateDirectory(_home);
        _within = LineageTokens.Within(_tokens);
    }

    public void Dispose()
    {
        foreach (var process in _started)
        {
            SessionProcesses.EndIfRunning(process);
            process.Dispose();
        }

        _within.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_refused_runs_end_is_written_once_its_killed_process_exit_is_read_and_before_its_capture_wait_returns()
    {
        var process = StandIn();
        var proc = ChildLineage.Opened(_home, "s1");
        _tokens.Hand(proc, process);
        var capture = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool? exitedAtEnd = null;

        var abandoned = SessionWaits.AbandonedAsync(
            process, capture.Task, "s1", log: null, output: null, within: Poll.Patience,
            exitRead: () =>
            {
                exitedAtEnd = process.HasExited;
                ChildLineage.Exited(_home, "s1", proc, clean: false);
            });

        await Poll.Until(() => Lines().Count == 2, () => "the refused run wrote no end", Poll.Patience);
        Assert.True(exitedAtEnd);
        Assert.False(abandoned.IsCompleted);
        var end = Lines()[^1];
        Assert.Equal(("end", proc, false), (end.GetProperty("by").GetString(), end.GetProperty("proc").GetString(), end.GetProperty("clean").GetBoolean()));
        Assert.Equal(TokenReading.Gone, _tokens.Reading(proc));
        capture.SetResult();
        await abandoned.WaitAsync(Poll.Patience);
    }

    /// <summary>A stand-in agent that runs until it is ended.</summary>
    private Process StandIn()
    {
        var info = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        info.ArgumentList.Add("-e");
        info.ArgumentList.Add("setInterval(() => {}, 1000)");
        var process = Process.Start(info)!;
        _started.Add(process);
        return process;
    }

    private List<JsonElement> Lines(string session = "s1") =>
        [.. File.ReadAllLines(ChildLineage.PathOf(_home, session)).Select(line => JsonDocument.Parse(line).RootElement)];
}
