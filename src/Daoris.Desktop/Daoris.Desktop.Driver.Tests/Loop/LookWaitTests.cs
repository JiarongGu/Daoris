using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LOOKWAIT1 (D181 point 6) on the watch: each look's <c>watch.looked</c> carries its stages, each start's preparation among them
/// with its quest, and a stage that hangs is named in the machine log while it hangs, so the next slow look on an installed machine
/// names its part.
/// </summary>
/// <remarks>Over the real client and planner with the service's doors and each start's run standing in, in-process.</remarks>
public sealed class LookWaitTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-lookwait-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(Poll.Patience);

    public LookWaitTests()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Path.Combine(_home, "engine"));
    }

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task Each_look_s_line_carries_its_stages_a_start_s_preparation_named_by_its_quest()
    {
        _ledger.Publish("q1", "engine");
        using (var log = new MachineLog(_home, "driver"))
        {
            var runs = new StandInRuns(_ledger.Client(), _ledger);
            // A start's preparation that takes a moment, as git's does, so its stage is past the line's floor.
            var watch = Watch(log, runs, async (start, opened, token) =>
            {
                await Task.Delay(80, token);
                return await runs.Runner(start, opened, token);
            });

            var reports = new List<TickReport>();
            var watching = watch.RunAsync(Into(reports), onError: null, _closing.Token);
            try
            {
                await Poll.Until(() => runs.Started.Count == 1, () => "q1 never started", Bound);
                await Poll.Until(() => Count(reports) >= 1, () => "no look reported", Bound);
            }
            finally
            {
                await _closing.CancelAsync();
                await Settled(watching);
            }
        }

        var stages = Lines("watch.looked").Select(data => data.GetProperty("stages").GetString()).OfType<string>().ToList();
        Assert.Contains(stages, line => line.Split(' ').Any(entry => entry.StartsWith("start=", StringComparison.Ordinal) && entry.EndsWith("@#q1", StringComparison.Ordinal)));
    }

    /// <summary>
    /// 🔴 LOOKWAIT1b (D181 point 6): a start whose git waited for its place says that wait as its stage's queue, apart from its
    /// preparation, so a look slow for want of a place is told apart from slow work. The places are a gate of the test's own, held
    /// full as a fan-out holds git's: the shared gate cannot be held full here, and any gate's wait is said the same way.
    /// </summary>
    [Fact]
    public async Task A_start_whose_git_waits_for_a_place_says_the_wait_as_its_queue()
    {
        _ledger.Publish("q1", "engine");
        using var held = new ManualResetEventSlim();
        var places = new ProcessStarts(1, info =>
        {
            if (info.FileName == "holder") held.Wait(Bound);
            return null;
        });
        var holder = places.StartAsync(new ProcessStartInfo("holder") { UseShellExecute = false }, CancellationToken.None);
        using (var log = new MachineLog(_home, "driver"))
        {
            var runs = new StandInRuns(_ledger.Client(), _ledger);
            var watch = Watch(log, runs, async (start, opened, token) =>
            {
                var git = places.StartAsync(new ProcessStartInfo("git") { UseShellExecute = false }, StartClass.Background, token);
                await Task.Delay(150, token);
                held.Set();
                await git;
                return await runs.Runner(start, opened, token);
            });

            var reports = new List<TickReport>();
            var watching = watch.RunAsync(Into(reports), onError: null, _closing.Token);
            try
            {
                await Poll.Until(() => runs.Started.Count == 1, () => "q1 never started", Bound);
                await Poll.Until(() => Count(reports) >= 1, () => "no look reported", Bound);
            }
            finally
            {
                held.Set();
                await _closing.CancelAsync();
                await Settled(watching);
            }
        }

        await holder.WaitAsync(Bound);
        var stages = Lines("watch.looked").Select(data => data.GetProperty("stages").GetString()).OfType<string>().ToList();
        Assert.True(
            stages.Any(line => line.Split(' ').Any(entry => entry.TrimStart('~').StartsWith("start.queue=", StringComparison.Ordinal))),
            string.Join(" | ", stages));
    }

    /// <summary>🔴 A start that hangs is named while it hangs: its stage and its quest, before anything ends it.</summary>
    [Fact]
    public async Task A_start_that_hangs_is_named_in_the_log_while_it_hangs()
    {
        _ledger.Publish("q1", "engine");
        var hung = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (var log = new MachineLog(_home, "driver"))
        {
            var runs = new StandInRuns(_ledger.Client(), _ledger);
            var watch = Watch(log, runs, async (start, opened, token) =>
            {
                await hung.Task.WaitAsync(token);
                return await runs.Runner(start, opened, token);
            }, new LookStages(log, TimeSpan.FromMilliseconds(100)));

            var watching = watch.RunAsync((_, _) => Task.CompletedTask, onError: null, _closing.Token);
            try
            {
                // The start's line: under load another stage may pass a tenth of a second too, and be named as well.
                await Poll.Until(
                    () => Lines("look.slow").Any(data => data.GetProperty("stage").GetString() == "start"), () => "the hung start never named", Bound);
            }
            finally
            {
                hung.TrySetResult();
                await _closing.CancelAsync();
                await Settled(watching);
            }
        }

        var slow = Lines("look.slow").Where(data => data.GetProperty("stage").GetString() == "start").ToList();
        Assert.Equal("#q1", Assert.Single(slow).GetProperty("subject").GetString());
    }

    private DriverWatch Watch(
        MachineLog log, StandInRuns runs, Func<Consideration, Action, CancellationToken, Task<StartRun>> runner, LookStages? stages = null)
    {
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        File.WriteAllText(config, """
            { "drivable": ["engine"], "adapter": "stub", "cap": 3, "pollSeconds": 60, "trees": ["engine"] }
            """);
        var watch = new DriverWatch(
            _ledger.Client(), config, _home, new SessionProcesses(), sync: null,
            harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            Runner = runner,
            Stops = runs.Stops,
            Log = log,
            Stages = stages ?? new LookStages(log),
        };
        runs.Keeping = watch.Running;
        return watch;
    }

    private static Func<TickReport, DriverConfig, Task> Into(List<TickReport> reports) => (report, _) =>
    {
        lock (reports) reports.Add(report);
        return Task.CompletedTask;
    };

    private static int Count(List<TickReport> reports)
    {
        lock (reports) return reports.Count;
    }

    private static async Task Settled(Task running)
    {
        try { await running.WaitAsync(Bound); }
        catch (OperationCanceledException) { }
    }

    private JsonElement[] Lines(string name)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return [.. Directory.EnumerateFiles(folder, "*.jsonl")
            .SelectMany(file =>
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            })
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .Where(line => line.GetProperty("event").GetString() == name)
            .Select(line => line.GetProperty("data"))];
    }
}
