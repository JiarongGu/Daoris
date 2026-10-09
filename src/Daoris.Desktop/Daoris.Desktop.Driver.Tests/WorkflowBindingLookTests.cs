using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WORKFLOW1e (D157 point 11, the workflow design §4.5): a run is bound at its first start in its repository, on the machine that
/// drives it, to the workflow §4.1 chooses then, with what chose it; a later quest of the same run keeps that binding, and a
/// start that opened nothing binds nothing.
/// </summary>
/// <remarks>Over the real client and planner with the service's doors and each start's run standing in, in-process (DEV3).</remarks>
public sealed class WorkflowBindingLookTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-workflow-look-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(TimeSpan.FromSeconds(60));

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

    private static JsonNode Steps() => JsonNode.Parse("""[{"id":"work","kind":"work"},{"id":"landing","kind":"landing","form":"merge","accept":"you"}]""")!;

    [Fact]
    public async Task A_run_is_bound_at_its_first_start_to_the_repository_s_choice_and_the_look_says_so()
    {
        WorkflowStore.Add(_home, new WorkflowVersionAdded("merge-after", "Merge after you accept", "terminal", "2026-10-09T09:00:00Z", Steps()), kept: []);
        _ledger.Publish("q1", "engine");
        var (watch, runs) = Watch(""" "workflows": { "engine": { "default": "merge-after" } }, """);
        var reports = new List<TickReport>();

        var watching = watch.RunAsync((report, _) => { lock (reports) reports.Add(report); return Task.CompletedTask; }, onError: null, _closing.Token);
        try
        {
            await Poll.Until(() => WorkflowRunBindings.Bound(_home, "q1"), () => $"started: {string.Join(", ", runs.Started)}", Bound);

            var binding = WorkflowRunBindings.Read(_home, "q1")!;
            Assert.Equal(("merge-after", 1, WorkflowLevels.Repository, "engine"), (binding.Workflow, binding.Version, binding.Level, binding.Repository));
            await Poll.Until(
                () => Events(reports).Contains("workflow  #q1 → engine: its run follows `merge-after` v1, `engine`'s default."),
                () => string.Join(" | ", Events(reports)), Bound);
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
            await Poll.Until(() => WorkflowRunBindings.Bound(_home, "q1"), () => $"started: {string.Join(", ", runs.Started)}", Bound);
            var first = File.ReadAllText(WorkflowRunBindings.PathOf(_home, "q1"));
            var binding = WorkflowRunBindings.Read(_home, "q1")!;
            Assert.Equal((WorkflowSelection.Current, WorkflowLevels.Task, "f00d01"), (binding.Workflow, binding.Level, binding.Ask));
            Assert.NotEmpty(binding.CurrentSteps);

            // The chain's next step in the same repository is the same run: a choice changed since reaches new work only.
            runs.End("q1");
            _ledger.WorkflowChosen("f00d01", kind: null, workflow: "merge-after");
            _ledger.Publish("q2", "engine", from: "ask #f00d01", parent: "q1");
            watch.Nudge();
            await Poll.Until(() => runs.Started.Contains("q2"), () => $"started: {string.Join(", ", runs.Started)}", Bound);
            watch.Nudge();
            await Poll.Until(() => watch.Running.Running == 1, () => "q2 never ran", Bound);

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
            await Poll.Until(() => Count(reports) >= 2, () => "no look", Bound);

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
        try { await running.WaitAsync(Bound); }
        catch (OperationCanceledException) { }
    }

    private (DriverWatch Watch, StandInRuns Runs) Watch(string more)
    {
        var config = Path.Combine(_home, "driver.json");
        File.WriteAllText(config, $$"""
            { {{more}} "drivable": ["engine"], "adapter": "stub", "cap": 2, "pollSeconds": 1, "trees": ["engine"] }
            """);
        var service = _ledger.Client();
        var runs = new StandInRuns(service, _ledger);
        var watch = new DriverWatch(
            service, config, _home, new SessionProcesses(), sync: null,
            harnesses: new HarnessRoster(AdapterSet.Built(), Path.Combine(_home, "harnesses.json")))
        {
            Runner = runs.Runner,
        };
        runs.Keeping = watch.Running;
        return (watch, runs);
    }
}
