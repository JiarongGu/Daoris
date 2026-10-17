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

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-perform-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly Port _port = new();

    private DateTimeOffset _now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

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

        public Func<WorkReconcileCall, WorkReconcileAnswer> Answer { get; set; } = _ => new(ReconcileStates.Unknown);

        public async Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct)
        {
            Assert.Equal(Plugin, plugin);
            if (OnPerform is { } before) await before(call);
            var generation = Generation ?? throw new DriverException("not running");
            Sent.Add((call, generation));
            return generation;
        }

        public Task<(WorkReconcileAnswer Answer, int Generation)> ReconcileStepAsync(string plugin, WorkReconcileCall call, CancellationToken ct)
        {
            var generation = Generation ?? throw new DriverException("not running");
            Asked.Add(call);
            return Task.FromResult((Answer(call), generation));
        }

        public int? GenerationOf(string plugin) => Generation;
    }

    private NodeRecordFiles Files => new(_home);

    /// <summary>A new performer over the same home: what a loop started again after a restart holds.</summary>
    private WorkPerformer Performer() => new(Files, _port, () => _now);

    private static NodeDeclaration? Declared(string unit, string node) => unit == Unit && node == Deploy.Id ? Deploy : null;

    private NodeRecord Record() => Files.Read(Unit)!;

    private NodeStatus Status() => Record().Statuses[Deploy.Id];

    /// <summary>The evaluation as the loop runs it: the statuses from the record, their moves kept on it.</summary>
    private WireEvaluated Evaluate(WorkPerformer performer, string run = "run-1")
    {
        var record = Record();
        var before = new Dictionary<string, NodeStatus>(StringComparer.Ordinal)
        {
            [Deploy.Id] = record.Statuses.GetValueOrDefault(Deploy.Id) ?? NodeLifecycle.Start(Unit, Deploy.Id),
        };
        var evaluated = WireEvaluation.Evaluate([Deploy], [], before,
            new WireRun("graph-1", run) { Revisions = new Dictionary<string, string?> { [Unit] = Tip }, Fired = record.Fired.ToHashSet(StringComparer.Ordinal) });
        Assert.Null(evaluated.Refusal);
        performer.Keep(before, evaluated);
        return evaluated;
    }

    private async Task<WorkPerformed> StartAsync(WorkPerformer performer)
    {
        var start = Assert.Single(Evaluate(performer).Starts);
        return await performer.StartAsync(Deploy, start, CancellationToken.None);
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
}
