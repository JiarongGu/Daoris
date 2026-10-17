using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PERFORM1b (the fabric events design §D6, §E2): durable execution of a plugin's performed node over a stand-in port. An attempt is
/// on its unit's record before it is sent; a loop that ended between the record and the send is reconciled, never sent twice; an outcome is
/// applied once; an answer is taken from the plugin's current process alone, a replay of it from a later one included; a transient failure is
/// retried after its persisted delay, any other never; an outcome nothing establishes is held for the person.
/// </summary>
public sealed class WorkPerformerTests : IDisposable
{
    private const string Unit = "unit-a";
    private const string Plugin = "acme.deploy";
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";

    private static readonly NodeDeclaration Deploy = new("deploy", Plugin + "/deploy", 1, NodeReach.Performed)
    {
        Retry = new NodeRetry(3, [30, 60]),
        Perform = new NodePerform(Idempotent: true, Reconcile: true, Transient: ["unavailable"]),
    };

    /// <summary>A second performed node on the same unit (FABRIC-PERFORM1h): its record is the one both nodes' attempts share.</summary>
    private static readonly NodeDeclaration Notify = new("notify", Plugin + "/notify", 1, NodeReach.Performed)
    {
        Perform = new NodePerform(Idempotent: true, Reconcile: true, Transient: []),
    };

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-perform-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly Port _port = new();

    private DateTimeOffset _now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Whether this process holds the home's lease, as the record's edits ask it.</summary>
    private bool _held = true;

    /// <summary>The deploy node as the test declares it: a test may take its <c>reconcile</c> away.</summary>
    private NodeDeclaration _deploy = Deploy;

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary>A plugin's work process as the performer sees it: its generation, its sends and what it answers when asked.</summary>
    private sealed class Port : IWorkPerformPort
    {
        public int? Generation { get; set; } = 1;

        public List<(WorkPerformCall Call, int Generation)> Sent { get; } = [];

        public List<WorkReconcileCall> Asked { get; } = [];

        public Func<WorkPerformCall, Task>? OnPerform { get; set; }

        /// <summary>What a reconciliation waits on before it answers: a test hears an outcome meanwhile (FABRIC-PERFORM1h).</summary>
        public Func<WorkReconcileCall, Task>? OnReconcile { get; set; }

        public Func<WorkReconcileCall, WorkReconcileAnswer> Answer { get; set; } = _ => new(ReconcileStates.Unknown);

        public async Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct)
        {
            Assert.Equal(Plugin, plugin);
            if (OnPerform is { } before) await before(call);
            var generation = Generation ?? throw new DriverException("not running");
            lock (Sent) Sent.Add((call, generation));
            return generation;
        }

        public async Task<(WorkReconcileAnswer Answer, int Generation)> ReconcileStepAsync(string plugin, WorkReconcileCall call, CancellationToken ct)
        {
            if (OnReconcile is { } before) await before(call);
            var generation = Generation ?? throw new DriverException("not running");
            Asked.Add(call);
            return (Answer(call), generation);
        }

        public int? GenerationOf(string plugin) => Generation;
    }

    private NodeRecordFiles Files => new(_home, () => _held);

    /// <summary>A new performer over the same home: what a loop started again after a restart holds.</summary>
    private WorkPerformer Performer() => new(Files, _port, () => _now);

    private NodeDeclaration? Declared(string unit, string node) =>
        unit != Unit ? null : node == Deploy.Id ? _deploy : node == Notify.Id ? Notify : null;

    private NodeRecord Record() => Files.Read(Unit)!;

    private NodeStatus Status() => Record().Statuses[Deploy.Id];

    /// <summary>The evaluation as the loop runs it: the statuses from the record, their moves kept on it.</summary>
    private WireEvaluated Evaluate(WorkPerformer performer, IReadOnlyList<NodeDeclaration>? nodes = null, string run = "run-1")
    {
        nodes ??= [_deploy];
        var record = Record();
        var before = nodes.ToDictionary(node => node.Id, node => record.Statuses.GetValueOrDefault(node.Id) ?? NodeLifecycle.Start(Unit, node.Id), StringComparer.Ordinal);
        var evaluated = WireEvaluation.Evaluate(nodes, [], before, Run(record, run));
        Assert.Null(evaluated.Refusal);
        performer.Keep(before, evaluated);
        return evaluated;
    }

    private static WireRun Run(NodeRecord record, string run = "run-1") =>
        new("graph-1", run) { Revisions = new Dictionary<string, string?> { [Unit] = Tip }, Fired = record.Fired.ToHashSet(StringComparer.Ordinal) };

    private async Task<WorkPerformed> StartAsync(WorkPerformer performer)
    {
        var start = Assert.Single(Evaluate(performer).Starts);
        return await performer.StartAsync(_deploy, start, CancellationToken.None);
    }

    /// <summary>A move the person makes on the deploy node (a stop, a new attempt), kept on its record.</summary>
    private void Move(NodeMove move)
    {
        var record = Record();
        var moved = NodeLifecycle.Apply(_deploy, record.Statuses[Deploy.Id], move);
        Assert.True(moved.Moved, moved.Refused);
        Files.Write(record with { Statuses = new Dictionary<string, NodeStatus>(record.Statuses, StringComparer.Ordinal) { [Deploy.Id] = moved.Status } });
    }

    private WorkStepHeard Heard(string invocation, WorkOutcome outcome, int generation = 1, string id = "e1") =>
        new(Plugin, generation, new WorkStepFrame("deploy", outcome, _now, id, Invocation: invocation));

    private static WorkOutcome Reached => new(WorkStates.Reached, Evidence: new WorkEvidence("acme.deploy/receipt@1:r1", null, null, "deployed"));

    [Fact]
    public async Task An_attempt_is_written_on_its_units_record_with_its_start_before_it_is_sent()
    {
        var performer = Performer();
        NodeAttempt? seen = null;
        NodeStatus? started = null;
        _port.OnPerform = call =>
        {
            var record = Files.Read(Unit)!;
            seen = record.Invoked(call.Invocation);
            started = record.Statuses[Deploy.Id];
            return Task.CompletedTask;
        };

        var done = await StartAsync(performer);

        Assert.Equal(PerformedWhat.Sent, done.What);
        Assert.NotNull(seen);
        Assert.Equal((AttemptStates.Recorded, 1, Plugin), (seen!.State, seen.Attempt, seen.Plugin));
        Assert.Equal((NodeStates.Active, 1, (int?)1), (started!.State, started.Attempt, started.Unreconciled));
        var (call, _) = Assert.Single(_port.Sent);
        Assert.Equal((seen.Call.Invocation, seen.Call.IdempotencyKey, Tip, Deploy.Kind), (call.Invocation, call.IdempotencyKey, call.SubjectRevision, call.Step));
        var sent = Record().Invoked(call.Invocation)!;
        Assert.Equal((AttemptStates.Sent, (int?)1, 1), (sent.State, sent.Generation, sent.Deliveries));
    }

    [Fact]
    public async Task A_loop_ended_between_the_record_and_the_send_is_reconciled_first_and_never_sent_twice()
    {
        var first = Performer();
        _port.OnPerform = _ => throw new OperationCanceledException();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => StartAsync(first));
        Assert.Empty(_port.Sent);
        var recorded = Assert.Single(Record().Attempts);
        Assert.Equal(AttemptStates.Recorded, recorded.State);

        // The loop starts again: no new attempt is offered while the recorded one is unreconciled.
        _port.OnPerform = null;
        var again = Performer();
        Assert.Empty(Evaluate(again).Starts);

        _port.Answer = call => new(call.IdempotencyKey == recorded.Call.IdempotencyKey ? ReconcileStates.NotStarted : ReconcileStates.Unknown);
        var reconciled = await again.ReconcileAsync(Declared, CancellationToken.None);

        Assert.Equal(PerformedWhat.Redelivered, Assert.Single(reconciled).What);
        Assert.Equal(recorded.Call.Invocation, Assert.Single(_port.Asked).Invocation);
        var (call, _) = Assert.Single(_port.Sent);
        Assert.Equal((recorded.Call.Invocation, recorded.Call.IdempotencyKey), (call.Invocation, call.IdempotencyKey));
        Assert.Equal(AttemptStates.Sent, Assert.Single(Record().Attempts).State);
        Assert.Equal((NodeStates.Active, 1), (Status().State, Status().Attempt));
        Assert.Empty(Evaluate(again).Starts);
    }

    [Fact]
    public async Task A_duplicate_answer_is_applied_once()
    {
        var performer = Performer();
        await StartAsync(performer);
        var invocation = Assert.Single(_port.Sent).Call.Invocation;

        var heard = performer.Hear([Heard(invocation, Reached), Heard(invocation, Reached, id: "e2")], Declared);

        Assert.Equal([PerformedWhat.Applied, PerformedWhat.Replay], heard.Select(each => each.What));
        Assert.Equal(NodeStates.Reached, Status().State);
        Assert.Null(Status().Unreconciled);
        var attempt = Assert.Single(Record().Attempts);
        Assert.Equal((AttemptStates.Settled, WorkStates.Reached), (attempt.State, attempt.Outcome));
    }

    [Fact]
    public async Task A_stale_generations_answer_is_refused_and_a_replay_from_the_current_one_is_taken()
    {
        var performer = Performer();
        await StartAsync(performer);
        var invocation = Assert.Single(_port.Sent).Call.Invocation;
        _port.Generation = 2;

        var stale = Assert.Single(performer.Hear([Heard(invocation, Reached, generation: 1)], Declared));
        Assert.Equal((PerformedWhat.NotTaken, WorkRefusals.NotSender), (stale.What, stale.Code));
        Assert.Equal(NodeStates.Active, Status().State);

        var other = Assert.Single(performer.Hear([new WorkStepHeard("other.plugin", 2, Heard(invocation, Reached).Step)], Declared));
        Assert.Equal((PerformedWhat.NotTaken, WorkRefusals.NotSender), (other.What, other.Code));

        Assert.Equal(PerformedWhat.Applied, Assert.Single(performer.Hear([Heard(invocation, Reached, generation: 2)], Declared)).What);
        Assert.Equal(NodeStates.Reached, Status().State);
        Assert.Equal(PerformedWhat.Replay, Assert.Single(performer.Hear([Heard(invocation, Reached, generation: 2, id: "e3")], Declared)).What);
        var none = Assert.Single(performer.Hear([Heard("i-unknown", Reached, generation: 2)], Declared));
        Assert.Equal((PerformedWhat.NotTaken, WorkPerformer.NotAttempt), (none.What, none.Code));
    }

    [Fact]
    public async Task A_transient_failure_is_retried_with_a_new_key_after_its_persisted_delay()
    {
        var performer = Performer();
        await StartAsync(performer);
        var first = Assert.Single(_port.Sent).Call;

        Assert.Equal(PerformedWhat.Applied, Assert.Single(performer.Hear([Heard(first.Invocation, new(WorkStates.Failed, Code: "unavailable"))], Declared)).What);
        Assert.Equal((NodeStates.Failed, true), (Status().State, Status().Transient));
        Assert.Equal(_now.AddSeconds(30), Record().Invoked(first.Invocation)!.RetryAt);

        // A restart keeps the delay: not before it, then ready, then a new attempt with a new key.
        _now = _now.AddSeconds(10);
        var again = Performer();
        Assert.Empty(again.Due(Declared));
        Assert.Equal(NodeStates.Failed, Status().State);
        _now = _now.AddSeconds(21);
        Assert.Equal(PerformedWhat.Retry, Assert.Single(again.Due(Declared)).What);
        Assert.Equal(NodeStates.Ready, Status().State);

        Assert.Equal(PerformedWhat.Sent, (await StartAsync(again)).What);
        var second = _port.Sent[1].Call;
        Assert.Equal(2, Status().Attempt);
        Assert.NotEqual(first.IdempotencyKey, second.IdempotencyKey);
        Assert.NotEqual(first.Invocation, second.Invocation);
    }

    [Fact]
    public async Task A_failure_whose_code_is_not_transient_is_never_retried_by_itself()
    {
        var performer = Performer();
        await StartAsync(performer);
        var call = Assert.Single(_port.Sent).Call;

        performer.Hear([Heard(call.Invocation, new(WorkStates.Failed, Code: "denied"))], Declared);
        _now = _now.AddHours(1);

        Assert.Empty(Performer().Due(Declared));
        Assert.Equal((NodeStates.Failed, false), (Status().State, Status().Transient));
        Assert.Null(Record().Invoked(call.Invocation)!.RetryAt);
        Assert.Empty(Evaluate(Performer()).Starts);
        Assert.Single(_port.Sent);
    }

    [Fact]
    public async Task An_outcome_reconciliation_cannot_establish_is_held_for_the_person_with_no_new_attempt()
    {
        var performer = Performer();
        await StartAsync(performer);
        _port.Generation = 2;

        var reconciled = await Performer().ReconcileAsync(Declared, CancellationToken.None);

        Assert.Equal(PerformedWhat.Held, Assert.Single(reconciled).What);
        Assert.Equal((NodeStates.Unknown, (int?)1), (Status().State, Status().Unreconciled));
        Assert.Equal(AttemptStates.Held, Assert.Single(Record().Attempts).State);
        Assert.Empty(await Performer().ReconcileAsync(Declared, CancellationToken.None));
        Assert.Empty(Evaluate(Performer()).Starts);
        Assert.Single(_port.Sent);
    }

    [Fact]
    public async Task A_done_answer_records_the_outcome_as_if_it_came_by_its_event()
    {
        var performer = Performer();
        await StartAsync(performer);

        // A restart: the attempt was sent by a loop that has ended, so it is asked after before anything else.
        _port.Answer = _ => new(ReconcileStates.Done, Reached);
        var reconciled = await Performer().ReconcileAsync(Declared, CancellationToken.None);

        Assert.Equal(PerformedWhat.Applied, Assert.Single(reconciled).What);
        Assert.Equal(NodeStates.Reached, Status().State);
        Assert.Equal(AttemptStates.Settled, Assert.Single(Record().Attempts).State);
    }

    // ——— FABRIC-PERFORM1h: the review's findings, each seen failing on PERFORM1b first

    [Fact]
    public async Task Two_starts_on_one_ready_node_dispatch_one_attempt()
    {
        var start = Assert.Single(Evaluate(Performer()).Starts);
        Task<WorkPerformed> one, two;

        // Both starts reach the record while another edit holds its lock: each must read what the other wrote.
        using (RecordLock.Take(RecordLock.PathOf(_home, "units-" + NodeRecordFiles.KeyOf(Unit))))
        {
            one = Task.Run(() => Performer().StartAsync(Deploy, start, CancellationToken.None));
            two = Task.Run(() => Performer().StartAsync(Deploy, start, CancellationToken.None));
            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }

        var done = await Task.WhenAll(one, two);

        Assert.Single(done, each => each.What == PerformedWhat.Sent);
        Assert.Single(done, each => each.What == PerformedWhat.Refused);
        Assert.Single(_port.Sent);
        Assert.Equal(_port.Sent[0].Call.Invocation, Assert.Single(Record().Attempts).Call.Invocation);
        Assert.Equal((NodeStates.Active, 1, 1), (Status().State, Status().Attempt, Status().Tries));
    }

    [Fact]
    public async Task Without_the_lease_no_move_is_kept_and_no_attempt_is_written_or_sent()
    {
        var performer = Performer();
        var fresh = new Dictionary<string, NodeStatus>(StringComparer.Ordinal) { [Deploy.Id] = NodeLifecycle.Start(Unit, Deploy.Id) };
        var evaluated = WireEvaluation.Evaluate([Deploy], [], fresh, Run(Record()));
        _held = false;

        var kept = Assert.Single(performer.Keep(fresh, evaluated));
        Assert.Equal((PerformedWhat.Refused, WorkPerformer.NotHeld), (kept.What, kept.Code));
        Assert.Empty(Record().Statuses);

        _held = true;
        performer.Keep(fresh, evaluated);
        _held = false;
        var started = await performer.StartAsync(Deploy, Assert.Single(evaluated.Starts), CancellationToken.None);

        Assert.Equal((PerformedWhat.Refused, WorkPerformer.NotHeld), (started.What, started.Code));
        Assert.Empty(_port.Sent);
        Assert.Empty(Record().Attempts);
        Assert.Equal(NodeStates.Ready, Status().State);
    }

    [Fact]
    public async Task A_receipt_awaited_while_another_node_of_its_unit_starts_keeps_that_nodes_attempt()
    {
        var performer = Performer();
        var starts = Evaluate(performer, [Deploy, Notify]).Starts;
        var receipt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _port.OnPerform = call => call.Step == Deploy.Kind ? receipt.Task : Task.CompletedTask;

        var deploying = performer.StartAsync(Deploy, starts.Single(start => start.Node == Deploy.Id), CancellationToken.None);
        Assert.Equal(PerformedWhat.Sent, (await performer.StartAsync(Notify, starts.Single(start => start.Node == Notify.Id), CancellationToken.None)).What);
        receipt.SetResult();
        Assert.Equal(PerformedWhat.Sent, (await deploying).What);

        var record = Record();
        Assert.Equal(2, record.Attempts.Count);
        Assert.All(record.Attempts, attempt => Assert.Equal((AttemptStates.Sent, 1), (attempt.State, attempt.Deliveries)));
        Assert.Equal((NodeStates.Active, NodeStates.Active), (record.Statuses[Deploy.Id].State, record.Statuses[Notify.Id].State));
    }

    [Fact]
    public async Task A_receipt_after_its_outcome_was_heard_never_takes_the_outcome_back()
    {
        var performer = Performer();
        var start = Assert.Single(Evaluate(performer).Starts);
        var receipt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? invocation = null;
        _port.OnPerform = call =>
        {
            invocation = call.Invocation;
            return receipt.Task;
        };

        var sending = performer.StartAsync(Deploy, start, CancellationToken.None);
        Assert.Equal(PerformedWhat.Applied, Assert.Single(performer.Hear([Heard(invocation!, Reached)], Declared)).What);
        receipt.SetResult();
        await sending;

        Assert.Equal((NodeStates.Reached, (int?)null), (Status().State, Status().Unreconciled));
        var attempt = Assert.Single(Record().Attempts);
        Assert.Equal((AttemptStates.Settled, WorkStates.Reached), (attempt.State, attempt.Outcome));
    }

    [Fact]
    public async Task A_running_answer_awaited_while_the_outcome_is_heard_never_takes_the_outcome_back()
    {
        await StartAsync(Performer());
        var invocation = Assert.Single(_port.Sent).Call.Invocation;
        var answer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _port.OnReconcile = _ => answer.Task;
        _port.Answer = _ => new(ReconcileStates.Running);

        // A restart asks after the attempt sent by the loop that ended; its outcome is heard while the question is out.
        var again = Performer();
        var reconciling = again.ReconcileAsync(Declared, CancellationToken.None);
        Assert.Equal(PerformedWhat.Applied, Assert.Single(again.Hear([Heard(invocation, Reached)], Declared)).What);
        answer.SetResult();
        await reconciling;

        Assert.Equal((NodeStates.Reached, (int?)null), (Status().State, Status().Unreconciled));
        var attempt = Assert.Single(Record().Attempts);
        Assert.Equal((AttemptStates.Settled, WorkStates.Reached), (attempt.State, attempt.Outcome));
        Assert.Empty(await Performer().ReconcileAsync(Declared, CancellationToken.None));
    }

    [Fact]
    public async Task An_idempotent_only_attempt_no_longer_current_is_held_and_never_settled_as_not_started()
    {
        _deploy = Deploy with { Perform = new NodePerform(Idempotent: true, Reconcile: false, Transient: ["unavailable"]) };
        await StartAsync(Performer());
        var sent = Assert.Single(_port.Sent).Call;

        // The person stops the node while its attempt is out; a restart then finds the attempt sent by a loop since ended.
        Move(new NodeMove(NodeStates.Stopped, NodeCause.Person));
        var reconciled = Assert.Single(await Performer().ReconcileAsync(Declared, CancellationToken.None));

        // Idempotency allows the same key again; it does not say the effect never began. Nothing establishes it, so it is the person's.
        Assert.Equal(PerformedWhat.Held, reconciled.What);
        var attempt = Assert.Single(Record().Attempts);
        Assert.Equal((AttemptStates.Held, (string?)null), (attempt.State, attempt.Outcome));
        Assert.Equal((NodeStates.Stopped, (int?)1), (Status().State, Status().Unreconciled));
        Assert.Single(_port.Sent);
        Assert.Empty(_port.Asked);

        // Its late completion is still taken, as a stale attempt's: it settles the attempt and lifts the gate.
        Assert.Equal(PerformedWhat.Stale, Assert.Single(Performer().Hear([Heard(sent.Invocation, Reached)], Declared)).What);
        Assert.Null(Status().Unreconciled);
        Assert.Equal((AttemptStates.Settled, WorkStates.Reached), (Record().Attempts[0].State, Record().Attempts[0].Outcome));
    }

    [Fact]
    public async Task A_retry_waits_the_delay_of_its_retry_within_the_binding_not_of_its_lifetime_attempt()
    {
        var performer = Performer();
        await StartAsync(performer);
        performer.Hear([Heard(_port.Sent[0].Call.Invocation, new(WorkStates.Failed, Code: "denied"))], Declared);

        // The person asks for a new attempt: its tries count afresh while its attempt number grows.
        Move(new NodeMove(NodeStates.Ready, NodeCause.Person));
        await StartAsync(performer);
        Assert.Equal((2, 1), (Status().Attempt, Status().Tries));
        var second = _port.Sent[1].Call.Invocation;
        performer.Hear([Heard(second, new(WorkStates.Failed, Code: "unavailable"))], Declared);

        // Its first retry waits the first delay (30 s), not the second (60 s) its lifetime number would index.
        Assert.Equal(_now.AddSeconds(30), Record().Invoked(second)!.RetryAt);
    }
}
