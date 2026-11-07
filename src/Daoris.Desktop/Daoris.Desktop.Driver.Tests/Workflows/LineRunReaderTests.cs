using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A person's verification of a branch is performed by the runtime through the check's port, its
/// verdict recorded on the run's node record under the gates' source, and read on its line (<c>workflow run --line</c>, <c>WORKFLOW_RUN {line}</c>)
/// with the commit it judged, the branch it is asked of, its verdict and each gate's evidence.
/// </summary>
public sealed class LineRunReaderTests : NodeRuntimeFixture
{
    private const string Run = "v-0123456789abcdef";
    private const string Commit = "fedcba9876543210fedcba9876543210fedcba98";

    private const string Graph = """
        {"schemaVersion":2,"entries":["verify"],"end":"verify",
         "nodes":[{"id":"verify","kind":"check","version":1,"reach":"performed","timeoutSeconds":600}],"wires":[]}
        """;

    /// <summary>The executor as the runtime sees it: what it is sent, and what it answers once it says it has an answer.</summary>
    private sealed class Checks : ICheckPort
    {
        public List<NodeAttempt> Sent { get; } = [];

        public HashSet<string> Answers { get; } = [];

        public Func<NodeAttempt, WorkReconcileAnswer> Answer { get; set; } = _ => new(ReconcileStates.Running);

        public Task SendAsync(string unit, NodeAttempt attempt, NodeDeclaration node, CancellationToken ct)
        {
            Sent.Add(attempt);
            return Task.CompletedTask;
        }

        public Task<WorkReconcileAnswer> ReconcileAsync(string unit, NodeAttempt attempt, CancellationToken ct) => Task.FromResult(Answer(attempt));

        public bool Answered(string invocation) => Answers.Contains(invocation);

        public void Stop(string invocation)
        {
        }
    }

    private string SaveVerification()
    {
        Directory.CreateDirectory(WorkflowStore.FolderOf(_home));
        File.WriteAllText(WorkflowStore.PathOf(_home, "verify"),
            $$"""{"id":"verify","name":"Verify","pipeline":"verification","versions":[{"version":1,"at":"2026-10-31T09:00:00Z","door":"terminal","schema":2,"graph":{{Graph}}}]}""");
        var version = Assert.Single(new WorkflowFiles(_home).Load("verify")!.Read.Versions);
        Assert.Null(version.Problem);
        return version.Digest!;
    }

    private WorkflowRunRead Read(string repository = "engine") =>
        LineRunReader.Read(repository, _home, DriverConfig.Parse("{}"), _ => "default", WorkflowPorts.Records(_home));

    /// <summary>Bind a person's verification of <c>feature/x</c>, send its check, and answer it with <paramref name="result"/>.</summary>
    private async Task<(Checks Checks, string Invocation)> VerifiedAsync(Func<string, VerifyResult> result)
    {
        var digest = SaveVerification();
        VerifyRuns.Bind(_home, new VerifyRun(Run, "engine", "default", "checkout", "verify", 1, digest, Commit, VerifyRuns.Person, _now)
        {
            Branch = "feature/x", Accepted = "none",
        });
        var checks = new Checks();
        var runtime = Runtime();
        runtime.Doors = runtime.Doors.With(NodeKinds.Check, checks);

        await runtime.PassAsync(Look("{}"), CancellationToken.None);
        var invocation = Assert.Single(checks.Sent).Call.Invocation;
        var running = Assert.Single(Read().Runs).Line!;
        Assert.Equal((LineVerdicts.Running, "feature/x", Commit, VerifyRuns.Person), (running.Verdict, running.Branch, running.Commit, running.Level));
        Assert.Empty(running.Gates);

        var written = result(invocation);
        written.Write(VerifyFiles.ResultOf(_home, Run, invocation));
        checks.Answers.Add(invocation);
        checks.Answer = _ => CheckExecution.AnswerOf(written);
        await runtime.PassAsync(Look("{}"), CancellationToken.None);
        return (checks, invocation);
    }

    [Fact]
    public async Task A_person_s_verification_of_a_branch_is_performed_and_its_verdict_recorded_and_read_on_its_line()
    {
        var (_, invocation) = await VerifiedAsync(invocation => new VerifyResult(invocation, Commit, VerifyVerdicts.Reached, null,
            [new VerifyGate("build", "pass", 0) { WallSeconds = 12 }, new VerifyGate("test", "pass", 0) { WallSeconds = 40 }]) { Declaration = "none" });

        var status = Files.Read(RunUnits.IdOf(Run))!.Statuses["verify"];
        Assert.Equal((NodeStates.Reached, NodeSource.Gates), (status.State, status.By));
        Assert.Equal($"gates:{invocation}", status.Acceptance!.Evidence!.Ref);

        var read = Read();
        var run = Assert.Single(read.Runs);
        Assert.Null(run.Problem);
        var line = run.Line!;
        Assert.Equal((Run, LineVerdicts.Verified, (string?)null), (line.Run, line.Verdict, line.Gate));
        Assert.Equal(["build", "test"], line.Gates.Select(gate => gate.Name));
        Assert.Equal("reached", Assert.Single(run.Steps, step => step.Node == NodeKinds.Check).Detail);

        var said = WorkflowRunWords.Say(read.Runs, null);
        Assert.Contains("engine · `verify` v1 workflow · verification v-0123456789abcdef of feature/x at fedcba9 · verified\n", said);
        Assert.Contains("  asked by you at 2026-10-17 09:00 UTC\n", said);
        Assert.Contains("  gate build: pass, exit 0, 12s\n", said);
    }

    [Fact]
    public async Task A_failed_verification_names_its_gate_on_the_record_and_the_line()
    {
        await VerifiedAsync(invocation => new VerifyResult(invocation, Commit, VerifyVerdicts.Failed, "a gate failed",
            [new VerifyGate("build", "pass", 0), new VerifyGate("test", "fail", 1)]) { Failed = "test", Declaration = "none" });

        Assert.Equal(NodeStates.Failed, Files.Read(RunUnits.IdOf(Run))!.Statuses["verify"].State);
        var line = Assert.Single(Read().Runs).Line!;
        Assert.Equal((LineVerdicts.Failed, "test"), (line.Verdict, line.Gate));
        Assert.Equal((int?)1, line.Gates.Single(gate => gate.Name == "test").Exit);
        Assert.Contains("· failed: `test`", WorkflowRunWords.Say(Read().Runs, null));
    }

    [Fact]
    public async Task An_unknown_verdict_says_why_and_is_held_for_the_person()
    {
        await VerifiedAsync(invocation => new VerifyResult(invocation, Commit, VerifyVerdicts.Unknown, "the declaration changed", [])
        {
            Code = VerifyResult.Changed,
        });

        var run = Assert.Single(Read().Runs);
        Assert.Equal((LineVerdicts.Unknown, VerifyResult.Changed), (run.Line!.Verdict, run.Line.Why));
        Assert.NotNull(Assert.Single(run.Steps, step => step.Node == NodeKinds.Check).Held);
        Assert.Contains("· not known: the gates changed since they were accepted", WorkflowRunWords.Say(Read().Runs, null));
    }

    [Fact]
    public void A_line_reads_its_own_repository_s_runs_newest_first_and_none_says_how_one_is_asked()
    {
        var digest = SaveVerification();
        VerifyRuns.Bind(_home, new VerifyRun("v-0000000000000001", "engine", "default", "checkout", "verify", 1, digest, Commit, VerifyRuns.Person, _now));
        VerifyRuns.Bind(_home, new VerifyRun("v-0000000000000002", "engine", "default", "checkout", "verify", 1, digest, Commit, VerifyRuns.Person, _now.AddMinutes(5)));
        VerifyRuns.Bind(_home, new VerifyRun("v-0000000000000003", "other", "default", "checkout", "verify", 1, digest, Commit, VerifyRuns.Person, _now.AddMinutes(9)));

        Assert.Equal(["v-0000000000000002", "v-0000000000000001"], Read().Runs.Select(run => run.Line!.Run));
        Assert.All(Read().Runs, run => Assert.Equal(LineVerdicts.Waiting, run.Line!.Verdict));

        var none = Read("aurora");
        Assert.Empty(none.Runs);
        Assert.Contains("`daoris gate verify aurora`", none.Problem);
    }

    [Fact]
    public void A_run_whose_version_no_longer_reads_says_so_and_is_not_known()
    {
        SaveVerification();
        VerifyRuns.Bind(_home, new VerifyRun(Run, "engine", "default", "checkout", "verify", 1, "gph-gone", Commit, VerifyRuns.Person, _now));

        var run = Assert.Single(Read().Runs);
        Assert.Contains("cannot be read here", run.Problem);
        Assert.Equal(LineVerdicts.Unknown, run.Line!.Verdict);
        Assert.Empty(run.Steps);
    }
}
