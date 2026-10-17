using System.Security.Cryptography;

namespace Daoris.Driver;

/// <summary>
/// What the performer asks of a plugin's work process (FABRIC-PERFORM1b): <see cref="HookSet"/>'s fenced calls, each returning the generation
/// that answered, and the generation now current. A port, so the durable rules are proved without a process.
/// </summary>
public interface IWorkPerformPort
{
    Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct);

    Task<(WorkReconcileAnswer Answer, int Generation)> ReconcileStepAsync(string plugin, WorkReconcileCall call, CancellationToken ct);

    int? GenerationOf(string plugin);
}

/// <summary>What the performer did with one start, answer or attempt (<see cref="WorkPerformed.What"/>), for the loop's log and the tests.</summary>
public static class PerformedWhat
{
    /// <summary>The attempt was written and its send acknowledged.</summary>
    public const string Sent = "sent";

    /// <summary>The start was refused by the one table or the record (<see cref="WorkPerformed.Code"/>): nothing was written or sent.</summary>
    public const string Refused = "refused";

    /// <summary>The attempt was written, and its send failed or went unanswered: its outcome is unknown until reconciled.</summary>
    public const string Unknown = "unknown";

    /// <summary>An outcome applied to its attempt's node: reached, failed, or unknown by the plugin's word.</summary>
    public const string Applied = "applied";

    /// <summary>An outcome its attempt already has: applied once, never again.</summary>
    public const string Replay = "replay";

    /// <summary>An answer not taken (<see cref="WorkPerformed.Code"/>): no attempt by that invocation, another sender, a stale generation.</summary>
    public const string NotTaken = "not-taken";

    /// <summary>An earlier attempt's outcome, recorded as a stale attempt's: it lifts the gate and moves nothing (§D3).</summary>
    public const string Stale = "stale";

    /// <summary>Reconciliation found it never started: delivered again with the same key.</summary>
    public const string Redelivered = "redelivered";

    /// <summary>Reconciliation found it running: it waits to its deadline.</summary>
    public const string Running = "running";

    /// <summary>Nothing establishes its outcome by itself: the person's (§D6).</summary>
    public const string Held = "held";

    /// <summary>A declared retry's delay passed: the node is ready for a new attempt with a new key.</summary>
    public const string Retry = "retry";

    /// <summary>Reconciliation could not be asked now (the process is not up, or its answer was not taken): asked again later.</summary>
    public const string Unanswered = "unanswered";
}

/// <summary>One thing the performer did, on one unit's node.</summary>
public sealed record WorkPerformed(string Unit, string Node, string What, string? Code = null);

/// <summary>
/// Durable execution of a plugin's performed nodes (FABRIC-PERFORM1b; the fabric events design §D6, §E2): each attempt written on its unit's
/// record with its start and the trigger firings it consumes before <c>work/perform</c> is sent, one idempotency key per attempt and its
/// invocation bound to it there; on a restart, a lost process, a deadline or an unknown outcome, <c>work/reconcile</c> before any new
/// attempt; a failure retried only for a declared <c>transient</c> code within the declared attempts, after a persisted delay; an answer taken
/// only from its plugin's current process, a replay of an outcome applied once. Run by the loop that holds the home's lease, the record's one
/// writer; every move goes through <see cref="NodeLifecycle.Apply"/>.
/// </summary>
public sealed class WorkPerformer(NodeRecordFiles records, IWorkPerformPort port, Func<DateTimeOffset> clock)
{
    /// <summary>An attempt's deadline where its node declares no <see cref="NodeDeclaration.TimeoutSeconds"/>.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(15);

    /// <summary>A failure's code where the plugin gave none: not transient, so it stops the node for the person (§D6).</summary>
    public const string NoCode = "unspecified";

    /// <summary>Why an answer or a start is not taken (<see cref="WorkPerformed.Code"/>).</summary>
    public const string NotAttempt = "not-attempt";

    public const string Unreadable = "unreadable";

    public const string NotPlugin = "not-plugin";

    public const string NotDeclared = "not-declared";

    /// <summary>A status the record no longer holds as it was evaluated: another move was kept since, so this one is not written over it.</summary>
    public const string Moved = "moved";

    /// <summary>Why an attempt is held: its step declares neither <c>idempotent</c> nor <c>reconcile</c>, or a done answer named no outcome.</summary>
    public const string NoReconcile = "no-reconcile";

    public const string NoOutcome = "no-outcome";

    /// <summary>
    /// The invocations this performer's loop delivered: an attempt sent by a loop since ended is asked after before anything else, since a
    /// new loop's generations start again at 1 (FABRIC-PERFORM1a) and cannot tell its process from the old one's.
    /// </summary>
    private readonly HashSet<string> _sentHere = new(StringComparer.Ordinal);

    /// <summary>
    /// Keeps an evaluation's moves on their units' records (WIRE1e's note: the moves and their history), where each node still stands as it
    /// stood when evaluated (<paramref name="before"/>): a status another writer moved since is not written over.
    /// </summary>
    public IReadOnlyList<WorkPerformed> Keep(IReadOnlyDictionary<string, NodeStatus> before, WireEvaluated evaluated)
    {
        if (evaluated.Refusal is not null) return [];
        var done = new List<WorkPerformed>();
        var now = clock();
        foreach (var unit in evaluated.Statuses.Values.Select(status => status.Unit).Distinct(StringComparer.Ordinal))
        {
            if (records.Read(unit) is not { } record)
            {
                done.Add(new(unit, "", PerformedWhat.Refused, Unreadable));
                continue;
            }

            var statuses = new Dictionary<string, NodeStatus>(record.Statuses, StringComparer.Ordinal);
            var history = record.History.ToList();
            var changed = false;
            foreach (var (node, status) in evaluated.Statuses.Where(each => each.Value.Unit == unit))
            {
                var held = statuses.GetValueOrDefault(node) ?? NodeLifecycle.Start(unit, node);
                if (!before.TryGetValue(node, out var was) || !Same(held, was))
                {
                    done.Add(new(unit, node, PerformedWhat.Refused, Moved));
                    continue;
                }

                if (Same(held, status) && statuses.ContainsKey(node)) continue;
                statuses[node] = status;
                changed = true;
                foreach (var move in evaluated.Moves.Where(move => move.Node == node && move.Moved.Cleared is not null))
                {
                    history.Add(new NodeHistory(now, move.Why, move.Moved.Cleared!));
                }
            }

            if (changed) records.Write(record with { Statuses = statuses, History = history });
        }

        return done;
    }

    /// <summary>
    /// Starts a plugin's performed node on the start the evaluation proposed: the start, its firings and the attempt written first, then sent.
    /// A loop ended while the send is out (its token cancelled) writes nothing more: the next loop reconciles the attempt before anything else.
    /// </summary>
    public async Task<WorkPerformed> StartAsync(NodeDeclaration node, WireStart start, CancellationToken ct)
    {
        if (start.Move.Binding is not { } binding) return new("", start.Node, PerformedWhat.Refused, NodeRefusal.NoBinding);
        var unit = binding.Unit;
        if (!NodeKinds.IsPlugin(node.Kind) || node.Reach != NodeReach.Performed) return new(unit, node.Id, PerformedWhat.Refused, NotPlugin);
        if (records.Read(unit) is not { } record) return new(unit, node.Id, PerformedWhat.Refused, Unreadable);
        var moved = NodeLifecycle.Apply(node, record.Statuses.GetValueOrDefault(node.Id) ?? NodeLifecycle.Start(unit, node.Id), start.Move);
        if (!moved.Moved) return new(unit, node.Id, PerformedWhat.Refused, moved.Refused);

        // §D6: the attempt (invocation, key, revision, inputs, deadline, state) on the unit's record first, with its start and its firings.
        var now = clock();
        var call = new WorkPerformCall(unit, node.Kind, node.Version, Random("i-", 12), Random("k-", 16), binding.SubjectRevision ?? "",
            [.. binding.Inputs.OrderBy(input => input.Key, StringComparer.Ordinal).Select(input => input.Value.Ref)], now + Timeout(node));
        var attempt = new NodeAttempt(node.Id, moved.Status.Attempt, node.Kind[..node.Kind.IndexOf('/')], call, AttemptStates.Recorded) { RecordedAt = now };
        var fired = record.Fired.Concat(start.Firings.Select(firing => firing.Key)).Distinct(StringComparer.Ordinal).ToList();
        record = Kept(record, moved, "start", now) with { Attempts = [.. record.Attempts, attempt], Fired = fired };
        records.Write(record);
        return await SendAsync(record, attempt, node, PerformedWhat.Sent, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The step events heard (<see cref="HookSet.TakeSteps"/>): each completion applied to the attempt its invocation names, taken only from
    /// that attempt's plugin's current process. An event naming no invocation is an observed step's, PERFORM1c's.
    /// </summary>
    public IReadOnlyList<WorkPerformed> Hear(IReadOnlyList<WorkStepHeard> heard, Func<string, string, NodeDeclaration?> declared)
    {
        var done = new List<WorkPerformed>();
        foreach (var each in heard)
        {
            if (each.Step.Invocation is not { } invocation) continue;
            if (Find(invocation) is not { } found)
            {
                done.Add(new("", "", PerformedWhat.NotTaken, NotAttempt));
                continue;
            }

            var (record, attempt) = found;

            // §C4's first rule: the sender is the process the frame was read from, the attempt's plugin's current one. A completion binds to
            // its invocation, not its generation, so a later process's replay of it is taken (PERFORM1a's review).
            if (!string.Equals(each.Plugin, attempt.Plugin, StringComparison.Ordinal) || port.GenerationOf(attempt.Plugin) != each.Generation)
            {
                done.Add(new(record.Unit, attempt.Node, PerformedWhat.NotTaken, WorkRefusals.NotSender));
                continue;
            }

            if (declared(record.Unit, attempt.Node) is not { } node)
            {
                done.Add(new(record.Unit, attempt.Node, PerformedWhat.NotTaken, NotDeclared));
                continue;
            }

            done.Add(Outcome(record, attempt, node, each.Step.Outcome, each.Generation));
        }

        return done;
    }

    /// <summary>
    /// Asks after every attempt whose outcome is not established and is due (§D6: after a restart with an unresolved attempt, after its
    /// deadline, after a lost connection), before any new attempt: the one table refuses a start while its node holds it unreconciled.
    /// </summary>
    public async Task<IReadOnlyList<WorkPerformed>> ReconcileAsync(Func<string, string, NodeDeclaration?> declared, CancellationToken ct)
    {
        var done = new List<WorkPerformed>();
        var now = clock();
        foreach (var record in records.All().Records)
        {
            foreach (var attempt in record.Attempts.Where(attempt => Asked(attempt, now)).ToList())
            {
                if (declared(record.Unit, attempt.Node) is not { } node) continue;
                done.Add(await ReconcileOneAsync(record.Unit, attempt.Call.Invocation, node, ct).ConfigureAwait(false));
            }
        }

        return done;
    }

    /// <summary>Every declared retry whose persisted delay has passed: its node made ready for a new attempt, with a new key at its start.</summary>
    public IReadOnlyList<WorkPerformed> Due(Func<string, string, NodeDeclaration?> declared)
    {
        var done = new List<WorkPerformed>();
        var now = clock();
        foreach (var read in records.All().Records)
        {
            var record = read;
            foreach (var (id, status) in read.Statuses.Where(each => each.Value is { State: NodeStates.Failed, Transient: true }))
            {
                if (record.Newest(id) is not { RetryAt: { } at } attempt || at > now || declared(record.Unit, id) is not { } node) continue;
                var moved = NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Ready, NodeCause.Daoris));
                record = Kept(record, moved, "retry", now);
                record = With(record, attempt, attempt with { RetryAt = null });
                done.Add(new(record.Unit, id, moved.Moved ? PerformedWhat.Retry : PerformedWhat.Refused, moved.Refused));
            }

            if (!ReferenceEquals(record, read)) records.Write(record);
        }

        return done;
    }

    /// <summary>Whether Daoris asks after an attempt now: one never acknowledged, unknown, or sent by a loop since ended, to a process since
    /// replaced, or past its deadline; a running answer waits to its <see cref="NodeAttempt.AskAt"/>.</summary>
    private bool Asked(NodeAttempt attempt, DateTimeOffset now) => attempt.State switch
    {
        AttemptStates.Recorded or AttemptStates.Unknown => attempt.AskAt is not { } ask || ask <= now,
        AttemptStates.Sent => !_sentHere.Contains(attempt.Call.Invocation) || port.GenerationOf(attempt.Plugin) != attempt.Generation
            || (attempt.AskAt ?? attempt.Call.DeadlineAt) <= now,
        _ => false,
    };

    private async Task<WorkPerformed> ReconcileOneAsync(string unit, string invocation, NodeDeclaration node, CancellationToken ct)
    {
        if (records.Read(unit) is not { } record || record.Invoked(invocation) is not { } attempt) return new(unit, node.Id, PerformedWhat.Refused, Unreadable);
        var now = clock();

        // §E2's active → unknown: its outcome cannot be established now. Written before the question, so a loop that ends here asks again.
        var status = record.Statuses.GetValueOrDefault(node.Id) ?? NodeLifecycle.Start(unit, node.Id);
        if (status.State == NodeStates.Active && status.Attempt == attempt.Attempt)
        {
            record = Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Unknown, NodeCause.Daoris) { Attempt = attempt.Attempt }), "unknown", now);
        }

        var unknown = attempt with { State = AttemptStates.Unknown };
        record = With(record, attempt, unknown);
        records.Write(record);
        if (node.Perform is not { } perform || !(perform.Reconcile || perform.Idempotent)) return Hold(record, unknown, NoReconcile);

        WorkReconcileAnswer answer;
        var generation = port.GenerationOf(attempt.Plugin) ?? 0;
        if (perform.Reconcile)
        {
            try
            {
                (answer, generation) = await port.ReconcileStepAsync(
                    attempt.Plugin, new WorkReconcileCall(unit, attempt.Call.Step, invocation, attempt.Call.IdempotencyKey, attempt.Call.SubjectRevision), ct)
                    .ConfigureAwait(false);
            }
            catch (Exception error) when (Unanswered(error))
            {
                return new(unit, node.Id, PerformedWhat.Unanswered);
            }
        }
        else
        {
            // §D6: an idempotent step's redelivery uses the same key, and its plugin deduplicates it durably.
            answer = new WorkReconcileAnswer(ReconcileStates.NotStarted);
        }

        status = record.Statuses.GetValueOrDefault(node.Id) ?? NodeLifecycle.Start(unit, node.Id);
        var current = status.Attempt == attempt.Attempt && status.State == NodeStates.Unknown;
        switch (answer.State)
        {
            case ReconcileStates.Done when answer.Outcome is { } outcome:
                return Outcome(record, unknown, node, outcome, generation);
            case ReconcileStates.Done:
                return Hold(record, unknown, NoOutcome);
            case ReconcileStates.Failed:
                return Outcome(record, unknown, node, answer.Outcome is { State: WorkStates.Failed } failed ? failed : new WorkOutcome(WorkStates.Failed, NoCode), generation);
            case ReconcileStates.NotStarted when current:
                record = Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Active, NodeCause.Daoris)), "redeliver", now);
                records.Write(record);
                return await SendAsync(record, unknown, node, PerformedWhat.Redelivered, ct).ConfigureAwait(false);
            case ReconcileStates.NotStarted:
                // It never started and is no longer current (stopped, skipped, superseded): nothing to deliver, and the gate lifts.
                record = With(record, unknown, unknown with { State = AttemptStates.Settled, Outcome = ReconcileStates.NotStarted, Generation = generation });
                records.Write(record with { Statuses = With(record.Statuses, NodeLifecycle.Reconciled(status, attempt.Attempt)) });
                return new(unit, node.Id, PerformedWhat.Stale);
            case ReconcileStates.Running:
                if (current) record = Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Active, NodeCause.Daoris)), "running", now);
                _sentHere.Add(invocation);
                var wait = attempt.Call.DeadlineAt > now ? attempt.Call.DeadlineAt : now + Timeout(node);
                records.Write(With(record, unknown, unknown with { State = current ? AttemptStates.Sent : AttemptStates.Unknown, Generation = generation, AskAt = wait }));
                return new(unit, node.Id, PerformedWhat.Running);
            default:
                // §D6: unknown stops the step, and the person sees it; no new attempt is made by itself.
                return Hold(record, unknown with { Generation = generation }, ReconcileStates.Unknown);
        }
    }

    /// <summary>
    /// One outcome applied to its attempt's node through the one table: a reach on its binding with the plugin's evidence and outputs, a
    /// failure with its code (transient where the step declares it, its retry's deadline persisted), or the plugin's unknown. A settled
    /// attempt's is a replay; an earlier attempt's is recorded as a stale one's and lifts the gate it held (§D3).
    /// </summary>
    private WorkPerformed Outcome(NodeRecord record, NodeAttempt attempt, NodeDeclaration node, WorkOutcome outcome, int generation)
    {
        var unit = record.Unit;
        if (attempt.State == AttemptStates.Settled) return new(unit, node.Id, PerformedWhat.Replay);
        var now = clock();
        var status = record.Statuses.GetValueOrDefault(node.Id) ?? NodeLifecycle.Start(unit, node.Id);
        var settled = attempt with { State = AttemptStates.Settled, Outcome = outcome.State, Code = outcome.Code, Generation = generation };
        if (status.Attempt != attempt.Attempt || status.State is not (NodeStates.Active or NodeStates.Unknown))
        {
            records.Write(With(record, attempt, settled) with { Statuses = With(record.Statuses, NodeLifecycle.Reconciled(status, attempt.Attempt)) });
            return new(unit, node.Id, PerformedWhat.Stale);
        }

        NodeMove move;
        switch (outcome.State)
        {
            case WorkStates.Reached:
                move = new NodeMove(NodeStates.Reached, NodeCause.Authority)
                {
                    Source = NodeSource.Plugin, Attempt = attempt.Attempt, Acceptance = status.Binding is { } binding ? Accepted(node, attempt, binding, outcome) : null,
                };
                break;
            case WorkStates.Failed:
                var code = outcome.Code ?? NoCode;
                settled = settled with { Code = code };
                move = new NodeMove(NodeStates.Failed, NodeCause.Authority)
                {
                    Source = NodeSource.Plugin, Attempt = attempt.Attempt, Code = code, Transient = node.Perform?.Transient.Contains(code, StringComparer.Ordinal) == true,
                };
                break;
            default:
                if (status.State == NodeStates.Unknown)
                {
                    records.Write(With(record, attempt, attempt with { State = AttemptStates.Unknown, Generation = generation }));
                    return new(unit, node.Id, PerformedWhat.Applied, WorkStates.Unknown);
                }

                move = new NodeMove(NodeStates.Unknown, NodeCause.Daoris) { Attempt = attempt.Attempt };
                settled = attempt with { State = AttemptStates.Unknown, Generation = generation };
                break;
        }

        var moved = NodeLifecycle.Apply(node, status, move);
        if (!moved.Moved) return Hold(record, attempt with { Outcome = outcome.State, Generation = generation }, moved.Refused!);
        if (moved.Status is { State: NodeStates.Failed, Transient: true } failedStatus && failedStatus.Tries < (node.Retry?.MaxAttempts ?? 1))
        {
            settled = settled with { RetryAt = now + NodeLifecycle.DelayBefore(node, failedStatus.Attempt + 1) };
        }

        records.Write(With(Kept(record, moved, outcome.State, now), attempt, settled));
        return new(unit, node.Id, PerformedWhat.Applied, outcome.State);
    }

    /// <summary>
    /// The acceptance a reach is on: its attempt's binding, with the plugin's evidence reference (of the declared schema, its word, D143) and
    /// its outputs, each at the schema its reference names (§D6's <c>&lt;schema&gt;:&lt;id&gt;</c>), so the one table refuses one undeclared or
    /// of another schema. That the evidence is believed is PERFORM1c's.
    /// </summary>
    private static NodeAcceptance Accepted(NodeDeclaration node, NodeAttempt attempt, NodeAcceptance binding, WorkOutcome outcome) => binding with
    {
        Evidence = new NodePort(node.Evidence ?? node.Kind, outcome.Evidence?.Ref ?? attempt.Call.Invocation, binding.SubjectRevision, null),
        Outputs = (outcome.Outputs ?? new Dictionary<string, WorkOutput>()).ToDictionary(
            output => output.Key,
            output => new NodePort(output.Value.Ref.IndexOf(':') is > 0 and var colon ? output.Value.Ref[..colon] : "", output.Value.Ref,
                output.Value.Revision ?? binding.SubjectRevision, null),
            StringComparer.Ordinal),
    };

    /// <summary>
    /// One delivery of an attempt, its key unchanged: acknowledged, it is sent by this loop's process of that generation; failed or unanswered,
    /// its outcome is unknown until reconciled. A cancelled token is the loop ending, which writes nothing more.
    /// </summary>
    private async Task<WorkPerformed> SendAsync(NodeRecord record, NodeAttempt attempt, NodeDeclaration node, string what, CancellationToken ct)
    {
        int generation;
        try
        {
            generation = await port.PerformAsync(attempt.Plugin, attempt.Call, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (Unanswered(error))
        {
            var now = clock();
            var status = record.Statuses.GetValueOrDefault(node.Id) ?? NodeLifecycle.Start(record.Unit, node.Id);
            if (status.State == NodeStates.Active && status.Attempt == attempt.Attempt)
            {
                record = Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Unknown, NodeCause.Daoris) { Attempt = attempt.Attempt }), "unknown", now);
            }

            records.Write(With(record, attempt, attempt with { State = AttemptStates.Unknown }));
            return new(record.Unit, node.Id, PerformedWhat.Unknown, PerformedWhat.Unanswered);
        }

        _sentHere.Add(attempt.Call.Invocation);
        records.Write(With(record, attempt, attempt with
        {
            State = AttemptStates.Sent, Generation = generation, Deliveries = attempt.Deliveries + 1, AskAt = null,
        }));
        return new(record.Unit, node.Id, what);
    }

    /// <summary>An attempt nothing establishes by itself: held for the person, its node left as it stands, its gate held (§D6).</summary>
    private WorkPerformed Hold(NodeRecord record, NodeAttempt attempt, string code)
    {
        var held = attempt with { State = AttemptStates.Held, Code = code };
        records.Write(With(record, attempt, held));
        return new(record.Unit, attempt.Node, PerformedWhat.Held, code);
    }

    /// <summary>A failure of the call itself, not the loop ending: the attempt's outcome is unknown, never a second blind dispatch.</summary>
    private static bool Unanswered(Exception error) =>
        error is not OperationCanceledException && error is DriverException or IOException or TimeoutException or InvalidOperationException;

    private (NodeRecord Record, NodeAttempt Attempt)? Find(string invocation)
    {
        foreach (var record in records.All().Records)
        {
            if (record.Invoked(invocation) is { } attempt) return (record, attempt);
        }

        return null;
    }

    /// <summary>The record with a move's status kept and what it cleared added to the history.</summary>
    private static NodeRecord Kept(NodeRecord record, NodeMoved moved, string why, DateTimeOffset now) => !moved.Moved ? record : record with
    {
        Statuses = With(record.Statuses, moved.Status),
        History = moved.Cleared is { } cleared ? [.. record.History, new NodeHistory(now, why, cleared)] : record.History,
    };

    private static Dictionary<string, NodeStatus> With(IReadOnlyDictionary<string, NodeStatus> statuses, NodeStatus status) =>
        new(statuses, StringComparer.Ordinal) { [status.Node] = status };

    private static NodeRecord With(NodeRecord record, NodeAttempt was, NodeAttempt now) => record with
    {
        Attempts = [.. record.Attempts.Select(attempt => string.Equals(attempt.Call.Invocation, was.Call.Invocation, StringComparison.Ordinal) ? now : attempt)],
    };

    private static bool Same(NodeStatus one, NodeStatus other) =>
        string.Equals(NodeRecordFiles.StatusText(one), NodeRecordFiles.StatusText(other), StringComparison.Ordinal);

    private static TimeSpan Timeout(NodeDeclaration node) => node.TimeoutSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : DefaultTimeout;

    private static string Random(string prefix, int bytes) => prefix + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(bytes));
}
