using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PARALLEL1g (D181 point 3): the owed opinions, the asks for a helper and the due landings are each judged as their topic's judge beside
/// the look, which plans over the facts as last judged. A slow judgment holds its own subject alone: another chain's quest starts; a
/// chain whose opinion is owed and not yet judged is held, never started early, and goes on once the judgment lets it; the landing's
/// wakes are spent once.
/// </summary>
/// <remarks>
/// Over the real client and planner with the service's doors and each start's run standing in, in-process, as
/// <see cref="LookParallelTests"/> drives the watch. Each judgment is held on a door only it asks, so no git runs: an owed opinion's on
/// every quest's list, which its chain is read from, a helper's delivery on the helpers' door, a landing's selection on every quest's list.
/// </remarks>
public sealed class LookJudgesTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(20);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-judges-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(Poll.Patience);

    public LookJudgesTests()
    {
        Directory.CreateDirectory(_home);
        // The checkouts sit under a folder of their own: `tools` beside the home would read as its entry (HOMELAYOUT2's census).
        _ledger.Register("engine", Path.Combine(_home, "repos", "engine")).Register("tools", Path.Combine(_home, "repos", "tools"));
    }

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>🔴 An owed opinion's judgment that does not answer holds no start of another chain: a quest of its own starts meanwhile.</summary>
    [Fact]
    public async Task A_slow_opinion_judgment_holds_no_start_of_another_chain()
    {
        Owed();
        var judging = Hold(path => path == "/api/quests", closed: true);
        var (watch, runs) = Watch();

        var watching = Task.Run(() => watch.RunAsync((_, _) => Task.CompletedTask, onError: null, _closing.Token));
        try
        {
            await judging.Asked.Task.WaitAsync(Bound);

            _ledger.Publish("q3", "tools");
            watch.Nudge(ChangeTopics.Ledger, "quests");
            await Poll.Until(() => runs.Started.Contains("q3"), () => "q3 waited for q1's opinion to be judged", Bound);

            Assert.DoesNotContain("q2", runs.Started);
        }
        finally
        {
            judging.Release();
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    /// <summary>
    /// 🔴 A chain whose opinion is owed and not yet judged does not start its next step, however many looks meet it: each holds it as
    /// waiting for its opinion, not judged yet. Once the judgment lands (here it closes the opinion: its tree is gone), the step starts.
    /// </summary>
    [Fact]
    public async Task A_chain_whose_opinion_is_not_yet_judged_does_not_start_its_next_step()
    {
        Owed();
        var judging = Hold(path => path == "/api/quests", closed: true);
        var (watch, runs) = Watch();
        var reports = new List<TickReport>();

        var watching = Task.Run(() => watch.RunAsync(Into(reports), onError: null, _closing.Token));
        try
        {
            await judging.Asked.Task.WaitAsync(Bound);
            for (var wake = 0; wake < 3; wake++)
            {
                var seen = Count(reports);
                watch.Nudge(ChangeTopics.Ledger, "quests");
                await Poll.Until(() => Count(reports) > seen, () => $"wake {wake} never looked: q1's opinion held the look", Bound);
            }

            var held = Snapshot(reports)[^1].Considerations.Single(c => c.Quest.Id == "q2");
            Assert.Equal(StartVerdict.WaitsForOpinion, held.Verdict);
            Assert.Contains("not judged yet", held.Reason);
            Assert.DoesNotContain("q2", runs.Started);

            judging.Release();
            await Poll.Until(() => runs.Started.Contains("q2"), () => "q2 never started once q1's opinion was judged", Bound);
            Assert.Empty(new OpinionDues(_home).Open());
        }
        finally
        {
            judging.Release();
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    /// <summary>🔴 An ask for a helper whose answer's handing does not answer holds no other start: a quest starts meanwhile.</summary>
    [Fact]
    public async Task A_slow_helper_judgment_holds_no_other_start()
    {
        // A conversation asked a helper, which answered; the answer waits to be handed, and the helpers' door does not answer.
        _ledger.Working("s-asker", "engine", Path.Combine(_home, "trees", "s-asker"));
        var files = new HelperFiles(_home);
        Directory.CreateDirectory(Path.GetDirectoryName(files.PathOf("h1", "ask"))!);
        File.WriteAllText(files.PathOf("h1", "ask"), new JsonObject
        {
            ["id"] = "h1", ["asker"] = "s-asker", ["question"] = "Which test covers the look?", ["askedBy"] = HelperAsk.ByAgent,
            ["at"] = "2026-10-19T10:00:00Z",
        }.ToJsonString());
        files.WriteState(new HelperState("h1", HelperStates.Answered, null, "s-helper", DateTimeOffset.UtcNow) { Agent = "codex-acp" });
        var handing = Hold(path => path.StartsWith("/api/helpers", StringComparison.Ordinal), closed: false);
        var (watch, runs) = Watch();

        var watching = Task.Run(() => watch.RunAsync((_, _) => Task.CompletedTask, onError: null, _closing.Token));
        try
        {
            await handing.Asked.Task.WaitAsync(Bound);

            _ledger.Publish("q3", "tools");
            watch.Nudge(ChangeTopics.Ledger, "quests");
            await Poll.Until(() => runs.Started.Contains("q3"), () => "q3 waited for the helper's answer to be handed", Bound);
        }
        finally
        {
            handing.Release();
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    /// <summary>
    /// 🔴 A landing's selection that does not answer holds no later start, and the wakes it judged with are spent once: what it decides
    /// is said once, and no wake is left to judge again.
    /// </summary>
    [Fact]
    public async Task A_slow_landing_selection_holds_no_start_and_spends_its_wakes_once()
    {
        // The home's lease, as the loop that lands holds it (FABRIC-END7's review); a due entry whose rule no longer accepts by itself.
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Headless, out _);
        new AutoLandings(_home).Due(new AutoLanding(
            "s8", "q8", "engine", "default", DaorisHome.Entry(_home, HomeEntry.Trees, "default", "engine", "s-8"), DateTimeOffset.UtcNow));
        var selecting = Hold(path => path == "/api/quests", closed: true);
        var (watch, runs) = Watch(opinions: false);
        var reports = new List<TickReport>();

        var watching = Task.Run(() => watch.RunAsync(Into(reports), onError: null, _closing.Token));
        try
        {
            await selecting.Asked.Task.WaitAsync(Bound);

            _ledger.Publish("q3", "tools");
            watch.Nudge(ChangeTopics.Ledger, "quests");
            await Poll.Until(() => runs.Started.Contains("q3"), () => "q3 waited for the landing's selection", Bound);

            selecting.Release();
            await Poll.Until(() => new AutoLandings(_home).Open().Count == 0, () => "the landing never closed s8", Bound);
            for (var wake = 0; wake < 2; wake++)
            {
                var seen = Count(reports);
                watch.Nudge(ChangeTopics.Ledger, "quests");
                await Poll.Until(() => Count(reports) > seen, () => $"wake {wake} never looked", Bound);
            }

            Assert.Single(Snapshot(reports).SelectMany(report => report.Events),
                line => line.StartsWith("landing  session s8 (#q8 → engine): its repository's rule no longer accepts automatically", StringComparison.Ordinal));
            var left = watch.Landing.Take(whole: false);
            Assert.False(left.Whole);
        }
        finally
        {
            selecting.Release();
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    /// <summary>
    /// A done quest `q1` on `engine` owed a second opinion under `steps`, its tree gone, and its chain's next step `q2` open; the rule
    /// reads `engine` at `steps`, as the watch's config says.
    /// </summary>
    private void Owed()
    {
        _ledger.Publish("q1", "engine");
        _ledger.Move("q1", "Done");
        _ledger.Publish("q2", "engine", parent: "q1");
        new OpinionDues(_home).Due(new OpinionDue(
            "s1", "q1", "engine", "default", DaorisHome.Entry(_home, HomeEntry.Trees, "default", "engine", "s-1"), OpinionRules.Steps,
            DateTimeOffset.UtcNow));
    }

    /// <summary>A door held for the test: <see cref="Held.Asked"/> once it is first asked, answered once released.</summary>
    private Held Hold(Func<string, bool> door, bool closed)
    {
        var held = new Held();
        _ledger.Before = async (request, token) =>
        {
            if (!door(request.RequestUri!.AbsolutePath)) return;
            if (closed != request.RequestUri.Query.Contains("includeClosed=true", StringComparison.Ordinal)) return;
            held.Asked.TrySetResult();
            await held.Released.Task.WaitAsync(token);
        };
        return held;
    }

    private sealed class Held
    {
        public TaskCompletionSource Asked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => Released.TrySetResult();
    }

    private (DriverWatch Watch, StandInRuns Runs) Watch(bool opinions = true)
    {
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        (DriverConfig.Empty with
        {
            Drivable = ["engine", "tools"], Adapter = "stub", Cap = 3, PollSeconds = 60,
            Opinions = opinions
                ? new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase) { ["engine"] = new(["codex-acp"], [OpinionRules.Steps], false) }
                : DriverConfig.Empty.Opinions,
        }).WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}")).Save(config);
        var service = _ledger.Client();
        var runs = new StandInRuns(service, _ledger);
        var watch = new DriverWatch(
            service, config, _home, new SessionProcesses(), sync: null,
            harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            Runner = runs.Runner,
            Stops = runs.Stops,
        };
        runs.Keeping = watch.Running;
        return (watch, runs);
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

    private static List<TickReport> Snapshot(List<TickReport> reports)
    {
        lock (reports) return [.. reports];
    }

    private static async Task Settled(Task running)
    {
        try { await running.WaitAsync(Bound); }
        catch (OperationCanceledException) { }
        catch (TimeoutException) { }
    }
}
