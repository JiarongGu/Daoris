using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PIPELINE1d (the pipeline design §4.2): a check started through the performer is handed to the real heavy executor, the start
/// returning while its gate still runs, and its verdict reaches the run's record through the reconcile alone, under the <c>gates</c> source.
/// </summary>
/// <remarks>Real git, node and job processes: the Process half's.</remarks>
[Trait(Category.Name, Category.Process)]
public sealed class CheckExecutionProcessTests : IAsyncLifetime
{
    private const string Unit = "run:v-0123456789abcdef";

    private static readonly NodeDeclaration Check = CheckNodes.Performed(new NodeDeclaration("verify", NodeKinds.Check, 1, NodeReach.Performed));

    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "check-execution", Guid.NewGuid().ToString("N")[..8]);
    private HeavyExecutor _executor = null!;
    private string _commit = "";
    private string _blob = "";

    private string Home => Path.Combine(_folder, "home");

    private string Checkout => Path.Combine(_folder, "engine");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Checkout);
        // Waits for its go file, then exits with the code it names.
        await File.WriteAllTextAsync(Path.Combine(_folder, "gate.mjs"), """
            import { existsSync, readFileSync, writeFileSync } from 'node:fs';
            const [go, began] = process.argv.slice(2);
            writeFileSync(began, 'began');
            const wait = setInterval(() => { if (!existsSync(go)) return; clearInterval(wait); process.exit(Number(readFileSync(go, 'utf8'))); }, 20);
            setTimeout(() => process.exit(9), 120000);
            """);
        var gate = $"node \"{Path.Combine(_folder, "gate.mjs")}\" \"{Path.Combine(_folder, "go")}\" \"{Path.Combine(_folder, "began")}\"";
        await GitFixture.GitAsync(Checkout, "init", "-q", "-b", "main");
        await GitFixture.GitAsync(Checkout, "config", "user.email", "test@example.com");
        await GitFixture.GitAsync(Checkout, "config", "user.name", "Test");
        await File.WriteAllTextAsync(Path.Combine(Checkout, "daoris.gates.json"), JsonSerializer.Serialize(new { gates = new[] { new { name = "suite", run = gate, heavy = true } } }));
        await GitFixture.GitAsync(Checkout, "add", "-A");
        await GitFixture.GitAsync(Checkout, "commit", "-q", "-m", "gates");
        _commit = (await GitFixture.GitAsync(Checkout, "rev-parse", "HEAD")).Trim();
        _blob = (await GitFixture.GitAsync(Checkout, "rev-parse", "HEAD:daoris.gates.json")).Trim();
        _executor = new HeavyExecutor(Home, new VerifyWorld { Poll = TimeSpan.FromMilliseconds(50) });
    }

    public async Task DisposeAsync()
    {
        await _executor.DisposeAsync();
        GitSeed.Discard(_folder);
    }

    private sealed class NoPlugins : IWorkPerformPort
    {
        public Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct) => throw new InvalidOperationException(plugin);

        public Task<(WorkReconcileAnswer Answer, int Generation)> ReconcileStepAsync(string plugin, WorkReconcileCall call, CancellationToken ct) =>
            throw new InvalidOperationException(plugin);

        public int? GenerationOf(string plugin) => null;

        public void Refused(string plugin, string code)
        {
        }
    }

    private NodeRecordFiles Files => new(Home, () => true);

    private WorkPerformer Performer() => new(Files, new NoPlugins(), () => DateTimeOffset.UtcNow)
    {
        Checks = new CheckExecution(_executor, (unit, attempt, _) =>
            new VerifySend(CheckExecution.RunOf(unit), attempt.Call.Invocation, "engine", Checkout, attempt.Call.SubjectRevision, _blob) { Tree = Checkout + "-verify" }),
    };

    private NodeDeclaration? Declared(string unit, string node) => unit == Unit && node == Check.Id ? Check : null;

    private NodeStatus Status() => Files.Read(Unit)!.Statuses[Check.Id];

    private async Task StartAsync(WorkPerformer performer)
    {
        var record = Files.Read(Unit)!;
        var before = new Dictionary<string, NodeStatus>(StringComparer.Ordinal) { [Check.Id] = NodeLifecycle.Start(Unit, Check.Id) };
        var evaluated = WireEvaluation.Evaluate([Check], [], before,
            new WireRun("graph-1", "v-0123456789abcdef") { Revisions = new Dictionary<string, string?> { [Unit] = _commit }, Fired = record.Fired.ToHashSet(StringComparer.Ordinal) });
        performer.Keep(before, evaluated);
        Assert.Equal(PerformedWhat.Sent, (await performer.StartAsync(Check, Assert.Single(evaluated.Starts), CancellationToken.None)).What);
    }

    [Theory]
    [InlineData(0, NodeStates.Reached)]
    [InlineData(3, NodeStates.Failed)]
    public async Task The_start_returns_while_the_gate_runs_and_its_verdict_reaches_the_record_through_the_reconcile(int exit, string state)
    {
        var performer = Performer();
        await StartAsync(performer);

        // The pass goes on: the gate runs on the executor's task, and a reconcile meanwhile asks nothing.
        await Poll.Until(() => File.Exists(Path.Combine(_folder, "began")));
        Assert.Empty(await performer.ReconcileAsync(Declared, CancellationToken.None));
        Assert.Equal(NodeStates.Active, Status().State);

        await File.WriteAllTextAsync(Path.Combine(_folder, "go"), exit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var invocation = Files.Read(Unit)!.Attempts.Single().Call.Invocation;
        await Poll.Until(() => _executor.Answered(invocation));
        await performer.ReconcileAsync(Declared, CancellationToken.None);

        Assert.Equal((state, NodeSource.Gates), (Status().State, Status().By));
        var result = VerifyResult.Read(VerifyFiles.ResultOf(Home, "v-0123456789abcdef", invocation))!;
        Assert.Equal(_commit, result.Commit);
        Assert.Equal(exit, result.Gates.Single().Exit);
        Assert.True(File.Exists(result.Gates.Single().Log));
    }
}
