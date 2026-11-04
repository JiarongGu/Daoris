using static Daoris.Driver.WorkPerformer;

namespace Daoris.Driver;

/// <summary>One outcome applied to its attempt's node through the one table, as a current attempt's or a stale one's.</summary>
internal sealed class WorkOutcomes(Func<DateTimeOffset> clock)
{
    /// <summary>
    /// One outcome applied to its attempt's node through the one table: a reach on its binding with the plugin's evidence and outputs, a
    /// failure with its code (transient where the step declares it, its retry's deadline persisted), or the plugin's unknown. A settled
    /// attempt's is a replay; an earlier attempt's is recorded as a stale one's and lifts the gate it held (§D3). Judged on the record an
    /// edit holds: the record it returns is written, nothing when null.
    /// </summary>
    internal (NodeRecord? Next, WorkPerformed Result) Outcome(NodeRecord record, NodeAttempt attempt, NodeDeclaration node, WorkOutcome outcome, int generation)
    {
        var unit = record.Unit;
        if (attempt.State == AttemptStates.Settled)
        {
            // §C4's fifth rule (FABRIC-PERFORM1c): one outcome is kept per attempt. The same again is a replay, applied once; another came late.
            return (null, string.Equals(attempt.Outcome, outcome.State, StringComparison.Ordinal)
                ? new(unit, node.Id, PerformedWhat.Replay)
                : new(unit, node.Id, PerformedWhat.NotTaken, WorkRefusals.Late));
        }

        var now = clock();
        var status = StatusOf(record, node.Id);
        var settled = attempt with { State = AttemptStates.Settled, Outcome = outcome.State, Code = outcome.Code, Generation = generation };
        if (status.Attempt != attempt.Attempt || status.State is not (NodeStates.Active or NodeStates.Unknown))
        {
            var stale = With(record, attempt, settled);
            return (stale with { Statuses = With(stale.Statuses, NodeLifecycle.Reconciled(status, attempt.Attempt)) }, new(unit, node.Id, PerformedWhat.Stale));
        }

        // FABRIC-PIPELINE1d: a check's reach and failure are its kind's own source, the gates' exits as the executor ran them.
        var source = IsCheck(attempt) ? NodeSource.Gates : NodeSource.Plugin;
        NodeMove move;
        switch (outcome.State)
        {
            case WorkStates.Reached:
                move = new NodeMove(NodeStates.Reached, NodeCause.Authority)
                {
                    Source = source, Attempt = attempt.Attempt, Acceptance = status.Binding is { } binding ? Accepted(node, attempt.Call.Invocation, binding, outcome) : null,
                };
                break;
            case WorkStates.Failed:
                var code = outcome.Code ?? NoCode;
                settled = settled with { Code = code };
                move = new NodeMove(NodeStates.Failed, NodeCause.Authority)
                {
                    Source = source, Attempt = attempt.Attempt, Code = code, Transient = node.Perform?.Transient.Contains(code, StringComparer.Ordinal) == true,
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
    /// A completion of an attempt whose work moved past the revision it was dispatched on (FABRIC-PERFORM1c, §C4's fourth rule): its own
    /// outcome, evidence for no current binding. Its node, still on that attempt, is invalidated as the evaluation would (§D4's lapse), and the
    /// outcome is then a stale attempt's (§D3): it settles the attempt and lifts the gate, reaching nothing, so nothing downstream reads it and
    /// the node starts again on the new revision. An unknown outcome leaves the attempt unknown, asked after before any new attempt.
    /// </summary>
    internal (NodeRecord? Next, WorkPerformed Result) Lapse(NodeRecord record, NodeAttempt attempt, NodeDeclaration node, WorkOutcome outcome, int generation)
    {
        var status = StatusOf(record, node.Id);
        if (attempt.State != AttemptStates.Settled && status.Attempt == attempt.Attempt && status.State is NodeStates.Active or NodeStates.Unknown)
        {
            record = Kept(record, NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Invalidated, NodeCause.Daoris)), WireWhy.Lapsed, clock());
        }

        var stale = new WorkPerformed(record.Unit, node.Id, PerformedWhat.Stale, WorkRefusals.Stale);
        if (attempt.State != AttemptStates.Settled && outcome.State == WorkStates.Unknown)
        {
            return (With(record, attempt, attempt with { State = AttemptStates.Unknown, Generation = generation }), stale);
        }

        var (next, said) = Outcome(record, attempt, node, outcome, generation);
        return (next, said.What == PerformedWhat.Stale ? stale : said);
    }

    /// <summary>
    /// The acceptance a reach is on: its attempt's binding, with the plugin's evidence reference at the schema it names (FABRIC-PERFORM1c,
    /// <see cref="WorkBelief.Evidence"/>) and its outputs, each at the schema its reference names (§D6's <c>&lt;schema&gt;:&lt;id&gt;</c>), so
    /// the one table refuses evidence of another schema than the node declares, and an output undeclared or of another schema.
    /// </summary>
    /// <param name="word">What stands for the plugin's word where it gave no reference: an attempt's invocation, an event's id (FABRIC-PERFORM1i).</param>
    internal static NodeAcceptance Accepted(NodeDeclaration node, string word, NodeAcceptance binding, WorkOutcome outcome) => binding with
    {
        Evidence = WorkBelief.Evidence(node, word, binding.SubjectRevision, outcome.Evidence),
        Outcome = outcome.Decision,
        Outputs = (outcome.Outputs ?? new Dictionary<string, WorkOutput>()).ToDictionary(
            output => output.Key,
            output => new NodePort(WorkBelief.SchemaOf(output.Value.Ref) ?? "", output.Value.Ref, output.Value.Revision ?? binding.SubjectRevision, null),
            StringComparer.Ordinal),
    };
}
