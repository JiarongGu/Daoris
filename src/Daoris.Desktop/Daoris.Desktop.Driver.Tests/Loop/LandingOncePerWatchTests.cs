using Microsoft.Data.Sqlite;
using Daoris.Contracts.Parts;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The look's landing settles the deliveries a stopped run left once per watch, not once per driver: the watch builds a driver for each
/// look, so a flag kept on the pass reset with every look and the settlement ran at each. A driver handed the watch's memory takes
/// the watch's turn; one handed none has its own, as a one-look run's does.
/// </summary>
public sealed class LandingOncePerWatchTests : IDisposable
{
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";

    private static readonly BoundLand Bound = new("merge", "main", false, null, null, "{}", "digest", "workflow:w@1:r");

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "landing-once", Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    public LandingOncePerWatchTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        _ledger.Dispose();
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private Daoris.Driver.Driver Look(LandingOnce? once = null) =>
        new(_ledger.Client(), DriverConfig.Empty, ProviderRegistry.Built(), _home) { Landed = once ?? new LandingOnce() };

    private static async Task<List<string>> LandAsync(Daoris.Driver.Driver driver)
    {
        var round = new LookRound(LookPart.Look, [], new LookState([]));
        await driver.LookPasses.Of<UnresolvedDeliveries>().RunAsync(round, CancellationToken.None);
        return round.Events;
    }

    /// <summary>A delivery left unresolved that this process holds, so that settling it is said ("being landed or settled here now") each time it is tried.</summary>
    private async Task<IDisposable> SayingDeliveryAsync()
    {
        var (started, refusal) = await CurrentDeliveries.StartAsync(
            _home, Landing.Merge, "repo", "ws", "unused-root", new LandingSubject("s1", null, "t"), Tip, "main", "feature/x", null, AcceptedBy.Person, false,
            () => null, CancellationToken.None, Bound);
        Assert.Null(refusal);
        return DeliveryRuns.Hold(_home, started!.Claim.Key)!;
    }

    [Fact]
    public async Task Looks_built_for_one_watch_settle_the_unresolved_deliveries_at_the_first_look_and_not_again()
    {
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Desktop, out _);
        Assert.NotNull(lease);
        using var held = await SayingDeliveryAsync();
        var watch = new LandingOnce();

        var first = await LandAsync(Look(watch));
        var second = await LandAsync(Look(watch));
        var third = await LandAsync(Look(watch));

        Assert.Contains(first, line => line.Contains("being landed or settled here now"));
        Assert.Empty(second);
        Assert.Empty(third);
    }

    [Fact]
    public async Task A_driver_handed_no_memory_has_its_own_so_two_of_them_each_settle_once()
    {
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Desktop, out _);
        Assert.NotNull(lease);
        using var held = await SayingDeliveryAsync();
        var one = new Daoris.Driver.Driver(_ledger.Client(), DriverConfig.Empty, ProviderRegistry.Built(), _home);
        var other = new Daoris.Driver.Driver(_ledger.Client(), DriverConfig.Empty, ProviderRegistry.Built(), _home);

        Assert.NotEmpty(await LandAsync(one));
        Assert.Empty(await LandAsync(one));
        Assert.NotEmpty(await LandAsync(other));
    }

    [Fact]
    public async Task A_look_that_did_not_hold_the_lease_leaves_the_turn_for_the_look_that_does()
    {
        using var held = await SayingDeliveryAsync();
        var watch = new LandingOnce();

        Assert.Empty(await LandAsync(Look(watch)));

        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Desktop, out _);
        Assert.NotNull(lease);
        Assert.NotEmpty(await LandAsync(Look(watch)));
    }
}
