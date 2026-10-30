using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PIPELINE1d (the pipeline design §4.3): a person's verification of a commit is a run of its own, bound once under the home's
/// <c>workflows/lines/</c>, which arms the runtime; its check observes the run itself at that commit and is handed to the executor with the
/// run's repository, checkout and accepted declaration, and nothing of it is told to a plugin or read from the chains' runs.
/// </summary>
public sealed class VerifyRunTests : NodeRuntimeFixture
{
    private const string Run = "v-0123456789abcdef";
    private const string Commit = "fedcba9876543210fedcba9876543210fedcba98";

    private const string Graph = """
        {"schemaVersion":2,"entries":["verify"],"end":"verify",
         "nodes":[{"id":"verify","kind":"check","version":1,"reach":"performed","timeoutSeconds":600}],"wires":[]}
        """;

    private sealed class Checks : ICheckPort
    {
        public List<(string Unit, NodeAttempt Attempt, VerifySend? Send)> Sent { get; } = [];

        public Func<string, NodeAttempt, NodeDeclaration, VerifySend?> SendOf { get; set; } = (_, _, _) => null;

        public Task SendAsync(string unit, NodeAttempt attempt, NodeDeclaration node, CancellationToken ct)
        {
            Sent.Add((unit, attempt, SendOf(unit, attempt, node)));
            return Task.CompletedTask;
        }

        public Task<WorkReconcileAnswer> ReconcileAsync(string unit, NodeAttempt attempt, CancellationToken ct) => Task.FromResult(new WorkReconcileAnswer(ReconcileStates.Running));

        public bool Answered(string invocation) => false;

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

    [Fact]
    public async Task A_person_s_verification_bound_under_lines_hands_its_check_on_the_run_at_its_commit_to_the_executor()
    {
        var digest = SaveVerification();
        VerifyRuns.Bind(_home, new VerifyRun(Run, "engine", "default", "checkout", "verify", 1, digest, Commit, VerifyRuns.Person, _now)
        {
            Branch = "feature/x", Accepted = "1111111111111111111111111111111111111111",
        });
        var checks = new Checks { SendOf = (unit, attempt, node) => VerifyRuns.SendOf(_home, unit, attempt, node) };
        var runtime = Runtime();
        runtime.Checks = checks;

        await runtime.PassAsync(Look("{}"), CancellationToken.None);

        var (unit, attempt, send) = Assert.Single(checks.Sent);
        Assert.Equal(RunUnits.IdOf(Run), unit);
        Assert.Equal((NodeKinds.Check, Commit), (attempt.Plugin, attempt.Call.SubjectRevision));
        Assert.NotNull(send);
        Assert.Equal((Run, "engine", "checkout", Commit, "1111111111111111111111111111111111111111", TimeSpan.FromSeconds(600)),
            (send.Run, send.Repository, send.Checkout, send.Commit, send.Accepted, send.Bound));
        Assert.Equal(NodeStates.Active, Files.Read(RunUnits.IdOf(Run))!.Statuses["verify"].State);
        // Nothing of it is a plugin's or a chain's.
        Assert.Empty(_port.Sent);
        Assert.False(Directory.Exists(WorkflowRunBindings.FolderOf(_home)));
    }

    [Fact]
    public async Task With_no_executor_its_check_waits_ready()
    {
        var digest = SaveVerification();
        VerifyRuns.Bind(_home, new VerifyRun(Run, "engine", "default", "checkout", "verify", 1, digest, Commit, VerifyRuns.Person, _now));

        await Runtime().PassAsync(Look("{}"), CancellationToken.None);

        Assert.Equal(NodeStates.Ready, Files.Read(RunUnits.IdOf(Run))!.Statuses["verify"].State);
    }

    [Fact]
    public void A_binding_is_written_once_and_reads_back()
    {
        var run = new VerifyRun(Run, "engine", "default", "checkout", "verify", 1, "gph-x", Commit, VerifyRuns.Person, _now) { Branch = "feature/x", Accepted = "none" };
        VerifyRuns.Bind(_home, run);

        Assert.Equal(run, Assert.Single(VerifyRuns.All(_home)));
        Assert.Throws<DriverException>(() => VerifyRuns.Bind(_home, run));
        Assert.Matches("^v-[0-9a-f]{16}$", VerifyRuns.NewId());
    }
}
