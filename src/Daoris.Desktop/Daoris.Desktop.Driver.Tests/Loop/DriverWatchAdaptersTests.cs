using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1m-i part 1 (the agent-layer design §6.3 point 1): the watch is handed its adapter set, so a look's driver and the roster are
/// built over the set a caller gives it, which the parity oracle builds a look over for either path. Handed none, it builds over today's, so the
/// twenty places that build a watch are unchanged.
/// </summary>
public sealed class DriverWatchAdaptersTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-watch-adapters-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(Poll.Patience);

    public DriverWatchAdaptersTests()
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

    private static AdapterSet Only(string name) =>
        new(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase) { [name] = AdapterSet.Built().Resolve(name) });

    private DriverWatch Watch(AdapterSet? adapters, HarnessRoster? harnesses = null)
    {
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        File.WriteAllText(config, """{ "drivable": ["engine"], "adapter": "stub", "cap": 2, "pollSeconds": 1, "trees": ["engine"] }""");
        var service = _ledger.Client();
        return new DriverWatch(service, config, _home, new SessionProcesses(), sync: null, harnesses: harnesses, adapters: adapters)
        {
            Runner = new StandInRuns(service, _ledger).Runner,
        };
    }

    [Fact]
    public void A_watch_handed_no_adapter_set_builds_over_the_built_one()
    {
        var watch = Watch(adapters: null);

        Assert.Equal(AdapterSet.Built().Names, watch.Harnesses.Adapters.Names);
    }

    [Fact]
    public void A_watch_handed_an_adapter_set_builds_its_roster_over_it()
    {
        var watch = Watch(Only("stub"));

        Assert.Equal(["stub"], watch.Harnesses.Adapters.Names);
    }

    /// <summary>
    /// Each look's driver is built over the set the watch holds: a look puts the adapters it runs on the roster (D64 §3), so the roster reads
    /// the injected set's names after it, never the built set's.
    /// </summary>
    [Fact]
    public async Task Each_look_runs_over_the_adapter_set_the_watch_was_handed()
    {
        var roster = new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses));
        var watch = Watch(Only("stub"), roster);
        var reports = 0;

        var watching = watch.RunAsync((_, _) => { Interlocked.Increment(ref reports); return Task.CompletedTask; }, onError: null, _closing.Token);
        try
        {
            await Poll.Until(() => Volatile.Read(ref reports) >= 1, () => "no look", Poll.Patience);

            Assert.Equal(["stub"], roster.Adapters.Names);
        }
        finally
        {
            await _closing.CancelAsync();
            try { await watching.WaitAsync(Poll.Patience); } catch (OperationCanceledException) { }
        }
    }
}
