using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The room through the admission's files: a gate's learned need replacing its declared one after its first runs, the person's
/// share and the pressure signals measured from a machine whose load rises and falls, and the environments agents of one repository
/// share before starting. Files only, as the admission's own tests; the machine is a fixture whose clock, memory and processors move.
/// </summary>
public sealed class RoomAdmissionTests : IDisposable
{
    private const long Gb = 1L << 30;

    private static readonly DateTimeOffset Fixed = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);

    private readonly HeavyFixture _fixture = new("room-admission");

    public void Dispose() => _fixture.Dispose();

    /// <summary>A machine of 64 GB whose memory, clock, processors and Daoris's share a test moves.</summary>
    private sealed class FixtureMachine
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);

        public double FreeGb { get; set; } = 40;

        public double DaorisGb { get; set; }

        public double Busy { get; set; } = 0.1;

        private long _busyTicks;
        private long _totalTicks;

        /// <summary>The machine ten seconds on, its processors <see cref="Busy"/> for them.</summary>
        public void Advance(int seconds = 10)
        {
            Now += TimeSpan.FromSeconds(seconds);
            _totalTicks += seconds * 1000;
            _busyTicks += (long)(seconds * 1000 * Busy);
        }

        public CheckRules Rules(int? pinned = null, int? floorGb = null) =>
            new(pinned, 16, 4, () => new MemoryReading((long)(FreeGb * Gb), (long)(FreeGb * Gb), 64 * Gb), floorGb)
            {
                World = new RoomWorld(() => Now, () => new CpuTicks(_busyTicks, _totalTicks), _ => (long)(DaorisGb * Gb)),
            };
    }

    private HeavyAsk Check(string run, string? checkout = null, string? agent = null)
    {
        var at = checkout ?? _fixture.Checkout;
        return new HeavyAsk(HeavySlot.KeyOf(at)!, "Daoris", null,
            () => new HeavyHolder(Environment.ProcessId, 1, at, ["reached"], Fixed, agent, null) { Run = run, Now = new HeavyGateNow("build", 1, Fixed) })
        {
            Check = () => new VerifyHolder(Environment.ProcessId, 1, run, "i-" + run, "Daoris", Fixed),
            Gates = ["reached"],
            Agent = agent,
        };
    }

    private (HeavyTaken? Taken, CheckWait? Wait) Take(FixtureMachine machine, HeavyAsk ask, int? pinned = null) =>
        CheckAdmission.TryTake(_fixture.Home, machine.Rules(pinned), ask, (_, _) => true);

    private void Learn(string checkout, params double[] peaksGb)
    {
        var key = LearnedNeeds.KeyOf(HeavySlot.KeyOf(checkout)!, LoadKind.Test, "reached");
        foreach (var peak in peaksGb) LearnedNeeds.Record(_fixture.Home, key, new RunSample(Fixed, (long)(peak * Gb), 3, 120, false));
    }

    [Fact]
    public void A_gates_learned_need_replaces_the_declared_one_after_its_first_runs()
    {
        // 9 GB free, one run holding: the declared 8 GB test need and the 1.3 GB measured margin do not fit; the learned 3.3 GB does.
        var machine = new FixtureMachine { FreeGb = 9 };
        using var first = Take(machine, Check("r-1")).Taken;
        var (declared, wait) = Take(machine, Check("r-2", _fixture.Other));
        Assert.Null(declared);
        Assert.Equal(CheckWait.Reserve, wait!.Code);

        Learn(_fixture.Other, 3.0);
        Assert.Null(Take(machine, Check("r-2", _fixture.Other)).Taken);

        Learn(_fixture.Other, 3.0);
        using var learned = Take(machine, Check("r-2", _fixture.Other)).Taken;
        Assert.NotNull(learned);
    }

    [Fact]
    public void The_persons_swings_measured_from_the_machine_are_kept_free_and_forgotten_after_ten_minutes()
    {
        var machine = new FixtureMachine { FreeGb = 40 };
        using var first = Take(machine, Check("r-1")).Taken;

        // Their own work climbs from 24 GB to 50 GB in use (64 - free) and falls back: at its present, the swing is what it stood above.
        foreach (var free in new[] { 40.0, 30.0, 14.0, 30.0 })
        {
            machine.FreeGb = free;
            machine.Advance();
            RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4, running: 2);
        }

        var view = RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4, running: 2);
        Assert.Equal(34 * Gb, view.Person.Person);
        Assert.Equal(16 * Gb, view.Person.Swing);
        Assert.Equal("headroom", view.KeepBy);

        machine.Advance(11 * 60);
        Assert.Equal(0, RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4, running: 2).Person.Swing);
    }

    [Fact]
    public void A_machine_whose_load_rises_and_falls_admits_fewer_runs_then_more()
    {
        var machine = new FixtureMachine { FreeGb = 50, Busy = 0.2 };
        machine.Advance();
        var checkouts = new[] { _fixture.Checkout, _fixture.Other };
        int Admitted()
        {
            var taken = new List<HeavyTaken>();
            try
            {
                for (var run = 0; run < 6; run++)
                {
                    var (one, _) = Take(machine, Check($"r-{run}", checkouts[run % 2], $"agent-{run}"));
                    if (one is null) break;
                    taken.Add(one);
                }

                return taken.Count;
            }
            finally
            {
                foreach (var one in taken) one.Dispose();
            }
        }

        var calm = Admitted();

        // Their own work takes 58 GB: free memory falls to 6, under any run's need, so only the first run (the floor apart) is admitted.
        machine.FreeGb = 6;
        machine.Advance();
        var busy = Admitted();

        machine.FreeGb = 50;
        machine.Advance(11 * 60);
        var calmAgain = Admitted();

        Assert.True(busy < calm, $"{busy} runs admitted beside heavy use of their own, {calm} when it was calm");
        Assert.Equal(calm, calmAgain);
        Assert.True(calm >= 2, $"{calm} runs admitted into 50 GB free");
    }

    [Fact]
    public void Saturated_processors_halve_the_limit_and_it_grows_back_as_the_pressure_clears()
    {
        var machine = new FixtureMachine { FreeGb = 50, Busy = 0.97 };
        machine.Advance();
        RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4, running: 2);
        machine.Advance();
        var backedOff = RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4, running: 2);
        Assert.Equal((2, RoomLimit.Cpu), (backedOff.Limit, backedOff.Signal));

        machine.Busy = 0.2;
        for (var look = 0; look < 14; look++)
        {
            machine.Advance(10);
            RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4, running: 2);
        }

        var grown = RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4, running: 2);
        Assert.True(grown.Limit > backedOff.Limit, $"the limit stayed at {grown.Limit} after the processors cleared");
    }

    [Fact]
    public void A_run_whose_tests_timed_out_under_load_halves_the_limit_once()
    {
        var machine = new FixtureMachine { FreeGb = 50 };
        machine.Advance();
        RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4, running: 2);
        LearnedNeeds.Record(_fixture.Home, "k|test|reached", new RunSample(machine.Now + TimeSpan.FromSeconds(1), 3 * Gb, 2, 100, true));
        machine.Advance(40);

        var after = RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4, running: 2);
        Assert.Equal((2, RoomLimit.Timeouts), (after.Limit, after.Signal));

        machine.Advance(40);
        Assert.Equal(2, RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4, running: 2).Limit);
    }

    [Fact]
    public void A_run_is_kept_from_its_job_as_a_sample_and_a_failed_runs_timeout_words_say_so()
    {
        Assert.True(RunMeter.TimedOut(true, "FAIL x\n  Test timed out in 10000ms."));
        Assert.True(RunMeter.TimedOut(true, "Error: timeout of 5000ms exceeded"));
        Assert.False(RunMeter.TimedOut(false, "Test timed out in 10000ms."));
        Assert.False(RunMeter.TimedOut(true, "FAIL x\n  expected 3, got 4"));
        Assert.False(RunMeter.TimedOut(true, "fatal: unable to access the remote: connection timed out"));
        Assert.False(RunMeter.TimedOut(true, "it ran past its bound and was timed out by the driver"));
        Assert.False(RunMeter.TimedOut(true, null));
    }

    [Fact]
    public void One_run_keeping_the_processors_busy_is_no_pressure_and_a_quiet_second_ask_is_admitted()
    {
        var machine = new FixtureMachine { FreeGb = 50, Busy = 0.97 };
        machine.Advance();
        using var first = Take(machine, Check("r-1")).Taken;
        machine.Advance();
        RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4, running: 1);
        machine.Advance();

        var view = RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4, running: 1);
        using var second = Take(machine, Check("r-2", _fixture.Other)).Taken;

        Assert.Equal(4, view.Limit);
        Assert.NotNull(second);
    }

    [Fact]
    public void A_terminals_world_reads_the_room_but_takes_no_sample_of_the_persons_share()
    {
        var machine = new FixtureMachine { FreeGb = 40 };
        machine.Advance();
        var reading = machine.Rules() with { World = machine.Rules().World with { Samples = false } };
        RoomLedger.Observe(_fixture.Home, reading, "k", null, 4);
        Assert.Empty(RoomLedger.Read(_fixture.Home)!.Samples);

        RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4);
        Assert.Single(RoomLedger.Read(_fixture.Home)!.Samples);
    }

    [Fact]
    public void The_row_shows_the_limit_a_look_now_would_leave_and_a_pressure_older_than_the_calm_span_as_cleared()
    {
        var machine = new FixtureMachine { FreeGb = 40, Busy = 0.97 };
        machine.Advance();
        RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4, running: 2);
        machine.Advance();
        RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4, running: 2);
        Assert.Equal((2, RoomSignals.Cpu), RoomWhysAt(machine.Now));

        machine.Advance(30 * 60);
        var (limit, signal) = RoomWhysAt(machine.Now);
        Assert.Equal((3, RoomSignals.Clear), (limit, signal));
    }

    private (int Limit, string Signal) RoomWhysAt(DateTimeOffset now)
    {
        var why = RoomWhys.Read(_fixture.Home, new MemoryReading(40 * Gb, 40 * Gb, 64 * Gb), null, now, 4);
        return (why.Limit, why.Signal);
    }

    [Fact]
    public void What_the_reserve_row_says_is_read_from_the_same_files_the_admission_writes()
    {
        var machine = new FixtureMachine { FreeGb = 40 };
        machine.Advance();
        RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4, running: 2);
        Learn(_fixture.Checkout, 5, 6);

        var why = RoomWhys.Read(_fixture.Home, machine.Rules().Memory(), 8, machine.Now, 4);

        Assert.Equal("floor", why.KeepBy);
        Assert.Equal(8 * Gb, why.Keep);
        Assert.Equal((4, 4), (why.Limit, why.Ceiling));
        Assert.Equal((NeedReading.Learned, 2), (why.NeedSource, why.NeedRuns));
        Assert.Equal((long)(6 * Gb * 1.1), why.Need);
        Assert.Contains("kept by the floor", ReserveVerb.Said(why).First());
    }
}
