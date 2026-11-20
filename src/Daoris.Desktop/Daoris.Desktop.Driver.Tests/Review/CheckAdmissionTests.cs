using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The machine's check slots: <c>auto</c> admits another check while free memory stays above a check's need, up to a quarter
/// of the processors within the machine's heavy-run admission; a pinned number is that many; the first check always runs; a check waiting
/// says why. Files only, no process: the slots are the home's lock files, held here by this process.
/// </summary>
public sealed class CheckAdmissionTests : IDisposable
{
    private const long Gb = 1L << 30;

    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private readonly HeavyFixture _fixture = new("check-admission");

    public void Dispose() => _fixture.Dispose();

    private static CheckRules Auto(double freeGb, int processors = 16, int machine = 4) =>
        new(null, processors, machine, () => new MemoryReading((long)(freeGb * Gb), (long)(freeGb * Gb)));

    private static VerifyHolder Holder(string run, DateTimeOffset since) => new(Environment.ProcessId, 1, run, "i-" + run, "Daoris", since);

    private (VerifySlot? Slot, CheckWait? Wait) Take(CheckRules rules, string run, DateTimeOffset? since = null) =>
        CheckAdmission.TryTake(_fixture.Home, rules, Now, () => Holder(run, since ?? Now.AddMinutes(-10)), (_, _) => true);

    [Fact]
    public void With_memory_for_two_two_checks_run_at_once_and_the_third_waits_for_a_slot()
    {
        // Eight processors: two checks at most; 13 GB free admits a second at 6 GB.
        var rules = new CheckRules(null, 8, 4, () => new MemoryReading(13 * Gb, 13 * Gb));

        var (first, _) = Take(rules, "r-1");
        var (second, _) = Take(rules, "r-2");
        var (third, wait) = Take(rules, "r-3");

        using (first)
        using (second)
        {
            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Null(third);
            Assert.Equal(CheckWait.Slots, wait!.Code);
            Assert.Equal("a slot: 2 of 2 run", wait.Sentence);
            Assert.Equal(2, VerifySlot.Look(_fixture.Home, (_, _) => true).Holders.Count);
        }
    }

    [Fact]
    public void With_memory_for_one_the_first_runs_and_the_next_waits_for_memory_saying_how_much_is_free_and_needed()
    {
        var (first, _) = Take(Auto(4.1), "r-1");
        var (second, wait) = Take(Auto(4.1), "r-2");

        using (first)
        {
            Assert.NotNull(first);
            Assert.Null(second);
            Assert.Equal(CheckWait.Memory, wait!.Code);
            Assert.Equal("4.1", wait.Values["free"]);
            Assert.Equal("6", wait.Values["need"]);
            Assert.Equal("memory: 4.1 GB free, a check needs 6 GB", wait.Sentence);
        }
    }

    [Fact]
    public void A_check_still_starting_counts_its_need_again_so_two_never_climb_to_their_peak_on_one_reading()
    {
        var (first, _) = Take(Auto(10), "r-1", Now.AddSeconds(-30));
        var (second, wait) = Take(Auto(10), "r-2");

        using (first)
        {
            Assert.Null(second);
            Assert.Equal("12", wait!.Values["need"]);
            Assert.Equal("memory: 10.0 GB free, a check needs 12 GB with 1 still starting", wait.Sentence);
        }
    }

    [Fact]
    public void A_pinned_number_is_that_many_whatever_the_memory_and_one_pins_one()
    {
        var one = new CheckRules(1, 64, 6, () => new MemoryReading(100 * Gb, 100 * Gb));
        var (first, _) = Take(one, "r-1");
        var (second, wait) = Take(one, "r-2");
        using (first)
        {
            Assert.Null(second);
            Assert.Equal("a slot: 1 of 1 run", wait!.Sentence);
        }

        var three = new CheckRules(3, 4, 2, () => new MemoryReading(1 * Gb, 1 * Gb));
        var taken = new[] { Take(three, "a").Slot, Take(three, "b").Slot, Take(three, "c").Slot };
        try
        {
            Assert.All(taken, Assert.NotNull);
            Assert.Equal(CheckWait.Slots, Take(three, "d").Wait!.Code);
        }
        finally
        {
            foreach (var slot in taken) slot?.Dispose();
        }
    }

    [Fact]
    public void Memory_that_cannot_be_read_admits_one_check_as_before()
    {
        var unread = new CheckRules(null, 32, 6, () => null);
        var (first, _) = Take(unread, "r-1");
        var (second, wait) = Take(unread, "r-2");

        using (first)
        {
            Assert.NotNull(first);
            Assert.Null(second);
            Assert.Equal("a slot: 1 of 1 run", wait!.Sentence);
        }
    }

    [Theory]
    [InlineData(2, 4, 1)]
    [InlineData(8, 4, 2)]
    [InlineData(32, 4, 4)]
    [InlineData(32, 6, 6)]
    [InlineData(64, 6, 6)]
    public void Auto_admits_a_quarter_of_the_processors_at_most_within_the_machine_s_admission_and_one_at_least(int processors, int machine, int ceiling) =>
        Assert.Equal(ceiling, new CheckRules(null, processors, machine, () => null).Ceiling);

    [Fact]
    public void A_slot_let_go_is_taken_again_and_its_record_goes_with_it()
    {
        var (first, _) = Take(Auto(20), "r-1");
        first!.Dispose();

        Assert.Empty(VerifySlot.Look(_fixture.Home, (_, _) => true).Holders);
        using var again = Take(Auto(20), "r-2").Slot;
        Assert.NotNull(again);
        Assert.Equal(["r-2"], VerifySlot.Look(_fixture.Home, (_, _) => true).Holders.Select(holder => holder.Run));
    }
}
