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
            Gate = "reached",
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
            RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4);
        }

        var view = RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4);
        Assert.Equal(34 * Gb, view.Person.Person);
        Assert.Equal(16 * Gb, view.Person.Swing);
        Assert.Equal("headroom", view.KeepBy);

        machine.Advance(11 * 60);
        Assert.Equal(0, RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4).Person.Swing);
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
        RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4);
        machine.Advance();
        var backedOff = RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4);
        Assert.Equal((2, RoomLimit.Cpu), (backedOff.Limit, backedOff.Signal));

        machine.Busy = 0.2;
        for (var look = 0; look < 14; look++)
        {
            machine.Advance(10);
            RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4);
        }

        var grown = RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4);
        Assert.True(grown.Limit > backedOff.Limit, $"the limit stayed at {grown.Limit} after the processors cleared");
    }

    [Fact]
    public void A_run_whose_tests_timed_out_under_load_halves_the_limit_once()
    {
        var machine = new FixtureMachine { FreeGb = 50 };
        machine.Advance();
        RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4);
        LearnedNeeds.Record(_fixture.Home, "k|test|reached", new RunSample(machine.Now + TimeSpan.FromSeconds(1), 3 * Gb, 2, 100, true));
        machine.Advance(40);

        var after = RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4);
        Assert.Equal((2, RoomLimit.Timeouts), (after.Limit, after.Signal));

        machine.Advance(40);
        Assert.Equal(2, RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4).Limit);
    }

    [Fact]
    public void A_run_is_kept_from_its_job_as_a_sample_and_a_failed_runs_timeout_words_say_so()
    {
        Assert.True(RunMeter.TimedOut(true, "FAIL x\n  Test timed out in 10000ms."));
        Assert.True(RunMeter.TimedOut(true, "Error: timeout of 5000ms exceeded"));
        Assert.False(RunMeter.TimedOut(false, "Test timed out in 10000ms."));
        Assert.False(RunMeter.TimedOut(true, "FAIL x\n  expected 3, got 4"));
        Assert.False(RunMeter.TimedOut(true, null));
    }

    [Fact]
    public void Agents_of_one_repository_share_one_running_environment_where_its_rule_says_reuse()
    {
        var machine = new FixtureMachine();
        var rule = new KindRule(2, 1, true);
        LeaseRequest Ask(string agent, int pid) => new(HeavySlot.KeyOf(_fixture.Checkout)!, "work", agent, LoadKind.Environment, "stack", pid, rule);

        var first = LoadLeases.Ask(_fixture.Home, machine.Rules(), Ask("agent-1", 101), _ => true);
        var second = LoadLeases.Ask(_fixture.Home, machine.Rules(), Ask("agent-2", 102), _ => true);
        var third = LoadLeases.Ask(_fixture.Home, machine.Rules(), Ask("agent-3", 103), _ => true);

        Assert.False(first.Joined);
        Assert.True(second.Joined);
        Assert.True(third.Joined);
        Assert.Equal(first.Lease!.Id, third.Lease!.Id);
        Assert.Single(LoadLeases.Live(_fixture.Home, _ => true));
        Assert.Equal(3, LoadLeases.Live(_fixture.Home, _ => true)[0].Users.Count);

        Assert.False(LoadLeases.Release(_fixture.Home, first.Lease.Id, new LeaseUser("agent-1", 101), alive: _ => true));
        Assert.False(LoadLeases.Release(_fixture.Home, first.Lease.Id, new LeaseUser("agent-2", 102), alive: _ => true));
        Assert.True(LoadLeases.Release(_fixture.Home, first.Lease.Id, new LeaseUser("agent-3", 103), alive: _ => true));
        Assert.Empty(LoadLeases.Live(_fixture.Home, _ => true));
    }

    [Fact]
    public void Where_the_rule_is_not_to_share_each_agent_starts_its_own_up_to_the_bound_and_no_more()
    {
        var machine = new FixtureMachine();
        LeaseRequest Ask(string agent, int pid, string name) => new(HeavySlot.KeyOf(_fixture.Checkout)!, "work", agent, LoadKind.Environment, name, pid, new KindRule(2, 1, false));

        var one = LoadLeases.Ask(_fixture.Home, machine.Rules(), Ask("agent-1", 101, "stack"), _ => true);
        var again = LoadLeases.Ask(_fixture.Home, machine.Rules(), Ask("agent-1", 101, "stack-2"), _ => true);
        var two = LoadLeases.Ask(_fixture.Home, machine.Rules(), Ask("agent-2", 102, "stack"), _ => true);
        var three = LoadLeases.Ask(_fixture.Home, machine.Rules(), Ask("agent-3", 103, "stack"), _ => true);

        Assert.NotNull(one.Lease);
        Assert.Null(again.Lease);
        Assert.Contains("this agent's 1 environment", again.Wait!.Sentence);
        Assert.NotNull(two.Lease);
        Assert.NotEqual(one.Lease.Id, two.Lease.Id);
        Assert.Null(three.Lease);
        Assert.Contains("2 environments at once", three.Wait!.Sentence);
    }

    [Fact]
    public void One_project_asking_for_many_environments_leaves_room_for_another_projects_checks()
    {
        var machine = new FixtureMachine { FreeGb = 60 };
        LeaseRequest Ask(int agent) => new(HeavySlot.KeyOf(_fixture.Checkout)!, "work", $"agent-{agent}", LoadKind.Environment, $"stack-{agent}", 100 + agent, new KindRule(null, 1, false));

        // The other project has asked lately; ten agents of the first ask an environment each.
        RoomLedger.Observe(_fixture.Home, machine.Rules(), HeavySlot.KeyOf(_fixture.Other)!, null, 4);
        var taken = Enumerable.Range(0, 10).Select(agent => LoadLeases.Ask(_fixture.Home, machine.Rules(), Ask(agent), _ => true)).Count(each => each.Lease is not null);

        Assert.InRange(taken, 1, 3);
        using var check = Take(machine, Check("r-1", _fixture.Other)).Taken;
        Assert.NotNull(check);
    }

    [Fact]
    public void A_lease_whose_every_user_has_ended_frees_its_place()
    {
        var machine = new FixtureMachine();
        var request = new LeaseRequest(HeavySlot.KeyOf(_fixture.Checkout)!, "work", "agent-1", LoadKind.Gpu, "gpu", 4242);
        Assert.NotNull(LoadLeases.Ask(_fixture.Home, machine.Rules(), request, _ => true).Lease);

        Assert.Single(LoadLeases.Live(_fixture.Home, _ => true));
        Assert.Empty(LoadLeases.Live(_fixture.Home, pid => pid != 4242));
        Assert.NotNull(LoadLeases.Ask(_fixture.Home, machine.Rules(), request with { Agent = "agent-2", Pid = 4343 }, pid => pid != 4242).Lease);
    }

    [Fact]
    public void A_released_environment_s_peak_is_kept_so_the_next_ask_is_judged_by_it()
    {
        var machine = new FixtureMachine();
        var request = new LeaseRequest(HeavySlot.KeyOf(_fixture.Checkout)!, "work", "agent-1", LoadKind.Environment, "stack", 101);
        var lease = LoadLeases.Ask(_fixture.Home, machine.Rules(), request, _ => true).Lease!;

        LoadLeases.Release(_fixture.Home, lease.Id, new LeaseUser("agent-1", 101), new RunSample(Fixed, 9 * Gb, 2, 600, false), _ => true);

        var kept = LearnedNeeds.Samples(_fixture.Home, LearnedNeeds.KeyOf(lease.Project, LoadKind.Environment, "stack"));
        Assert.Equal(9 * Gb, Assert.Single(kept).PeakMemory);
    }

    [Fact]
    public void What_the_reserve_row_says_is_read_from_the_same_files_the_admission_writes()
    {
        var machine = new FixtureMachine { FreeGb = 40 };
        machine.Advance();
        RoomLedger.Observe(_fixture.Home, machine.Rules(), "k", null, 4);
        Learn(_fixture.Checkout, 5, 6);

        var why = RoomWhy.Read(_fixture.Home, machine.Rules().Memory(), 8, machine.Now, 4);

        Assert.Equal("floor", why.KeepBy);
        Assert.Equal(8 * Gb, why.Keep);
        Assert.Equal((4, 4), (why.Limit, why.Ceiling));
        Assert.Equal((NeedReading.Learned, 2), (why.NeedSource, why.NeedRuns));
        Assert.Equal((long)(6 * Gb * 1.1), why.Need);
        Assert.Contains("kept by the floor", ReserveVerb.Said(why).First());
    }
}
