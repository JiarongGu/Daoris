using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The room's pure rules: what the person's own work is measured to take back, the concurrency's backing off and growing back,
/// admission by what is free now against a learned need, and a fair share by project and agent. Fixture machines, no file read.
/// </summary>
public sealed class RoomJudgeTests
{
    private const long Gb = 1L << 30;

    private static readonly DateTimeOffset T0 = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyDictionary<string, DateTimeOffset> Nobody = new Dictionary<string, DateTimeOffset>();

    /// <summary>A machine of 64 GB whose person holds <paramref name="person"/> GB now and has swung <paramref name="swing"/> GB higher, with <paramref name="free"/> GB free.</summary>
    private static RoomView Machine(double free, double person = 10, double swing = 0, int limit = 8, int floorGb = 0, IReadOnlyDictionary<string, DateTimeOffset>? recent = null) =>
        new((long)(free * Gb), (long)(free * Gb), (long)floorGb * Gb, new PersonRoom((long)(person * Gb), (long)(swing * Gb), PersonMeasure.MarginOf(64 * Gb)), limit,
            RoomLimit.Start, 0.2, recent ?? Nobody, T0);

    private static RoomAsk Test(string project = "a", string? agent = null, double needGb = 6, string kind = LoadKind.Test, KindRule? rule = null) =>
        new(project, agent, kind, new LoadCost((long)(needGb * Gb), 4), NeedReading.Learned, rule ?? KindRule.Default(kind)) { Name = project };

    /// <summary>How many of the same ask the room admits one after another, each just started so its memory is still to come.</summary>
    private static (int Admitted, CheckWait? Then) Admits(RoomView view, RoomAsk ask, IReadOnlyList<RoomHold>? already = null, int? pinned = null)
    {
        var holds = (already ?? []).ToList();
        for (var count = 0; count < 64; count++)
        {
            if (RoomJudge.Decide(view, holds, ask, pinned) is { } wait) return (holds.Count - (already?.Count ?? 0), wait);
            holds.Add(new RoomHold(ask.Project, ask.Agent, ask.Kind, ask.Need, true, $"{ask.Project} run {count}"));
        }

        return (holds.Count, null);
    }

    [Fact]
    public void A_load_that_rises_and_falls_admits_more_runs_then_fewer_then_more_again()
    {
        // The same 40 GB free, the same learned 6 GB need: only what the person's own work is measured to swing by differs.
        var quiet = Admits(Machine(40, swing: 0), Test()).Admitted;
        var swinging = Admits(Machine(40, swing: 15), Test()).Admitted;
        var calmAgain = Admits(Machine(40, swing: 0), Test()).Admitted;

        Assert.Equal(6, quiet);
        Assert.Equal(3, swinging);
        Assert.Equal(quiet, calmAgain);
    }

    [Fact]
    public void Free_memory_now_against_the_learned_need_decides_not_a_fraction_of_the_machine()
    {
        // 25 GB free on a 64 GB machine: the 2026-11-24 default kept 21 GB and admitted nothing; the room admits what the free memory holds.
        var admitted = Admits(Machine(25, person: 30), Test(needGb: 6)).Admitted;

        Assert.True(admitted >= 3, $"{admitted} runs admitted into 25 GB free with a 6 GB need");
    }

    [Fact]
    public void A_lighter_learned_need_admits_what_the_declared_need_held_back()
    {
        var declared = LoadKind.Declared(LoadKind.Test).Memory / (double)Gb;

        Assert.Equal(0, Admits(Machine(9, swing: 0), Test(needGb: declared), [new RoomHold("a", null, LoadKind.Test, new LoadCost(8 * Gb, 4), false, "first")]).Admitted);
        Assert.Equal(2, Admits(Machine(9, swing: 0), Test(needGb: 3.3), [new RoomHold("a", null, LoadKind.Test, new LoadCost(3 * Gb, 4), false, "first")]).Admitted);
    }

    [Fact]
    public void The_first_run_asks_only_the_floor_so_a_machine_never_starves_with_none_of_daoris_s_runs_on_it()
    {
        Assert.Null(RoomJudge.Decide(Machine(2, person: 60, swing: 30), [], Test(), null));
        var held = RoomJudge.Decide(Machine(30, person: 20, floorGb: 40), [], Test(), null);

        Assert.Equal(CheckWait.Reserve, held!.Code);
        Assert.Equal("40", held.Values["reserve"]);
    }

    [Fact]
    public void The_persons_floor_holds_where_their_measured_headroom_is_less_and_headroom_raises_it_where_it_is_more()
    {
        // The setting is a floor: 20 GB where their own work swings 2 GB, their headroom (swing and margin) where it swings 25.
        var lowSwing = RoomJudge.Decide(Machine(24, swing: 2, floorGb: 20), [new RoomHold("a", null, LoadKind.Test, new LoadCost(Gb, 1), false, "r")], Test(), null);
        var highSwing = RoomJudge.Decide(Machine(24, swing: 25, floorGb: 20), [new RoomHold("a", null, LoadKind.Test, new LoadCost(Gb, 1), false, "r")], Test(), null);

        Assert.Equal("20", lowSwing!.Values["reserve"]);
        Assert.Equal("26.3", highSwing!.Values["reserve"]);
    }

    [Fact]
    public void A_pinned_checks_overrides_the_limit_the_need_and_the_measured_room_but_not_the_persons_floor()
    {
        var one = new[] { new RoomHold("a", null, LoadKind.Test, new LoadCost(Gb, 1), false, "r") };
        var backedOff = Machine(40, swing: 30, limit: 1);

        Assert.Equal(CheckWait.Slots, RoomJudge.Decide(backedOff, one, Test(), null)!.Code);
        Assert.Null(RoomJudge.Decide(backedOff, one, Test(), 4));
        Assert.Null(RoomJudge.Decide(Machine(3, swing: 30), one, Test(), 4));
        Assert.Equal(CheckWait.Reserve, RoomJudge.Decide(Machine(30, floorGb: 40), one, Test(), 4)!.Code);
    }

    [Fact]
    public void One_project_asking_for_many_runs_still_leaves_room_for_another_project()
    {
        // Project b has asked in the last minutes; a asks for run after run and is held to the limit less b's seat.
        var recent = new Dictionary<string, DateTimeOffset> { ["b"] = T0 - TimeSpan.FromSeconds(40) };
        var view = Machine(60, limit: 4, recent: recent);

        var (admitted, wait) = Admits(view, Test("a", needGb: 2));
        Assert.Equal(3, admitted);
        Assert.Equal(CheckWait.Repository, wait!.Code);
        Assert.Contains("a fair share", wait.Sentence);

        var holds = Enumerable.Range(0, admitted).Select(index => new RoomHold("a", null, LoadKind.Test, new LoadCost(2 * Gb, 1), false, $"a{index}")).ToList();
        Assert.Null(RoomJudge.Decide(view, holds, Test("b", needGb: 2), null));
    }

    [Fact]
    public void One_project_asking_for_many_environments_is_held_to_its_bound_and_leaves_room_for_another_projects_runs()
    {
        var environment = Test("work", "agent-1", needGb: 4, kind: LoadKind.Environment, rule: new KindRule(2, null, false));
        var view = Machine(60, limit: 4, recent: new Dictionary<string, DateTimeOffset> { ["game"] = T0 - TimeSpan.FromMinutes(1) });

        var (admitted, wait) = Admits(view, environment);
        Assert.Equal(2, admitted);
        Assert.Equal(CheckWait.Repository, wait!.Code);
        Assert.Contains("2 environments at once", wait.Sentence);

        var holds = Enumerable.Range(0, admitted).Select(index => new RoomHold("work", $"agent-{index}", LoadKind.Environment, new LoadCost(4 * Gb, 1), false, $"environment {index}")).ToList();
        Assert.Null(RoomJudge.Decide(view, holds, Test("game", needGb: 6), null));
    }

    [Fact]
    public void An_agent_holds_one_environment_by_default_never_ten()
    {
        var environment = Test("work", "agent-1", needGb: 4, kind: LoadKind.Environment, rule: new KindRule(5, 1, false));
        var held = new RoomHold("work", "agent-1", LoadKind.Environment, new LoadCost(4 * Gb, 1), false, "environment web");

        var wait = RoomJudge.Decide(Machine(60), [held], environment, null);

        Assert.Equal(CheckWait.Repository, wait!.Code);
        Assert.Contains("this agent's 1 environment", wait.Sentence);
        Assert.Null(RoomJudge.Decide(Machine(60), [held], environment with { Agent = "agent-2" }, null));
    }

    [Fact]
    public void One_agent_of_a_project_leaves_a_seat_for_each_other_agent_that_has_asked()
    {
        var recent = new Dictionary<string, DateTimeOffset> { ["a"] = T0, ["a|y"] = T0 - TimeSpan.FromSeconds(90), ["a|x"] = T0 };
        var view = Machine(60, limit: 4, recent: recent);

        var (admitted, wait) = Admits(view, Test("a", "x", needGb: 2));

        Assert.Equal(3, admitted);
        Assert.Contains("a fair share of the project's", wait!.Sentence);
        Assert.Null(RoomJudge.Decide(view, Enumerable.Range(0, 3).Select(index => new RoomHold("a", "x", LoadKind.Test, new LoadCost(2 * Gb, 1), false, $"x{index}")).ToList(), Test("a", "y", needGb: 2), null));
    }

    [Fact]
    public void A_seat_is_kept_only_for_a_project_that_has_asked_lately_so_a_lone_project_uses_the_whole_limit()
    {
        var stale = new Dictionary<string, DateTimeOffset> { ["b"] = T0 - TimeSpan.FromMinutes(5) };

        Assert.Equal(4, Admits(Machine(60, limit: 4, recent: stale), Test("a", needGb: 2)).Admitted);
    }

    [Fact]
    public void Concurrency_halves_on_pressure_at_most_once_in_thirty_seconds_and_grows_back_a_run_a_minute()
    {
        var limit = RoomLimit.Initial(4);
        limit = RoomPressure.Next(limit, null, 4, T0);
        Assert.Equal(4, limit.Limit);

        limit = RoomPressure.Next(limit, RoomLimit.Memory, 4, T0 + TimeSpan.FromSeconds(10));
        Assert.Equal((2, RoomLimit.Memory), (limit.Limit, limit.Signal));
        Assert.Equal(2, RoomPressure.Next(limit, RoomLimit.Cpu, 4, T0 + TimeSpan.FromSeconds(20)).Limit);
        limit = RoomPressure.Next(limit, RoomLimit.Cpu, 4, T0 + TimeSpan.FromSeconds(45));
        Assert.Equal(1, limit.Limit);
        Assert.Equal(1, RoomPressure.Next(limit, RoomLimit.Timeouts, 4, T0 + TimeSpan.FromSeconds(100)).Limit);

        // Pressure last seen at +100 s: a minute of none adds one, another minute another, to the ceiling and no further.
        limit = RoomPressure.Next(limit, RoomLimit.Timeouts, 4, T0 + TimeSpan.FromSeconds(100));
        limit = RoomPressure.Next(limit, null, 4, T0 + TimeSpan.FromSeconds(130));
        Assert.Equal(1, limit.Limit);
        limit = RoomPressure.Next(limit, null, 4, T0 + TimeSpan.FromSeconds(161));
        Assert.Equal((2, RoomLimit.Clear), (limit.Limit, limit.Signal));
        limit = RoomPressure.Next(limit, null, 4, T0 + TimeSpan.FromSeconds(222));
        limit = RoomPressure.Next(limit, null, 4, T0 + TimeSpan.FromSeconds(283));
        limit = RoomPressure.Next(limit, null, 4, T0 + TimeSpan.FromSeconds(344));
        Assert.Equal(4, limit.Limit);
    }

    [Fact]
    public void The_signals_are_free_memory_under_what_is_kept_the_processors_saturated_and_a_run_whose_tests_timed_out()
    {
        var limit = RoomLimit.Initial(4) with { PressureAt = T0 };
        var calm = Machine(40, swing: 0);

        Assert.Null(RoomLedger.SignalOf(calm, null, limit));
        Assert.Equal(RoomLimit.Memory, RoomLedger.SignalOf(Machine(0.5, swing: 5), null, limit));
        Assert.Equal(RoomLimit.Cpu, RoomLedger.SignalOf(calm with { Busy = 0.95 }, null, limit));
        Assert.Equal(RoomLimit.Timeouts, RoomLedger.SignalOf(calm, T0 + TimeSpan.FromSeconds(5), limit));
        Assert.Null(RoomLedger.SignalOf(calm, T0 - TimeSpan.FromSeconds(5), limit));
    }

    [Fact]
    public void The_person_s_headroom_is_how_far_their_use_has_stood_above_its_present_in_the_last_ten_minutes_and_a_margin()
    {
        RoomSample At(int minutes, double used, double daoris = 0) => new(T0 - TimeSpan.FromMinutes(minutes), (long)(used * Gb), (long)(daoris * Gb), null);
        var samples = new[] { At(14, 55), At(8, 24, 4), At(5, 40, 2), At(1, 30, 4) };

        var room = PersonMeasure.Person(samples, T0, 64 * Gb);

        // Use outside Daoris: 20, 38, 26 over the window (the 14-minute-old 55 is out of it): now 26, up to 38.
        Assert.Equal(26 * Gb, room.Person);
        Assert.Equal(12 * Gb, room.Swing);
        Assert.Equal(PersonMeasure.MarginOf(64 * Gb), room.Margin);
        Assert.Equal(room.Swing + room.Margin, room.Headroom);
        Assert.Equal(1L << 30, PersonMeasure.MarginOf(10 * Gb));
        Assert.Equal(PersonMeasure.MarginOf(64 * Gb), PersonMeasure.Person([], T0, 64 * Gb).Headroom);
    }

    [Fact]
    public void Daoris_s_own_share_is_never_the_persons()
    {
        var samples = new[] { new RoomSample(T0, 40 * Gb, 30 * Gb, null) };

        Assert.Equal(10 * Gb, PersonMeasure.Person(samples, T0, 64 * Gb).Person);
    }

    [Fact]
    public void The_processors_busy_is_the_mean_of_the_last_ten_seconds_only()
    {
        var samples = new[]
        {
            new RoomSample(T0 - TimeSpan.FromSeconds(30), 0, 0, 0.1),
            new RoomSample(T0 - TimeSpan.FromSeconds(8), 0, 0, 0.9),
            new RoomSample(T0 - TimeSpan.FromSeconds(3), 0, 0, 1.0),
        };

        Assert.Equal(0.95, PersonMeasure.Busy(samples, T0)!.Value, 3);
        Assert.Null(PersonMeasure.Busy([], T0));
    }

    [Fact]
    public void A_gates_learned_need_is_its_largest_recent_peak_a_tenth_over_after_two_runs_and_declared_before()
    {
        RunSample Run(int day, double peakGb, double cores) => new(T0 + TimeSpan.FromDays(day), (long)(peakGb * Gb), cores, 100, false);
        var declared = LoadKind.Declared(LoadKind.Test);

        Assert.Equal((declared, NeedReading.DeclaredSource), (LearnedNeeds.Need([], declared).Cost, LearnedNeeds.Need([], declared).Source));
        Assert.Equal(NeedReading.DeclaredSource, LearnedNeeds.Need([Run(1, 3, 2)], declared).Source);

        var learned = LearnedNeeds.Need([Run(1, 3, 2), Run(2, 3.4, 4)], declared);
        Assert.Equal(NeedReading.Learned, learned.Source);
        Assert.Equal((long)((long)(3.4 * Gb) * 1.1), learned.Cost.Memory);
        Assert.Equal(3.0, learned.Cost.Cores);

        // A gate that got lighter is read as lighter once its heavy runs are older than the newest five.
        var lighter = LearnedNeeds.Need([Run(1, 12, 8), .. Enumerable.Range(2, 5).Select(day => Run(day, 2, 2))], declared);
        Assert.Equal((long)(2 * Gb * 1.1), lighter.Cost.Memory);
    }
}
