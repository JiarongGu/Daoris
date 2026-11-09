using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run is bound at its first start in its repository, on the machine that
/// drives it, to the workflow chosen then, with what chose it; a later quest of the same run keeps that binding, and a
/// start that opened nothing binds nothing.
/// </summary>
/// <remarks>Over the real client and planner with the service's doors and each start's run standing in, in-process.</remarks>
public sealed class WorkflowBindingLookTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-workflow-look-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(Poll.Patience);

    public WorkflowBindingLookTests()
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

    private static JsonNode Graph() => JsonNode.Parse("""
        {"schemaVersion":2,"entries":["claimed"],"end":"landing",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}}],
         "wires":[{"id":"c>l","kind":"after","from":"claimed","to":"landing"}]}
        """)!;

    [Fact]
    public async Task A_run_is_bound_at_its_first_start_to_the_repository_s_choice_and_the_look_says_so()
    {
        StoredSteps.AddGraph(WorkflowPorts.Store(_home), "merge-after", "Merge after you accept", "2026-10-09T09:00:00Z", Graph());
        _ledger.Publish("q1", "engine");
        var (watch, runs) = Watch(""" "workflows": { "engine": { "default": "merge-after" } }, """);
        var reports = new List<TickReport>();

        var watching = watch.RunAsync((report, _) => { lock (reports) reports.Add(report); return Task.CompletedTask; }, onError: null, _closing.Token);
        try
        {
            await Poll.Until(() => WorkflowRunBindings.Bound(_home, "q1"), () => $"started: {string.Join(", ", runs.Started)}", Poll.Patience);

            var binding = WorkflowRunBindings.Read(_home, "q1")!;
            Assert.Equal(("merge-after", 1, WorkflowLevels.Repository, "engine"), (binding.Workflow, binding.Version, binding.Level, binding.Repository));
            await Poll.Until(
                () => Events(reports).Contains("workflow  #q1 → engine: its run follows `merge-after` v1, `engine`'s default."),
                () => string.Join(" | ", Events(reports)), Poll.Patience);
        }
        finally
        {
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    [Fact]
    public async Task The_ask_s_choice_binds_its_chain_s_run_and_a_later_step_keeps_the_binding()
    {
        _ledger.WorkflowChosen("f00d01", kind: null, workflow: "current");
        _ledger.Publish("q1", "engine", from: "ask #f00d01");
        var (watch, runs) = Watch("");

        var watching = watch.RunAsync((_, _) => Task.CompletedTask, onError: null, _closing.Token);
        try
        {
            await Poll.Until(() => WorkflowRunBindings.Bound(_home, "q1"), () => $"started: {string.Join(", ", runs.Started)}", Poll.Patience);
            var first = File.ReadAllText(WorkflowRunBindings.PathOf(_home, "q1"));
            var binding = WorkflowRunBindings.Read(_home, "q1")!;
            Assert.Equal((WorkflowSelection.Current, WorkflowLevels.Task, "f00d01"), (binding.Workflow, binding.Level, binding.Ask));
            Assert.NotNull(binding.Problem);

            // The chain's next step in the same repository is the same run: a choice changed since reaches new work only.
            runs.End("q1");
            _ledger.WorkflowChosen("f00d01", kind: null, workflow: "merge-after");
            _ledger.Publish("q2", "engine", from: "ask #f00d01", parent: "q1");
            watch.Nudge();
            await Poll.Until(() => runs.Started.Contains("q2"), () => $"started: {string.Join(", ", runs.Started)}", Poll.Patience);
            watch.Nudge();
            await Poll.Until(() => watch.Running.Running == 1, () => "q2 never ran", Poll.Patience);

            Assert.Equal(first, File.ReadAllText(WorkflowRunBindings.PathOf(_home, "q1")));
            Assert.False(WorkflowRunBindings.Bound(_home, "q2"));
        }
        finally
        {
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    [Fact]
    public async Task A_start_that_opened_nothing_binds_nothing()
    {
        _ledger.Publish("q1", "engine");
        var (watch, runs) = Watch(""" "holds": ["engine"], """);
        var reports = new List<TickReport>();

        var watching = watch.RunAsync((report, _) => { lock (reports) reports.Add(report); return Task.CompletedTask; }, onError: null, _closing.Token);
        try
        {
            await Poll.Until(() => Count(reports) >= 2, () => "no look", Poll.Patience);

            Assert.Empty(runs.Started);
            Assert.False(WorkflowRunBindings.Bound(_home, "q1"));
        }
        finally
        {
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    private static int Count(List<TickReport> reports)
    {
        lock (reports) return reports.Count;
    }

    private static List<string> Events(List<TickReport> reports)
    {
        lock (reports) return [.. reports.SelectMany(report => report.Events)];
    }

    private static async Task Settled(Task running)
    {
        try { await running.WaitAsync(Poll.Patience); }
        catch (OperationCanceledException) { }
    }

    private (DriverWatch Watch, StandInRuns Runs) Watch(string more)
    {
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        File.WriteAllText(config, $$"""
            { {{more}} "drivable": ["engine"], "adapter": "stub", "cap": 2, "pollSeconds": 1, "trees": ["engine"] }
            """);
        var service = _ledger.Client();
        var runs = new StandInRuns(service, _ledger);
        var watch = new DriverWatch(
            service, config, _home, new SessionProcesses(), sync: null,
            harnesses: new HarnessRoster(ProviderRegistry.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            // A start binds its run before its take, and the look says what it bound: the stand-in's run binds as the real start does.
            Runner = async (start, opened, ct) =>
            {
                var hold = StartHold.TryTake(_home, Path.Combine(_home, "engine"))!;
                WorkflowBind bound;
                using (hold.Lock)
                {
                    bound = await WorkflowRunStart.AtStartAsync(
                        new WorkflowStarted(start.Quest, start.Workspace), new StartWorld(service), _home, DriverConfig.Parse(File.ReadAllText(config)), [],
                        () => DateTimeOffset.UtcNow, hold, ct);
                }

                if (bound.Came == WorkflowBindCame.Bound) runs.Keeping!.Bound(bound);
                return await runs.Runner(start, opened, ct);
            },
        };
        runs.Keeping = watch.Running;
        return (watch, runs);
    }
}
