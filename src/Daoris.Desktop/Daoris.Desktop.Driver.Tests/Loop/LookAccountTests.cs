using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A look's time is said by its stages: over a home holding many runs bound to a graph with a review, each with its record, the
/// <c>watch.looked</c> line's own stages add up to the look's milliseconds, and the look reads each run once, so it stays short as the
/// runs grow. Over the real client with the ledger's doors standing in, in-process, as the node wakes' tests drive the watch.
/// </summary>
public sealed class LookAccountTests(ITestOutputHelper output) : IDisposable
{
    private const int Runs = 120;

    private const string Graph = """
        {"schemaVersion":2,"entries":["work"],"end":"push",
         "nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},{"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"branch","pattern":"daoris/{quest}"}},
                  {"id":"push","kind":"push","version":1,"reach":"performed"}],
         "wires":[{"id":"a","kind":"after","from":"work","to":"review"},{"id":"b","kind":"after","from":"review","to":"landing"},
                  {"id":"c","kind":"after","from":"landing","to":"push"}]}
        """;

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-lookaccount-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(Poll.Patience);

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_look_over_many_runs_is_said_whole_by_its_stages_and_reads_each_run_once()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Path.Combine(_home, "engine"));
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        File.WriteAllText(config, """{ "drivable": ["engine"], "adapter": "stub", "cap": 3, "pollSeconds": 600 }""");
        var at = DateTimeOffset.UtcNow.AddHours(-1);
        var digest = RunHome.SaveGraph(_home, Graph);
        var records = new NodeRecordFiles(_home, () => true);
        for (var i = 0; i < Runs; i++)
        {
            var run = $"q{i:D3}";
            RunHome.Bind(_home, run, digest, at);
            var unit = RunUnits.IdOf(run);
            records.Write(new NodeRecord(unit)
            {
                Statuses = new SortedDictionary<string, NodeStatus>(StringComparer.Ordinal)
                {
                    ["review"] = new(unit, "review", NodeStates.Reached) { Attempt = 1, Since = at },
                    ["landing"] = new(unit, "landing", NodeStates.Reached) { Attempt = 1, Since = at.AddMinutes(1) },
                },
            });
        }

        var watched = Stopwatch.StartNew();
        using (var log = new MachineLog(_home, "driver"))
        {
            var watch = new DriverWatch(_ledger.Client(), config, _home, new SessionProcesses(), sync: null,
                harnesses: new HarnessRoster(ProviderRegistry.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)))
            {
                Log = log,
            };
            var looked = 0;
            var watching = watch.RunAsync((_, _) =>
            {
                Interlocked.Increment(ref looked);
                return Task.CompletedTask;
            }, onError: null, _closing.Token);

            try
            {
                await Poll.Until(() => Volatile.Read(ref looked) >= 1, () => "no look", Poll.Patience);
                // A second whole look, as the loop's next: what a look costs once the process has read the home.
                watch.Nudge();
                await Poll.Until(() => Volatile.Read(ref looked) >= 2, () => "no second look", Poll.Patience);
            }
            finally
            {
                await _closing.CancelAsync();
                try { await watching.WaitAsync(Poll.Patience); }
                catch (OperationCanceledException) { }
            }
        }

        var looks = Looked();
        foreach (var look in looks.Take(2))
        {
            var ms = look.GetProperty("ms").GetInt64();
            var stages = look.GetProperty("stages").GetString() ?? "";
            var own = Own(stages);
            output.WriteLine($"a look over {Runs} runs: {ms} ms, its stages {own} ms: {stages}");
            // Every await of the look is in a stage of its own: what no stage names is the look's bookkeeping, about a second at most.
            Assert.True(ms - own <= 1_000 + ms / 5, $"the look took {ms} ms and its stages say {own} ms: {stages}");
        }

        // The runs' outcomes are read once a look, each run's record once: a look over this many runs stays short once the process has
        // read the home (a first look's own reads and its code's first runs are a loaded machine's to stretch).
        var warm = looks[1].GetProperty("ms").GetInt64();
        Assert.True(warm < 2_000, $"the second look over {Runs} runs took {warm} ms ({watched.ElapsedMilliseconds} ms watched): {looks[1].GetProperty("stages").GetString()}");
    }

    /// <summary>The milliseconds a line's own stages hold and queue: every entry not marked as work beside the look.</summary>
    internal static long Own(string stages) =>
        stages.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(entry => !entry.StartsWith('~'))
            .Select(entry => entry.Split('=')[1])
            .Sum(value => long.Parse(value.Split('x', '@')[0], System.Globalization.CultureInfo.InvariantCulture));

    private JsonElement[] Looked() =>
        Directory.EnumerateFiles(DaorisHome.Entry(_home, HomeEntry.Logs), "*.jsonl")
            .SelectMany(File.ReadAllLines)
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .Where(line => line.GetProperty("event").GetString() == "watch.looked")
            .Select(line => line.GetProperty("data"))
            .ToArray();
}
