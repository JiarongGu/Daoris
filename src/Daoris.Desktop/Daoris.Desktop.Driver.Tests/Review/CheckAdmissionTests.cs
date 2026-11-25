using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The machine's one admission of heavy runs: <c>auto</c> admits another while free memory stays above a run's need, up to a quarter
/// of the processors within the machine's heavy-run admission; a pinned number is that many within it; the first always runs; every heavy
/// run on the machine is counted, once; a repository may say one at a time; a run waiting says why. Files only, no process: the slots are
/// the home's lock files, held here by this process.
/// </summary>
public sealed class CheckAdmissionTests : IDisposable
{
    private const long Gb = 1L << 30;

    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private readonly HeavyFixture _fixture = new("check-admission");

    public void Dispose() => _fixture.Dispose();

    private static CheckRules Auto(double freeGb, int processors = 16, int machine = 4) =>
        new(null, processors, machine, () => new MemoryReading((long)(freeGb * Gb), (long)(freeGb * Gb)));

    private static readonly HeavyGateNow Past = new("build", 1, Now);

    /// <summary>A check's ask: a check slot and a slot of the repository at <paramref name="checkout"/>, its first gate ended unless <paramref name="starting"/>.</summary>
    private HeavyAsk Check(string run, string? checkout = null, bool starting = false, int? declared = null)
    {
        var at = checkout ?? _fixture.Checkout;
        return new HeavyAsk(HeavySlot.KeyOf(at)!, "Daoris", declared,
            () => new HeavyHolder(Environment.ProcessId, 1, at, ["reached"], Now, null, null) { Run = run, Now = starting ? null : Past })
        {
            Check = () => new VerifyHolder(Environment.ProcessId, 1, run, "i-" + run, "Daoris", Now),
        };
    }

    /// <summary>A session's ask: a slot of the repository at <paramref name="checkout"/> alone, no check slot.</summary>
    private static HeavyAsk Session(string session, string checkout, bool starting = false, int? declared = null) =>
        new(HeavySlot.KeyOf(checkout)!, "engine", declared,
            () => new HeavyHolder(Environment.ProcessId, 1, checkout, ["build"], Now, session, null) { Now = starting ? null : Past });

    private (HeavyTaken? Taken, CheckWait? Wait) Take(CheckRules rules, HeavyAsk ask) => CheckAdmission.TryTake(_fixture.Home, rules, ask, (_, _) => true);

    [Fact]
    public void With_memory_for_two_two_checks_run_at_once_and_the_third_waits_for_a_slot()
    {
        // Eight processors: two checks at most; 20 GB free admits a second at 8 GB.
        var rules = new CheckRules(null, 8, 4, () => new MemoryReading(20 * Gb, 20 * Gb));

        var (first, _) = Take(rules, Check("r-1"));
        var (second, _) = Take(rules, Check("r-2", _fixture.Tree));
        var (third, wait) = Take(rules, Check("r-3", _fixture.Other));

        using (first)
        using (second)
        {
            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Null(third);
            Assert.Equal(CheckWait.Slots, wait!.Code);
            Assert.Equal("a slot: 2 of 2 run", wait.Sentence);
            Assert.Equal(2, VerifySlot.Look(_fixture.Home, (_, _) => true).Holders.Count);
            Assert.Equal(2, CheckAdmission.Holding(_fixture.Home, (_, _) => true).Count);
        }
    }

    [Fact]
    public void With_memory_for_one_the_first_runs_and_the_next_waits_for_memory_saying_how_much_is_free_and_needed()
    {
        var (first, _) = Take(Auto(4.5), Check("r-1"));
        var (second, wait) = Take(Auto(4.5), Check("r-2", _fixture.Tree));

        using (first)
        {
            Assert.NotNull(first);
            Assert.Null(second);
            Assert.Equal(CheckWait.Memory, wait!.Code);
            Assert.Equal("4.5", wait.Values["free"]);
            Assert.Equal("8", wait.Values["need"]);
            Assert.Equal("memory: 4.5 GB free, a check needs 8 GB", wait.Sentence);
        }
    }

    /// <summary>🔴 A reading just short of the need says so, floored: never "6.0 GB free, needs 6 GB".</summary>
    [Fact]
    public void Free_memory_just_short_of_the_need_is_floored_never_said_as_the_need()
    {
        var wait = CheckAdmission.Judge(_fixture.Home, [new HeavyHeld(Now, "k", false, "the verification run r-1")], Auto(7.99), Check("r-2", _fixture.Other));

        Assert.Equal("memory: 7.9 GB free, a check needs 8 GB", wait!.Sentence);
    }

    /// <summary>🔴 A run whose first gate has not ended counts its need again, so two never climb to their peaks on one reading.</summary>
    [Fact]
    public void A_run_still_in_its_first_gate_counts_its_need_again()
    {
        var (first, _) = Take(Auto(12), Check("r-1", starting: true));
        var (second, wait) = Take(Auto(12), Check("r-2", _fixture.Tree));

        using (first)
        {
            Assert.Null(second);
            Assert.Equal("16", wait!.Values["need"]);
            Assert.Equal("memory: 12.0 GB free, a check needs 16 GB with 1 still starting", wait.Sentence);
        }
    }

    [Fact]
    public void A_holder_starts_until_its_first_gate_ends()
    {
        var holder = new HeavyHolder(1, 1, "c", ["a", "b"], Now, null, null);

        Assert.True(CheckAdmission.Starting(holder));
        Assert.True(CheckAdmission.Starting(holder with { Now = new HeavyGateNow("a", 0, Now) }));
        Assert.False(CheckAdmission.Starting(holder with { Now = new HeavyGateNow("b", 1, Now) }));
        Assert.False(CheckAdmission.Starting(holder with { Now = new HeavyGateNow("a", 0, Now), Failed = new HeavyGateFailed("a", 1) }));
    }

    /// <summary>🔴 A pinned number is that many whatever the memory, and never more than the machine's admission holds.</summary>
    [Fact]
    public void A_pinned_number_is_that_many_whatever_the_memory_within_the_machine_s_admission()
    {
        var one = new CheckRules(1, 64, 6, () => new MemoryReading(100 * Gb, 100 * Gb));
        var (first, _) = Take(one, Check("r-1"));
        var (second, wait) = Take(one, Check("r-2", _fixture.Other));
        using (first)
        {
            Assert.Null(second);
            Assert.Equal("a slot: 1 of 1 run", wait!.Sentence);
        }

        File.Delete(RoomLedger.PathOf(_fixture.Home));
        var eight = new CheckRules(8, 4, 2, () => new MemoryReading(1 * Gb, 1 * Gb));
        Assert.Equal(2, eight.Slots);
        var taken = new[] { Take(eight, Check("a")).Taken, Take(eight, Check("b", _fixture.Tree)).Taken };
        try
        {
            Assert.All(taken, Assert.NotNull);
            Assert.Equal("a slot: 2 of 2 run", Take(eight, Check("c", _fixture.Other)).Wait!.Sentence);
        }
        finally
        {
            foreach (var slot in taken) slot?.Dispose();
        }
    }

    /// <summary>🔴 Memory that cannot be read admits one, and says the slots as held: never "2 of 1".</summary>
    [Fact]
    public void Memory_that_cannot_be_read_admits_one_run_as_before()
    {
        var unread = new CheckRules(null, 32, 6, () => null);
        var (first, _) = Take(unread, Check("r-1"));
        var (second, wait) = Take(unread, Check("r-2", _fixture.Tree));

        using (first)
        {
            Assert.NotNull(first);
            Assert.Null(second);
            Assert.Equal("a slot: 1 of 1 run", wait!.Sentence);
        }

        var two = new[] { new HeavyHeld(Now, "a", false, "x"), new HeavyHeld(Now, "b", false, "y") };
        Assert.Equal("a slot: 2 of 2 run", CheckAdmission.Judge(_fixture.Home, two, unread, Check("r-3", _fixture.Other))!.Sentence);
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
    public void A_slot_let_go_is_taken_again_and_its_records_go_with_it()
    {
        var (first, _) = Take(Auto(20), Check("r-1"));
        first!.Dispose();

        Assert.Empty(CheckAdmission.Holding(_fixture.Home, (_, _) => true));
        using var again = Take(Auto(20), Check("r-2")).Taken;
        Assert.NotNull(again);
        Assert.Equal(["r-2"], VerifySlot.Look(_fixture.Home, (_, _) => true).Holders.Select(holder => holder.Run));
        Assert.Equal("r-2", HeavySlot.Look(_fixture.Home, HeavySlot.KeyOf(_fixture.Checkout)!, (_, _) => true).Holders.Single().Run);
    }

    /// <summary>
    /// 🔴 One admission sees every heavy run: a session's heavy run in another repository holds a place in it, so with one slot a check
    /// waits behind it; a check holding both its slots is counted once.
    /// </summary>
    [Fact]
    public void A_session_s_heavy_run_in_another_repository_counts_against_a_check_and_a_check_counts_once()
    {
        var two = new CheckRules(2, 16, 4, () => null);
        var (session, _) = Take(two, Session("s-1", _fixture.Other));
        var (check, _) = Take(two, Check("r-1"));
        using (session)
        using (check)
        {
            Assert.NotNull(session);
            Assert.NotNull(check);
            Assert.Null(session!.Check);
            Assert.Equal(2, CheckAdmission.Holding(_fixture.Home, (_, _) => true).Count);
            Assert.Equal("a slot: 2 of 2 run", Take(two, Check("r-2", _fixture.Tree)).Wait!.Sentence);
        }
    }

    /// <summary>🔴 A repository that says one at a time (<c>heavySlots</c> 1) runs its heavy runs one after another, whatever the machine admits.</summary>
    [Fact]
    public void A_repository_that_declares_one_slot_runs_one_heavy_run_at_a_time()
    {
        var plenty = new CheckRules(4, 16, 4, () => null);

        var (first, _) = Take(plenty, Check("r-1", declared: 1));
        var (second, wait) = Take(plenty, Check("r-2", declared: 1));
        var (other, _) = Take(plenty, Session("s-1", _fixture.Other));
        using (first)
        using (other)
        {
            Assert.NotNull(first);
            Assert.Null(second);
            Assert.Equal(CheckWait.Repository, wait!.Code);
            Assert.Equal("the verification run r-1", wait.Sentence);
            Assert.NotNull(other);
        }
    }

    [Fact]
    public void A_lock_timeout_at_the_admission_says_the_lock_is_held_not_a_slot_count()
    {
        var path = CheckAdmission.AdmittingPath(_fixture.Home);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var holding = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        var (taken, wait) = Take(Auto(12), Check("r-1"));

        Assert.Null(taken);
        Assert.Equal(CheckWait.Admitting, wait!.Code);
        Assert.Equal("the admission's lock, which another start holds", wait.Sentence);
        Assert.Empty(VerifySlot.Look(_fixture.Home, (_, _) => true).Holders);
    }

    /// <summary>
    /// 🔴 Two starts at once never both see no holder: with memory for one, eight takes racing on eight threads admit exactly one, the
    /// judgement, the take and the record made under the admission's one lock.
    /// </summary>
    [Fact]
    public async Task Racing_takes_with_memory_for_one_admit_exactly_one()
    {
        var keys = new[] { _fixture.Checkout, _fixture.Tree, _fixture.Other };
        using var start = new ManualResetEventSlim();
        var takes = Enumerable.Range(0, 8).Select(index => Task.Run(() =>
        {
            start.Wait();
            return Take(Auto(12), Check($"r-{index}", keys[index % keys.Length], starting: true)).Taken;
        })).ToList();
        start.Set();
        var taken = await Task.WhenAll(takes).WaitAsync(Poll.Patience);
        try
        {
            Assert.Single(taken, one => one is not null);
        }
        finally
        {
            foreach (var one in taken) one?.Dispose();
        }
    }
}
