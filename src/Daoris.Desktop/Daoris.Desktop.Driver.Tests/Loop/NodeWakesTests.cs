using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-RUN1d (the fabric events design §A3, §C6): the evaluation runtime's own wakes run link 8 alone. A <c>nodes</c> wake (a timer its
/// records persist come due, the awaited set it told changed) runs the runtime's pass and no local plan, so it is no look; over the real
/// client with the service's doors standing in, in-process, as the topics' tests drive the watch.
/// </summary>
public sealed class NodeWakesTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-nodewakes-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(Poll.Patience);

    private int _passes;

    public NodeWakesTests()
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
    public async Task A_nodes_wake_runs_the_runtimes_pass_and_no_plan()
    {
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        // A work cycle naming a plugin's performed step arms the runtime; no plugin declares it here, so nothing is ever sent.
        File.WriteAllText(config, """
            { "drivable": ["engine"], "adapter": "stub", "cap": 3, "pollSeconds": 600, "trees": ["engine"],
              "workCycles": { "engine": { "quest": {
                "schemaVersion": 2, "entries": ["deploy"], "end": "deploy",
                "nodes": [{ "id": "deploy", "kind": "acme.deploy/deploy", "version": 1, "reach": "performed" }], "wires": [] } } } }
            """);
        await using var hooks = new HookSet(_home, say: (_, _) => { });
        int looks;
        using (var log = new MachineLog(_home, "driver"))
        {
            var watch = new DriverWatch(_ledger.Client(), config, _home, new SessionProcesses(), sync: null, hooks: hooks,
                harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)))
            {
                Log = log,
                Passed = _ => Interlocked.Increment(ref _passes),
            };
            var reports = new List<TickReport>();
            var watching = watch.RunAsync((report, _) =>
            {
                lock (reports) reports.Add(report);
                return Task.CompletedTask;
            }, onError: null, _closing.Token);

            try
            {
                // The loop's start: its whole look, and the runtime's pass beside it.
                await Poll.Until(() => Count(reports) >= 1 && Volatile.Read(ref _passes) >= 1, () => "no look, or no pass", Bound);
                await Poll.Until(() => !watch.Running.Passing, () => "the start's pass never ended", Bound);
                (looks, var passes) = (Count(reports), Volatile.Read(ref _passes));

                // A timer the records persist came due: the runtime's pass alone, never the local plan.
                watch.NodesWoken("due");
                await Poll.Until(() => Volatile.Read(ref _passes) > passes, () => "the wake ran no pass", Bound);
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                Assert.Equal(looks, Count(reports));
            }
            finally
            {
                await _closing.CancelAsync();
                try { await watching.WaitAsync(Bound); }
                catch (OperationCanceledException) { }
            }
        }

        // No look's line names the runtime's wake: the only look is the start's.
        Assert.DoesNotContain(Looked(), data => data.GetProperty("topic").GetString() == ChangeTopics.Nodes);
        Assert.Equal(looks, Looked().Length);
    }

    private static int Count(List<TickReport> reports)
    {
        lock (reports) return reports.Count;
    }

    private JsonElement[] Looked() =>
        Directory.EnumerateFiles(DaorisHome.Entry(_home, HomeEntry.Logs), "*.jsonl")
            .SelectMany(File.ReadAllLines)
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .Where(line => line.GetProperty("event").GetString() == "watch.looked")
            .Select(line => line.GetProperty("data"))
            .ToArray();
}
