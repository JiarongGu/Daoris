using Microsoft.Data.Sqlite;
using Daoris.Contracts.Parts;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The look's landing settles the deliveries a stopped run left once per watch, not once per driver: the watch builds a driver for each
/// look, so a flag kept on the pass reset with every look and the settlement ran at each. A driver handed the watch's memory takes the
/// watch's turn; one handed none has its own, as a one-look run's does. A settlement that held a delivery to the next look, as its lines say,
/// gives its turn back, so the next look asks again.
/// </summary>
public sealed class LandingOncePerWatchTests : IDisposable
{
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";

    private const string Said = "being landed or settled here now";

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
    public async Task Looks_built_for_one_watch_settle_at_the_first_look_and_ask_no_more_once_it_ended_clean()
    {
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Desktop, out _);
        Assert.NotNull(lease);
        var watch = new LandingOnce();

        Assert.Empty(await LandAsync(Look(watch)));

        // A delivery that stands from here is its unit's own landing's to go on from: the watch's settlement is spent.
        using var held = await SayingDeliveryAsync();
        Assert.Empty(await LandAsync(Look(watch)));
        Assert.Empty(await LandAsync(Look(watch)));
    }

    [Fact]
    public async Task A_delivery_held_to_the_next_look_is_asked_again_at_it_until_it_ends_clean()
    {
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Desktop, out _);
        Assert.NotNull(lease);
        var watch = new LandingOnce();
        var held = await SayingDeliveryAsync();

        Assert.Contains(await LandAsync(Look(watch)), line => line.Contains(Said));
        Assert.Contains(await LandAsync(Look(watch)), line => line.Contains(Said));

        held.Dispose();
        await LandAsync(Look(watch));
        Assert.Empty(await LandAsync(Look(watch)));
    }

    [Fact]
    public async Task A_driver_handed_no_memory_has_its_own_so_each_settles_once_it_ended_clean()
    {
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Desktop, out _);
        Assert.NotNull(lease);
        var one = new Daoris.Driver.Driver(_ledger.Client(), DriverConfig.Empty, ProviderRegistry.Built(), _home);
        var other = new Daoris.Driver.Driver(_ledger.Client(), DriverConfig.Empty, ProviderRegistry.Built(), _home);
        Assert.Empty(await LandAsync(one));
        using var held = await SayingDeliveryAsync();

        Assert.Empty(await LandAsync(one));
        Assert.Contains(await LandAsync(other), line => line.Contains(Said));
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

    /// <summary>The watch builds a driver for each look: the memory it hands them is the one place the settlement is once per watch.</summary>
    [Fact]
    public async Task The_watchs_looks_share_one_memory_so_a_settlement_that_ended_clean_is_not_asked_at_the_next_look()
    {
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        File.WriteAllText(config, """{ "drivable": [] }""");
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Desktop, out _);
        Assert.NotNull(lease);
        using var log = new MachineLog(_home, "driver");
        using var closing = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var looks = new List<string[]>();
        IDisposable? held = null;
        DriverWatch? watch = null;
        watch = new DriverWatch(_ledger.Client(), config, _home, new SessionProcesses(), sync: null,
            harnesses: new HarnessRoster(ProviderRegistry.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses))) { Log = log };
        try
        {
            await watch.RunAsync(
                async (report, _) =>
                {
                    looks.Add([.. report.Events]);
                    // The first look found nothing left; the delivery stands from here, and only a settlement asked again would say it.
                    if (looks.Count == 1) held = await SayingDeliveryAsync();
                    if (looks.Count < 3) watch.Nudge();
                    else await closing.CancelAsync();
                },
                onError: null,
                closing.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            held?.Dispose();
        }

        Assert.True(looks.Count >= 3, $"the watch looked {looks.Count} times");
        Assert.DoesNotContain(looks.SelectMany(each => each), line => line.Contains(Said));
    }
}
