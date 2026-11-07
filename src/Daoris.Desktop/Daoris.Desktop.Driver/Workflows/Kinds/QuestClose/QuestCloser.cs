using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Daoris's own <c>quest.close</c>: the attempt written with its key and token before
/// the close is sent, the door's answer kept on it, and a timeout or a restart sending the same close again.
/// </summary>
/// <remarks>
/// <para><b>The fence</b>: step 1 is one locked edit that judges the start on the record as the evaluation
/// left it and writes the attempt with its key and token; step 2 is the door's one transaction; step 3 is one locked edit that keeps the answer
/// on that attempt, only while it still waits for it. Nothing is awaited inside an edit. What holds the driver's facts still between
/// steps 1 and 3 is the lease's one writer and the pass's one-at-a-time: no other writer edits the unit's record meanwhile.</para>
/// <para><b>Step 4:</b> an unanswered close is unknown, and the next pass sends the same close again, its key, token and quests unchanged: the
/// door answers its first outcome, a refusal included, and writes nothing twice.</para>
/// </remarks>
public sealed class QuestCloser(NodeRecordFiles records, IQuestClosePort port, Func<DateTimeOffset> clock)
{
    /// <summary>A refused close is never a transient failure: a new close is made under a new key once what refused it moves.</summary>
    public const string Refused = "refused";

    private readonly List<string> _said = [];

    /// <summary>What the closer has said since its last asking: each refusal in the door's words.</summary>
    public IReadOnlyList<string> TakeSaid()
    {
        var said = _said.ToList();
        _said.Clear();
        return said;
    }

    /// <summary>The fence's steps 1 to 3 on the start the evaluation proposed: the attempt written first, then the close sent, then its answer kept.</summary>
    public async Task<WorkPerformed> StartAsync(NodeDeclaration node, WireStart start, NodeStatus evaluated, IReadOnlyList<QuestCloseItem> named, CancellationToken ct)
    {
        if (start.Move.Binding is not { } binding) return new("", start.Node, PerformedWhat.Refused, NodeRefusal.NoBinding);
        var unit = binding.Unit;
        if (node.Kind != NodeKinds.QuestClose || named.Count == 0) return new(unit, node.Id, PerformedWhat.Refused, WorkPerformer.NotDeclared);
        var now = clock();
        var (refused, started) = records.Edit<(NodeAttempt? Attempt, string? Refused)>(unit, record =>
        {
            var held = WorkPerformer.StatusOf(record, node.Id);
            if (!WorkPerformer.Same(held, evaluated)) return (null, (null, WorkPerformer.Moved));
            var moved = NodeLifecycle.Apply(node, held, start.Move);
            if (!moved.Moved) return (null, (null, moved.Refused));
            var call = new WorkPerformCall(unit, node.Kind, node.Version, WorkPerformer.Random("i-", 12), WorkPerformer.Random("k-", 16), binding.SubjectRevision ?? "",
                [.. binding.Inputs.OrderBy(input => input.Key, StringComparer.Ordinal).Select(input => input.Value.Ref)], now + WorkPerformer.Timeout(node));
            var attempt = new NodeAttempt(node.Id, moved.Status.Attempt, QuestClosing.Sender, call, AttemptStates.Recorded)
            {
                RecordedAt = now,
                Close = new QuestCloseCall(QuestClosing.Token(unit, binding.SubjectRevision, binding.GraphDigest, binding.PrerequisiteReceipts), [.. named]),
            };
            var fired = record.Fired.Concat(start.Firings.Select(firing => firing.Key)).Distinct(StringComparer.Ordinal).ToList();
            return (WorkPerformer.Kept(record, moved, "start", now) with { Attempts = [.. record.Attempts, attempt], Fired = fired }, (attempt, null));
        });
        if (refused is not null) return new(unit, node.Id, PerformedWhat.Refused, refused);
        if (started.Attempt is not { } recorded) return new(unit, node.Id, PerformedWhat.Refused, started.Refused);
        return await AnswerAsync(unit, recorded, node, PerformedWhat.Sent, ask: false, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Step 4: every close attempt whose outcome is not established (written and never answered, or unanswered) is asked after, each question
    /// taking one of <paramref name="sends"/>. Asked whatever graph governs its unit now: a close in flight is reconciled.
    /// </summary>
    /// <remarks>
    /// Never sent again blind. A close written before a crash, never delivered, is
    /// judged on the revision it carries, and its key's idempotency protects an outcome already kept, not a key never used, so a resend
    /// after the branch moved would close on obsolete evidence. Its door is asked what became of the key, answering from its own record and
    /// executing nothing: a key it took is settled from the outcome it kept; a key it never took is withdrawn there, in the same transaction,
    /// and its attempt is settled as never started, its node judged again on what is there now (<see cref="Withdrawn"/>), so a close is made
    /// again only through the fresh fence, under a new key. An unanswered question leaves the close unknown, offered to the person.
    /// </remarks>
    public async Task<IReadOnlyList<WorkPerformed>> ReconcileAsync(WorkSends sends, CancellationToken ct)
    {
        var done = new List<WorkPerformed>();
        foreach (var record in records.All().Records)
        {
            foreach (var attempt in record.Attempts.Where(Waiting).ToList())
            {
                if (!sends.Take()) return done;
                done.Add(await AnswerAsync(record.Unit, attempt, Declaration(attempt), PerformedWhat.Redelivered, ask: true, ct).ConfigureAwait(false));
            }
        }

        return done;
    }

    /// <summary>
    /// The person's word on each held close, and on each unknown one (<see cref="QuestClosing.Unanswered"/>),
    /// applied before any close is asked after (<see cref="ReconcileAsync"/>): a close's recovery, never a plugin's <i>it happened</i> or <i>it
    /// did not</i>, since its outcome is its door's answer and no word of the person's stands in for it, and a new close is never made. The word
    /// is cleared as it is applied, so only one not applied yet is ever written.
    /// </summary>
    /// <remarks>
    /// <para><b><c>record</c></b>: the door's answer kept on the attempt (it closed the quest) recorded again through the one table, its key as
    /// the evidence. Refused again, the attempt stays held with the new refusal; its node moved off, it settles as a stale one's.</para>
    /// <para><b><c>resend</c></b>: the attempt waits for its answer again, so its door is asked within this pass what became of its key, its
    /// token and named submissions; the door answers its first outcome and writes nothing.</para>
    /// <para><b><c>stop</c></b>, offered apart: every live node of its work stopped by the person, and the close's answer kept as it is (a
    /// kept one settles the attempt, its receipt kept; one not kept is asked for again with its key, and lands as a stale one's). The quest's
    /// Done, if the door wrote it, stands.</para>
    /// <para>A plugin's word recorded on a close, or <c>record</c> with nothing kept, is taken back, saying why.</para>
    /// <para><b>A close for a submission reopened or replaced since</b> (<see cref="QuestClosing.Moved"/>, judged on
    /// <paramref name="quests"/>): <c>record</c> and <c>stop</c> are taken back, saying why. Its answer closes none of the work there is now,
    /// and a stop through it would stop the next submission's: its nodes already on that work, or the close node itself, since a stop is the
    /// person's word for the work and not for a revision (<see cref="NodeStates.Bound"/>), so the next submission's close would never run.
    /// The nodes it is bound to lapse with its revision. <c>resend</c> asks after its key alone.</para>
    /// </remarks>
    /// <param name="declared">The declaration governing each unit's node now.</param>
    /// <param name="quests">The quests the pass read, by id; null, no close is judged moved.</param>
    public IReadOnlyList<WorkPerformed> Resolve(Func<string, string, NodeDeclaration?> declared, IReadOnlyDictionary<string, QuestView>? quests = null)
    {
        var done = new List<WorkPerformed>();
        foreach (var read in records.All().Records)
        {
            if (!read.Attempts.Any(Worded)) continue;
            var lines = new List<string>();
            var (refused, resolved) = records.Edit<List<WorkPerformed>>(read.Unit, current =>
            {
                lines.Clear();
                var said = new List<WorkPerformed>();
                var record = current;
                foreach (var attempt in current.Attempts.Where(Worded))
                {
                    var (next, result) = Recover(record, attempt, declared, quests, lines);
                    record = next;
                    said.Add(result);
                }

                return (ReferenceEquals(record, current) ? null : record, said);
            });
            if (refused is not null) done.Add(new(read.Unit, "", PerformedWhat.Refused, refused));
            else
            {
                done.AddRange(resolved!);
                _said.AddRange(lines);
            }
        }

        return done;
    }

    /// <summary>Whether any close attempt holds the person's word not applied yet: the pass that applies it reads the quests afresh.</summary>
    public bool Worded() => records.All().Records.Any(record => record.Attempts.Any(Worded));

    /// <summary>A close attempt holding a word to apply. An unknown close takes the person's word as a held one does, before it is asked after.</summary>
    // A close found closed only in part takes the person's stop on its settled attempt (QuestClosing.Partly).
    private static bool Worded(NodeAttempt attempt) =>
        attempt is { Plugin: QuestClosing.Sender, State: AttemptStates.Held or AttemptStates.Unknown, Resolution: not null, Close: not null }
            or { Plugin: QuestClosing.Sender or QuestClosing.Chained, State: AttemptStates.Settled, Resolution: HeldResolutions.Stop };

    /// <summary>One word on a held close applied (<see cref="Resolve"/>): the record after it, and what it came to; a word taken back says why in <paramref name="lines"/>.</summary>
    private (NodeRecord Next, WorkPerformed Result) Recover(
        NodeRecord record, NodeAttempt attempt, Func<string, string, NodeDeclaration?> declared, IReadOnlyDictionary<string, QuestView>? quests, List<string> lines)
    {
        var unit = record.Unit;
        var word = attempt.Resolution!;
        var node = declared(unit, attempt.Node) ?? Declaration(attempt);
        var cleared = attempt with { Resolution = null, ResolvedAt = null };

        // The stop of a close found closed only in part. Its answer stands for the quests it kept, so the attempt stays settled,
        // and its work's live nodes are stopped: the lost quest's work is the winner's, never closed here over its take.
        if (attempt.State == AttemptStates.Settled)
        {
            var stopped = new List<string>();
            record = Stopped(record, node, declared, stopped);
            if (stopped.Count > 0) lines.Add($"work  {unit}: stopped by you, as its close was found closed only in part: {string.Join(", ", stopped)}.");
            return (WorkPerformer.With(record, attempt, cleared), new(unit, node.Id, PerformedWhat.Resolved, word));
        }

        if (WorkPerformer.WordRefused(attempt, word) is { } code)
        {
            return (WorkPerformer.With(record, attempt, cleared with { Code = code }), new(unit, node.Id, PerformedWhat.Refused, code));
        }

        // S1's close never records on, nor stops, the work of S2 at its commit; the nodes bound to S1 lapse with its revision.
        if (word != HeldResolutions.Resend && quests is not null && QuestClosing.Moved(attempt, quests) is { } moved)
        {
            lines.Add($"work  {unit}: your `{word}` on its close ({node.Id}, attempt {attempt.Attempt}) was not taken: #{moved.Id}'s submission "
                + $"{moved.Machine}/{moved.Sequence}, which that close names, was reopened or replaced since, so the work there is now is never that close's to "
                + (word == HeldResolutions.Record ? "record" : "stop")
                + (attempt.State == AttemptStates.Unknown ? "; its door is still asked what became of it." : "; say resend to ask its door what became of it."));
            // Kept in its history too, said on the page once its close is not listed (its node moved off, before
            // this pass ends or later); while it is listed, its held row says it (submission-moved).
            var why = $"{QuestClosing.TakenBack} #{moved.Id} {word} {attempt.Attempt}";
            record = record with { History = [.. record.History, new NodeHistory(clock(), why, WorkPerformer.StatusOf(record, node.Id))] };
            return (WorkPerformer.With(record, attempt, cleared with { Code = WorkPerformer.SubmissionMoved }),
                new(unit, node.Id, PerformedWhat.Refused, WorkPerformer.SubmissionMoved));
        }

        if (word == HeldResolutions.Resend)
        {
            // The person's resend answers a word of theirs taken back on it, which is then unsaid.
            var answered = $" {attempt.Attempt}";
            record = record with
            {
                History = [.. record.History.Where(entry => !(entry.Was.Node == node.Id && entry.Why.StartsWith(QuestClosing.TakenBack + " #", StringComparison.Ordinal)
                                                              && entry.Why.EndsWith(answered, StringComparison.Ordinal)))],
            };
            return (WorkPerformer.With(record, attempt, cleared with { State = AttemptStates.Unknown }), new(unit, node.Id, PerformedWhat.Resolved, word));
        }

        if (word == HeldResolutions.Record)
        {
            var (next, result) = Answered(record, cleared, node, closed: true, null, attempt.Close);
            return (next, result.What == PerformedWhat.Held ? result with { What = PerformedWhat.Refused } : new(unit, node.Id, PerformedWhat.Resolved, word));
        }

        // Stop, apart from the close's answer: its work's live nodes stopped by the person, whatever the close comes to.
        record = Stopped(record, node, declared, []);
        if (attempt.Outcome != WorkStates.Reached)
        {
            return (WorkPerformer.With(record, attempt, cleared with { State = AttemptStates.Unknown }), new(unit, node.Id, PerformedWhat.Resolved, word));
        }

        var after = WorkPerformer.StatusOf(record, node.Id);
        record = WorkPerformer.With(record, attempt, cleared with { State = AttemptStates.Settled, Code = null });
        return (record with { Statuses = WorkPerformer.With(record.Statuses, NodeLifecycle.Reconciled(after, attempt.Attempt)) }, new(unit, node.Id, PerformedWhat.Resolved, word));
    }

    /// <summary>The record with every live node of its work stopped by the person, each kept in its history and named in <paramref name="stopped"/>.</summary>
    private NodeRecord Stopped(NodeRecord record, NodeDeclaration node, Func<string, string, NodeDeclaration?> declared, List<string> stopped)
    {
        var now = clock();
        foreach (var (id, status) in record.Statuses.Where(each => NodeStates.Live.Contains(each.Value.State)).ToList())
        {
            if ((id == node.Id ? node : declared(record.Unit, id)) is not { } live) continue;
            var moved = NodeLifecycle.Apply(live, status, new NodeMove(NodeStates.Stopped, NodeCause.Person));
            record = WorkPerformer.Kept(record, moved, "person-stop", now);
            if (moved.Moved) stopped.Add(id);
        }

        return record;
    }

    /// <summary>A close node's declaration from its attempt: a close in flight is answered whatever graph governs its unit now.</summary>
    private static NodeDeclaration Declaration(NodeAttempt attempt) => new(attempt.Node, NodeKinds.QuestClose, attempt.Call.StepVersion, NodeReach.Performed);

    /// <summary>
    /// The chain's close reached on a quest it closed: a chain quest's own close node, proposed to start once its
    /// quest is Done by the close its chain's last quest made, is started and reached in one locked edit on that close's key, never sent.
    /// </summary>
    public WorkPerformed ReachByChain(NodeDeclaration node, WireStart start, NodeStatus evaluated, NodeAttempt chained)
    {
        if (start.Move.Binding is not { } binding) return new("", start.Node, PerformedWhat.Refused, NodeRefusal.NoBinding);
        var unit = binding.Unit;
        var now = clock();
        var (refused, result) = records.Edit(unit, record =>
        {
            var held = WorkPerformer.StatusOf(record, node.Id);
            if (!WorkPerformer.Same(held, evaluated)) return (null, new WorkPerformed(unit, node.Id, PerformedWhat.Refused, WorkPerformer.Moved));
            var begun = NodeLifecycle.Apply(node, held, start.Move);
            if (!begun.Moved) return (null, new WorkPerformed(unit, node.Id, PerformedWhat.Refused, begun.Refused));
            var reached = NodeLifecycle.Apply(node, begun.Status, Reach(node, begun.Status, chained.Call.IdempotencyKey));
            if (!reached.Moved) return (null, new WorkPerformed(unit, node.Id, PerformedWhat.Refused, reached.Refused));
            var fired = record.Fired.Concat(start.Firings.Select(firing => firing.Key)).Distinct(StringComparer.Ordinal).ToList();

            // A node with attempts of its own (the close's own node, started again once its quest's Done moved its revision)
            // keeps one for the attempt started here, settled on the chain's close and never sent, so its record still agrees with itself
            // (each node's attempts end at its status's) and reads; marked as the chain's, so nothing reconciles or says it.
            var attempts = record.Attempts.Any(attempt => attempt.Node == node.Id)
                ? [.. record.Attempts, chained with
                {
                    Node = node.Id, Attempt = begun.Status.Attempt, Plugin = QuestClosing.Chained, State = AttemptStates.Settled, RecordedAt = now,
                    Outcome = WorkStates.Reached, Code = null, Call = chained.Call with { Unit = unit, Invocation = WorkPerformer.Random("i-", 12) },
                }]
                : record.Attempts;
            return (WorkPerformer.Kept(WorkPerformer.Kept(record, begun, "start", now), reached, "chain", now) with { Fired = fired, Attempts = attempts },
                new WorkPerformed(unit, node.Id, PerformedWhat.Applied, WorkStates.Reached));
        });
        return refused is null ? result! : new(unit, node.Id, PerformedWhat.Refused, refused);
    }

    /// <summary>Whether a close attempt waits for its door's answer: written and never answered, or unanswered.</summary>
    private static bool Waiting(NodeAttempt attempt) =>
        attempt.Plugin == QuestClosing.Sender && attempt.Close is not null && attempt.State is AttemptStates.Recorded or AttemptStates.Unknown;

    /// <summary>
    /// One send of a close, its key, token and quests as written (step 2), or, with <paramref name="ask"/>, the question of what became of it;
    /// and the answer kept on its attempt after the await (step 3), only while that attempt still waits for it. Unanswered,
    /// the attempt and its node are unknown, and it is asked after again.
    /// </summary>
    private async Task<WorkPerformed> AnswerAsync(string unit, NodeAttempt attempt, NodeDeclaration node, string what, bool ask, CancellationToken ct)
    {
        var close = attempt.Close!;
        var invocation = attempt.Call.Invocation;
        QuestCloseAnswer answer;
        try
        {
            var key = attempt.Call.IdempotencyKey;
            answer = ask
                ? await port.CloseOutcomeAsync(close.Quests[^1].Id, key, close.Token, close.Quests, ct).ConfigureAwait(false)
                : await port.CloseQuestsAsync(close.Quests[^1].Id, key, close.Token, close.Quests, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (Unanswered(error, ct))
        {
            var now = clock();
            var (unsent, _) = records.Edit<bool>(unit, record =>
            {
                if (record.Invoked(invocation) is not { State: AttemptStates.Recorded or AttemptStates.Unknown } current) return (null, false);
                var status = WorkPerformer.StatusOf(record, node.Id);
                if (status.State == NodeStates.Active && status.Attempt == current.Attempt)
                {
                    record = WorkPerformer.Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Unknown, NodeCause.Daoris) { Attempt = current.Attempt }), "unknown", now);
                }

                return (WorkPerformer.With(record, current, current with { State = AttemptStates.Unknown }), true);
            });
            return new(unit, node.Id, PerformedWhat.Unknown, unsent ?? PerformedWhat.Unanswered);
        }

        var withdrawn = answer is { Outcome: QuestCloseOutcomes.Refused, Code: QuestCloseOutcomes.Withdrawn };
        var (refused, result) = records.Edit(unit, record => record.Invoked(invocation) is { State: AttemptStates.Recorded or AttemptStates.Unknown } current
            ? withdrawn ? Withdrawn(record, current, node) : Outcome(record, current, node, answer)
            : (null, new WorkPerformed(unit, node.Id, PerformedWhat.NotTaken, WorkPerformer.Moved)));
        if (refused is not null) return new(unit, node.Id, PerformedWhat.Unknown, refused);
        if (withdrawn && result!.What != PerformedWhat.NotTaken)
        {
            _said.Add($"work  {unit}: its close was never taken by its door, so its key is withdrawn and it is judged again on what is there now.");
        }
        else if (result is { What: PerformedWhat.Applied, Code: WorkStates.Failed })
        {
            _said.Add($"work  {unit}: its close was not made ({answer.Code ?? answer.Outcome}): {answer.Message}");
        }

        return result!.What == PerformedWhat.Applied && what == PerformedWhat.Redelivered ? result with { What = PerformedWhat.Applied } : result;
    }

    /// <summary>
    /// A close whose door never took its key: it never ran, and the door withdrew the key as it answered, so it never will. The
    /// attempt is settled as never started, its key kept as the one withdrawn, and a node still on it is invalidated, its attempt superseded:
    /// the next evaluation judges it again on what is there now, and where it is still eligible makes a new close, under a new key, through the
    /// fresh fence. A node moved off it is left as it stands, the gate the attempt held lifted.
    /// </summary>
    private (NodeRecord Next, WorkPerformed Result) Withdrawn(NodeRecord record, NodeAttempt attempt, NodeDeclaration node)
    {
        var now = clock();
        var status = WorkPerformer.StatusOf(record, node.Id);
        var result = new WorkPerformed(record.Unit, node.Id, PerformedWhat.Stale);
        if (status.Attempt == attempt.Attempt && status.State is NodeStates.Active or NodeStates.Unknown)
        {
            var superseded = NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Invalidated, NodeCause.Daoris));
            if (superseded.Moved)
            {
                record = WorkPerformer.Kept(record, superseded, QuestCloseOutcomes.Withdrawn, now);
                result = result with { What = PerformedWhat.Applied, Code = NodeStates.Invalidated };
            }
        }

        var after = WorkPerformer.StatusOf(record, node.Id);
        record = WorkPerformer.With(record, attempt, attempt with
        {
            State = AttemptStates.Settled, Outcome = ReconcileStates.NotStarted, Code = QuestCloseOutcomes.Withdrawn, Resolution = null, ResolvedAt = null,
        });
        return (record with { Statuses = WorkPerformer.With(record.Statuses, NodeLifecycle.Reconciled(after, attempt.Attempt)) }, result);
    }

    /// <summary>
    /// The door's answer applied to its attempt's node through the one table: closed is the reach, on the close's key as its evidence; refused
    /// is the failure, with the door's code and its words kept. An answer that is neither never reaches here: the client throws it as unanswered.
    /// An attempt its node has moved off is settled as a stale one's, lifting the gate it held.
    /// </summary>
    private (NodeRecord? Next, WorkPerformed Result) Outcome(NodeRecord record, NodeAttempt attempt, NodeDeclaration node, QuestCloseAnswer answer)
    {
        var closed = answer.Outcome == QuestCloseOutcomes.Closed;
        return Answered(record, attempt with { Deliveries = attempt.Deliveries + 1 }, node, closed, closed ? null : answer.Code ?? Refused,
            closed ? attempt.Close : attempt.Close! with { Words = answer.Message });
    }

    /// <summary>
    /// A door's answer applied to its attempt's node (<see cref="Outcome"/>, and the person's <c>record</c>): settled with it where it binds or
    /// where the node moved off the attempt; held where the one table refuses it, the answer kept on the attempt and any word taken back, so
    /// the person's next word is asked afresh and never applied twice.
    /// </summary>
    private (NodeRecord Next, WorkPerformed Result) Answered(NodeRecord record, NodeAttempt attempt, NodeDeclaration node, bool closed, string? code, QuestCloseCall? close)
    {
        var unit = record.Unit;
        var now = clock();
        var outcome = closed ? WorkStates.Reached : WorkStates.Failed;
        var answered = attempt with { Outcome = outcome, Close = close, Resolution = null, ResolvedAt = null };
        var settled = answered with { State = AttemptStates.Settled, Code = code };
        var status = WorkPerformer.StatusOf(record, node.Id);
        if (status.Attempt != attempt.Attempt || status.State is not (NodeStates.Active or NodeStates.Unknown))
        {
            var stale = WorkPerformer.With(record, attempt, settled);
            return (stale with { Statuses = WorkPerformer.With(stale.Statuses, NodeLifecycle.Reconciled(status, attempt.Attempt)) }, new(unit, node.Id, PerformedWhat.Stale));
        }

        var move = closed
            ? Reach(node, status, attempt.Call.IdempotencyKey)
            : new NodeMove(NodeStates.Failed, NodeCause.Authority) { Source = NodeSource.CloseDoor, Attempt = attempt.Attempt, Code = code };
        var moved = NodeLifecycle.Apply(node, status, move);
        if (!moved.Moved)
        {
            var (held, result) = WorkPerformer.Hold(record, answered, moved.Refused!);
            return (held!, result);
        }

        return (WorkPerformer.With(WorkPerformer.Kept(record, moved, outcome, now), attempt, settled), new(unit, node.Id, PerformedWhat.Applied, outcome));
    }

    /// <summary>The close's reach (the close door's write is its authority): its binding, with the close's key as its evidence.</summary>
    private static NodeMove Reach(NodeDeclaration node, NodeStatus status, string key) => new(NodeStates.Reached, NodeCause.Authority)
    {
        Source = NodeSource.CloseDoor,
        Attempt = status.Attempt,
        Acceptance = status.Binding is { } binding
            ? binding with { Evidence = new NodePort(node.Evidence ?? node.Kind, "close:" + key, binding.SubjectRevision, null) }
            : null,
    };

    /// <summary>A failure of the call itself, not the loop ending: the close's outcome is unknown, and it is sent again, never made anew.</summary>
    private static bool Unanswered(Exception error, CancellationToken ct) =>
        !ct.IsCancellationRequested
        && error is HttpRequestException or IOException or TimeoutException or TaskCanceledException or DriverException or JsonException;
}
