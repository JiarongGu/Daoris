using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TOOL4f (D125 §4) through a look: when every account of the person's order is cooling, a carry-on is held at spawn
/// with nothing spawned and no record, waiting for the first reset, never parked, and the wait is said once.
/// </summary>
/// <remarks>
/// Over the real client and planner with the service's doors standing in (<see cref="StandInLedger"/>), as
/// <c>AccountLimitHoldTests</c> is: the repository opens a tree per session, so the start asks git nothing before its
/// selection holds it. A carry-on that does rotate opens a tree and spawns, which is <c>AccountRotationTickTests</c>, in
/// the <c>Process</c> half.
/// </remarks>
public sealed class AccountRotationHoldTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Seen = new(2026, 10, 1, 14, 0, 0, TimeSpan.FromMinutes(345));

    private static readonly DateTimeOffset Until = new(2026, 10, 3, 16, 2, 0, TimeSpan.FromMinutes(345));

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-rotation-hold-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly DateTimeOffset _now = Seen;

    public AccountRotationHoldTests()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Path.Combine(_home, "engine"));
        foreach (var account in new[] { "account-1", "account-2" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "stub", account));
        }

        new HarnessSettings().WithDefault("stub", "account-1").WithRotation("stub", ["account-1", "account-2"])
            .Save(Path.Combine(_home, "harnesses.json"));
    }

    public void Dispose()
    {
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private HarnessRoster Roster() =>
        new(AdapterSet.Built(), Path.Combine(_home, "harnesses.json")) { Clock = () => _now, Zone = Zone };

    private static DriverConfig Config => DriverConfig.Empty with
    {
        Drivable = ["engine"],
        Trees = ["engine"],
        Adapter = "stub",
        // One failure would park it: a limit must not, and nor must a wait over an order.
        Strikes = 1,
        PollSeconds = 1,
    };

    private Daoris.Driver.Driver Driver(ServiceClient service, HarnessRoster roster) =>
        new(service, Config, AdapterSet.Built(), _home, harnesses: roster);

    private void Cool(string account, DateTimeOffset until) =>
        AccountCooling.Cool(_home, new CoolingEntry("stub", account, until, true, "weekly", Seen, "s1"), _now);

    /// <summary>A cut-off on account-1, as the driver records one: the quest taken, its session failed with a limit.</summary>
    private async Task CutOffAsync(ServiceClient service)
    {
        _ledger.Publish("q1", "engine");
        var (id, _) = await service.OpenSessionAsync("q1", "stub", null, "account-1", Path.Combine(_home, "engine"), null);
        await service.AdvanceAsync(id!, "working");
        _ledger.Move("q1", "Taken");
        await service.AdvanceAsync(id!, "failed", note: "the agent's turn failed with the quest still taken.", limit: true);
    }

    [Fact]
    public async Task Every_account_of_the_order_cooling_holds_the_carry_on_until_the_first_reset_unparked()
    {
        using var service = _ledger.Client();
        var roster = Roster();
        await CutOffAsync(service);
        Cool("account-1", Until.AddHours(5));
        Cool("account-2", Until);

        var look = await Driver(service, roster).TickAsync().WaitAsync(Bound);

        var sitting = Assert.Single(look.Considerations);
        Assert.Equal(StartVerdict.Blocked, sitting.Verdict);
        Assert.Equal(
            $"every `stub` account this start may use is cooling; the first ready, `account-2`, at Oct 3, 16:02 ({Zone.Id}), "
            + "as the agent said. Daoris starts nothing on them until then.",
            sitting.Reason);
        Assert.False(look.Progressed);
        Assert.Single(_ledger.Sessions);

        var wait = Assert.Single(look.Waits);
        Assert.Equal(("stub", "stub", "account-2", Until), (wait.Adapter, wait.Agent, wait.Account, wait.Until));
        Assert.Equal(["q1"], wait.Quests);
        Assert.Equal(sitting.Reason, wait.Sentence);
    }

    [Fact]
    public async Task A_wait_over_an_order_is_written_once_however_many_looks_it_lasts()
    {
        using var service = _ledger.Client();
        var lines = new List<AccountLine>();
        service.AccountLined += lines.Add;
        var roster = Roster();
        await CutOffAsync(service);
        Cool("account-1", Until);
        Cool("account-2", Until.AddHours(1));

        for (var look = 0; look < 3; look++) await Driver(service, roster).TickAsync().WaitAsync(Bound);

        var line = Assert.Single(lines, l => l.Event == "starts.waiting");
        Assert.Equal("account-1", line.Data.Single(field => field.Key == "account").Value);
        Assert.DoesNotContain(lines, l => l.Event == "account.rotated");
    }
}
