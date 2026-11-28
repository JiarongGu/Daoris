using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A need is learned per repository, kind and single gate and read by every tree of the repository; a run of several gates asks the largest
/// of its gates, never a sum; a wait says whether its need is learned or declared. Files only, as the admission's other tests.
/// </summary>
public sealed class LearnedGateNeedsTests : IDisposable
{
    private const long Gb = 1L << 30;

    private static readonly DateTimeOffset At = new(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);

    private readonly HeavyFixture _fixture = new("learned-gates");

    public void Dispose() => _fixture.Dispose();

    private static CheckRules Free(double gb) =>
        new(null, 16, 4, () => new MemoryReading((long)(gb * Gb), (long)(gb * Gb), 64 * Gb))
        {
            World = new RoomWorld(() => At, () => new CpuTicks(0, 0), _ => 0),
        };

    private void Learn(string checkout, string gate, params double[] peaksGb)
    {
        var key = LearnedNeeds.KeyOf(HeavySlot.KeyOf(checkout)!, LoadKind.Test, gate);
        for (var run = 0; run < peaksGb.Length; run++) LearnedNeeds.Record(_fixture.Home, key, new RunSample(At.AddMinutes(run), (long)(peaksGb[run] * Gb), 2, 60, false));
    }

    [Fact]
    public void Every_tree_of_a_repository_reads_one_history_and_another_repository_reads_its_own()
    {
        Assert.Equal(HeavySlot.KeyOf(_fixture.Checkout), HeavySlot.KeyOf(_fixture.Tree));
        Assert.NotEqual(HeavySlot.KeyOf(_fixture.Checkout), HeavySlot.KeyOf(_fixture.Other));

        Learn(_fixture.Checkout, "test", 3, 3);
        var learned = LearnedNeeds.Read(LearnedNeeds.PathOf(_fixture.Home));
        var declared = LoadKind.Declared(LoadKind.Test);

        Assert.Equal(NeedReading.Learned, LearnedNeeds.OfGates(learned, HeavySlot.KeyOf(_fixture.Tree)!, LoadKind.Test, ["test"], declared).Source);
        Assert.Equal(NeedReading.DeclaredSource, LearnedNeeds.OfGates(learned, HeavySlot.KeyOf(_fixture.Other)!, LoadKind.Test, ["test"], declared).Source);
    }

    [Fact]
    public void A_run_of_several_gates_needs_the_largest_of_its_gates_never_their_sum_and_a_gate_not_yet_run_counts_at_its_declared_cost()
    {
        Learn(_fixture.Checkout, "lint", 1, 1);
        Learn(_fixture.Checkout, "test", 6, 6);
        var learned = LearnedNeeds.Read(LearnedNeeds.PathOf(_fixture.Home));
        var key = HeavySlot.KeyOf(_fixture.Tree)!;
        var declared = LoadKind.Declared(LoadKind.Test);

        var both = LearnedNeeds.OfGates(learned, key, LoadKind.Test, ["lint", "test"], declared);
        Assert.Equal((NeedReading.Learned, (long)(6 * Gb * 1.1)), (both.Source, both.Cost.Memory));

        var unseen = LearnedNeeds.OfGates(learned, key, LoadKind.Test, ["lint", "build"], declared);
        Assert.Equal((NeedReading.DeclaredSource, declared.Memory), (unseen.Source, unseen.Cost.Memory));

        Assert.Equal(NeedReading.DeclaredSource, LearnedNeeds.OfGates(learned, key, LoadKind.Test, [], declared).Source);
    }

    [Fact]
    public void A_runs_gates_are_each_kept_under_their_own_key_with_the_peak_and_cores_they_had_when_they_ended()
    {
        var home = _fixture.Home;
        var key = HeavySlot.KeyOf(_fixture.Checkout)!;
        for (var run = 0; run < 2; run++)
        {
            var meter = new RunMeter(home, key, LoadKind.Test, () => At.AddMinutes(run));
            meter.Note("lint", 1 * Gb, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), whole: true, failed: false, output: null);
            meter.Note("test", 6 * Gb, TimeSpan.FromSeconds(70), TimeSpan.FromSeconds(60), whole: true, failed: false, output: null);
        }

        var lint = LearnedNeeds.Samples(home, LearnedNeeds.KeyOf(key, LoadKind.Test, "lint"));
        var test = LearnedNeeds.Samples(home, LearnedNeeds.KeyOf(key, LoadKind.Test, "test"));
        Assert.Equal([1 * Gb, 1 * Gb], lint.Select(sample => sample.PeakMemory));
        Assert.Equal([6 * Gb, 6 * Gb], test.Select(sample => sample.PeakMemory));

        // Cores are each gate's own: its CPU since the gate before, over its own wall time.
        Assert.All(lint, sample => Assert.Equal(1.0, sample.Cores));
        Assert.All(test, sample => Assert.Equal(1.0, sample.Cores));
        Assert.DoesNotContain(LearnedNeeds.Read(LearnedNeeds.PathOf(home)).Keys, name => name.Contains('+', StringComparison.Ordinal));
    }

    [Fact]
    public void A_gate_that_failed_without_a_timeout_teaches_nothing_and_one_that_timed_out_is_kept_as_a_signal()
    {
        var home = _fixture.Home;
        var key = HeavySlot.KeyOf(_fixture.Checkout)!;
        var meter = new RunMeter(home, key, LoadKind.Test, () => At);
        meter.Note("test", 6 * Gb, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60), whole: false, failed: true, output: "expected 3, got 4");
        Assert.Empty(LearnedNeeds.Samples(home, LearnedNeeds.KeyOf(key, LoadKind.Test, "test")));

        meter.Note("test", 6 * Gb, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60), whole: false, failed: true, output: "Test timed out in 10000ms");
        Assert.True(Assert.Single(LearnedNeeds.Samples(home, LearnedNeeds.KeyOf(key, LoadKind.Test, "test"))).TimedOut);
    }

    [Fact]
    public void A_session_running_gates_in_a_new_tree_is_admitted_on_what_each_gate_took_in_any_other_tree_and_the_wait_said_declared_until_then()
    {
        var other = new HeavyHeld(At, "another-repository", false, "another run") { Gates = ["test"] };
        HeavyAsk Ask() => new(HeavySlot.KeyOf(_fixture.Tree)!, "engine", null, () => new HeavyHolder(Environment.ProcessId, 1, _fixture.Tree, ["lint", "test"], At, "s-9", null))
        {
            Gates = ["lint", "test"],
        };

        // 9 GB free beside one run: the declared 8 GB of a gate that has never run does not fit.
        var before = CheckAdmission.Judge(_fixture.Home, [other], Free(9), Ask());
        Assert.Equal(CheckWait.Reserve, before!.Code);
        Assert.Equal(NeedReading.DeclaredSource, before.Values["source"]);

        // Each gate ran twice in the repository's root checkout, never together: the new tree's run of both is the larger of the two.
        Learn(_fixture.Checkout, "lint", 1, 1);
        Learn(_fixture.Checkout, "test", 3, 3);
        Assert.Null(CheckAdmission.Judge(_fixture.Home, [other], Free(9), Ask()));
    }

    [Fact]
    public void A_check_in_a_new_tree_is_admitted_on_its_measured_need_where_one_other_check_is_starting_and_a_wait_for_more_says_it_is_learned()
    {
        // 2026-11-27: the check's eight runs peaked between 4.3 and 9.5 GiB (a tenth over the newest five: 10.4 GiB).
        Learn(_fixture.Checkout, "reached", 4.303, 5.629, 4.810, 5.870, 7.321, 5.380, 6.180, 9.481);
        HeavyAsk Check(string run, string checkout) => new(HeavySlot.KeyOf(checkout)!, "Daoris", null,
            () => new HeavyHolder(Environment.ProcessId, 1, checkout, [], At, null, null) { Run = run })
        {
            Gates = ["reached"],
        };

        HeavyHeld Starting(string run) => new(At, HeavySlot.KeyOf(_fixture.Checkout), true, $"the verification run {run}") { Gates = ["reached"] };

        Assert.Null(CheckAdmission.Judge(_fixture.Home, [Starting("r-1")], Free(22.6), Check("r-2", _fixture.Tree)));

        var wait = CheckAdmission.Judge(_fixture.Home, [Starting("r-1"), Starting("r-3")], Free(22.6), Check("r-2", _fixture.Tree));
        Assert.Equal(CheckWait.Memory, wait!.Code);
        Assert.Equal("31.3", wait.Values["need"]);
        Assert.Equal(NeedReading.Learned, wait.Values["source"]);
        Assert.Contains("(learned)", wait.Sentence, StringComparison.Ordinal);
    }
}
