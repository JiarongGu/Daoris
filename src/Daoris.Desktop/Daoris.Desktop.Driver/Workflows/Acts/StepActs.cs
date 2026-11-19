namespace Daoris.Driver;

/// <summary>
/// The acts a run's agent takes as its plugin's tools, journaled on the step it holds and settling it by their receipts. Each round of the run's
/// working session (<see cref="StepHandOffs.SessionOf"/>) that answered a receipt since the step waited (<see cref="StepActFiles"/>) is
/// journaled once on the agent-held step waiting on its holder whose receipts take its fact (<see cref="StepReceipts"/>), with the attempt and
/// epoch it is taken in; a round no such step takes is journaled nowhere. Once each fact the step maps has its newest receipt reached, the
/// step is settled done at the settle door as its holder, the receipts its evidence, and the next pass applies it.
/// </summary>
public static class StepActs
{
    /// <summary>The words a step its receipts settle is settled with, its evidence after them.</summary>
    public const string Settled = "by its receipts";

    public static async Task JournalAsync(
        NodeRecordFiles records, GovernedUnit governed, RunScope scope, Func<CancellationToken, Task<IReadOnlyList<SessionRecord>?>>? sessions, PlaceCastBook? casts,
        PlaceRiskBook? risk, Func<DateTimeOffset> clock, List<string> said, CancellationToken ct)
    {
        if (records.Read(governed.Unit.Id) is not { Ended: null } kept) return;
        var steps = HeldSteps.Of([governed], _ => kept, new StepTenureFiles(records.Home, records.HoldsLease), casts, risk)
            .Where(step => step.Holder == StepHolders.Agent && step.Why == PersonWaits.Task && step.Presses.Count == 0)
            .Select(step => (Step: step, Node: governed.Graph.Nodes.FirstOrDefault(node => node.Id == step.Node)))
            .Where(each => each.Node is not null && StepReceipts.Mapped(each.Node).Count > 0)
            .Select(each => (each.Step, Node: each.Node!, WorkPerformer.StatusOf(kept, each.Node!.Id).Attempt))
            .ToList();
        if (steps.Count == 0) return;
        var executors = (IReadOnlyList<string>)[.. scope.Quest?.Executors ?? [], .. scope.Branch?.Executors ?? []];
        var read = sessions is null ? null : await sessions(ct).ConfigureAwait(false);
        if (StepHandOffs.SessionOf(governed.Run, executors, read) is not { } session) return;

        var journal = StepActFiles.Journal(records.Home, governed.Unit.Id).ToList();
        var known = journal.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
        var added = 0;
        foreach (var round in PluginActRounds.Rounds(records.Home, session).Where(round => !known.Contains(round.Id)))
        {
            var taking = steps.FirstOrDefault(each => StepReceipts.Takes(each.Node, round.Fact) && (each.Step.Since is not { } since || round.At >= since));
            if (taking.Node is null) continue;
            journal.Add(round with { Node = taking.Node.Id, Attempt = taking.Attempt, Epoch = taking.Step.Epoch });
            said.Add($"act {round.Fact} ({round.State}) of step {taking.Node.Id} of #{governed.Run} journaled from session {session}");
            added++;
        }

        if (added > 0) StepActFiles.Keep(records.Home, governed.Unit.Id, journal);
        foreach (var (step, node, attempt) in steps)
        {
            if (kept.Pending.Any(word => word.Node == node.Id)) continue;
            var rows = journal.Where(row => row.Node == node.Id && row.Attempt == attempt).ToList();
            if (StepReceipts.Settled(node, rows) is not { } evidence) continue;
            var by = new SettleBy(step.HolderName is { } name ? CastHolders.AgentNamed(name) : CastHolders.Agent, step.Epoch);
            var worded = RunNodeWords.Word(records, governed.Run, node.Id, NodeWordActs.Done, TaskEvidence.Words(Settled, evidence), clock, epoch: step.Epoch, settle: by);
            said.Add(worded.Done
                ? $"step {node.Id} of #{governed.Run} settled by its receipts"
                : $"step {node.Id} of #{governed.Run} not settled by its receipts ({worded.Refused})");
            // The long loop: what an act came to on the way is proposed into the step's delivery knowledge, for the next run's hand-off.
            if (worded.Done && StepReceipts.Knowledge(node) is { } entry && StepLessons.Of(node, rows) is { } lesson
                && StepLessons.Propose(records.Home, new StepLesson(scope.Run.Binding.Repository, entry, governed.Run, node.Id, lesson, clock())))
            {
                said.Add($"what step {node.Id} of #{governed.Run} came to proposed into `{entry}`");
            }
        }
    }
}
