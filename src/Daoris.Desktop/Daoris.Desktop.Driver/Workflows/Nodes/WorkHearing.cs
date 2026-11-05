using static Daoris.Driver.WorkPerformer;

namespace Daoris.Driver;

/// <summary>The performer's hearing of the step events: each completion believed, its receipts taken, its outcome applied to the attempt it names.</summary>
internal sealed class WorkHearing(
    NodeRecordFiles records, IWorkPerformPort port, Func<DateTimeOffset> clock, WorkReceipting receipting, WorkOutcomes outcomes)
{
    /// <summary>
    /// The step events heard (<see cref="HookSet.TakeSteps"/>): each completion applied to the attempt its invocation names, once
    /// <see cref="WorkBelief.Completion"/> believes it (FABRIC-PERFORM1c: §C4's five rules, the unit as <paramref name="watched"/> tells it now);
    /// one not believed moves nothing and is counted by its code (<see cref="IWorkPerformPort.Refused"/>). An event naming no invocation is an
    /// observed plugin node's, passed over here and judged by <see cref="WorkObserver"/> (FABRIC-PERFORM1i).
    /// </summary>
    public IReadOnlyList<WorkPerformed> Hear(
        IReadOnlyList<WorkStepHeard> heard, Func<string, string, NodeDeclaration?> declared, Func<string, WatchedUnit?> watched) =>
        Hear(heard, declared, (unit, _) => watched(unit));

    /// <summary>
    /// <see cref="Hear(IReadOnlyList{WorkStepHeard}, Func{string, string, NodeDeclaration?}, Func{string, WatchedUnit?})"/> with the unit as told
    /// for each attempt (FABRIC-CUTOVER1c; the cutover design §2.4.1): on a run's record, the subject the attempt was sent for, which may be one the
    /// run has since left, so its answer still reaches the run's record.
    /// </summary>
    public IReadOnlyList<WorkPerformed> Hear(
        IReadOnlyList<WorkStepHeard> heard, Func<string, string, NodeDeclaration?> declared, Func<string, NodeAttempt, WatchedUnit?> watched)
    {
        var done = new List<WorkPerformed>();

        // §C4: a frame not believed is no answer, counted as a plugin.step line of its code with no words.
        WorkPerformed Counted(WorkStepHeard each, WorkPerformed result)
        {
            if (result is { What: PerformedWhat.NotTaken, Code: { } code }) port.Refused(each.Plugin, code);
            return result;
        }

        foreach (var each in heard)
        {
            if (each.Step.Invocation is not { } invocation) continue;
            if (Find(invocation) is not { } found)
            {
                done.Add(Counted(each, new("", "", PerformedWhat.NotTaken, NotAttempt)));
                continue;
            }

            var (unit, attempt) = found;

            // FABRIC-PERFORM1c: §C4's five rules on the attempt the invocation names, before the one table sees the answer. The sender is the
            // process the frame was read from, the attempt's plugin's current one; a completion binds to its invocation, not its generation, so
            // a later process's replay of it is taken (PERFORM1a's review).
            // FABRIC-CLOSE1b: a close's outcome is its door's answer alone; no plugin's frame speaks for it.
            var node = declared(unit, attempt.Node);
            var told = watched(unit, attempt);
            if ((attempt.Plugin == QuestClosing.Sender
                    ? WorkRefusals.NotSender
                    : WorkBelief.Completion(each, attempt, attempt.Generation is { } held ? port.GenerationOf(attempt.Plugin, held) : port.GenerationOf(attempt.Plugin), node, told, clock())) is { } code)
            {
                done.Add(Counted(each, new(unit, attempt.Node, PerformedWhat.NotTaken, code)));
                continue;
            }

            // Applied to the attempt as the record holds it now, under its lock (FABRIC-PERFORM1h): on its binding, or as a stale attempt's where
            // the work moved past the revision it was dispatched on.
            var lapsed = WorkBelief.Lapsed(attempt, told!);

            // FABRIC-PERFORM1d (§D3): what the attempt created is taken as receipts first, so a crash before its outcome is kept converges
            // on the replay, which takes none twice; a completion whose receipts could not be kept is not applied.
            var resources = each.Step.Outcome.Resources;
            var believed = node!;
            if (receipting.Receipted(unit, attempt, believed, resources, lapsed) is { } unkept)
            {
                done.Add(Counted(each, unkept));
                continue;
            }

            var applied = records.Edited(unit, believed.Id, record => record.Invoked(invocation) is not { } current
                ? (null, new WorkPerformed(unit, believed.Id, PerformedWhat.NotTaken, NotAttempt))
                : lapsed ? outcomes.Lapse(record, current, believed, each.Step.Outcome, each.Generation)
                : outcomes.Outcome(record, current, believed, each.Step.Outcome, each.Generation));
            receipting.Supersede(unit, attempt, resources, applied);
            done.Add(Counted(each, applied));
        }

        return done;
    }

    /// <summary>The unit whose record names an invocation, and the attempt as read: what an answer is judged on again under the lock.</summary>
    private (string Unit, NodeAttempt Attempt)? Find(string invocation)
    {
        foreach (var record in records.All().Records)
        {
            if (record.Invoked(invocation) is { } attempt) return (record.Unit, attempt);
        }

        return null;
    }
}
