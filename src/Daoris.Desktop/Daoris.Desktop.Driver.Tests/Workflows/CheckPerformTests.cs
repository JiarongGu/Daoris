using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PIPELINE1d (the pipeline design §4.1, §4.2): a <c>check</c> started through the performer as a performed node, its attempt naming
/// the executor and handed to it, its answer through the reconcile alone under the <c>gates</c> source, never asked at its deadline, held for
/// the person with <i>Run again</i> and <i>Not now</i> where unknown, a stop told to the executor, and a restart sending the same attempt again.
/// </summary>
public sealed class CheckPerformTests : IDisposable
{
    private const string Unit = "run:v-0123456789abcdef";
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";

    private static readonly NodeDeclaration Check = CheckNodes.Performed(new NodeDeclaration("verify", NodeKinds.Check, 1, NodeReach.Performed));

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "check-perform", Guid.NewGuid().ToString("N")[..8]);
    private readonly Checks _checks = new();
    private DateTimeOffset _now = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    public void Dispose() => GitSeed.Discard(_home);

    /// <summary>The executor as the performer sees it: what it was sent and stopped, and what it answers.</summary>
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

    private WorkPerformer Performer() => new(Files, new NoPlugins(), () => _now) { Checks = _checks };

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
    public async Task The_person_s_stop_of_a_running_check_is_told_to_the_executor()
    {
        var performer = Performer();
        await StartAsync(performer);
        var invocation = Attempt().Call.Invocation;
        var record = Record();
        var stopped = NodeLifecycle.Apply(Check, Status(), new NodeMove(NodeStates.Stopped, NodeCause.Person));
        Files.Write(record with { Statuses = new Dictionary<string, NodeStatus>(record.Statuses, StringComparer.Ordinal) { [Check.Id] = stopped.Status } });

        performer.StopChecks(Unit, [Check.Id]);
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
