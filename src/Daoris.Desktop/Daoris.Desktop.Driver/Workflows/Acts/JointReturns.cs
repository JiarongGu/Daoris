namespace Daoris.Driver;

/// <summary>
/// A joint check's failure sent back to the part it is on, through the request door, and that part's fix taken (<see cref="JointChecks"/>).
/// Once per failed test, the request is said to the part's working session as the tester's (the session awaiting the part's run, else the
/// one that gave the receipt held over), and a row journaled on the part's step under <see cref="JointChecks.Returned"/>, naming the session
/// asked. That session's rounds of the fact after it are journaled on the same step as its fix, whether the part's run still stands or
/// ended, and a reached fix is proposed into the step's delivery knowledge (the long loop). The joint check's pass is the one writer of these
/// rows on the part's journal; the part's own pass writes the rest.
/// </summary>
public static class JointReturns
{
    public static async Task KeepAsync(
        NodeRecordFiles records, GovernedUnit governed, RunScope scope, Func<CancellationToken, Task<IReadOnlyList<SessionRecord>?>>? sessions,
        Func<string, string, string, CancellationToken, Task<SayAnswer>>? say, Func<DateTimeOffset> clock, List<string> said, CancellationToken ct)
    {
        if (scope.Parts is not { } report || records.Read(governed.Unit.Id) is not { Ended: null } kept) return;
        foreach (var node in governed.Graph.Nodes.Where(each => JointChecks.Over(each).Count > 0))
        {
            var status = WorkPerformer.StatusOf(kept, node.Id);
            if (status.State != NodeStates.Active) continue;
            foreach (var (part, fact) in JointChecks.Over(node))
            {
                if (PartReceipts.RunOf(report, part) is not { } run) continue;
                var journal = StepActFiles.Journal(records.Home, RunUnits.IdOf(run)).ToList();
                if (Fix(records, run, fact, journal, clock, said)) continue;
                var own = StepActFiles.Journal(records.Home, governed.Unit.Id).Where(row => row.Node == node.Id && row.Attempt == status.Attempt);
                if (say is null || JointChecks.Failed(node, own) is not { } failed || failed.Part != part) continue;
                if (journal.Any(row => row.Id == JointChecks.Returned + failed.Id) || journal.LastOrDefault(row => row.Fact == fact) is not { Node: { } step } given) continue;

                var read = sessions is null ? null : await sessions(ct).ConfigureAwait(false);
                var session = (read is null ? null : RunWaiters.Of(run, read).LastOrDefault()) ?? given.Session;
                var words = $"The joint check `{node.Id}` of run #{governed.Run}, held by `{node.Role}`, failed on your part `{part}` of ask #{report.Ask}: "
                            + $"{failed.Message ?? failed.Fact}. Its holder asks you to fix it under your delivery knowledge and take your act again: its "
                            + $"receipt `{fact}` is journaled on step `{step}` of run #{run}, and the check is taken again on it. Where you cannot, ask the "
                            + "role that can through `question_ask`, and end your turn.";
                if (!(await say(session, words, $"joint-return:{governed.Run}:{node.Id}:{failed.Id}", ct).ConfigureAwait(false)).Kept) continue;
                journal.Add(failed with { Id = JointChecks.Returned + failed.Id, Session = session, Node = step, Attempt = given.Attempt, Epoch = given.Epoch });
                StepActFiles.Keep(records.Home, RunUnits.IdOf(run), journal);
                said.Add($"joint check {node.Id} of #{governed.Run} failed on part {part}: sent back to step {step} of #{run} (session {session})");
            }
        }
    }

    /// <summary>
    /// The rounds of the part's asked session journaled on the step a failure went back to, while it is open: whether one is open (the part
    /// then has nothing more to be asked until its fix reaches).
    /// </summary>
    private static bool Fix(NodeRecordFiles records, string run, string fact, List<StepActRow> journal, Func<DateTimeOffset> clock, List<string> said)
    {
        if (journal.LastOrDefault(row => row.Id.StartsWith(JointChecks.Returned, StringComparison.Ordinal)) is not { Node: { } step }
            || JointChecks.Open(journal.Where(row => row.Node == step)) is not { } open)
        {
            return false;
        }

        var known = journal.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
        var fixes = PluginActRounds.Rounds(records.Home, open.Session).Where(round => round.Fact == fact && round.At >= open.At && !known.Contains(round.Id)).ToList();
        if (fixes.Count == 0) return true;
        journal.AddRange(fixes.Select(round => round with { Node = step, Attempt = open.Attempt, Epoch = open.Epoch }));
        StepActFiles.Keep(records.Home, RunUnits.IdOf(run), journal);
        said.Add($"fix {fact} of step {step} of #{run} journaled after its joint check failed");
        if (fixes.LastOrDefault(round => round.State == StepReceipts.Reached) is not { } reached) return true;

        if (new GraphRuns(records).Find(run) is { } part && part.Version.Graph?.Nodes.FirstOrDefault(node => node.Id == step) is { } node
            && StepReceipts.Knowledge(node) is { } entry
            && StepLessons.Propose(records.Home, new StepLesson(part.Binding.Repository, entry, run, step, JointChecks.Lesson(open, reached), clock())))
        {
            said.Add($"what step {step} of #{run} came to after its joint check proposed into `{entry}`");
        }

        return false;
    }
}
