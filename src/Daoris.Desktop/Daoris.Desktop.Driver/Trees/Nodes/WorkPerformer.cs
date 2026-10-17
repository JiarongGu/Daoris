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

    /// <summary>
    /// A node that dispatches nothing was started (FABRIC-RUN1): an observed built-in made active with the firings its start consumed, read
    /// from its records at the next evaluation.
    /// </summary>
    public const string Begun = "begun";
}

/// <summary>One thing the performer did, on one unit's node.</summary>
public sealed record WorkPerformed(string Unit, string Node, string What, string? Code = null);

/// <summary>
/// The attempts one pass may call a plugin's work process about (FABRIC-RUN1e): a new start's send, and each attempt a reconciliation asks
/// after or delivers again, take one each from the same count, so <see cref="NodeRuntime.SendsPerPass"/> bounds the whole pass, not its
/// starts alone. What is left over waits for the next pass.
/// </summary>
public sealed class WorkSends(int left)
{
    /// <summary>No bound: a caller outside a pass (a test, a tool) that asks after every attempt due.</summary>
    public static WorkSends Unbounded => new(int.MaxValue);

    /// <summary>The calls still allowed in this pass.</summary>
    public int Left { get; private set; } = left;

    /// <summary>Takes one call, or says none is left.</summary>
    public bool Take()
    {
        if (Left <= 0) return false;
        Left--;
        return true;
    }
}

/// <summary>
/// Durable execution of a plugin's performed nodes (FABRIC-PERFORM1b; the fabric events design §D6, §E2): each attempt written on its unit's
/// record with its start and the trigger firings it consumes before <c>work/perform</c> is sent, one idempotency key per attempt and its
/// invocation bound to it there; on a restart, a lost process, a deadline or an unknown outcome, <c>work/reconcile</c> before any new
/// attempt; a failure retried only for a declared <c>transient</c> code within the declared attempts, after a persisted delay; an answer taken
/// only from its plugin's current process, a replay of an outcome applied once. Run by the loop that holds the home's lease, the record's one
/// writer; every move goes through <see cref="NodeLifecycle.Apply"/>.
/// </summary>
/// <remarks>
/// <b>Every change is one locked edit</b> (FABRIC-PERFORM1h, <see cref="NodeRecordFiles.Edit{T}"/>): read, judged and written in one hold of
/// the record's lock with the lease asked inside it, never decided on a record read earlier. Nothing is awaited inside an edit, so a send's
/// receipt and a reconciliation's answer are applied after their await to the record as it is then, to their own invocation alone, and only
/// while that attempt still waits for them: an outcome kept meanwhile is never taken back.
/// </remarks>
public sealed class WorkPerformer(NodeRecordFiles records, IWorkPerformPort port, Func<DateTimeOffset> clock)
{
    /// <summary>An attempt's deadline where its node declares no <see cref="NodeDeclaration.TimeoutSeconds"/>.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(15);

    /// <summary>A failure's code where the plugin gave none: not transient, so it stops the node for the person (§D6).</summary>
    public const string NoCode = "unspecified";

    /// <summary>Why an answer or a start is not taken (<see cref="WorkPerformed.Code"/>).</summary>
    public const string NotAttempt = "not-attempt";

    public const string Unreadable = NodeRecordFiles.Unreadable;

    /// <summary>This process does not hold the home's lease, asked inside the record's lock: nothing is written or sent (FABRIC-PERFORM1h).</summary>
    public const string NotHeld = NodeRecordFiles.NotHeld;

    public const string NotPlugin = "not-plugin";

    public const string NotDeclared = "not-declared";

    /// <summary>
    /// A status the record no longer holds as it was evaluated, or an attempt no longer waiting for the answer that came: another move was kept
    /// since, so this one is not written over it.
    /// </summary>
    public const string Moved = "moved";

    /// <summary>Why an attempt is held: its step declares neither <c>idempotent</c> nor <c>reconcile</c>, or a done answer named no outcome.</summary>
    public const string NoReconcile = "no-reconcile";

    public const string NoOutcome = "no-outcome";

    /// <summary>
    /// Why an attempt of a step declaring <c>idempotent</c> alone is held (FABRIC-PERFORM1h): its node has moved off it (stopped, skipped,
    /// superseded), so it is not delivered again, and idempotency does not say whether its effect began.
    /// </summary>
    public const string NotCurrent = "not-current";

    /// <summary>
    /// The invocations this performer's loop delivered: an attempt sent by a loop since ended is asked after before anything else, since a
    /// new loop's generations start again at 1 (FABRIC-PERFORM1a) and cannot tell its process from the old one's.
    /// </summary>
    private readonly HashSet<string> _sentHere = new(StringComparer.Ordinal);

    /// <summary>
    /// Keeps an evaluation's moves on their units' records (WIRE1e's note: the moves and their history), where each node still stands as it
    /// stood when evaluated (<paramref name="before"/>): a status another writer moved since is not written over. Compared and written in one
    /// hold of the record's lock (FABRIC-PERFORM1h).
    /// </summary>
    public IReadOnlyList<WorkPerformed> Keep(IReadOnlyDictionary<string, NodeStatus> before, WireEvaluated evaluated)
    {
        if (evaluated.Refusal is not null) return [];
        var done = new List<WorkPerformed>();
        var now = clock();
        foreach (var unit in evaluated.Statuses.Values.Select(status => status.Unit).Distinct(StringComparer.Ordinal))
        {
            var (refused, kept) = records.Edit<List<WorkPerformed>>(unit, record =>
            {
                var said = new List<WorkPerformed>();
                var statuses = new Dictionary<string, NodeStatus>(record.Statuses, StringComparer.Ordinal);
                var history = record.History.ToList();
                var changed = false;
                foreach (var (node, status) in evaluated.Statuses.Where(each => each.Value.Unit == unit))
                {
                    var held = statuses.GetValueOrDefault(node) ?? NodeLifecycle.Start(unit, node);
                    if (!before.TryGetValue(node, out var was) || !Same(held, was))
                    {
                        said.Add(new(unit, node, PerformedWhat.Refused, Moved));
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

                return (changed ? record with { Statuses = statuses, History = history } : null, said);
            });
            if (refused is not null) done.Add(new(unit, "", PerformedWhat.Refused, refused));
            else done.AddRange(kept!);
        }

        return done;
    }

    /// <summary>
    /// Starts a plugin's performed node on the start the evaluation proposed: the start, its firings and the attempt written first, then sent.
    /// The start is judged on the record under its lock, so a second start on the same node reads the first and is refused (FABRIC-PERFORM1h).
    /// A loop ended while the send is out (its token cancelled) writes nothing more: the next loop reconciles the attempt before anything else.
    /// </summary>
    public async Task<WorkPerformed> StartAsync(NodeDeclaration node, WireStart start, CancellationToken ct)
    {
        if (start.Move.Binding is not { } binding) return new("", start.Node, PerformedWhat.Refused, NodeRefusal.NoBinding);
        var unit = binding.Unit;
        if (!NodeKinds.IsPlugin(node.Kind) || node.Reach != NodeReach.Performed) return new(unit, node.Id, PerformedWhat.Refused, NotPlugin);

        // §D6: the attempt (invocation, key, revision, inputs, deadline, state) on the unit's record first, with its start and its firings.
        var now = clock();
        var (refused, started) = records.Edit<(NodeAttempt? Attempt, string? Refused)>(unit, record =>
        {
            var moved = NodeLifecycle.Apply(node, StatusOf(record, node.Id), start.Move);
            if (!moved.Moved) return (null, (null, moved.Refused));
            var call = new WorkPerformCall(unit, node.Kind, node.Version, Random("i-", 12), Random("k-", 16), binding.SubjectRevision ?? "",
                [.. binding.Inputs.OrderBy(input => input.Key, StringComparer.Ordinal).Select(input => input.Value.Ref)], now + Timeout(node));
            var attempt = new NodeAttempt(node.Id, moved.Status.Attempt, node.Kind[..node.Kind.IndexOf('/')], call, AttemptStates.Recorded) { RecordedAt = now };
            var fired = record.Fired.Concat(start.Firings.Select(firing => firing.Key)).Distinct(StringComparer.Ordinal).ToList();
            return (Kept(record, moved, "start", now) with { Attempts = [.. record.Attempts, attempt], Fired = fired }, (attempt, null));
        });
        if (refused is not null) return new(unit, node.Id, PerformedWhat.Refused, refused);
        if (started.Attempt is not { } recorded) return new(unit, node.Id, PerformedWhat.Refused, started.Refused);
        return await SendAsync(unit, recorded, node, PerformedWhat.Sent, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts a node that dispatches nothing (FABRIC-RUN1: an observed built-in, read from its records at the next evaluation) on the start
    /// the evaluation proposed, its firings kept with it (WIRE1c), where the record still holds the node as it was evaluated
    /// (<paramref name="evaluated"/>): one locked edit, so a start another writer made since is not made again.
    /// </summary>
    public WorkPerformed Begin(NodeDeclaration node, WireStart start, NodeStatus evaluated)
    {
        if (start.Move.Binding is not { } binding) return new("", start.Node, PerformedWhat.Refused, NodeRefusal.NoBinding);
        var unit = binding.Unit;
        var now = clock();
        return Edited(unit, node.Id, record =>
        {
            var held = StatusOf(record, node.Id);
            if (!Same(held, evaluated)) return (null, new WorkPerformed(unit, node.Id, PerformedWhat.Refused, Moved));
            var moved = NodeLifecycle.Apply(node, held, start.Move);
            if (!moved.Moved) return (null, new WorkPerformed(unit, node.Id, PerformedWhat.Refused, moved.Refused));
            var fired = record.Fired.Concat(start.Firings.Select(firing => firing.Key)).Distinct(StringComparer.Ordinal).ToList();
            return (Kept(record, moved, "start", now) with { Fired = fired }, new WorkPerformed(unit, node.Id, PerformedWhat.Begun));
        });
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

            var (unit, attempt) = found;

            // §C4's first rule: the sender is the process the frame was read from, the attempt's plugin's current one. A completion binds to
            // its invocation, not its generation, so a later process's replay of it is taken (PERFORM1a's review).
            if (!string.Equals(each.Plugin, attempt.Plugin, StringComparison.Ordinal) || port.GenerationOf(attempt.Plugin) != each.Generation)
            {
                done.Add(new(unit, attempt.Node, PerformedWhat.NotTaken, WorkRefusals.NotSender));
                continue;
            }

            if (declared(unit, attempt.Node) is not { } node)
            {
                done.Add(new(unit, attempt.Node, PerformedWhat.NotTaken, NotDeclared));
                continue;
            }

            // Applied to the attempt as the record holds it now, under its lock (FABRIC-PERFORM1h).
            done.Add(Edited(unit, node.Id, record => record.Invoked(invocation) is { } current
                ? Outcome(record, current, node, each.Step.Outcome, each.Generation)
                : (null, new WorkPerformed(unit, node.Id, PerformedWhat.NotTaken, NotAttempt))));
        }

        return done;
    }

    /// <summary>
    /// Asks after every attempt whose outcome is not established and is due (§D6: after a restart with an unresolved attempt, after its
    /// deadline, after a lost connection), before any new attempt: the one table refuses a start while its node holds it unreconciled.
    /// </summary>
    public Task<IReadOnlyList<WorkPerformed>> ReconcileAsync(Func<string, string, NodeDeclaration?> declared, CancellationToken ct) =>
        ReconcileAsync(declared, WorkSends.Unbounded, ct);

    /// <summary>
    /// <see cref="ReconcileAsync(Func{string, string, NodeDeclaration?}, CancellationToken)"/> within a pass's calls (FABRIC-RUN1e): each attempt
    /// asked after or delivered again takes one of <paramref name="sends"/>, and the attempts past them are asked at the next pass.
    /// </summary>
    public async Task<IReadOnlyList<WorkPerformed>> ReconcileAsync(Func<string, string, NodeDeclaration?> declared, WorkSends sends, CancellationToken ct)
    {
        var done = new List<WorkPerformed>();
        var now = clock();
        foreach (var record in records.All().Records)
        {
            foreach (var attempt in record.Attempts.Where(attempt => Asked(attempt, now)).ToList())
            {
                // FABRIC-RUN1e: the pass's calls spent, the attempts left are asked at the next pass, still before any start there.
                if (sends.Left <= 0) return done;
                if (declared(record.Unit, attempt.Node) is not { } node) continue;
                done.Add(await ReconcileOneAsync(record.Unit, attempt.Call.Invocation, node, sends, ct).ConfigureAwait(false));
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
            if (!read.Statuses.Values.Any(Retrying)) continue;
            var (refused, retried) = records.Edit<List<WorkPerformed>>(read.Unit, current =>
            {
                var said = new List<WorkPerformed>();
                var record = current;
                foreach (var (id, status) in current.Statuses.Where(each => Retrying(each.Value)))
                {
                    if (record.Newest(id) is not { RetryAt: { } at } attempt || at > now || declared(record.Unit, id) is not { } node) continue;
                    var moved = NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Ready, NodeCause.Daoris));
                    record = Kept(record, moved, "retry", now);
                    record = With(record, attempt, attempt with { RetryAt = null });
                    said.Add(new(record.Unit, id, moved.Moved ? PerformedWhat.Retry : PerformedWhat.Refused, moved.Refused));
                }

                return (ReferenceEquals(record, current) ? null : record, said);
            });
            if (refused is not null) done.Add(new(read.Unit, "", PerformedWhat.Refused, refused));
            else done.AddRange(retried!);
        }

        return done;
    }

    private static bool Retrying(NodeStatus status) => status is { State: NodeStates.Failed, Transient: true };

    /// <summary>Whether Daoris asks after an attempt now: one never acknowledged, unknown, or sent by a loop since ended, to a process since
    /// replaced, or past its deadline; a running answer waits to its <see cref="NodeAttempt.AskAt"/>.</summary>
    private bool Asked(NodeAttempt attempt, DateTimeOffset now) => attempt.State switch
    {
        AttemptStates.Recorded or AttemptStates.Unknown => attempt.AskAt is not { } ask || ask <= now,
        AttemptStates.Sent => !SentHere(attempt.Call.Invocation) || port.GenerationOf(attempt.Plugin) != attempt.Generation
            || (attempt.AskAt ?? attempt.Call.DeadlineAt) <= now,
        _ => false,
    };

    /// <remarks>
    /// FABRIC-RUN1e: an attempt called about takes one of <paramref name="sends"/>, its question and the redelivery that answer may bring
    /// together; one held with no call takes none, and neither does one whose plugin's process is not up, which is not asked now.
    /// </remarks>
    private async Task<WorkPerformed> ReconcileOneAsync(string unit, string invocation, NodeDeclaration node, WorkSends sends, CancellationToken ct)
    {
        var now = clock();

        // §E2's active → unknown: its outcome cannot be established now. Written before the question, so a loop that ends here asks again,
        // and only while the record still holds the attempt as one to ask after.
        var (refused, asked) = records.Edit<NodeAttempt?>(unit, record =>
        {
            if (record.Invoked(invocation) is not { } attempt || !Asked(attempt, now)) return (null, null);
            var status = StatusOf(record, node.Id);
            if (status.State == NodeStates.Active && status.Attempt == attempt.Attempt)
            {
                record = Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Unknown, NodeCause.Daoris) { Attempt = attempt.Attempt }), "unknown", now);
            }

            var unknown = attempt with { State = AttemptStates.Unknown };
            return (With(record, attempt, unknown), unknown);
        });
        if (refused is not null) return new(unit, node.Id, PerformedWhat.Refused, refused);
        if (asked is null) return new(unit, node.Id, PerformedWhat.NotTaken, Moved);
        if (node.Perform is not { } perform || !(perform.Reconcile || perform.Idempotent))
        {
            return Edited(unit, node.Id, record => Still(record, invocation, node.Id, current => Hold(record, current, NoReconcile)));
        }

        if (!perform.Reconcile)
        {
            // §D6: an idempotent step's redelivery uses the same key, its plugin deduplicates it durably, and its answer comes by `work/step`.
            // Idempotency allows the same key again; it does not say the effect never began (FABRIC-PERFORM1h). So an attempt its node has moved
            // off is held for the person, its gate kept, never settled as not started while its effect may still be running.
            var decided = Edited(unit, node.Id, record => Still(record, invocation, node.Id, current =>
            {
                var status = StatusOf(record, node.Id);
                if (status.State != NodeStates.Unknown || status.Attempt != current.Attempt) return Hold(record, current, NotCurrent);
                return (Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Active, NodeCause.Daoris)), "redeliver", now),
                    new WorkPerformed(unit, node.Id, PerformedWhat.Redelivered));
            }));
            return decided.What == PerformedWhat.Redelivered && sends.Take() ? await SendAsync(unit, asked, node, PerformedWhat.Redelivered, ct).ConfigureAwait(false) : decided;
        }

        if (port.GenerationOf(asked.Plugin) is null || !sends.Take()) return new(unit, node.Id, PerformedWhat.Unanswered);
        WorkReconcileAnswer answer;
        int generation;
        try
        {
            (answer, generation) = await port.ReconcileStepAsync(
                asked.Plugin, new WorkReconcileCall(unit, asked.Call.Step, invocation, asked.Call.IdempotencyKey, asked.Call.SubjectRevision), ct)
                .ConfigureAwait(false);
        }
        catch (Exception error) when (Unanswered(error))
        {
            return new(unit, node.Id, PerformedWhat.Unanswered);
        }

        // After the await, the record as it is now (FABRIC-PERFORM1h): the answer is about this invocation alone, and applies only while the
        // attempt is still unknown, so an outcome heard while the question was out is kept, never taken back by a later `running`.
        now = clock();
        var applied = Edited(unit, node.Id, record => Still(record, invocation, node.Id, current =>
        {
            var status = StatusOf(record, node.Id);
            var isCurrent = status.Attempt == current.Attempt && status.State == NodeStates.Unknown;
            switch (answer.State)
            {
                case ReconcileStates.Done when answer.Outcome is { } outcome:
                    return Outcome(record, current, node, outcome, generation);
                case ReconcileStates.Done:
                    return Hold(record, current, NoOutcome);
                case ReconcileStates.Failed:
                    return Outcome(record, current, node, answer.Outcome is { State: WorkStates.Failed } failed ? failed : new WorkOutcome(WorkStates.Failed, NoCode), generation);
                case ReconcileStates.NotStarted when isCurrent:
                    return (Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Active, NodeCause.Daoris)), "redeliver", now),
                        new WorkPerformed(unit, node.Id, PerformedWhat.Redelivered));
                case ReconcileStates.NotStarted:
                    // Its plugin's answer that it never started, and no longer current (stopped, skipped, superseded): nothing to deliver, and the
                    // gate lifts.
                    var settled = With(record, current, current with { State = AttemptStates.Settled, Outcome = ReconcileStates.NotStarted, Generation = generation });
                    return (settled with { Statuses = With(settled.Statuses, NodeLifecycle.Reconciled(status, current.Attempt)) }, new WorkPerformed(unit, node.Id, PerformedWhat.Stale));
                case ReconcileStates.Running:
                    var running = isCurrent ? Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Active, NodeCause.Daoris)), "running", now) : record;
                    var wait = current.Call.DeadlineAt > now ? current.Call.DeadlineAt : now + Timeout(node);
                    return (With(running, current, current with { State = isCurrent ? AttemptStates.Sent : AttemptStates.Unknown, Generation = generation, AskAt = wait }),
                        new WorkPerformed(unit, node.Id, PerformedWhat.Running));
                default:
                    // §D6: unknown stops the step, and the person sees it; no new attempt is made by itself.
                    return Hold(record, current with { Generation = generation }, ReconcileStates.Unknown);
            }
        }));
        if (applied.What == PerformedWhat.Running) lock (_sentHere) _sentHere.Add(invocation);
        return applied.What == PerformedWhat.Redelivered ? await SendAsync(unit, asked, node, PerformedWhat.Redelivered, ct).ConfigureAwait(false) : applied;
    }

    /// <summary>
    /// One outcome applied to its attempt's node through the one table: a reach on its binding with the plugin's evidence and outputs, a
    /// failure with its code (transient where the step declares it, its retry's deadline persisted), or the plugin's unknown. A settled
    /// attempt's is a replay; an earlier attempt's is recorded as a stale one's and lifts the gate it held (§D3). Judged on the record an
    /// edit holds: the record it returns is written, nothing when null.
    /// </summary>
    private (NodeRecord? Next, WorkPerformed Result) Outcome(NodeRecord record, NodeAttempt attempt, NodeDeclaration node, WorkOutcome outcome, int generation)
    {
        var unit = record.Unit;
        if (attempt.State == AttemptStates.Settled) return (null, new(unit, node.Id, PerformedWhat.Replay));
        var now = clock();
        var status = StatusOf(record, node.Id);
        var settled = attempt with { State = AttemptStates.Settled, Outcome = outcome.State, Code = outcome.Code, Generation = generation };
        if (status.Attempt != attempt.Attempt || status.State is not (NodeStates.Active or NodeStates.Unknown))
        {
            var stale = With(record, attempt, settled);
            return (stale with { Statuses = With(stale.Statuses, NodeLifecycle.Reconciled(status, attempt.Attempt)) }, new(unit, node.Id, PerformedWhat.Stale));
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
                    return (With(record, attempt, attempt with { State = AttemptStates.Unknown, Generation = generation }), new(unit, node.Id, PerformedWhat.Applied, WorkStates.Unknown));
                }

                move = new NodeMove(NodeStates.Unknown, NodeCause.Daoris) { Attempt = attempt.Attempt };
                settled = attempt with { State = AttemptStates.Unknown, Generation = generation };
                break;
        }

        var moved = NodeLifecycle.Apply(node, status, move);
        if (!moved.Moved) return Hold(record, attempt with { Outcome = outcome.State, Generation = generation }, moved.Refused!);
        if (moved.Status is { State: NodeStates.Failed, Transient: true } failedStatus && failedStatus.Tries < (node.Retry?.MaxAttempts ?? 1))
        {
            // FABRIC-PERFORM1h: the delay of the next try within the binding, as the bound counts it; the attempt's own number keeps growing
            // across bindings and runs, and would index a later delay.
            settled = settled with { RetryAt = now + NodeLifecycle.DelayBefore(node, failedStatus.Tries + 1) };
        }

        return (With(Kept(record, moved, outcome.State, now), attempt, settled), new(unit, node.Id, PerformedWhat.Applied, outcome.State));
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
    /// its outcome is unknown until reconciled. A cancelled token is the loop ending, which writes nothing more. The receipt is applied after
    /// its await to the record as it is then (FABRIC-PERFORM1h): it counts its delivery, and moves to sent only an attempt still waiting for
    /// it; an outcome heard or held meanwhile stays, and so does every other attempt and node on the unit.
    /// </summary>
    private async Task<WorkPerformed> SendAsync(string unit, NodeAttempt attempt, NodeDeclaration node, string what, CancellationToken ct)
    {
        var invocation = attempt.Call.Invocation;
        int generation;
        try
        {
            generation = await port.PerformAsync(attempt.Plugin, attempt.Call, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (Unanswered(error))
        {
            var now = clock();
            var (unsent, _) = records.Edit<bool>(unit, record =>
            {
                if (record.Invoked(invocation) is not { State: AttemptStates.Recorded or AttemptStates.Unknown } current) return (null, false);
                var status = StatusOf(record, node.Id);
                if (status.State == NodeStates.Active && status.Attempt == current.Attempt)
                {
                    record = Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Unknown, NodeCause.Daoris) { Attempt = current.Attempt }), "unknown", now);
                }

                return (With(record, current, current with { State = AttemptStates.Unknown }), true);
            });
            return new(unit, node.Id, PerformedWhat.Unknown, unsent ?? PerformedWhat.Unanswered);
        }

        lock (_sentHere) _sentHere.Add(invocation);
        var (refused, _) = records.Edit<bool>(unit, record =>
        {
            if (record.Invoked(invocation) is not { } current) return (null, false);
            var receipt = current.State is AttemptStates.Recorded or AttemptStates.Unknown
                ? current with { State = AttemptStates.Sent, Generation = generation, Deliveries = current.Deliveries + 1, AskAt = null }
                : current with { Deliveries = current.Deliveries + 1 };
            return (With(record, current, receipt), true);
        });

        // A receipt the record could not keep leaves the attempt as it stood, recorded or unknown: the next loop asks after it.
        return refused is null ? new(unit, node.Id, what) : new(unit, node.Id, PerformedWhat.Unknown, refused);
    }

    /// <summary>An attempt nothing establishes by itself: held for the person, its node left as it stands, its gate held (§D6).</summary>
    private static (NodeRecord? Next, WorkPerformed Result) Hold(NodeRecord record, NodeAttempt attempt, string code) =>
        (With(record, attempt, attempt with { State = AttemptStates.Held, Code = code }), new(record.Unit, attempt.Node, PerformedWhat.Held, code));

    /// <summary>
    /// A reconciliation's change, made on the attempt as the record holds it now and only while it is still as this performer left it before
    /// the question (unknown): one settled meanwhile is a replay, one moved otherwise (sent, held) is not taken again (FABRIC-PERFORM1h).
    /// </summary>
    private static (NodeRecord? Next, WorkPerformed Result) Still(
        NodeRecord record, string invocation, string node, Func<NodeAttempt, (NodeRecord? Next, WorkPerformed Result)> change) => record.Invoked(invocation) switch
    {
        null => (null, new(record.Unit, node, PerformedWhat.NotTaken, NotAttempt)),
        { State: AttemptStates.Settled } => (null, new(record.Unit, node, PerformedWhat.Replay)),
        { State: AttemptStates.Unknown } attempt => change(attempt),
        _ => (null, new(record.Unit, node, PerformedWhat.NotTaken, Moved)),
    };

    /// <summary>One edit of a unit's record that says what it did; an edit refused (no lease, a record that does not read) says why.</summary>
    private WorkPerformed Edited(string unit, string node, Func<NodeRecord, (NodeRecord? Next, WorkPerformed Result)> change)
    {
        var (refused, result) = records.Edit(unit, change);
        return refused is null ? result! : new(unit, node, PerformedWhat.Refused, refused);
    }

    /// <summary>A failure of the call itself, not the loop ending: the attempt's outcome is unknown, never a second blind dispatch.</summary>
    private static bool Unanswered(Exception error) =>
        error is not OperationCanceledException && error is DriverException or IOException or TimeoutException or InvalidOperationException;

    /// <summary>The unit whose record names an invocation, and the attempt as read: what an answer is judged on again under the lock.</summary>
    private (string Unit, NodeAttempt Attempt)? Find(string invocation)
    {
        foreach (var record in records.All().Records)
        {
            if (record.Invoked(invocation) is { } attempt) return (record.Unit, attempt);
        }

        return null;
    }

    private bool SentHere(string invocation)
    {
        lock (_sentHere) return _sentHere.Contains(invocation);
    }

    private static NodeStatus StatusOf(NodeRecord record, string node) => record.Statuses.GetValueOrDefault(node) ?? NodeLifecycle.Start(record.Unit, node);

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
