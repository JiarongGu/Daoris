using Daoris.Driver;
using Daoris.Testing;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every entry carries its chain's correlation; an older row reads as its own; <c>daoris bus chain</c> lists one chain in one read.
/// </summary>
public sealed class BusCorrelationTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _home = Fixture();

    private static string Fixture()
    {
        var home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "bus-correlation", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home);
        return home;
    }

    public void Dispose() => GitSeed.Discard(_home);

    private BusLog Open(MovedClock clock) => BusLog.Over(new DesktopBusLogStore(_home), new BusLogOptions { Clock = clock });

    private IReadOnlyList<BusEnvelope> Logged()
    {
        using var store = new DesktopBusLogStore(_home);
        return store.Read(0, 100_000);
    }

    private static VerifySend Send(string run, string invocation) => new(run, invocation, "engine", "c:/x", "aaaa1111", "none");

    /// <summary>🔴 A check's start, its verdict and a later entry that follows the verdict share the start's id as their correlation; another chain's is its own.</summary>
    [Fact]
    public async Task A_chain_shares_the_correlation_of_its_first_entry()
    {
        var clock = new MovedClock();
        var one = Send("run-1", "inv-1");
        await using (var bus = Open(clock))
        {
            Assert.True(bus.Append(CheckBusFacts.CheckStarted(one)));
            Assert.True(bus.Append(CheckBusFacts.CheckStarted(Send("run-2", "inv-9"))));
            Assert.True(bus.Append(CheckBusFacts.CheckSettled(one, new VerifyResult("inv-1", "aaaa1111", VerifyVerdicts.Reached, null, []))));
            await bus.FlushAsync();
        }

        var logged = Logged();
        var start = logged.Single(entry => entry.Subject == "run-1" && entry.Kind == BusTypes.CheckStarted.Kind);
        var verdict = logged.Single(entry => entry.Subject == "run-1" && entry.Kind == BusTypes.CheckPassed.Kind);
        var other = logged.Single(entry => entry.Subject == "run-2");
        Assert.Equal(start.Id, start.Correlation);
        Assert.Equal(start.Id, verdict.Correlation);
        Assert.Contains(start.Id, verdict.Causes);
        Assert.Equal(other.Id, other.Correlation);
        Assert.NotEqual(start.Correlation, other.Correlation);
    }

    /// <summary>🔴 The chain read lists a chain's entries in order in one read, and says so of a word that names none.</summary>
    [Fact]
    public async Task The_chain_read_lists_one_chain_in_one_read()
    {
        var clock = new MovedClock();
        var one = Send("run-1", "inv-1");
        await using (var bus = Open(clock))
        {
            Assert.True(bus.Append(CheckBusFacts.CheckStarted(one)));
            Assert.True(bus.Append(CheckBusFacts.CheckStarted(Send("run-2", "inv-9"))));
            Assert.True(bus.Append(CheckBusFacts.CheckSettled(one, new VerifyResult("inv-1", "aaaa1111", VerifyVerdicts.Reached, null, []))));
            await bus.FlushAsync();
        }

        var chain = Logged().Single(entry => entry.Subject == "run-1" && entry.Kind == BusTypes.CheckStarted.Kind).Correlation;
        Assert.Equal(0, BusChainCommand.Run(["chain", chain], _home, out var said));
        output.WriteLine(said);
        Assert.Contains("2 entries", said, StringComparison.Ordinal);
        Assert.Contains(BusTypes.CheckStarted.Kind, said, StringComparison.Ordinal);
        Assert.Contains(BusTypes.CheckPassed.Kind, said, StringComparison.Ordinal);
        Assert.DoesNotContain("run-2", said, StringComparison.Ordinal);

        Assert.Equal(0, BusChainCommand.Run(["chain", "nothing"], _home, out var none));
        Assert.Contains("no entry", none, StringComparison.Ordinal);
        Assert.Equal(2, BusChainCommand.Run(["chain"], _home, out var usage));
        Assert.Contains(BusChainCommand.Usage, usage, StringComparison.Ordinal);
    }

    /// <summary>🔴 A row written before the column existed reads as its own correlation, and the chain read finds it by its id.</summary>
    [Fact]
    public async Task An_older_row_reads_as_its_own_correlation()
    {
        var clock = new MovedClock();
        await using (var bus = Open(clock))
        {
            Assert.True(bus.Append(CheckBusFacts.CheckStarted(Send("run-1", "inv-1"))));
            await bus.FlushAsync();
        }

        using (var probe = StoreEngineSource.Connect(DesktopStore.LocationIn(_home), Daoris.Ports.Store.DesktopOpen.ReadWrite))
        {
            using var command = probe.CreateCommand();
            command.CommandText = "UPDATE bus_log SET correlation = NULL";
            command.ExecuteNonQuery();
        }

        var old = Assert.Single(Logged());
        Assert.Equal(old.Id, old.Correlation);
        Assert.Equal(0, BusChainCommand.Run(["chain", old.Id], _home, out var said));
        Assert.Contains("1 entry", said, StringComparison.Ordinal);
    }
}
