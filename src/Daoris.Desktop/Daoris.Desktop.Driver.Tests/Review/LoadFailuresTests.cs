using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The judgement of a load failure: tests that timed out are told by their log, the machine's load by the admission's count and the free commit, a
/// quiet slot by no other run working and enough commit, and a load failure is asked again like a check that could not say (no round).
/// </summary>
public sealed class LoadFailuresTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private const long Roomy = 16L << 30;
    private const long Tight = 2L << 30;

    private static AdmissionEntry Entry(string id, string what = "driver gate", int minutes = 5) =>
        new(id, "session", 1, what, Now.AddMinutes(-minutes), Now, null, null, null);

    private static AdmissionLook Look(params AdmissionEntry[] holds) => new(3, holds, []);

    [Theory]
    [InlineData("FAIL src/signOut.test.tsx > shows it\nError: Test timed out in 10000ms.", true)]
    [InlineData("Error: Timeout of 5000 ms exceeded", true)]
    [InlineData("Reason: Test host process crashed\nData collector 'Blame' message: The specified inactivity time of 60 seconds has elapsed.", true)]
    [InlineData("AssertionError: expected 3 to be 4", false)]
    [InlineData("", false)]
    public void A_log_that_says_a_test_timed_out_is_one_and_an_assertion_failure_is_not(string log, bool timedOut) =>
        Assert.Equal(timedOut, LoadFailures.TimedOut(log));

    [Fact]
    public void Red_classes_are_run_again_by_the_names_the_reached_runner_takes_and_an_odd_name_is_never_put_in_a_command()
    {
        Assert.Equal(["signOut.test.tsx", "Daoris.Driver.Tests.X"], LoadFailures.Names(["vitest|src/web/signOut.test.tsx", "dotnet|Daoris.Driver.Tests.X"]));
        Assert.Null(LoadFailures.Names([]));
        Assert.Null(LoadFailures.Names(["vitest|a b; rm -rf x"]));
    }

    private const string Marker = "reached-classes: ";

    private static string Log(string failure, params string[] red) =>
        $"{failure}\n{Marker}{{\"ran\":3,\"red\":[{string.Join(',', red.Select(id => $"\"{id}\""))}]}}";

    [Fact]
    public void Classes_are_run_again_only_where_each_red_class_s_own_failure_is_a_timeout_or_the_host_was_ended_by_its_hang_bound()
    {
        var timeout = Log("FAIL X.Y > waits\nError: Test timed out in 10000ms.", "dotnet|X.Y");
        Assert.Equal(["X.Y"], LoadFailures.Timeouts(timeout, Marker));

        var assertion = Log("FAIL Z.W > slow\nError: Test timed out in 10000ms.\n\nFAIL X.Y > adds\nAssertionError: expected 1 to be 2", "dotnet|X.Y");
        Assert.Null(LoadFailures.Timeouts(assertion, Marker));

        var mixed = Log("FAIL X.Y > waits\nError: Test timed out in 10000ms.\n\nFAIL Q.R > adds\nAssertionError: expected 1 to be 2", "dotnet|X.Y", "dotnet|Q.R");
        Assert.Null(LoadFailures.Timeouts(mixed, Marker));

        var hung = Log("Data collector 'Blame' message: The specified inactivity time of 60 seconds has elapsed.\nThe test running when the crash occurred:\nX.Y.No_source", "dotnet|X.Y");
        Assert.Equal(["X.Y"], LoadFailures.Timeouts(hung, Marker));
        Assert.Null(LoadFailures.Timeouts("no outcome line, but a Test timed out in 10ms", Marker));
    }

    [Fact]
    public void A_reached_runner_runs_classes_again_only_by_a_command_its_declaration_names()
    {
        var declared = ReachedRunner.Of("""{ "reached": { "run": "x --base {base}", "rerun": "x --classes {classes} --base {base}" } }""")!;
        Assert.Equal("x --classes A,B --base main", declared.RerunFor(["A", "B"], "main"));
        Assert.Null(declared.RerunFor(["A"], "main; rm -rf /"));
        Assert.Null(ReachedRunner.Of("""{ "reached": { "run": "x --base {base}" } }""")!.RerunFor(["A"], "main"));
    }

    [Fact]
    public void The_machine_is_loaded_while_another_heavy_run_holds_a_place_or_the_free_commit_is_short_of_what_one_run_needs()
    {
        Assert.True(new LoadReading(1, Roomy).Loaded);
        Assert.True(new LoadReading(0, Tight).Loaded);
        Assert.False(new LoadReading(0, Roomy).Loaded);
        Assert.False(new LoadReading(0, null).Loaded);
        Assert.Equal("2 other heavy runs, 2.0 GB free commit", new LoadReading(2, Tight).Words);
    }

    [Fact]
    public void A_quiet_slot_is_one_no_other_working_run_holds_and_where_a_run_waiting_for_one_goes_by_the_order_it_asked_in()
    {
        var mine = Entry("a", $"verification {LoadFailures.Mark}", minutes: 5);
        Assert.True(LoadFailures.Quiet(Look(mine), mine, Roomy));
        Assert.False(LoadFailures.Quiet(Look(mine), mine, Tight));
        Assert.False(LoadFailures.Quiet(Look(mine, Entry("b")), mine, Roomy));

        var later = Entry("c", $"verification {LoadFailures.Mark}", minutes: 1);
        var earlier = Entry("d", $"verification {LoadFailures.Mark}", minutes: 9);
        Assert.True(LoadFailures.Quiet(Look(mine, later), mine, Roomy));
        Assert.False(LoadFailures.Quiet(Look(mine, earlier), mine, Roomy));
        Assert.True(LoadFailures.Quiet(Look(earlier, later), earlier, Roomy));
    }

    [Fact]
    public void A_load_failure_is_asked_again_like_a_check_that_could_not_say()
    {
        Assert.True(UnrunChecks.Is(VerifyResult.Load));
        Assert.False(UnrunChecks.Is(VerifyResult.Changed));
    }
}
