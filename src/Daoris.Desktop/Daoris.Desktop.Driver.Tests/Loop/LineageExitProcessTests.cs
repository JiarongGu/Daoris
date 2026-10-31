using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COWORKVIEW1a rule 4 over real stand-in processes: no <c>end</c> is written before its process's exit is read. A refused run's is
/// written once its kill's exit is read and before its capture's wait returns; a chat's, once its exit is read within its bound and
/// its capture waited for; and a chat process whose exit is not read within the bound gets none, its token leaving at the exit the
/// set sees.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class LineageExitProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-lineage-exit-process-" + Guid.NewGuid().ToString("N")[..8]);

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

        var abandoned = Daoris.Driver.Driver.AbandonedAsync(
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

    [Fact]
    public async Task A_chats_end_after_its_catchs_kill_waits_for_the_exit_then_its_capture_and_is_written_after_both()
    {
        var process = StandIn();
        var proc = ChildLineage.Opened(_home, "c1");
        _tokens.Hand(proc, process);
        var capture = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SessionProcesses.EndIfRunning(process);

        var ending = ChatRunner.EndedAsync(_home, "c1", process, capture.Task, proc, stopped: () => false, within: Poll.Patience);

        await Poll.Until(() => _tokens.Reading(proc) == TokenReading.Gone, () => $"the token {proc} is still held", Poll.Patience);
        Assert.Single(Lines("c1"));
        Assert.False(ending.IsCompleted);
        capture.SetResult();
        await ending.WaitAsync(Poll.Patience);
        var end = Lines("c1")[^1];
        Assert.Equal(("end", proc, false), (end.GetProperty("by").GetString(), end.GetProperty("proc").GetString(), end.GetProperty("clean").GetBoolean()));
    }

    [Fact]
    public async Task A_chat_process_whose_exit_is_not_read_within_its_bound_gets_no_end_and_its_token_leaves_at_its_exit()
    {
        var process = StandIn();
        var proc = ChildLineage.Opened(_home, "c1");
        _tokens.Hand(proc, process);

        await ChatRunner.EndedAsync(_home, "c1", process, drain: null, proc, stopped: () => false, within: TimeSpan.Zero).WaitAsync(Poll.Patience);

        Assert.Single(Lines("c1"));
        Assert.Equal(TokenReading.Watched, _tokens.Reading(proc));
        SessionProcesses.EndIfRunning(process);
        await Poll.Until(() => _tokens.Reading(proc) == TokenReading.Gone, () => $"the token {proc} is still held", Poll.Patience);
        Assert.Single(Lines("c1"));
    }

    /// <summary>A stand-in agent that runs until it is ended.</summary>
    private Process StandIn()
    {
        var info = new ProcessStartInfo("node") { UseShellExecute = false, RedirectStandardOutput = true };
        info.ArgumentList.Add("-e");
        info.ArgumentList.Add("setInterval(() => {}, 1000)");
        var process = Process.Start(info)!;
        _started.Add(process);
        return process;
    }

    private List<JsonElement> Lines(string session = "s1") =>
        [.. File.ReadAllLines(ChildLineage.PathOf(_home, session)).Select(line => JsonDocument.Parse(line).RootElement)];
}
