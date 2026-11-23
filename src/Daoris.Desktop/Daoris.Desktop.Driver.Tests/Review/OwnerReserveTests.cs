using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The machine keeps a reserve free for the person's own work. A heavy run, a check or a build is admitted only while the
/// free memory stays at or above the reserve after the run's own need; a new admission waits while it is under it and says the reserve; the
/// reserve is the person's setting (<c>reserveGb</c>), a third of the machine's memory by default. Files only, as the admission's own tests.
/// </summary>
public sealed class OwnerReserveTests : IDisposable
{
    private const long Gb = 1L << 30;

    private static readonly DateTimeOffset Now = new(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);

    private readonly HeavyFixture _fixture = new("owner-reserve");

    public void Dispose() => _fixture.Dispose();

    /// <summary>A machine of <paramref name="totalGb"/> GB with <paramref name="freeGb"/> free, its reserve <paramref name="reserveGb"/> (null: auto).</summary>
    private static CheckRules Machine(double freeGb, int totalGb = 64, int? reserveGb = null, int? pinned = null) =>
        new(pinned, 16, 4, () => new MemoryReading((long)(freeGb * Gb), (long)(freeGb * Gb), totalGb * Gb), reserveGb);

    private HeavyAsk Check(string run, string? checkout = null)
    {
        var at = checkout ?? _fixture.Checkout;
        return new HeavyAsk(HeavySlot.KeyOf(at)!, "Daoris", null,
            () => new HeavyHolder(Environment.ProcessId, 1, at, ["reached"], Now, null, null) { Run = run, Now = new HeavyGateNow("build", 1, Now) })
        {
            Check = () => new VerifyHolder(Environment.ProcessId, 1, run, "i-" + run, "Daoris", Now),
        };
    }

    private (HeavyTaken? Taken, CheckWait? Wait) Take(CheckRules rules, HeavyAsk ask) => CheckAdmission.TryTake(_fixture.Home, rules, ask, (_, _) => true);

    [Fact]
    public void A_run_with_the_reserve_not_met_waits_and_says_the_reserve()
    {
        // 24 GB free, a run needs 8, the person keeps 20: 24 - 8 = 16 is under it.
        var (taken, wait) = Take(Machine(24, reserveGb: 20), Check("r-1"));

        Assert.Null(taken);
        Assert.Equal(CheckWait.Reserve, wait!.Code);
        Assert.Equal("24.0", wait.Values["free"]);
        Assert.Equal("8", wait.Values["need"]);
        Assert.Equal("20", wait.Values["reserve"]);
        Assert.Equal("the reserve for your own work: 24.0 GB free, a check needs 8 GB and 20 GB stays free", wait.Sentence);
        Assert.Empty(VerifySlot.Look(_fixture.Home, (_, _) => true).Holders);
    }

    [Fact]
    public void A_run_with_the_reserve_met_after_its_own_need_runs()
    {
        // 28 GB free: 28 - 8 = 20, the reserve exactly.
        var (taken, wait) = Take(Machine(28, reserveGb: 20), Check("r-1"));

        using (taken)
        {
            Assert.NotNull(taken);
            Assert.Null(wait);
        }
    }

    [Fact]
    public void A_waiting_run_starts_once_the_person_s_own_work_lets_go_of_memory()
    {
        var free = 24.0;
        var rules = new CheckRules(null, 16, 4, () => new MemoryReading((long)(free * Gb), (long)(free * Gb), 64 * Gb), 20);

        Assert.Equal(CheckWait.Reserve, Take(rules, Check("r-1")).Wait!.Code);
        free = 30;
        using var taken = Take(rules, Check("r-1")).Taken;

        Assert.NotNull(taken);
    }

    [Fact]
    public void A_pinned_number_of_checks_still_keeps_the_reserve()
    {
        var (taken, wait) = Take(Machine(24, reserveGb: 20, pinned: 4), Check("r-1"));

        Assert.Null(taken);
        Assert.Equal(CheckWait.Reserve, wait!.Code);
    }

    [Fact]
    public void A_reserve_of_none_keeps_nothing_back_and_the_first_run_always_runs()
    {
        var (taken, wait) = Take(Machine(1, reserveGb: 0), Check("r-1"));

        using (taken)
        {
            Assert.NotNull(taken);
            Assert.Null(wait);
        }
    }

    [Fact]
    public void Auto_keeps_a_third_of_the_machine_s_memory_to_the_whole_gb()
    {
        // 64 GB: 21 GB. 28 GB free less the run's 8 is 20: under it; 29 less 8 is 21: met.
        var (taken, wait) = Take(Machine(28), Check("r-1"));

        Assert.Null(taken);
        Assert.Equal("21", wait!.Values["reserve"]);
        using var met = Take(Machine(29), Check("r-1")).Taken;
        Assert.NotNull(met);
    }

    [Fact]
    public void A_machine_whose_memory_cannot_be_read_keeps_no_reserve_it_cannot_judge()
    {
        using var taken = Take(new CheckRules(null, 16, 4, () => null), Check("r-1")).Taken;

        Assert.NotNull(taken);
    }

    [Fact]
    public void A_second_run_beside_a_first_whose_gate_has_ended_needs_its_own_need_over_the_reserve()
    {
        var rules = Machine(40, reserveGb: 20);
        using var first = Take(rules, Check("r-1")).Taken;
        var (second, wait) = Take(rules, Check("r-2", _fixture.Other));

        // The first's gate has ended (its record says so), so the second needs its own 8 over the reserve: 40 >= 28.
        using (second)
        {
            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Null(wait);
        }
    }

    [Fact]
    public void Daoris_s_share_sums_each_process_under_its_roots_once()
    {
        // 1 starts 2 and 3; 3 starts 4; 9 is the person's own and is never counted. Roots 1 and 3 overlap.
        var parents = new Dictionary<int, int> { [1] = 0, [2] = 1, [3] = 1, [4] = 3, [9] = 0 };
        var commit = new Dictionary<int, long> { [1] = 1 * Gb, [2] = 2 * Gb, [3] = 4 * Gb, [4] = 8 * Gb, [9] = 100 * Gb };

        Assert.Equal(15 * Gb, DaorisShare.Of([1, 3], parents, pid => commit[pid]));
        Assert.Null(DaorisShare.Of([1], null, pid => commit[pid]));
    }

    [Fact]
    public void Daoris_s_share_of_this_machine_reads_this_process_at_least()
    {
        var share = DaorisShare.Read(_fixture.Home);

        Assert.True(share > 0, "this process holds some commit, and it is its own root");
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(64, 21)]
    [InlineData(96, 32)]
    public void The_default_is_a_third_of_the_memory_in_whole_gb(long totalGb, long reserveGb) =>
        Assert.Equal(reserveGb * Gb, ReserveSetting.DefaultOf(totalGb * Gb));

    [Fact]
    public void The_setting_reads_what_it_takes_and_is_auto_otherwise()
    {
        Assert.Equal(24, ReserveSetting.Read(24));
        Assert.Equal(0, ReserveSetting.Read(0));
        Assert.Null(ReserveSetting.Read(-1));
        Assert.Null(ReserveSetting.Read(ReserveSetting.Most + 1));
        Assert.Null(ReserveSetting.Read(null));
        Assert.Throws<DriverException>(() => DriverConfig.Empty.WithReserve(-3));
        Assert.Equal("auto", ReserveSetting.Said(null));
        Assert.Equal("24", ReserveSetting.Said(24));
    }

    [Fact]
    public void The_file_keeps_the_reserve_it_was_given_and_only_when_set()
    {
        var path = Path.Combine(_fixture.Home, "driver.json");
        Assert.DoesNotContain("reserveGb", DriverConfig.Empty.WithNotify(true).ToJson());
        File.WriteAllText(path, DriverConfig.Empty.WithReserve(24).ToJson());

        Assert.Equal(24, ReserveSetting.At(path));
        Assert.Contains("\"reserveGb\": 24", DriverConfig.Load(path).WithNotify(false).ToJson().Replace("\":24", "\": 24"));
    }
}
