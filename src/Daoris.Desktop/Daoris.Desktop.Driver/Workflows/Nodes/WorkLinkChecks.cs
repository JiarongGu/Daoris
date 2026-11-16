using static Daoris.Driver.WorkPerformer;

namespace Daoris.Driver;

/// <summary>
/// A holder's <i>Done outside Daoris</i> link on a plugin's step, handed to its plugin's check before the word is applied: asked at
/// <c>work/reconcile</c> with the link and the step's options, the check reads the thing the link names and settles or refuses it. A refusal
/// takes the word back with the check's code and words; a settled link stays the holder's word, credited to the identity the check read
/// (<see cref="NodeAttempt.By"/>); an answer naming no link (a kit that reads none) leaves the word its holder's, applied as before.
/// </summary>
internal sealed class WorkLinkChecks(NodeRecordFiles records, IWorkPerformPort port, Func<DateTimeOffset> clock)
{
    /// <summary>Whether <paramref name="attempt"/>'s link still waits on its plugin's check: unchecked, in flight or its failure kept, a reconciling plugin's step.</summary>
    public static bool Asks(NodeRecord record, NodeAttempt attempt, NodeDeclaration node) =>
        attempt is { Resolution: HeldResolutions.Happened, Link: not null, LinkChecked: null } && (StandDowns.InFlight(attempt) || KeptFailure(record, attempt))
        && attempt.Plugin != QuestClosing.Sender && attempt.Plugin != RequestStart.Sender && !OnPort(attempt) && node.Goal is null && node.Perform is { Reconcile: true };

    /// <summary>Every link waiting on its plugin's check, asked within the pass's calls; one whose plugin does not answer is asked at the next pass.</summary>
    public async Task<IReadOnlyList<WorkPerformed>> CheckAsync(Func<string, string, NodeDeclaration?> declared, WorkSends sends, CancellationToken ct)
    {
        var done = new List<WorkPerformed>();
        foreach (var record in records.All().Records)
        {
            foreach (var attempt in record.Attempts.Where(each => declared(record.Unit, each.Node) is { } node && Asks(record, each, node)).ToList())
            {
                if (port.GenerationOf(attempt.Plugin) is null || !sends.Take()) continue;
                var node = declared(record.Unit, attempt.Node)!;
                var call = new WorkReconcileCall(attempt.Call.Unit, attempt.Call.Step, attempt.Call.Invocation, attempt.Call.IdempotencyKey, attempt.Call.SubjectRevision)
                {
                    Link = attempt.Link, Options = node.Options,
                };
                WorkReconcileAnswer answer;
                try
                {
                    (answer, _) = await port.ReconcileStepAsync(attempt.Plugin, call, ct).ConfigureAwait(false);
                }
                catch (Exception error) when (Unanswered(error))
                {
                    continue;
                }

                if (answer.Link is null)
                {
                    records.Edited(record.Unit, node.Id, current => Checked(current, attempt));
                    continue;
                }

                if (answer.Link != attempt.Link || answer.Outcome is not { } outcome) continue;
                done.Add(records.Edited(record.Unit, node.Id, current => Judged(current, attempt, answer.State, outcome)));
            }
        }

        return done;
    }

    /// <summary>The word marked checked by an answer naming no link, a kit that reads none, where the record still holds it as asked.</summary>
    private (NodeRecord? Next, WorkPerformed Result) Checked(NodeRecord record, NodeAttempt asked) =>
        record.Invoked(asked.Call.Invocation) is { } current && current.Link == asked.Link && current.Resolution == HeldResolutions.Happened
            ? (With(record, current, current with { LinkChecked = clock() }), new(record.Unit, asked.Node, PerformedWhat.Worded, HeldResolutions.DoneOutside))
            : (null, new(record.Unit, asked.Node, PerformedWhat.NotTaken, Moved));

    /// <summary>The check's answer on the word as the record holds it now: one changed since (applied, taken back, another link) is not judged.</summary>
    private (NodeRecord? Next, WorkPerformed Result) Judged(NodeRecord record, NodeAttempt asked, string state, WorkOutcome outcome)
    {
        var unit = record.Unit;
        if (record.Invoked(asked.Call.Invocation) is not { } current || current.Link != asked.Link || current.Resolution != HeldResolutions.Happened)
        {
            return (null, new(unit, asked.Node, PerformedWhat.NotTaken, Moved));
        }

        if (state == ReconcileStates.Done && outcome.State == WorkStates.Reached)
        {
            return (With(record, current, current with { By = outcome.Evidence?.By, LinkChecked = clock() }), new(unit, asked.Node, PerformedWhat.Worded, HeldResolutions.DoneOutside));
        }

        var code = outcome.Code ?? NoCode;
        var words = outcome.Evidence?.Message ?? code;
        var back = current with
        {
            Resolution = null, ResolvedAt = null, Link = null, Note = null, Decision = null,
            Code = current.State == AttemptStates.Held ? code : current.Code,
            Progress = StandDowns.InFlight(current) ? new StepProgress(null, words, outcome.Evidence?.Url, null, clock()) : current.Progress,
        };
        return (With(record, current, back), new(unit, asked.Node, PerformedWhat.Refused, code) { Detail = words });
    }
}
