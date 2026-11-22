using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A repository's declared reached runner: read from the declaration's <c>reached</c> entry, its command given the base it judges against,
/// and its exit and last outcome line judged into a check's verdict that names the classes run, replayed and red.
/// </summary>
public sealed class ReachedVerdictTests
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";
    private const string Declaration = """{ "gates": [], "reached": { "run": "node tools/reached-checks.mjs --tier {tier} --base {base}", "outcome": "reached-classes: ", "clock": true } }""";

    private static VerifyResult Judge(int exit, string log) => ReachedVerdict.Judge("i-1", Commit, exit, log, "reached.log", "reached-classes: ");

    [Fact]
    public void A_declaration_s_reached_entry_reads_with_its_command_its_marker_and_its_clock()
    {
        var runner = ReachedRunner.Of(Declaration);

        Assert.NotNull(runner);
        Assert.Equal("node tools/reached-checks.mjs --tier fast --base main", runner.CommandFor("main", ReachedRunner.FastTier));
        Assert.Equal("reached-classes: ", runner.Outcome);
        Assert.True(runner.Clock);
    }

    [Fact]
    public void The_command_takes_the_tier_the_step_asks()
    {
        var runner = ReachedRunner.Of(Declaration)!;

        Assert.Equal("node tools/reached-checks.mjs --tier all --base main", runner.CommandFor("main", ReachedRunner.EveryTier));
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

        Assert.Equal("node tools/reached-checks.mjs --tier fast --base origin/main", runner.CommandFor("origin/main", ReachedRunner.FastTier));
        Assert.Null(runner.CommandFor("main; rm -rf x", ReachedRunner.FastTier));
        Assert.Null(runner.CommandFor("", ReachedRunner.FastTier));
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
        Assert.Contains(reds[0], told[..told.IndexOf('\n')]);
    }

    private static string Outcome(IEnumerable<string> red, IEnumerable<string>? flaked = null, IEnumerable<string>? mainRed = null) =>
        "reached-classes: " + System.Text.Json.JsonSerializer.Serialize(new { ran = 9, replayed = 0, flaked = flaked ?? [], mainRed = mainRed ?? [], red });

    [Fact]
    public void The_red_ids_told_are_capped_with_an_and_N_more_tail_in_the_header_the_failures_and_the_verdict_s_why()
    {
        var reds = Enumerable.Range(0, 30).Select(at => $"web-unit|src/f{at}.test.ts").ToList();
        var log = string.Join("\n", reds.Select(id => $" FAIL  {id[9..]} > fails\nError: x\n")) + "\n" + Outcome(reds, reds.Take(25).Select(id => id + ".flake"), reds.Take(40));
        var cap = ReachedVerdict.RedIds;

        var told = ReachedVerdict.Told(log, "reached-classes: ", "reached.log")!;
        var header = told[..told.IndexOf('\n')];
        var failures = told[told.IndexOf("its log,", StringComparison.Ordinal)..];

        Assert.Contains(reds[cap - 1], header);
        Assert.DoesNotContain(reds[cap], header);
        Assert.Contains($"and {30 - cap} more", header);
        Assert.Contains($"and {25 - cap} more", header);
        Assert.Contains(reds[cap - 1][9..], failures);
        Assert.DoesNotContain(reds[cap][9..], failures);
        Assert.Contains($"and {30 - cap} more red classes", failures);
        var why = Judge(1, log).Why.Split('\n')[0];
        Assert.DoesNotContain(reds[cap], why);
        Assert.Contains($"and {30 - cap} more", why);
    }

    [Fact]
    public void A_class_the_log_names_only_inside_another_class_s_name_names_no_failure_of_it_and_an_empty_name_matches_nothing()
    {
        const string log = """
              Failed Ns.FooTestsExtra.It_breaks [3 ms]
              Error Message: boom
             FAIL  src/app/foo.test.tsx > breaks
            Error: tsx boom
             FAIL  src/app/Other.test.tsx > breaks
            Error: other
            """;
        var told = ReachedVerdict.Told(log + "\n" + Outcome(["driver|Ns.FooTests", "web-unit|src/app/foo.test.ts", "web-unit|", "web-unit|src/app/Other.test.tsx"]), "reached-classes: ", "reached.log")!;

        Assert.Contains("driver|Ns.FooTests: its log names no failure of it", told);
        Assert.Contains("web-unit|src/app/foo.test.ts: its log names no failure of it", told);
        Assert.Contains("web-unit|: its log names no failure of it", told);
        Assert.DoesNotContain("boom", told);
        Assert.Contains("Error: other", told);
    }

    [Fact]
    public void A_passing_line_that_holds_the_word_Error_is_never_the_failure_it_tells()
    {
        const string log = """
             FAIL  src/app/a.test.tsx > breaks
            AssertionError: expected 1 to be 2
             ✓ src/app/a.test.tsx > surfaces an Error as a toast 12ms
             ✓ src/app/b.test.tsx > shows Error text 3ms
            """;
        var told = ReachedVerdict.Told(log + "\n" + Outcome(["web-unit|src/app/a.test.tsx", "web-unit|src/app/b.test.tsx"]), "reached-classes: ", "reached.log")!;

        Assert.Contains("AssertionError: expected 1 to be 2", told);
        Assert.DoesNotContain("surfaces an Error as a toast", told);
        Assert.DoesNotContain("shows Error text", told);
        Assert.Contains("web-unit|src/app/b.test.tsx: its log names no failure of it", told);
    }

    [Fact]
    public void A_log_line_holding_a_token_or_a_home_path_is_withheld_from_the_words_and_the_verdict_s_why()
    {
        var token = "ghp_" + new string('a', 24);
        const string Drive = "C", Slash = "\\", Folder = "Us" + "ers";
        var log = $" FAIL  src/app/a.test.tsx > breaks\nError: sent {token} to the host\n    at {Drive}:{Slash}{Folder}{Slash}someone{Slash}work{Slash}a.test.tsx:1\n  at src/app/a.test.tsx:2\n"
            + Outcome(["web-unit|src/app/a.test.tsx"]);

        var told = ReachedVerdict.Told(log, "reached-classes: ", "reached.log")!;
        var why = Judge(1, log).Why;

        foreach (var words in new[] { told, why })
        {
            Assert.DoesNotContain(token, words);
            Assert.DoesNotContain("someone", words);
            Assert.Contains("withheld", words);
            Assert.Contains("at src/app/a.test.tsx:2", words);
        }
    }

    private static readonly string Unix = "/" + "home" + "/someone";

    [Theory]
    [InlineData("Authorization: Basic dXNlcjpwYXNz", "dXNlcjpwYXNz")]
    [InlineData("curl -H 'Cookie: session=abc123secret'", "abc123secret")]
    [InlineData("fetch https://user:pw0rdvalue@host.example/x failed", "pw0rdvalue")]
    [InlineData("key sk-proj-AbCdEfGhIjKlMnOpQrStUv_wxyz", "AbCdEfGhIjKlMnOpQrStUv")]
    public void A_credential_the_git_boundary_cuts_never_reaches_the_reached_words_or_the_verdict_s_why(string line, string secret)
    {
        var log = $" FAIL  src/app/a.test.tsx > breaks\n{line}\n" + Outcome(["web-unit|src/app/a.test.tsx"]);

        foreach (var words in new[] { ReachedVerdict.Told(log, "reached-classes: ", "reached.log")!, Judge(1, log).Why })
        {
            Assert.DoesNotContain(secret, words);
        }
    }

    [Fact]
    public void A_private_key_block_is_cut_whole_before_the_log_is_split_into_lines()
    {
        var (begin, end) = ("-----BEGIN RSA PRIVATE" + " KEY-----", "-----END RSA PRIVATE" + " KEY-----");
        var log = $" FAIL  src/app/a.test.tsx > breaks\n{begin}\nMIIEowIBAAKCAQEAbodyline1\nbodyline2xyz\n{end}\nafter the block\n"
            + Outcome(["web-unit|src/app/a.test.tsx"]);

        foreach (var words in new[] { ReachedVerdict.Told(log, "reached-classes: ", "reached.log")!, Judge(1, log).Why })
        {
            Assert.DoesNotContain("bodyline", words);
            Assert.DoesNotContain("MIIEow", words);
        }
    }

    [Theory]
    [InlineData(390)]
    [InlineData(395)]
    [InlineData(399)]
    [InlineData(450)]
    public void A_token_or_a_home_path_that_starts_near_the_display_cut_withholds_the_whole_line(int before)
    {
        var secret = "ghp_" + new string('a', 24);
        foreach (var hidden in new[] { secret, Unix + "/work/a.test.tsx" })
        {
            var log = $" FAIL  src/app/a.test.tsx > breaks\n{new string('x', before)} {hidden}\n" + Outcome(["web-unit|src/app/a.test.tsx"]);

            foreach (var words in new[] { ReachedVerdict.Told(log, "reached-classes: ", "reached.log")!, Judge(1, log).Why })
            {
                Assert.DoesNotContain("ghp_", words);
                Assert.DoesNotContain("someone", words);
                Assert.Contains(WordsScan.Withheld, words);
            }
        }
    }

    [Fact]
    public void An_id_a_heading_a_deferred_suite_and_the_log_path_pass_the_same_boundary()
    {
        var token = "ghp_" + new string('b', 24);
        var log = " FAIL  src/app/other.test.tsx > breaks\nError: x\n"
            + "reached-classes: " + System.Text.Json.JsonSerializer.Serialize(new
            {
                ran = 1, replayed = 0, flaked = new[] { "web-unit|" + Unix + "/f.test.ts" }, mainRed = Array.Empty<string>(),
                red = new[] { "web-unit|" + Unix + "/a.test.ts", "web-unit|" + token }, deferred = new[] { Unix + "|C" },
            });
        const string Base = "D:/fixture-folder";

        var told = ReachedVerdict.Told(log, "reached-classes: ", Unix + "/reached.log", Base)!;
        var why = Judge(1, log).Why;
        var inside = ReachedVerdict.Told(log, "reached-classes: ", Base + "/checks/reached.log", Base)!;

        foreach (var words in new[] { told, why })
        {
            Assert.DoesNotContain("someone", words);
            Assert.DoesNotContain(token, words);
            Assert.Contains(WordsScan.Withheld, words);
        }

        Assert.Contains("its log, reached.log, fails them on:", told);
        Assert.Contains("its log, checks/reached.log, fails them on:", inside);
    }
}
