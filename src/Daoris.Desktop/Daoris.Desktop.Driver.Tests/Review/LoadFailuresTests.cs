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
        Assert.Equal("node tools/reached-checks.mjs --base main --tier fast --classes A,B", LoadFailures.Command("node tools/reached-checks.mjs --base main --tier fast", ["A", "B"]));
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
