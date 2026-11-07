using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Landed work is verified by itself, one run per line at a time. A batch's run is bound in a folder of its own at the
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
         "nodes":[{"id":"verify","kind":"check","version":1,"reach":"performed","timeoutSeconds":600,"options":{"ask":"fix"}}],"wires":[]}
        """;

    private static readonly NodeDeclaration Check = new CheckNode().Performed(new NodeDeclaration("verify", NodeKinds.Check, 1, NodeReach.Performed));

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

    /// <summary>The executor as the performer sees it: what it is sent, and what it answers once it has an answer.</summary>
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

    private WorkPerformer Performer() => new(Files, new NoPlugins(), () => _now) { Doors = NodeDoors.None.With(NodeKinds.Check, _checks) };

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
        // A failed gate's log is longer than the ask carries, ending in a mark the ask's file must still hold.
        var log = Path.Combine(_home, $"{run.Run}-suite.log");
        File.WriteAllText(log, new string('x', 300 * 1024) + "THE END");
        var result = new VerifyResult(invocation, run.Commit, verdict, verdict == VerifyVerdicts.Reached ? null : "why",
            [new VerifyGate("suite", verdict == VerifyVerdicts.Failed ? "fail" : "pass", 3) { Log = log }])
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
        // The tip it is put off at waits for the next landing, whose batch takes it in from the last verdict.
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
    public async Task Not_now_on_a_failure_settles_it_as_the_person_s_and_the_verdict_stays_failed()
    {
        var run = await SettledAsync(First, VerifyVerdicts.Failed);
        var held = Assert.Single(HeldAttempts.Read(Files));
        Assert.Equal([HeldResolutions.AskFix, HeldResolutions.Again, HeldResolutions.Stop], held.Words);

        Assert.Equal(PerformedWhat.Worded, HeldAttempts.Word(Files, RunUnits.IdOf(run.Run), Check.Id, 1, HeldResolutions.Stop).What);

        // It leaves *What needs you* and asks nothing again, yet the run is still the line's failed verdict, the next batch's base.
        Assert.Empty(HeldAttempts.Read(Files));
        Assert.Equal(LineVerdicts.Failed, LineBatching.Settled(Files.Read(RunUnits.IdOf(run.Run)), [Check.Id]));
        Assert.Equal(First, Owed(Second)?.Base);
    }

    [Fact]
    public async Task Ask_for_a_fix_publishes_one_ask_for_the_failed_gate_with_its_log_and_the_kind_the_check_names()
    {
        var run = await SettledAsync(First, VerifyVerdicts.Failed);
        var (service, heard) = ReviewChoiceStandIn.Of((method, path, _) => (method, path) switch
        {
            ("GET", "/api/asks") => (System.Net.HttpStatusCode.OK, "[]"),
            ("POST", "/api/asks") => (System.Net.HttpStatusCode.OK, """{"ask":{"id":"a1b2c3"},"message":"Asked."}"""),
            _ => null,
        });

        var pressed = await VerifyFixAsks.AskAsync(service, Files, RunUnits.IdOf(run.Run), Check.Id, 1, CancellationToken.None);

        Assert.True(pressed.Done);
        Assert.Equal("a1b2c3", pressed.Ask);
        using var sent = System.Text.Json.JsonDocument.Parse(Assert.Single(heard, call => call.Method == "POST").Body);
        var root = sent.RootElement;
        Assert.Equal(("engine", "default", "fix"), (root.GetProperty("to").GetString(), root.GetProperty("workspace").GetString(), root.GetProperty("kind").GetString()));
        Assert.StartsWith(VerifyFixAsks.First("main", "suite", run.Run), root.GetProperty("sentence").GetString(), StringComparison.Ordinal);
        var file = Assert.Single(root.GetProperty("attachments").EnumerateArray());
        Assert.Equal("suite.log", file.GetProperty("name").GetString());
        var content = Convert.FromBase64String(file.GetProperty("content").GetString()!);
        Assert.Equal(VerifyFixAsks.LogBytes, content.Length);
        Assert.EndsWith("THE END", System.Text.Encoding.UTF8.GetString(content), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ask_for_a_fix_finds_the_ask_it_made_and_adds_a_later_failure_of_the_gate_to_its_channel()
    {
        var first = await SettledAsync(First, VerifyVerdicts.Failed);
        string Listed(string run) =>
            $$"""[{"id":"a1","workspace":"default","sentence":"{{VerifyFixAsks.First("main", "suite", run)}}","state":"Published","tier":"named"}]""";
        var (service, heard) = ReviewChoiceStandIn.Of((method, path, _) => (method, path) switch
        {
            ("GET", "/api/asks") => (System.Net.HttpStatusCode.OK, Listed(first.Run)),
            _ => null,
        });

        var again = await VerifyFixAsks.AskAsync(service, Files, RunUnits.IdOf(first.Run), Check.Id, 1, CancellationToken.None);

        Assert.Equal((true, "a1"), (again.Done, again.Ask));
        Assert.DoesNotContain(heard, call => call.Method == "POST");

        // The next batch's failure of the same gate is another run: the open ask takes it on its channel, and no second ask is made.
        var second = await SettledAsync(Second, VerifyVerdicts.Failed);
        await Record.ExceptionAsync(() => VerifyFixAsks.AskAsync(service, Files, RunUnits.IdOf(second.Run), Check.Id, 1, CancellationToken.None));
        Assert.DoesNotContain(heard, call => call.Method == "POST" && call.Path == "/api/asks");
        Assert.Contains(heard, call => call.Method == "POST" && call.Path.StartsWith("/api/asks/a1", StringComparison.Ordinal));
    }

    /// <summary>The verification pipeline saved again with its check saying <c>failed: ask</c>, the person's standing say-so.</summary>
    private void SaysAsk() => File.WriteAllText(WorkflowStore.PathOf(_home, "verify"),
        $$"""{"id":"verify","name":"Verify","pipeline":"verification","versions":[{"version":1,"at":"2026-11-01T09:00:00Z","door":"terminal","schema":2,"graph":{{Graph.Replace("{\"ask\":\"fix\"}", "{\"ask\":\"fix\",\"failed\":\"ask\"}", StringComparison.Ordinal)}}}]}""");

    [Fact]
    public async Task A_failure_whose_check_says_failed_ask_is_asked_for_with_no_press_and_only_once()
    {
        SaysAsk();
        var run = await SettledAsync(First, VerifyVerdicts.Failed);
        var made = false;
        var (service, heard) = ReviewChoiceStandIn.Of((method, path, _) => (method, path) switch
        {
            ("GET", "/api/asks") => (System.Net.HttpStatusCode.OK, made
                ? $$"""[{"id":"a1b2c3","workspace":"default","sentence":"{{VerifyFixAsks.First("main", "suite", run.Run)}}","state":"Published","tier":"named"}]"""
                : "[]"),
            ("POST", "/api/asks") => (System.Net.HttpStatusCode.OK, """{"ask":{"id":"a1b2c3"},"message":"Asked."}"""),
            _ => null,
        });

        Assert.Equal([new VerifyFixStanding.Owed(RunUnits.IdOf(run.Run), Check.Id, 1)], VerifyFixStanding.Of(Files));
        var said = await VerifyFixStanding.AskAsync(service, Files, CancellationToken.None);
        made = true;
        var again = await VerifyFixStanding.AskAsync(service, Files, CancellationToken.None);

        Assert.Contains("asked for a fix of the gate suite: #a1b2c3", Assert.Single(said), StringComparison.Ordinal);
        Assert.Empty(again);
        Assert.Single(heard, call => call.Method == "POST" && call.Path == "/api/asks");
        // The failure stays the person's to see, with Run again and Not now.
        Assert.Single(HeldAttempts.Read(Files));
    }

    [Fact]
    public async Task A_failure_whose_check_waits_for_the_press_asks_nothing_by_itself()
    {
        await SettledAsync(First, VerifyVerdicts.Failed);
        var (service, heard) = ReviewChoiceStandIn.Of((_, _, _) => null);

        Assert.Empty(VerifyFixStanding.Of(Files));
        Assert.Empty(await VerifyFixStanding.AskAsync(service, Files, CancellationToken.None));
        Assert.Empty(heard);
    }

    [Fact]
    public void The_fix_ask_names_the_sessions_whose_landings_carried_the_batch()
    {
        var at = new DateTimeOffset(2026, 11, 6, 8, 0, 0, TimeSpan.Zero);
        IntegrationRequest Landed(string id, string branch, string tip, int minutes, string? session = null) =>
            new(id, "engine", "default", branch, tip, IntegrationRoutes.Merge, "main", IntegrationRequester.Press, new LandingIntegrate().Resolved(), at)
            {
                Session = session, ClosedAt = at.AddMinutes(minutes), ClosedWhy = IntegrationRequestCloses.Settled,
            };
        var settled = new[]
        {
            Landed("r3", "daoris/tree-b", Third, 3),
            Landed("r2", "quest/fix-q2", Second, 2, session: "s-quest"),
            Landed("r1", "daoris/tree-a", First, 1),
        };
        var sessions = new[]
        {
            new SessionRecord("s-b", "engine", "done") { Tree = Path.Combine("trees", "tree-b"), Created = at },
            new SessionRecord("s-other", "web", "done") { Tree = Path.Combine("trees", "tree-b"), Created = at },
        };

        // The batch holds the second and third landings; the first landed before its base.
        var carried = VerifyBatchSessions.Of(settled, new HashSet<string>([Second, Third], StringComparer.OrdinalIgnoreCase), sessions);

        Assert.Equal(["s-quest", "s-b"], carried);
        var run = new VerifyRun("v-0000000000000001", "engine", "default", "checkout", "verify", 1, _digest, Third, WorkflowLevels.RepositoryLanded, _now)
        {
            Line = "main", Base = First,
        };
        var result = new VerifyResult("i-1", Third, VerifyVerdicts.Failed, "why", [new VerifyGate("suite", "fail", 3)]) { Failed = "suite" };
        var words = VerifyFixAsks.Sentence(run, "main", "suite", result, VerifyFixAsks.First("main", "suite", run.Run),
            [new(Third, "3333333 third"), new(Second, "2222222 second")], carried);
        Assert.Contains("- 3333333 third\n- 2222222 second", words.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.EndsWith("The batch landed from the sessions `s-quest`, `s-b`.", words, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ask_for_a_fix_is_refused_on_a_verification_that_did_not_fail()
    {
        var run = await SettledAsync(First, VerifyVerdicts.Unknown);
        var (service, heard) = ReviewChoiceStandIn.Of((_, _, _) => null);

        var pressed = await VerifyFixAsks.AskAsync(service, Files, RunUnits.IdOf(run.Run), Check.Id, 1, CancellationToken.None);

        Assert.False(pressed.Done);
        Assert.Empty(heard);
    }

    [Theory]
    [InlineData("fix v-1", "v-1", null)]
    [InlineData("fix #v-1 verify", "v-1", "verify")]
    public void Workflow_fix_reads_a_run_and_an_optional_node(string words, string run, string? node)
    {
        Assert.Equal(new WorkflowFixAsk(run, node), WorkflowFixCommand.Read(words.Split(' '), out var problem));
        Assert.Null(problem);
        Assert.True(WorkflowFixCommand.Asks(words.Split(' ')));
    }

    [Theory]
    [InlineData("fix")]
    [InlineData("fix --yes")]
    [InlineData("fix v-1 verify extra")]
    public void Workflow_fix_with_other_words_is_refused_with_its_usage(string words)
    {
        Assert.Null(WorkflowFixCommand.Read(words.Split(' '), out var problem));
        Assert.Contains("workflow fix <run> [<node>]", problem, StringComparison.Ordinal);
        Assert.Contains("usage: daoris workflow fix <run> [<node>]", WorkflowFixCommand.Usage, StringComparison.Ordinal);
    }

    private (string Output, int Exit, IReadOnlyList<(string Method, string Path, string Body)> Heard) Fixed(
        string run, string? node, Func<string, string, string, (System.Net.HttpStatusCode, string)?> serve, string? session = null)
    {
        var (service, heard) = ReviewChoiceStandIn.Of(serve);
        var output = new StringWriter { NewLine = "\n" };
        var exit = WorkflowFixCommand.RunAsync(new WorkflowFixAsk(run, node), Files, () => service, output, instance: null,
            variable => variable == DriverChoicesCommand.SessionVariable ? session : null).GetAwaiter().GetResult();
        return (output.ToString(), exit, heard);
    }

    [Fact]
    public async Task Workflow_fix_asks_for_the_failed_gate_and_says_the_ask_and_a_repeat_finds_it()
    {
        var run = await SettledAsync(First, VerifyVerdicts.Failed);

        var asked = Fixed(run.Run, null, (method, path, _) => (method, path) switch
        {
            ("GET", "/api/asks") => (System.Net.HttpStatusCode.OK, "[]"),
            ("POST", "/api/asks") => (System.Net.HttpStatusCode.OK, """{"ask":{"id":"a1b2c3"},"message":"Asked."}"""),
            _ => null,
        });
        Assert.Equal((0, "workflow: asked for a fix of the gate suite: #a1b2c3\n"), (asked.Exit, asked.Output));

        var again = Fixed(run.Run, Check.Id, (method, path, _) => (method, path) switch
        {
            ("GET", "/api/asks") => (System.Net.HttpStatusCode.OK,
                $$"""[{"id":"a1","workspace":"default","sentence":"{{VerifyFixAsks.First("main", "suite", run.Run)}}","state":"Published","tier":"named"}]"""),
            _ => null,
        });
        Assert.Equal((0, "workflow: the ask for this failure is made already: #a1\n"), (again.Exit, again.Output));
        Assert.DoesNotContain(again.Heard, call => call.Method == "POST");
    }

    [Fact]
    public async Task Workflow_fix_refuses_an_unknown_run_and_a_node_that_did_not_fail_and_asks_nothing()
    {
        var unknown = await SettledAsync(First, VerifyVerdicts.Unknown);

        var noRun = Fixed("v-00000000000000aa", null, (_, _, _) => null);
        var notFailed = Fixed(unknown.Run, null, (_, _, _) => null);
        var noNode = Fixed(unknown.Run, "other", (_, _, _) => null);

        foreach (var refused in new[] { noRun, notFailed, noNode })
        {
            Assert.Equal((1, "workflow: that is not a failed verification held for you\n"), (refused.Exit, refused.Output));
            Assert.Empty(refused.Heard);
        }
    }

    [Fact]
    public async Task Workflow_fix_is_refused_from_a_session_s_terminal_and_asks_nothing()
    {
        var run = await SettledAsync(First, VerifyVerdicts.Failed);

        var refused = Fixed(run.Run, null, (_, _, _) => null, session: "s1");

        Assert.Equal(1, refused.Exit);
        Assert.Contains(DriverChoicesCommand.SessionsTerminal, refused.Output, StringComparison.Ordinal);
        Assert.Empty(refused.Heard);
    }

    [Fact]
    public async Task Work_held_lists_a_failed_check_s_words_without_ask_fix_and_points_at_the_verb_that_asks()
    {
        var run = await SettledAsync(First, VerifyVerdicts.Failed);
        var unit = RunUnits.IdOf(run.Run);

        var lines = HeldAttempts.Lines(HeldAttempts.Read(Files));

        Assert.Contains($"  say: daoris work held {unit} verify 1 again|stop", lines);
        Assert.Contains($"  ask for a fix: daoris workflow fix {run.Run} verify", lines);
        var refused = HeldAttempts.Word(Files, unit, Check.Id, 1, HeldResolutions.AskFix);
        Assert.Equal(PerformedWhat.Refused, refused.What);
        Assert.Contains("daoris workflow fix <run> <node>", HeldAttempts.Said(refused, 1, HeldResolutions.AskFix), StringComparison.Ordinal);
        Assert.Single(HeldAttempts.Read(Files));
    }

    [Fact]
    public async Task A_run_no_later_run_made_needed_is_cleared_with_the_history_and_the_newest_verdict_and_an_unsettled_run_stay()
    {
        var first = await SettledAsync(First, VerifyVerdicts.Reached);
        var second = await SettledAsync(Second, VerifyVerdicts.Failed);
        var third = Bound(Third);

        var going = LandedRunsLeft.Of(_home).Select(each => each.Path).ToList();

        Assert.Contains(VerifyRuns.PathOf(_home, first.Run), going);
        Assert.Contains(NodeRecordFiles.PathOf(_home, RunUnits.IdOf(first.Run)), going);
        Assert.DoesNotContain(VerifyRuns.PathOf(_home, second.Run), going);
        Assert.DoesNotContain(VerifyRuns.PathOf(_home, third.Run), going);
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
