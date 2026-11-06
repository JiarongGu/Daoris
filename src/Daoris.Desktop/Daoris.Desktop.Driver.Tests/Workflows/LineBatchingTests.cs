using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The pipeline design §4.3: landed work is verified by itself, one run per line at a time. A batch's run is bound in a folder of its own at the
/// line's newest tip, its base the tip of the newest run with a verdict, and run to its verdict while the line moves on; an unknown holds the
/// line for the person, whose <i>Run again</i> is a new attempt at the run's tip and whose <i>Not now</i> lets the next batch take it in; a held
/// check names its line, its batch and why, which <i>What needs you</i> says.
/// </summary>
public sealed class LineBatchingTests : IDisposable
{
    private const string Accepted = "abababababababababababababababababababab";
    private const string First = "1111111111111111111111111111111111111111";
    private const string Second = "2222222222222222222222222222222222222222";
    private const string Third = "3333333333333333333333333333333333333333";

    private const string Graph = """
        {"schemaVersion":2,"entries":["verify"],"end":"verify",
         "nodes":[{"id":"verify","kind":"check","version":1,"reach":"performed","timeoutSeconds":600}],"wires":[]}
        """;

    private static readonly NodeDeclaration Check = CheckNodes.Performed(new NodeDeclaration("verify", NodeKinds.Check, 1, NodeReach.Performed));

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "line-batching", Guid.NewGuid().ToString("N")[..8]);
    private readonly Checks _checks = new();
    private readonly string _digest;
    private DateTimeOffset _now = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

    public LineBatchingTests()
    {
        Directory.CreateDirectory(WorkflowStore.FolderOf(_home));
        File.WriteAllText(WorkflowStore.PathOf(_home, "verify"),
            $$"""{"id":"verify","name":"Verify","pipeline":"verification","versions":[{"version":1,"at":"2026-11-01T09:00:00Z","door":"terminal","schema":2,"graph":{{Graph}}}]}""");
        var version = Assert.Single(new WorkflowFiles(_home).Load("verify")!.Read.Versions);
        Assert.Null(version.Problem);
        _digest = version.Digest!;
    }

    public void Dispose() => GitSeed.Discard(_home);

    /// <summary>The executor as the performer sees it: what it was sent, and what it answers once it has an answer.</summary>
    private sealed class Checks : ICheckPort
    {
        public HashSet<string> Answers { get; } = [];

        public Func<NodeAttempt, WorkReconcileAnswer> Answer { get; set; } = _ => new(ReconcileStates.Running);

        public Task SendAsync(string unit, NodeAttempt attempt, NodeDeclaration node, CancellationToken ct) => Task.CompletedTask;

        public Task<WorkReconcileAnswer> ReconcileAsync(string unit, NodeAttempt attempt, CancellationToken ct) => Task.FromResult(Answer(attempt));

        public bool Answered(string invocation) => Answers.Contains(invocation);

        public void Stop(string invocation)
        {
        }
    }

    /// <summary>No plugin's process: a check's attempt is never a plugin's.</summary>
    private sealed class NoPlugins : IWorkPerformPort
    {
        public Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct) => throw new InvalidOperationException($"sent to {plugin}");

        public Task<(WorkReconcileAnswer Answer, int Generation)> ReconcileStepAsync(string plugin, WorkReconcileCall call, CancellationToken ct) =>
            throw new InvalidOperationException($"asked {plugin}");

        public int? GenerationOf(string plugin) => null;

        public void Refused(string plugin, string code)
        {
        }
    }

    private NodeRecordFiles Files => new(_home, () => true);

    private WorkPerformer Performer() => new(Files, new NoPlugins(), () => _now) { Checks = _checks };

    private static NodeDeclaration? Declared(string unit, string node) => RunUnits.Is(unit) && node == Check.Id ? Check : null;

    private static WorkflowLandedSelected Route(string? gates = Accepted) => new("verify", gates, WorkflowLevels.RepositoryLanded);

    private static LineHead Head(string tip) => new("engine", "default", "checkout", "main", tip);

    private VerifyRun? Owed(string tip) => LineBatching.Owed(_home, Head(tip), Route(), Files, _now);

    private NodeStatus Status(VerifyRun run) => Files.Read(RunUnits.IdOf(run.Run))!.Statuses[Check.Id];

    private NodeAttempt Attempt(VerifyRun run) => Files.Read(RunUnits.IdOf(run.Run))!.Attempts.Last();

    /// <summary>The run the line owes at <paramref name="tip"/>, bound under <c>lines/</c>.</summary>
    private VerifyRun Bound(string tip)
    {
        var run = Owed(tip);
        Assert.NotNull(run);
        VerifyRuns.Bind(_home, run);
        _now += TimeSpan.FromMinutes(1);
        return run;
    }

    /// <summary>The run's check started at its bound tip, as the pass starts it once its slots come.</summary>
    private async Task<WorkPerformed> StartAsync(VerifyRun run)
    {
        var unit = RunUnits.IdOf(run.Run);
        var performer = Performer();
        var record = Files.Read(unit);
        var before = new Dictionary<string, NodeStatus>(StringComparer.Ordinal)
        {
            [Check.Id] = record?.Statuses.GetValueOrDefault(Check.Id) ?? NodeLifecycle.Start(unit, Check.Id),
        };
        var evaluated = WireEvaluation.Evaluate([Check], [], before,
            new WireRun("graph-1", run.Run)
            {
                Revisions = new Dictionary<string, string?> { [unit] = run.Commit }, Fired = (record?.Fired ?? []).ToHashSet(StringComparer.Ordinal),
            });
        Assert.Null(evaluated.Refusal);
        performer.Keep(before, evaluated);
        return await performer.StartAsync(Check, Assert.Single(evaluated.Starts), CancellationToken.None);
    }

    /// <summary>The executor's result for the run's newest attempt, written where it keeps it and answered through the reconcile.</summary>
    private async Task AnswerAsync(VerifyRun run, string verdict)
    {
        var invocation = Attempt(run).Call.Invocation;
        var result = new VerifyResult(invocation, run.Commit, verdict, verdict == VerifyVerdicts.Reached ? null : "why",
            [new VerifyGate("suite", verdict == VerifyVerdicts.Failed ? "fail" : "pass", 3)])
        {
            Failed = verdict == VerifyVerdicts.Failed ? "suite" : null, Code = verdict == VerifyVerdicts.Unknown ? VerifyResult.Changed : null,
        };
        var path = VerifyFiles.ResultOf(_home, run.Run, invocation);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        result.Write(path);
        _checks.Answers.Add(invocation);
        _checks.Answer = attempt => CheckExecution.AnswerOf(result);
        await Performer().ReconcileAsync(Declared, CancellationToken.None);
    }

    private async Task<VerifyRun> SettledAsync(string tip, string verdict)
    {
        var run = Bound(tip);
        await StartAsync(run);
        await AnswerAsync(run, verdict);
        return run;
    }

    [Fact]
    public void A_line_s_first_run_is_bound_in_its_own_folder_at_its_tip_with_no_base_under_its_route()
    {
        var run = Bound(First);

        Assert.Equal(("engine", "main", First, (string?)null, WorkflowLevels.RepositoryLanded, Accepted, 1, _digest),
            (run.Repository, run.Line, run.Commit, run.Base, run.Level, run.Accepted, run.Version, run.Digest));
        Assert.Matches("^v-[0-9a-f]{16}$", run.Run);
        Assert.True(File.Exists(VerifyRuns.PathOf(_home, run.Run)));
        Assert.Equal(run, Assert.Single(LineBatching.On(_home, "engine", "main")));
        // Beside the chains' runs, never among them, and the version it binds kept as theirs are.
        Assert.False(Directory.Exists(WorkflowRunBindings.FolderOf(_home)));
        Assert.Equal([1], new WorkflowRunFiles(_home).KeptVersions("verify"));
    }

    [Fact]
    public async Task A_run_in_flight_binds_nothing_as_the_line_moves_and_once_verified_the_next_batch_begins_at_its_tip()
    {
        var first = Bound(First);
        // Waiting for its slots, then running: what lands meanwhile is the next batch's.
        Assert.Null(Owed(Second));
        Assert.Equal(PerformedWhat.Sent, (await StartAsync(first)).What);
        Assert.Equal(First, Attempt(first).Call.SubjectRevision);
        Assert.Null(Owed(Third));

        await AnswerAsync(first, VerifyVerdicts.Reached);

        Assert.Equal(NodeStates.Reached, Status(first).State);
        Assert.Null(Owed(First));
        var next = Owed(Third);
        Assert.NotNull(next);
        Assert.Equal((Third, First), (next.Commit, next.Base));
    }

    [Fact]
    public async Task A_failure_is_a_verdict_the_next_batch_begins_after()
    {
        var first = await SettledAsync(First, VerifyVerdicts.Failed);

        Assert.Equal(LineVerdicts.Failed, LineBatching.Settled(Files.Read(RunUnits.IdOf(first.Run)), [Check.Id]));
        Assert.Equal(First, Owed(Second)?.Base);
    }

    [Fact]
    public async Task An_unknown_holds_the_line_for_the_person_and_their_not_now_lets_the_next_batch_take_it_in()
    {
        await SettledAsync(First, VerifyVerdicts.Reached);
        var second = await SettledAsync(Second, VerifyVerdicts.Unknown);

        Assert.Equal(AttemptStates.Held, Attempt(second).State);
        Assert.Null(LineBatching.Settled(Files.Read(RunUnits.IdOf(second.Run)), [Check.Id]));
        Assert.Null(Owed(Third));

        var performer = Performer();
        Assert.Equal(PerformedWhat.Worded, performer.Word(RunUnits.IdOf(second.Run), Check.Id, 1, HeldResolutions.Stop).What);
        performer.Resolve(Declared);

        Assert.Equal(LineVerdicts.NotNow, LineBatching.Settled(Files.Read(RunUnits.IdOf(second.Run)), [Check.Id]));
        // The tip it was put off at waits for the next landing, whose batch takes it in from the last verdict.
        Assert.Null(Owed(Second));
        var third = Owed(Third);
        Assert.NotNull(third);
        Assert.Equal((Third, First), (third.Commit, third.Base));
    }

    [Fact]
    public async Task Run_again_on_an_unknown_is_a_new_attempt_at_the_run_s_tip_and_the_line_stays_held()
    {
        var run = await SettledAsync(First, VerifyVerdicts.Unknown);

        var performer = Performer();
        Assert.Equal(PerformedWhat.Worded, performer.Word(RunUnits.IdOf(run.Run), Check.Id, 1, HeldResolutions.Again).What);
        performer.Resolve(Declared);
        Assert.Equal(NodeStates.Ready, Status(run).State);
        Assert.Null(Owed(Second));

        Assert.Equal(PerformedWhat.Sent, (await StartAsync(run)).What);
        Assert.Equal((2, First), (Attempt(run).Attempt, Attempt(run).Call.SubjectRevision));
    }

    [Fact]
    public async Task A_held_verification_names_its_line_its_batch_and_its_failed_gate()
    {
        await SettledAsync(First, VerifyVerdicts.Reached);
        var second = await SettledAsync(Second, VerifyVerdicts.Failed);

        var held = Assert.Single(HeldAttempts.Read(Files));

        Assert.True(held.Failed);
        Assert.Equal(new HeldVerification(second.Run, "engine", Second, WorkflowLevels.RepositoryLanded) { Line = "main", Base = First, Gate = "suite" },
            held.Verification);
        Assert.Contains("the verification of engine at 2222222 (its line main, the batch since 1111111) failed at the gate suite",
            HeldAttempts.WhySaid(held), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_held_unknown_verification_says_why_and_that_it_holds_its_line()
    {
        var run = await SettledAsync(First, VerifyVerdicts.Unknown);

        var held = Assert.Single(HeldAttempts.Read(Files));

        Assert.Equal((run.Run, (string?)null, VerifyResult.Changed), (held.Verification!.Run, held.Verification.Base, held.Verification.Why));
        Assert.Contains("(its line main, its first verification) is not known (the gates changed since they were accepted), and holds its line main",
            HeldAttempts.WhySaid(held), StringComparison.Ordinal);
    }

    [Fact]
    public void A_person_s_run_is_never_counted_and_a_route_with_no_pipeline_or_no_gates_binds_nothing()
    {
        VerifyRuns.Bind(_home, new VerifyRun("v-00000000000000ff", "engine", "default", "checkout", "verify", 1, _digest, First, VerifyRuns.Person, _now)
        {
            Branch = "main",
        });

        Assert.Empty(LineBatching.On(_home, "engine", "main"));
        Assert.NotNull(Owed(First));
        Assert.Null(LineBatching.Owed(_home, Head(First), Route(gates: null), Files, _now));
        Assert.Null(LineBatching.Owed(_home, Head(First), new WorkflowLandedSelected(WorkflowSelection.Current, null, WorkflowLevels.Current), Files, _now));
    }
}
