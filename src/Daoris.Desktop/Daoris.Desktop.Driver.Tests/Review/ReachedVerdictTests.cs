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
}
