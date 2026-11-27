using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A <c>check</c> started through the performer as a performed node, its attempt naming
/// the executor and handed to it, its answer through the reconcile alone under the <c>gates</c> source, never asked at its deadline, held for
/// the person with <i>Run again</i> and <i>Not now</i> where unknown, a stop told to the executor, and a restart sending the same attempt again.
/// </summary>
public sealed class CheckPerformTests : IDisposable
{
    private const string Unit = "run:v-0123456789abcdef";
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";

    private static readonly NodeDeclaration Check = new CheckNode().Performed(new NodeDeclaration("verify", NodeKinds.Check, 1, NodeReach.Performed));

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "check-perform", Guid.NewGuid().ToString("N")[..8]);
    private readonly Checks _checks = new();
    private DateTimeOffset _now = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    public void Dispose() => GitSeed.Discard(_home);

    /// <summary>The executor as the performer sees it: what it is sent and told to stop, and what it answers.</summary>
    private sealed class Checks : ICheckPort
    {
        public List<NodeAttempt> Sent { get; } = [];

        public List<string> Stopped { get; } = [];

        public List<string> Asked { get; } = [];

        public HashSet<string> Answers { get; } = [];

        public Func<NodeAttempt, WorkReconcileAnswer> Answer { get; set; } = _ => new(ReconcileStates.Running);

        public Task SendAsync(string unit, NodeAttempt attempt, NodeDeclaration node, CancellationToken ct)
        {
            Sent.Add(attempt);
            return Task.CompletedTask;
        }

        public Task<WorkReconcileAnswer> ReconcileAsync(string unit, NodeAttempt attempt, CancellationToken ct)
        {
            Asked.Add(attempt.Call.Invocation);
            return Task.FromResult(Answer(attempt));
        }

        public bool Answered(string invocation) => Answers.Contains(invocation);

        public void Supersede(string invocation, string why) => Stop(invocation);

        public void Stop(string invocation) => Stopped.Add(invocation);
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

    private NodeDeclaration? Declared(string unit, string node) => unit == Unit && node == Check.Id ? Check : null;

    private NodeRecord Record() => Files.Read(Unit)!;

    private NodeStatus Status() => Record().Statuses[Check.Id];

    private NodeAttempt Attempt() => Record().Attempts.Single();

    private async Task<WorkPerformed> StartAsync(WorkPerformer performer)
    {
        var record = Files.Read(Unit)!;
        var before = new Dictionary<string, NodeStatus>(StringComparer.Ordinal) { [Check.Id] = record.Statuses.GetValueOrDefault(Check.Id) ?? NodeLifecycle.Start(Unit, Check.Id) };
        var evaluated = WireEvaluation.Evaluate([Check], [], before,
            new WireRun("graph-1", "v-0123456789abcdef") { Revisions = new Dictionary<string, string?> { [Unit] = Tip }, Fired = record.Fired.ToHashSet(StringComparer.Ordinal) });
        Assert.Null(evaluated.Refusal);
        performer.Keep(before, evaluated);
        return await performer.StartAsync(Check, Assert.Single(evaluated.Starts), CancellationToken.None);
    }

    private static VerifyResult Result(string verdict, string invocation) =>
        new(invocation, Tip, verdict, verdict == VerifyVerdicts.Reached ? null : "why", [new VerifyGate("suite", verdict == VerifyVerdicts.Failed ? "fail" : "pass", 3)])
        {
            Failed = verdict == VerifyVerdicts.Failed ? "suite" : null, Code = verdict == VerifyVerdicts.Unknown ? VerifyResult.Changed : null,
        };

    [Fact]
    public async Task A_check_is_written_on_its_record_and_handed_to_the_executor_as_a_performed_node()
    {
        var done = await StartAsync(Performer());

        Assert.Equal(PerformedWhat.Sent, done.What);
        var attempt = Attempt();
        Assert.Equal((NodeKinds.Check, AttemptStates.Sent, Tip), (attempt.Plugin, attempt.State, attempt.Call.SubjectRevision));
        Assert.Equal(attempt.Call.Invocation, Assert.Single(_checks.Sent).Call.Invocation);
        Assert.Equal(NodeStates.Active, Status().State);
        Assert.Equal(_now + HeavyExecutor.DefaultBound, attempt.Call.DeadlineAt);
    }

    [Fact]
    public async Task Its_answer_is_asked_only_once_the_executor_has_one_never_at_its_deadline_and_reached_under_the_gates()
    {
        var performer = Performer();
        await StartAsync(performer);
        var invocation = Attempt().Call.Invocation;

        _now += HeavyExecutor.DefaultBound + TimeSpan.FromMinutes(1);
        await performer.ReconcileAsync(Declared, CancellationToken.None);
        Assert.Empty(_checks.Asked);
        Assert.Equal(NodeStates.Active, Status().State);

        _checks.Answers.Add(invocation);
        _checks.Answer = attempt => CheckExecution.AnswerOf(Result(VerifyVerdicts.Reached, attempt.Call.Invocation));
        await performer.ReconcileAsync(Declared, CancellationToken.None);

        Assert.Equal([invocation], _checks.Asked);
        Assert.Equal((NodeStates.Reached, NodeSource.Gates), (Status().State, Status().By));
        Assert.Equal($"gates:{invocation}", Status().Acceptance!.Evidence!.Ref);
        Assert.Equal(AttemptStates.Settled, Attempt().State);
    }

    [Fact]
    public async Task A_failed_check_is_the_gates_failure_kept_for_the_person_s_new_attempt()
    {
        var performer = Performer();
        await StartAsync(performer);
        var invocation = Attempt().Call.Invocation;
        _checks.Answers.Add(invocation);
        _checks.Answer = attempt => CheckExecution.AnswerOf(Result(VerifyVerdicts.Failed, attempt.Call.Invocation));

        await performer.ReconcileAsync(Declared, CancellationToken.None);

        Assert.Equal((NodeStates.Failed, NodeSource.Gates, HeavyExecutor.GateFailed, false), (Status().State, Status().By, Status().Code, Status().Transient));
        Assert.True(WorkPerformer.KeptFailure(Record(), Attempt()));
    }

    [Fact]
    public async Task An_unknown_check_is_held_with_its_reason_and_takes_run_again_or_not_now()
    {
        var performer = Performer();
        await StartAsync(performer);
        var invocation = Attempt().Call.Invocation;
        _checks.Answers.Add(invocation);
        _checks.Answer = attempt => CheckExecution.AnswerOf(Result(VerifyVerdicts.Unknown, attempt.Call.Invocation));

        await performer.ReconcileAsync(Declared, CancellationToken.None);

        Assert.Equal((AttemptStates.Held, VerifyResult.Changed), (Attempt().State, Attempt().Code));
        Assert.Equal([HeldResolutions.Again, HeldResolutions.Stop], HeldResolutions.Of(Attempt()));
        Assert.Equal(PerformedWhat.Refused, performer.Word(Unit, Check.Id, 1, HeldResolutions.Happened).What);

        Assert.Equal(PerformedWhat.Worded, performer.Word(Unit, Check.Id, 1, HeldResolutions.Again).What);
        performer.Resolve(Declared);

        Assert.Equal(NodeStates.Ready, Status().State);
        Assert.Equal(AttemptStates.Settled, Attempt().State);
        Assert.Null(Status().Unreconciled);
    }

    [Fact]
    public async Task A_check_held_for_a_changed_declaration_is_run_again_by_itself_once_the_acceptance_moves()
    {
        const string Held = "1111111111111111111111111111111111111111";
        const string Newer = "2222222222222222222222222222222222222222";
        var performer = Performer();
        await StartAsync(performer);
        var invocation = Attempt().Call.Invocation;
        _checks.Answers.Add(invocation);
        _checks.Answer = attempt => CheckExecution.AnswerOf(Result(VerifyVerdicts.Unknown, attempt.Call.Invocation));
        await performer.ReconcileAsync(Declared, CancellationToken.None);
        Assert.Equal((AttemptStates.Held, VerifyResult.Changed), (Attempt().State, Attempt().Code));
        var path = VerifyFiles.ResultOf(_home, CheckExecution.RunOf(Unit), invocation);
        (Result(VerifyVerdicts.Unknown, invocation) with { Accepted = Held }).Write(path);

        Assert.Empty(ChangedChecks.Again(Files, _ => new ChecksStanding(Held, true), _now));
        Assert.Empty(ChangedChecks.Again(Files, _ => new ChecksStanding(null, true), _now));
        Assert.Empty(ChangedChecks.Again(Files, _ => null, _now));
        Assert.Null(Attempt().Resolution);

        var again = Assert.Single(ChangedChecks.Again(Files, _ => new ChecksStanding(Newer, true), _now));
        Assert.Equal((Unit, Check.Id, PerformedWhat.Worded), (again.Unit, again.Node, again.What));
        Assert.Equal(HeldResolutions.Again, Attempt().Resolution);
        Assert.Empty(ChangedChecks.Again(Files, _ => new ChecksStanding(Newer, true), _now));
        performer.Resolve(Declared);

        Assert.Equal((NodeStates.Ready, AttemptStates.Settled), (Status().State, Attempt().State));
    }

    [Fact]
    public async Task A_check_held_for_a_changed_declaration_is_run_again_by_itself_once_its_repository_takes_full_access()
    {
        const string Held = "1111111111111111111111111111111111111111";
        var performer = Performer();
        await StartAsync(performer);
        var invocation = Attempt().Call.Invocation;
        _checks.Answers.Add(invocation);
        _checks.Answer = attempt => CheckExecution.AnswerOf(Result(VerifyVerdicts.Unknown, attempt.Call.Invocation));
        await performer.ReconcileAsync(Declared, CancellationToken.None);
        (Result(VerifyVerdicts.Unknown, invocation) with { Accepted = Held }).Write(VerifyFiles.ResultOf(_home, CheckExecution.RunOf(Unit), invocation));
        Assert.Empty(ChangedChecks.Again(Files, _ => new ChecksStanding(Held, true), _now));
        Assert.Empty(ChangedChecks.Again(Files, _ => new ChecksStanding(null, false), _now));

        var again = Assert.Single(ChangedChecks.Again(Files, _ => new ChecksStanding(Held, false), _now));

        Assert.Equal((Unit, Check.Id, PerformedWhat.Worded), (again.Unit, again.Node, again.What));
        Assert.Equal(HeldResolutions.Again, Attempt().Resolution);
        performer.Resolve(Declared);
        Assert.Equal((NodeStates.Ready, AttemptStates.Settled), (Status().State, Attempt().State));
    }

    [Fact]
    public async Task A_check_held_for_a_changed_declaration_before_its_acceptance_was_kept_is_run_again_once_one_is_accepted()
    {
        var performer = Performer();
        await StartAsync(performer);
        var invocation = Attempt().Call.Invocation;
        _checks.Answers.Add(invocation);
        _checks.Answer = attempt => CheckExecution.AnswerOf(Result(VerifyVerdicts.Unknown, attempt.Call.Invocation));
        await performer.ReconcileAsync(Declared, CancellationToken.None);
        Result(VerifyVerdicts.Unknown, invocation).Write(VerifyFiles.ResultOf(_home, CheckExecution.RunOf(Unit), invocation));

        Assert.Single(ChangedChecks.Again(Files, _ => new ChecksStanding("3333333333333333333333333333333333333333", true), _now));
        Assert.Equal(HeldResolutions.Again, Attempt().Resolution);
    }

    [Fact]
    public async Task A_check_that_came_back_unrun_is_asked_again_as_neither_a_red_round_nor_a_pass_and_held_as_given_up_once_its_asks_are_spent()
    {
        var performer = Performer();
        _checks.Answer = attempt => CheckExecution.AnswerOf(Result(VerifyVerdicts.Unknown, attempt.Call.Invocation) with { Code = VerifyResult.Unrun });
        IReadOnlyList<WorkPerformed> last = [];
        for (var ask = 1; ask <= UnrunChecks.Bound; ask++)
        {
            await StartAsync(performer);
            _checks.Answers.Add(Record().Attempts.Last().Call.Invocation);
            last = await performer.ReconcileAsync(Declared, CancellationToken.None);

            if (ask < UnrunChecks.Bound)
            {
                Assert.Equal(NodeStates.Ready, Status().State);
                Assert.Equal((AttemptStates.Settled, WorkStates.Unknown, VerifyResult.Unrun), (Record().Attempts.Last().State, Record().Attempts.Last().Outcome, Record().Attempts.Last().Code));
                Assert.Empty(CheckRounds.Reds(Record()));
            }
        }

        await AssertGivenUpAsync(performer, last, "why");
    }

    [Fact]
    public async Task A_check_whose_executor_cannot_say_is_run_again_by_itself_and_passes_with_its_reason_logged_and_no_row_for_the_person()
    {
        const string Why = "its result does not read";
        var performer = Performer();
        _checks.Answer = _ => CheckExecution.AnswerOf(new VerifyAnswer(VerifyAnswers.Unknown) { Why = Why });
        await StartAsync(performer);
        _checks.Answers.Add(Attempt().Call.Invocation);

        var asked = Assert.Single(await performer.ReconcileAsync(Declared, CancellationToken.None));

        Assert.Equal((PerformedWhat.Retry, WorkStates.Unknown, Why), (asked.What, asked.Code, asked.Detail));
        Assert.Equal((NodeStates.Ready, AttemptStates.Settled), (Status().State, Record().Attempts.Last().State));
        Assert.Empty(HeldAttempts.Read(Files));

        _checks.Answer = attempt => CheckExecution.AnswerOf(Result(VerifyVerdicts.Reached, attempt.Call.Invocation));
        await StartAsync(performer);
        _checks.Answers.Add(Record().Attempts.Last().Call.Invocation);
        await performer.ReconcileAsync(Declared, CancellationToken.None);

        Assert.Equal(NodeStates.Reached, Status().State);
        Assert.Empty(HeldAttempts.Read(Files));
    }

    [Fact]
    public async Task A_check_that_stays_unknown_past_its_bound_is_given_up_with_its_reason_and_its_run_waits_for_a_new_attempt_never_ending()
    {
        var performer = Performer();
        _checks.Answer = _ => CheckExecution.AnswerOf(new VerifyAnswer(VerifyAnswers.Unknown) { Why = "its result does not read" });
        IReadOnlyList<WorkPerformed> last = [];
        for (var ask = 1; ask <= UnrunChecks.Bound; ask++)
        {
            await StartAsync(performer);
            _checks.Answers.Add(Record().Attempts.Last().Call.Invocation);
            last = await performer.ReconcileAsync(Declared, CancellationToken.None);
        }

        Assert.Contains("its result does not read", Assert.Single(last).Detail, StringComparison.Ordinal);
        await AssertGivenUpAsync(performer, last, "its result does not read");
    }

    private async Task AssertGivenUpAsync(WorkPerformer performer, IReadOnlyList<WorkPerformed> last, string? reason)
    {
        var record = Record();
        var gave = Assert.Single(last);
        Assert.Equal((PerformedWhat.Held, UnrunChecks.GaveUp), (gave.What, gave.Code));
        Assert.Contains("the run waits for a new attempt", gave.Detail, StringComparison.Ordinal);
        Assert.Equal(NodeStates.Unknown, Status().State);
        Assert.Equal(UnrunChecks.Bound, record.Attempts.Count);
        Assert.All(record.Attempts.SkipLast(1), attempt => Assert.Equal(AttemptStates.Settled, attempt.State));
        Assert.Equal((AttemptStates.Held, UnrunChecks.GaveUp), (record.Attempts.Last().State, record.Attempts.Last().Code));
        Assert.Empty(CheckRounds.Reds(record));
        var held = Assert.Single(HeldAttempts.Read(Files));
        var said = HeldAttempts.WhySaid(held);
        Assert.Equal(UnrunChecks.GaveUp, held.Why);
        Assert.DoesNotContain("did not bind", said, StringComparison.Ordinal);
        Assert.Contains("workflow again", said, StringComparison.Ordinal);
        Assert.Equal(reason, held.Reason);
        if (reason is not null) Assert.Contains(reason, said, StringComparison.Ordinal);
        var line = Assert.Single(WatchNodes.NodeRuntimeLines(new NodeRuntimePass(true, last, [], [])));
        Assert.Contains("the run waits for a new attempt", line, StringComparison.Ordinal);
        if (reason is not null) Assert.Contains(reason, line, StringComparison.Ordinal);
        var sent = _checks.Sent.Count;
        var asked = _checks.Asked.Count;
        Assert.Empty(await performer.ReconcileAsync(Declared, CancellationToken.None));
        Assert.Equal((sent, asked), (_checks.Sent.Count, _checks.Asked.Count));

        var (refused, kept) = Files.Edit<bool>(Unit, each =>
        {
            var (next, _) = UnknownChecks.Again(each, Status(), _now);
            return (next, next is not null);
        });
        Assert.Null(refused);
        Assert.True(kept);
        performer.Resolve(Declared);
        Assert.Equal(NodeStates.Ready, Status().State);
        await StartAsync(performer);
        Assert.Equal((NodeStates.Active, UnrunChecks.Bound + 1), (Status().State, Record().Attempts.Count));
    }

    [Fact]
    public async Task A_check_whose_old_job_could_not_be_opened_is_asked_again_and_never_run_beside_it_nor_held_for_the_person()
    {
        var performer = Performer();
        _checks.Answer = _ => CheckExecution.AnswerOf(new VerifyAnswer(VerifyAnswers.Unknown) { Why = "the job it left could not be opened (access denied)", Unaccounted = true });
        await StartAsync(performer);
        var invocation = Attempt().Call.Invocation;
        _checks.Answers.Add(invocation);

        for (var look = 1; look <= UnrunChecks.Bound + 1; look++)
        {
            var asked = Assert.Single(await performer.ReconcileAsync(Declared, CancellationToken.None));
            Assert.Equal((PerformedWhat.Unknown, VerifyResult.Unaccounted), (asked.What, asked.Code));
            Assert.Contains("access denied", asked.Detail, StringComparison.Ordinal);
        }

        Assert.Equal((invocation, AttemptStates.Unknown, 1), (Attempt().Call.Invocation, Attempt().State, Record().Attempts.Count));
        Assert.Single(_checks.Sent);

        _checks.Answer = attempt => CheckExecution.AnswerOf(Result(VerifyVerdicts.Reached, attempt.Call.Invocation));
        await performer.ReconcileAsync(Declared, CancellationToken.None);
        Assert.Equal(NodeStates.Reached, Status().State);
    }

    [Theory]
    [InlineData(VerifyResult.Changed)]
    [InlineData(VerifyResult.Stopped)]
    [InlineData(VerifyResult.Bound)]
    public async Task An_unknown_check_whose_cause_is_the_person_s_is_still_held_for_the_person(string code)
    {
        var performer = Performer();
        _checks.Answer = attempt => CheckExecution.AnswerOf(Result(VerifyVerdicts.Unknown, attempt.Call.Invocation) with { Code = code });
        await StartAsync(performer);
        _checks.Answers.Add(Attempt().Call.Invocation);

        await performer.ReconcileAsync(Declared, CancellationToken.None);

        Assert.Equal((AttemptStates.Held, code), (Attempt().State, Attempt().Code));
        Assert.Empty(UnrunChecks.Released(Files, Declared, _now));
        Assert.Equal(AttemptStates.Held, Attempt().State);
    }

    [Theory]
    [InlineData(VerifyResult.Unprepared)]
    [InlineData(VerifyResult.Busy)]
    public async Task A_check_whose_answer_did_not_bind_to_its_work_is_sent_again_by_itself_and_passes_with_no_row_for_the_person(string code)
    {
        const string Why = "the verification tree's preparation did not finish: npm ci exited 1";
        var performer = Performer();
        _checks.Answer = attempt => CheckExecution.AnswerOf(Result(VerifyVerdicts.Unknown, attempt.Call.Invocation) with { Code = code, Why = Why });
        await StartAsync(performer);
        _checks.Answers.Add(Attempt().Call.Invocation);

        var asked = Assert.Single(await performer.ReconcileAsync(Declared, CancellationToken.None));

        Assert.Equal((PerformedWhat.Retry, code, Why), (asked.What, asked.Code, asked.Detail));
        Assert.Equal((NodeStates.Ready, AttemptStates.Settled, code), (Status().State, Attempt().State, Attempt().Code));
        Assert.Empty(HeldAttempts.Read(Files));
        Assert.Empty(CheckRounds.Reds(Record()));

        _checks.Answer = attempt => CheckExecution.AnswerOf(Result(VerifyVerdicts.Reached, attempt.Call.Invocation));
        await StartAsync(performer);
        _checks.Answers.Add(Record().Attempts.Last().Call.Invocation);
        await performer.ReconcileAsync(Declared, CancellationToken.None);

        Assert.Equal((NodeStates.Reached, 2, 2), (Status().State, Record().Attempts.Count, _checks.Sent.Count));
        Assert.Empty(HeldAttempts.Read(Files));
    }

    [Fact]
    public async Task A_check_that_stays_unprepared_past_its_bound_is_given_up_with_its_reason_and_its_run_waits_for_a_new_attempt()
    {
        var performer = Performer();
        _checks.Answer = attempt => CheckExecution.AnswerOf(Result(VerifyVerdicts.Unknown, attempt.Call.Invocation) with { Code = VerifyResult.Unprepared, Why = "npm ci exited 1" });
        IReadOnlyList<WorkPerformed> last = [];
        for (var ask = 1; ask <= UnrunChecks.Bound; ask++)
        {
            await StartAsync(performer);
            _checks.Answers.Add(Record().Attempts.Last().Call.Invocation);
            last = await performer.ReconcileAsync(Declared, CancellationToken.None);
        }

        await AssertGivenUpAsync(performer, last, "npm ci exited 1");
    }

    [Fact]
    public async Task A_check_unknown_with_no_code_at_all_is_asked_again_at_once_never_held_for_a_later_look_to_release()
    {
        var performer = Performer();
        _checks.Answer = _ => new WorkReconcileAnswer(ReconcileStates.Unknown);
        await StartAsync(performer);
        _checks.Answers.Add(Attempt().Call.Invocation);

        var asked = Assert.Single(await performer.ReconcileAsync(Declared, CancellationToken.None));

        Assert.Equal((PerformedWhat.Retry, WorkStates.Unknown), (asked.What, asked.Code));
        Assert.Equal((NodeStates.Ready, AttemptStates.Settled), (Status().State, Attempt().State));
        Assert.Empty(HeldAttempts.Read(Files));
    }

    [Fact]
    public async Task A_check_said_done_with_no_verdict_is_asked_again_as_a_new_attempt()
    {
        var performer = Performer();
        _checks.Answer = _ => new WorkReconcileAnswer(ReconcileStates.Done);
        await StartAsync(performer);
        _checks.Answers.Add(Attempt().Call.Invocation);

        var asked = Assert.Single(await performer.ReconcileAsync(Declared, CancellationToken.None));

        Assert.Equal((PerformedWhat.Retry, WorkPerformer.NoOutcome), (asked.What, asked.Code));
        Assert.Equal((NodeStates.Ready, AttemptStates.Settled), (Status().State, Attempt().State));
        Assert.Empty(HeldAttempts.Read(Files));
    }

    [Fact]
    public async Task A_check_s_verdict_the_one_table_refuses_is_asked_again_as_a_new_attempt_never_held()
    {
        var performer = Performer();
        await StartAsync(performer);
        _checks.Answers.Add(Attempt().Call.Invocation);
        // The node holds no binding for the verdict to bind to, as after a restart that rebound it under the attempt.
        var record = Record();
        Files.Write(record with { Statuses = new Dictionary<string, NodeStatus>(record.Statuses, StringComparer.Ordinal) { [Check.Id] = Status() with { Binding = null } } });
        _checks.Answer = attempt => CheckExecution.AnswerOf(Result(VerifyVerdicts.Reached, attempt.Call.Invocation));

        var asked = Assert.Single(await performer.ReconcileAsync(Declared, CancellationToken.None));

        Assert.Equal((PerformedWhat.Retry, NodeRefusal.NoAcceptance), (asked.What, asked.Code));
        Assert.Contains("did not bind to the work it was sent on", asked.Detail, StringComparison.Ordinal);
        Assert.Equal((NodeStates.Ready, AttemptStates.Settled, asked.Code), (Status().State, Attempt().State, Attempt().Code));
        Assert.Empty(HeldAttempts.Read(Files));
    }

    [Theory]
    [InlineData(VerifyResult.Unprepared)]
    [InlineData(VerifyResult.Busy)]
    [InlineData(LostChecks.Code)]
    [InlineData(WorkPerformer.NoOutcome)]
    public async Task A_check_an_older_build_held_for_a_cause_only_a_new_attempt_answers_is_asked_again_by_itself(string code)
    {
        var performer = Performer();
        _checks.Answer = attempt => CheckExecution.AnswerOf(Result(VerifyVerdicts.Unknown, attempt.Call.Invocation));
        await StartAsync(performer);
        _checks.Answers.Add(Attempt().Call.Invocation);
        await performer.ReconcileAsync(Declared, CancellationToken.None);
        Files.Write(WorkPerformer.With(Record(), Attempt(), Attempt() with { Code = code }));
        Assert.Single(HeldAttempts.Read(Files));

        var released = Assert.Single(UnrunChecks.Released(Files, Declared, _now));

        Assert.Equal((PerformedWhat.Retry, code), (released.What, released.Code));
        Assert.Equal((NodeStates.Ready, AttemptStates.Settled), (Status().State, Attempt().State));
        Assert.Empty(HeldAttempts.Read(Files));
        Assert.Empty(UnrunChecks.Released(Files, Declared, _now));
    }

    [Fact]
    public async Task The_person_s_stop_of_a_running_check_is_told_to_the_executor()
    {
        var performer = Performer();
        await StartAsync(performer);
        var invocation = Attempt().Call.Invocation;
        var record = Record();
        var stopped = NodeLifecycle.Apply(Check, Status(), new NodeMove(NodeStates.Stopped, NodeCause.Person));
        Files.Write(record with { Statuses = new Dictionary<string, NodeStatus>(record.Statuses, StringComparer.Ordinal) { [Check.Id] = stopped.Status } });

        performer.StandDown(Unit, [Check.Id]);
        Assert.Equal([invocation], _checks.Stopped);

        // The result that follows is a stale attempt's: it settles the attempt and moves nothing.
        _checks.Answers.Add(invocation);
        _checks.Answer = attempt => CheckExecution.AnswerOf(Result(VerifyVerdicts.Unknown, attempt.Call.Invocation) with { Code = VerifyResult.Stopped });
        await performer.ReconcileAsync(Declared, CancellationToken.None);

        Assert.Equal(NodeStates.Stopped, Status().State);
        Assert.Equal(AttemptStates.Settled, Attempt().State);
        Assert.Null(Status().Unreconciled);
    }

    [Fact]
    public async Task A_restart_asks_the_executor_first_and_sends_the_same_attempt_again_under_its_key()
    {
        await StartAsync(Performer());
        var sent = Attempt();
        _checks.Answer = _ => CheckExecution.AnswerOf(new VerifyAnswer(VerifyAnswers.NotStarted));

        var restarted = Performer();
        var done = await restarted.ReconcileAsync(Declared, CancellationToken.None);

        Assert.Equal(PerformedWhat.Redelivered, Assert.Single(done).What);
        Assert.Equal(2, _checks.Sent.Count);
        Assert.Equal((sent.Call.Invocation, sent.Call.IdempotencyKey), (_checks.Sent[1].Call.Invocation, _checks.Sent[1].Call.IdempotencyKey));
        Assert.Equal(NodeStates.Active, Status().State);
    }

    private string Folder(NodeAttempt attempt) => VerifyFiles.AttemptFolder(_home, CheckExecution.RunOf(Unit), attempt.Call.Invocation);

    private void Began(NodeAttempt attempt, int send) =>
        AttemptMarks.Write(AttemptMarks.Began(_home, CheckExecution.RunOf(Unit), attempt.Call.Invocation, send), send, _now);

    private void Closed(NodeAttempt attempt, int send) =>
        AttemptMarks.Write(AttemptMarks.Closed(_home, CheckExecution.RunOf(Unit), attempt.Call.Invocation, send), send, _now);

    private async Task<WorkPerformed> LostAsync()
    {
        _checks.Answer = _ => CheckExecution.AnswerOf(new VerifyAnswer(VerifyAnswers.NotStarted));
        return Assert.Single(await Performer().ReconcileAsync(Declared, CancellationToken.None));
    }

    [Fact]
    public async Task A_check_lost_to_a_restart_is_run_again_once_and_lost_again_is_asked_again_as_a_new_attempt_its_log_s_tail_kept()
    {
        await StartAsync(Performer());
        var sent = Attempt();

        Assert.Equal(PerformedWhat.Redelivered, (await LostAsync()).What);
        Assert.Equal((2, 2), (_checks.Sent.Count, Attempt().Deliveries));
        Assert.Equal(PerformedWhat.Redelivered, (await LostAsync()).What);
        Assert.Equal(3, _checks.Sent.Count);

        Began(sent, 3);
        Assert.Equal(PerformedWhat.Redelivered, (await LostAsync()).What);
        Began(sent, 4);
        var log = Path.Combine(Folder(sent), "gate-01-suite.log");
        File.WriteAllText(log, string.Join('\n', Enumerable.Range(1, 40).Select(line => $"line {line}")) + "\n");
        var again = await LostAsync();

        Assert.Equal((PerformedWhat.Retry, LostChecks.Code, LostChecks.Lost), (again.What, again.Code, again.Detail));
        Assert.Equal(4, _checks.Sent.Count);
        Assert.Equal((NodeStates.Ready, AttemptStates.Settled, LostChecks.Code), (Status().State, Attempt().State, Attempt().Code));
        Assert.StartsWith($"line {40 - LostChecks.TailLines + 1}\n", Attempt().Tail);
        Assert.EndsWith("line 40", Attempt().Tail);
        Assert.Null(Attempt().Note);
        Assert.Empty(HeldAttempts.Read(Files));
        Assert.Empty(CheckRounds.Reds(Record()));
    }

    [Fact]
    public async Task A_check_the_executor_ended_by_its_own_close_is_sent_again_each_time_and_never_held()
    {
        await StartAsync(Performer());
        var sent = Attempt();

        for (var send = 1; send <= 4; send++)
        {
            Began(sent, send);
            Closed(sent, send);
            Assert.Equal(PerformedWhat.Redelivered, (await LostAsync()).What);
        }

        Assert.Equal((AttemptStates.Sent, 5), (Attempt().State, Attempt().Deliveries));
        Assert.Equal(5, _checks.Sent.Count);
    }

    [Fact]
    public async Task A_check_whose_process_vanished_with_no_word_of_why_is_asked_again_at_its_second_such_loss_whatever_closed_between()
    {
        await StartAsync(Performer());
        var sent = Attempt();

        Began(sent, 1);
        Assert.Equal(PerformedWhat.Redelivered, (await LostAsync()).What);
        Began(sent, 2);
        Closed(sent, 2);
        Assert.Equal(PerformedWhat.Redelivered, (await LostAsync()).What);
        Began(sent, 3);
        var again = await LostAsync();

        Assert.Equal((PerformedWhat.Retry, LostChecks.Code), (again.What, again.Code));
        Assert.Equal(3, _checks.Sent.Count);
    }

    [Fact]
    public async Task A_rerun_cut_off_while_still_queued_is_sent_again_though_the_first_send_made_the_folder()
    {
        await StartAsync(Performer());
        var sent = Attempt();

        Began(sent, 1);
        Assert.Equal(PerformedWhat.Redelivered, (await LostAsync()).What);
        Assert.Equal(PerformedWhat.Redelivered, (await LostAsync()).What);
        Assert.Equal(PerformedWhat.Redelivered, (await LostAsync()).What);

        Assert.Equal((AttemptStates.Sent, 4), (Attempt().State, Attempt().Deliveries));
    }

    [Fact]
    public async Task A_held_checks_run_again_stops_its_old_invocation_at_the_executor()
    {
        await StartAsync(Performer());
        var sent = Attempt();
        _checks.Answers.Add(sent.Call.Invocation);
        _checks.Answer = attempt => CheckExecution.AnswerOf(Result(VerifyVerdicts.Unknown, attempt.Call.Invocation));
        Assert.Equal(PerformedWhat.Held, Assert.Single(await Performer().ReconcileAsync(Declared, CancellationToken.None)).What);
        _checks.Stopped.Clear();

        var performer = Performer();
        Assert.Equal(PerformedWhat.Worded, performer.Word(Unit, Check.Id, Attempt().Attempt, HeldResolutions.Again).What);
        Assert.Equal(PerformedWhat.Resolved, Assert.Single(performer.Resolve(Declared)).What);
        Assert.Equal([sent.Call.Invocation], _checks.Stopped);
    }

    [Fact]
    public void A_tail_cut_inside_a_line_and_inside_a_character_starts_at_a_whole_line()
    {
        var run = CheckExecution.RunOf(Unit);
        var folder = VerifyFiles.AttemptFolder(_home, run, "i-tail");
        Directory.CreateDirectory(folder);

        // 20000 bytes in one line, then three: the read starts inside the long line, which is dropped whole.
        File.WriteAllText(Path.Combine(folder, "a.log"), new string('a', 20000) + "\nlast 1\nlast 2\nlast 3\n");
        Assert.Equal("last 1\nlast 2\nlast 3", LostChecks.Tail(_home, Unit, "i-tail"));

        File.Delete(Path.Combine(folder, "a.log"));
        File.WriteAllText(Path.Combine(folder, "b.log"), new string('界', 7000) + "\nend\n");
        Assert.Equal("end", LostChecks.Tail(_home, Unit, "i-tail"));

        File.Delete(Path.Combine(folder, "b.log"));
        File.WriteAllText(Path.Combine(folder, "c.log"), string.Concat(Enumerable.Repeat("😀", 3000)) + "y");
        var tail = LostChecks.Tail(_home, Unit, "i-tail")!;
        Assert.False(char.IsLowSurrogate(tail[0]));
        Assert.True(tail.Length <= LostChecks.TailChars);
        Assert.EndsWith("y", tail);
    }

    [Fact]
    public async Task No_completion_is_heard_for_a_check()
    {
        var performer = Performer();
        await StartAsync(performer);
        var invocation = Attempt().Call.Invocation;

        var heard = performer.Hear(
            [new WorkStepHeard(NodeKinds.Check, 1, new WorkStepFrame("check", new WorkOutcome(WorkStates.Reached), _now, "e1", Invocation: invocation))],
            Declared, _ => null);

        Assert.DoesNotContain(heard, each => each.What == PerformedWhat.Applied);
        Assert.Equal(NodeStates.Active, Status().State);
    }

    [Fact]
    public void A_result_answers_the_reconcile_as_its_verdict()
    {
        var reached = CheckExecution.AnswerOf(Result(VerifyVerdicts.Reached, "i-1"));
        var failed = CheckExecution.AnswerOf(Result(VerifyVerdicts.Failed, "i-1"));
        var unknown = CheckExecution.AnswerOf(Result(VerifyVerdicts.Unknown, "i-1"));

        Assert.Equal((ReconcileStates.Done, WorkStates.Reached), (reached.State, reached.Outcome!.State));
        Assert.Equal((ReconcileStates.Done, WorkStates.Failed, HeavyExecutor.GateFailed), (failed.State, failed.Outcome!.State, failed.Outcome.Code));
        Assert.Equal((ReconcileStates.Unknown, VerifyResult.Changed), (unknown.State, unknown.Outcome!.Code));
        Assert.Equal(ReconcileStates.Running, CheckExecution.AnswerOf(new VerifyAnswer(VerifyAnswers.Running)).State);
    }
}
