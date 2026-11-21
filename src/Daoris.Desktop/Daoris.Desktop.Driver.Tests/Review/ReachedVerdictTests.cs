using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A repository's declared reached runner: read from the declaration's <c>reached</c> entry, its command given the base it judges against,
/// and its exit and last outcome line judged into a check's verdict that names the classes run, replayed and red.
/// </summary>
public sealed class ReachedVerdictTests
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";
    private const string Declaration = """{ "gates": [], "reached": { "run": "node tools/reached-checks.mjs --base {base}", "outcome": "reached-classes: ", "clock": true } }""";

    private static VerifyResult Judge(int exit, string log) => ReachedVerdict.Judge("i-1", Commit, exit, log, "reached.log", "reached-classes: ");

    [Fact]
    public void A_declaration_s_reached_entry_reads_with_its_command_its_marker_and_its_clock()
    {
        var runner = ReachedRunner.Of(Declaration);

        Assert.NotNull(runner);
        Assert.Equal("node tools/reached-checks.mjs --base main", runner.CommandFor("main"));
        Assert.Equal("reached-classes: ", runner.Outcome);
        Assert.True(runner.Clock);
    }

    [Fact]
    public void A_declaration_without_a_reached_entry_or_a_command_declares_none()
    {
        Assert.Null(ReachedRunner.Of("""{ "gates": [] }"""));
        Assert.Null(ReachedRunner.Of("""{ "reached": { "outcome": "x" } }"""));
        Assert.Null(ReachedRunner.Of("not json"));
        Assert.Null(ReachedRunner.Of(null));
    }

    [Fact]
    public void A_base_that_is_not_a_plain_ref_is_never_written_into_the_command()
    {
        var runner = ReachedRunner.Of(Declaration)!;

        Assert.Equal("node tools/reached-checks.mjs --base origin/main", runner.CommandFor("origin/main"));
        Assert.Null(runner.CommandFor("main; rm -rf x"));
        Assert.Null(runner.CommandFor(""));
    }

    [Fact]
    public void A_clean_run_is_reached_and_says_the_classes_run_and_replayed_with_its_flakes_named()
    {
        var result = Judge(0, """reached-classes: {"ran":3,"replayed":5,"flaked":["dotnet|A.B"],"mainRed":[],"red":[]}""");

        Assert.Equal(VerifyVerdicts.Reached, result.Verdict);
        Assert.Contains("3 classes run, 5 replayed, 0 red", result.Why);
        Assert.Contains("dotnet|A.B", result.Why);
        Assert.Equal("pass", Assert.Single(result.Gates).Verdict);
    }

    [Fact]
    public void A_clean_run_with_no_outcome_line_reached_nothing_and_is_reached()
    {
        var result = Judge(0, "reached: nothing reached\n");

        Assert.Equal(VerifyVerdicts.Reached, result.Verdict);
        Assert.Contains("0 classes run", result.Why);
    }

    [Fact]
    public void A_red_class_fails_the_check_naming_the_runner_and_the_class()
    {
        var result = Judge(1, """reached-classes: {"ran":2,"replayed":0,"flaked":[],"mainRed":[],"red":["dotnet|X.Y"]}""");

        Assert.Equal(VerifyVerdicts.Failed, result.Verdict);
        Assert.Equal(ReachedVerdict.Gate, result.Failed);
        Assert.Contains("dotnet|X.Y", result.Why);
        Assert.Equal("fail", Assert.Single(result.Gates).Verdict);
    }

    [Fact]
    public void A_tool_error_exit_is_unknown_and_never_a_failure_blaming_a_class()
    {
        var result = Judge(2, "reached-checks: boom\n");

        Assert.Equal(VerifyVerdicts.Unknown, result.Verdict);
        Assert.Contains("exited 2", result.Why);
    }

    [Fact]
    public void A_hand_back_s_reached_command_whose_plan_held_Process_classes_says_it_ran_the_fast_tier_replayed_and_owes_the_heavy()
    {
        const string log = "reached: 3 heavy-tier classes deferred to the run after the landing (driver-process 2, modules-process 1)\n"
            + """reached-classes: {"ran":12,"replayed":40,"flaked":[],"mainRed":[],"red":[],"deferred":["driver-process|A","driver-process|B","modules-process|C"]}""" + "\n";

        Assert.Equal(
            "the reached classes: 12 run, 40 replayed from passes of the same inputs, 0 red; 3 heavy-tier classes owed after the landing (driver-process 2, modules-process 1)",
            ReachedVerdict.Told(log, "reached-classes: "));
        Assert.Null(ReachedVerdict.Told("no outcome line\n", "reached-classes: "));
    }

    /// <summary>The install's red of 2026-11-20 (run 5c0cb7bb0ac4), cut down: three flakes and one class red again alone, as vitest printed it.</summary>
    private const string RedLog = """
        reached: web-unit: node node_modules/vitest/vitest.mjs run …
         FAIL  src/locales/names.test.ts > the real glossary > hold the role
        Error: expected 角色
         FAIL  src/app/listDoors.test.tsx > the list's doors > lays a strip
        Error: Test timed out in 10000ms.

        reached: web-unit: listDoors.test.tsx: failed test 'the list's doors lays a strip'
        reached: web-unit: 2 failed, run again alone once: src/Daoris.Web/src/app/listDoors.test.tsx, src/Daoris.Web/src/locales/names.test.ts
         ✓ src/locales/names.test.ts (4 tests) 900ms
        ⎯⎯⎯⎯⎯⎯⎯ Failed Tests 1 ⎯⎯⎯⎯⎯⎯⎯

         FAIL  src/app/listDoors.test.tsx > the list's doors, on the window > lays a strip the window drew over the main area from a door
        Error: Test timed out in 10000ms.
        If this is a long-running test, pass a timeout value as the last argument or configure it globally with "testTimeout".
         ❯ src/app/listDoors.test.tsx:123:3
            123|   it('lays a strip the window drew over the main area from a door, and…

        ⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯[1/1]⎯
        reached: FLAKE: web-unit|src/Daoris.Web/src/locales/names.test.ts failed, then passed alone; it is not recorded as a pass
        reached: RED: web-unit|src/Daoris.Web/src/app/listDoors.test.tsx failed again alone
        reached-classes: {"ran":164,"replayed":287,"flaked":["web-unit|src/Daoris.Web/src/locales/names.test.ts"],"mainRed":[],"red":["web-unit|src/Daoris.Web/src/app/listDoors.test.tsx"],"deferred":["tool-e2e|tools/merge/reached.e2e.ts"]}
        """;

    [Fact]
    public void A_red_class_is_named_first_with_the_lines_its_log_failed_it_on_when_run_alone_and_the_flakes_after()
    {
        var told = ReachedVerdict.Told(RedLog, "reached-classes: ", "gate-02-reached.log")!;

        Assert.StartsWith("the reached classes: 1 red, failed again alone: web-unit|src/Daoris.Web/src/app/listDoors.test.tsx; 164 run, 287 replayed", told);
        var lines = told.Split('\n');
        var failure = Array.IndexOf(lines, " FAIL  src/app/listDoors.test.tsx > the list's doors, on the window > lays a strip the window drew over the main area from a door");
        Assert.True(failure > 0, told);
        Assert.Equal("Error: Test timed out in 10000ms.", lines[failure + 1]);
        Assert.Contains(" ❯ src/app/listDoors.test.tsx:123:3", lines);
        Assert.DoesNotContain("⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯[1/1]⎯", lines);
        Assert.DoesNotContain(lines, line => line.StartsWith("reached", StringComparison.Ordinal));
        Assert.Contains("its log, gate-02-reached.log, fails them on:", lines);
        Assert.True(told.IndexOf("listDoors", StringComparison.Ordinal) < told.IndexOf("passed on a second run alone", StringComparison.Ordinal));
        Assert.Contains("passed on a second run alone: web-unit|src/Daoris.Web/src/locales/names.test.ts", lines[0]);
    }

    [Fact]
    public void A_red_dotnet_class_is_told_by_its_failed_test_and_one_the_log_never_names_says_so()
    {
        const string log = """
              Failed Daoris.Desktop.Driver.Tests.AlphaTests.It_holds [12 ms]
              Error Message:
               Assert.Equal() Failure
              Stack Trace:
                 at Daoris.Desktop.Driver.Tests.AlphaTests.It_holds()

            reached: RED: driver|Daoris.Desktop.Driver.Tests.AlphaTests failed again alone
            reached-classes: {"ran":2,"replayed":0,"flaked":[],"mainRed":[],"red":["driver|Daoris.Desktop.Driver.Tests.AlphaTests","driver|Daoris.Desktop.Driver.Tests.GoneTests"]}
            """;

        var told = ReachedVerdict.Told(log, "reached-classes: ", "reached.log")!;
        var result = Judge(1, log);

        Assert.Contains("  Failed Daoris.Desktop.Driver.Tests.AlphaTests.It_holds [12 ms]\n  Error Message:\n   Assert.Equal() Failure", told);
        Assert.Contains("driver|Daoris.Desktop.Driver.Tests.GoneTests: its log names no failure of it", told);
        Assert.Contains("Assert.Equal() Failure", result.Why);
    }

    [Fact]
    public void A_red_class_s_failure_lines_keep_within_the_excerpt_s_bounds()
    {
        var reds = Enumerable.Range(0, 30).Select(at => $"web-unit|src/f{at}.test.ts").ToList();
        var log = string.Join("\n", reds.Select(id => $" FAIL  {id[9..]} > fails\n" + string.Join("\n", Enumerable.Range(0, 20).Select(line => $"Error line {line} " + new string('x', 300))) + "\n"))
            + "\nreached-classes: " + System.Text.Json.JsonSerializer.Serialize(new { ran = 30, replayed = 0, flaked = Array.Empty<string>(), mainRed = Array.Empty<string>(), red = reds });

        var told = ReachedVerdict.Told(log, "reached-classes: ", "reached.log")!;
        var failures = told[told.IndexOf("its log,", StringComparison.Ordinal)..];

        Assert.True(System.Text.Encoding.UTF8.GetByteCount(failures) <= GateLogExcerpt.Bytes + 512, $"{System.Text.Encoding.UTF8.GetByteCount(failures)} bytes");
        Assert.EndsWith("…", told);
        Assert.Contains(reds[29], told[..told.IndexOf('\n')]);
    }
}
