using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PIPELINE1d (the pipeline design §4.2): a check's verdict judged from its runner's answer gate by gate, from an opaque runner's
/// exit, and answered to the performer's reconcile from the attempt's folder alone.
/// </summary>
public sealed class VerifyJudgeTests : IDisposable
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";
    private static readonly string[] Declared = ["unit", "suite", "lint"];
    private static readonly string[] Heavy = ["unit", "suite"];
    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "verify-judge", Guid.NewGuid().ToString("N")[..8]);

    public VerifyJudgeTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => GitSeed.Discard(_folder);

    private static string Answer(string gates, string commit = Commit, int schema = 1) =>
        $$"""{ "schema": {{schema}}, "commit": "{{commit}}", "gates": [{{gates}}] }""";

    private static VerifyJudged ByFile(string? answer, int exit = 0) =>
        VerifyJudge.ByFile("i-1", Commit, exit, answer, Declared, Heavy, "runner.log");

    [Fact]
    public void Every_gate_the_check_names_passing_or_flaking_is_reached_gate_by_gate()
    {
        var judged = ByFile(Answer("""{ "name": "unit", "verdict": "pass", "exit": 0, "wallSeconds": 12.5, "log": "C:/logs/unit.log" }, { "name": "suite", "verdict": "flake", "exit": 0 }"""));

        Assert.False(judged.Busy);
        Assert.Equal(VerifyVerdicts.Reached, judged.Result!.Verdict);
        Assert.Equal(["unit", "suite"], judged.Result.Gates.Select(gate => gate.Name));
        Assert.Equal(12.5, judged.Result.Gates[0].WallSeconds);
        Assert.Equal("C:/logs/unit.log", judged.Result.Gates[0].Log);
        Assert.Equal("flake", judged.Result.Gates[1].Verdict);
    }

    [Fact]
    public void A_failing_gate_fails_the_check_naming_it_with_its_exit()
    {
        var judged = ByFile(Answer("""{ "name": "unit", "verdict": "pass" }, { "name": "suite", "verdict": "fail", "exit": 3, "log": "C:/logs/suite.log" }"""));

        Assert.Equal(VerifyVerdicts.Failed, judged.Result!.Verdict);
        Assert.Equal("suite", judged.Result.Failed);
        Assert.Equal(3, judged.Result.Gates.Single(gate => gate.Name == "suite").Exit);
        Assert.Contains("suite", judged.Result.Why);
    }

    [Theory]
    [InlineData("empty gates", "")]
    [InlineData("a missing gate", """{ "name": "unit", "verdict": "pass" }""")]
    [InlineData("a gate ended", """{ "name": "unit", "verdict": "pass" }, { "name": "suite", "verdict": "ended" }""")]
    [InlineData("an undeclared name", """{ "name": "unit", "verdict": "pass" }, { "name": "suite", "verdict": "pass" }, { "name": "made-up", "verdict": "pass" }""")]
    public void An_answer_that_does_not_show_every_gate_is_unknown_never_a_pass(string why, string gates)
    {
        var judged = ByFile(Answer(gates));

        Assert.True(judged.Result!.Verdict == VerifyVerdicts.Unknown, why);
        Assert.False(string.IsNullOrEmpty(judged.Result.Why));
    }

    [Fact]
    public void Another_commit_another_schema_and_a_missing_file_are_each_unknown()
    {
        var gates = """{ "name": "unit", "verdict": "pass" }, { "name": "suite", "verdict": "pass" }""";
        var other = ByFile(Answer(gates, commit: "fedcba9876543210fedcba9876543210fedcba98"));
        var schema = ByFile(Answer(gates, schema: 2));
        var missing = ByFile(null);

        Assert.Equal(VerifyVerdicts.Unknown, other.Result!.Verdict);
        Assert.Contains("fedcba9", other.Result.Why);
        Assert.Equal(VerifyVerdicts.Unknown, schema.Result!.Verdict);
        Assert.Contains("schema 2", schema.Result.Why);
        Assert.Equal(VerifyVerdicts.Unknown, missing.Result!.Verdict);
        Assert.Contains("no answer", missing.Result.Why);
    }

    [Fact]
    public void A_runner_by_file_is_busy_by_its_answer_or_its_exit_75()
    {
        Assert.True(ByFile($$"""{ "schema": 1, "commit": "{{Commit}}", "busy": true }""", exit: 75).Busy);
        Assert.True(ByFile(null, exit: 75).Busy);
        Assert.Null(ByFile(null, exit: 75).Result);
    }

    [Theory]
    [InlineData(0, VerifyVerdicts.Reached)]
    [InlineData(1, VerifyVerdicts.Failed)]
    [InlineData(3, VerifyVerdicts.Unknown)]
    public void An_opaque_runner_is_one_check_named_by_the_runner(int exit, string verdict)
    {
        var judged = VerifyJudge.Opaque("i-1", Commit, exit, "runner.log");

        Assert.False(judged.Busy);
        Assert.Equal(verdict, judged.Result!.Verdict);
        var gate = Assert.Single(judged.Result.Gates);
        Assert.Equal(VerifyJudge.RunnerGate, gate.Name);
        Assert.Equal("runner.log", gate.Log);
        if (verdict == VerifyVerdicts.Failed) Assert.Equal(VerifyJudge.RunnerGate, judged.Result.Failed);
    }

    [Fact]
    public void An_opaque_runner_exiting_75_is_busy()
    {
        Assert.True(VerifyJudge.Opaque("i-1", Commit, 75, "runner.log").Busy);
    }

    [Fact]
    public void A_result_is_written_whole_and_read_back_as_written()
    {
        var path = Path.Combine(_folder, "result.json");
        var written = new VerifyResult("i-1", Commit, VerifyVerdicts.Failed, "the gate 'suite' failed with exit 3",
            [new VerifyGate("unit", "pass", 0) { WallSeconds = 1.5, Log = "a.log" }, new VerifyGate("suite", "fail", 3) { Log = "b.log" }, new VerifyGate("lint", "not-run")])
        {
            Declaration = "1111111111111111111111111111111111111111", WallSeconds = 4, Failed = "suite",
        };

        written.Write(path);
        var read = VerifyResult.Read(path);

        Assert.NotNull(read);
        Assert.Equal(written.Verdict, read.Verdict);
        Assert.Equal(written.Why, read.Why);
        Assert.Equal("suite", read.Failed);
        Assert.Equal(written.Declaration, read.Declaration);
        Assert.Equal(["pass", "fail", "not-run"], read.Gates.Select(gate => gate.Verdict));
        Assert.Equal(1.5, read.Gates[0].WallSeconds);
        Assert.Equal(3, read.Gates[1].Exit);
        Assert.Null(read.Gates[2].Exit);
    }
}
