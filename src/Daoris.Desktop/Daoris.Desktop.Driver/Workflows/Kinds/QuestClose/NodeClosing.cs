namespace Daoris.Driver;

/// <summary>
/// The runtime's half of a graph closing a quest: the <c>quest.close</c> an evaluation proposes to start, the fence that finds a bound unit
/// moved, what a closure graph's unit owes before it is evaluated, and the pruning of the closure graphs nothing needs.
/// </summary>
/// <param name="quests">Every quest the last look read, by id.</param>
/// <param name="governed">The governed units the last look read, by id; null before the first read, or once a fence dropped them.</param>
/// <param name="fence">Drops what the pass governs, so the next pass reads the units afresh.</param>
internal sealed class NodeClosing(
    NodeRecordFiles records, Func<DateTimeOffset> clock, QuestCloser? closer, Func<Dictionary<string, QuestView>> quests,
    Func<Dictionary<string, GovernedUnit>?> governed, Action fence)
{
    /// <summary>How long a graph kept stays before the pass may drop it unbound: the take binds it within seconds of its keeping.</summary>
    private static readonly TimeSpan KeptGrace = TimeSpan.FromMinutes(10);

    /// <summary>
    /// A <c>quest.close</c> the evaluation proposes to start: on a quest bound to this graph alone, sent once its chain's submissions are
    /// current and unheld and each other quest it names has reached its close in its own graph, taking one of the pass's
    /// <paramref name="sends"/>; reached without a send where its chain's close already closed its quest; left ready while it waits. Null
    /// where nothing is done.
    /// </summary>
    public async Task<WorkPerformed?> CloseAsync(
        GovernedUnit unitGoverned, NodeDeclaration node, WireStart start, NodeStatus evaluated, WorkSends sends, List<string> said,
        Func<CancellationToken, Task<NodeUnitsRead>> units, CancellationToken ct)
    {
        if (closer is null || !unitGoverned.Closes || unitGoverned.Unit.Records.Quest is not { } quest) return null;
        var judged = QuestClosing.Judge(quest, quests());
        if (judged.Overridden)
        {
            // Done by its chain's close: reached on that close, never sent. Done otherwise is the person's, which Settle reads. Only a close
            // naming the quest at the submission it stands on now is that close; one kept for an earlier submission is not.
            return QuestClosing.Naming(records.All().Records, quest).FirstOrDefault(Closed) is { } chained
                ? closer.ReachByChain(node, start, evaluated, chained)
                : null;
        }

        if (judged.Waits is not null)
        {
            // A close that waits on its own judgement says why, once a pass however many rounds propose it; none once it closes.
            var waiting = $"work  {unitGoverned.Unit.Id}: its close waits: {judged.Waits}.";
            if (!said.Contains(waiting, StringComparer.Ordinal)) said.Add(waiting);
            return null;
        }

        // The evaluation that proposed this start fenced this quest's own prerequisites; each other quest the close names is fenced in its own
        // graph, on its own record, before anything is sent or read again. Said once a pass, however many rounds propose it.
        if (Unreached(unitGoverned, node, judged.Named) is { } unreached)
        {
            var line = $"work  {unitGoverned.Unit.Id}: its close waits for {unreached}.";
            if (!said.Contains(line, StringComparer.Ordinal)) said.Add(line);
            return null;
        }

        if (!sends.Take()) return null;

        // Git (each branch's tip, a fresh read of its refs), the binding and the chain are read again right before the close. The same
        // revision means every prerequisite acceptance bound to it still stands, since this loop is the record's one writer; another means a
        // commit, a submission or a hold moved since the evaluation, and the close waits for the next pass to judge it on what is there now.
        // The tips are this read's own snapshot, and one git did not answer leaves the close waiting.
        var again = await units(ct).ConfigureAwait(false);
        var read = NodeGoverning.Quests(again);
        var stale = Moved(unitGoverned, quest, again, read)
            ?? (!QuestClosing.Judge(read[quest.Id], read).Named.SequenceEqual(judged.Named) ? "what it would close moved" : null);
        if (stale is not null)
        {
            fence();
            var line = $"work  {unitGoverned.Unit.Id}: its close was not sent: {stale}, so it is judged again on what is there now.";
            if (!said.Contains(line, StringComparer.Ordinal)) said.Add(line);
            return null;
        }

        var sent = await closer.StartAsync(node, start, evaluated, judged.Named, ct).ConfigureAwait(false);
        said.AddRange(closer.TakeSaid());
        return sent;
    }

    /// <summary>
    /// Why a bound quest's unit, governed on the revision of the read before, does not stand on it in <paramref name="again"/>, the units
    /// read afresh: its quest, its binding, git's tips, or the chain's and its branches' revision. Null where it stands, so a close or a
    /// plugin's start made now rests on what is there.
    /// </summary>
    public static string? Moved(GovernedUnit governed, QuestView quest, NodeUnitsRead again, Dictionary<string, QuestView> quests) =>
        !quests.TryGetValue(quest.Id, out var now) ? "its quest was not read again"
        : !string.Equals(now.Closure, governed.GraphDigest, StringComparison.Ordinal) ? "its binding moved"
        // A read that asked no git never closes, so a production read that forgets the tips cannot bring the stale-tip defect back.
        : again.Tips is null ? "git's tips were not read for this look"
        : QuestClosing.Unread(now, quests, again.Units, again.Tips) is { } unread ? $"git did not say where `{unread}`, a branch it worked, stands"
        : QuestClosing.Revision(now, quests, again.Units, again.Tips) != governed.Unit.Subject.Revision ? "its chain or a branch it worked moved since it was judged"
        : null;

    /// <summary>
    /// The first quest a chain's close names, besides its own, whose own graph has not reached its close, said as the quest and the node it
    /// waits on; null where every one has. Each is judged on its own node record, under the graph it is bound to, at its current binding (its
    /// governed revision this pass), as its own evaluation judges it: the close is eligible there, and no prerequisite wired into it moves in
    /// that judgement, so what the close rests on is what its record holds. A record that does not read, a quest not governed by its graph
    /// here, or a graph not judged leaves the close waiting, never judged without it.
    /// </summary>
    /// <remarks>
    /// One record read for each other quest named, only for a close about to be sent; no git, since the evaluation is handed no git reads. Its
    /// revision is current at the send: each is computed from the same read as this quest's, and every input of it is an input of this
    /// quest's revision, which the fence reads again.
    /// </remarks>
    private string? Unreached(GovernedUnit unitGoverned, NodeDeclaration close, IReadOnlyList<QuestCloseItem> named)
    {
        foreach (var each in named)
        {
            var unit = QuestUnits.IdOf(each.Id);
            if (string.Equals(unit, unitGoverned.Unit.Id, StringComparison.Ordinal)) continue;
            string Waits(string why) => $"#{each.Id}, which it would close too: {why}";
            if (governed()?.GetValueOrDefault(unit) is not { Closes: true } own || !own.Declared.ContainsKey(close.Id))
            {
                return Waits($"it is not governed here by the graph it is bound to, so its `{close.Id}` is not judged");
            }

            if (records.Read(unit) is not { } record) return Waits($"its node record does not read, so its `{close.Id}` is not judged");
            var (_, evaluated) = NodeEvaluations.Evaluate(own, record);
            if (evaluated.Refusal is { } refusal) return Waits($"its graph was not judged ({refusal}), so its `{close.Id}` is not judged");
            var into = own.Graph.Wires.Where(wire => wire.To is { Unit: null } to && to.Node == close.Id && wire.From is { Unit: null }).ToList();
            if (evaluated.Of(close.Id) is not { Eligible: true })
            {
                var waiting = evaluated.Of(close.Id)?.Waiting ?? [];
                var source = into.FirstOrDefault(wire => waiting.Contains(wire.Id, StringComparer.Ordinal))?.From!.Node;
                return Waits(source is null
                    ? $"its `{close.Id}` is not reached in its own graph yet"
                    : $"its `{source}`, before its `{close.Id}` in its own graph, {Standing(record, evaluated.Statuses[source])}");
            }

            var moved = evaluated.Moves.Select(move => move.Node).ToHashSet(StringComparer.Ordinal);
            if (into.Select(wire => wire.From!.Node).FirstOrDefault(moved.Contains) is { } unkept)
            {
                return Waits($"its `{unkept}`, before its `{close.Id}` in its own graph, has moved and its record does not hold that yet");
            }
        }

        return null;

        // Where a prerequisite stands, said as the person would ask it: held, unknown, failed, or not reached yet.
        static string Standing(NodeRecord record, NodeStatus status) =>
            record.Attempts.Any(attempt => attempt.Node == status.Node && attempt.Attempt == status.Attempt && attempt.State == AttemptStates.Held)
                ? "is held for the person"
                : status.State switch
                {
                    NodeStates.Unknown => "is unknown",
                    NodeStates.Failed => "failed",
                    NodeStates.Rejected => "was rejected",
                    NodeStates.Stopped => "was stopped",
                    NodeStates.Invalidated => "was reached on work that has moved since",
                    _ => "is not reached yet",
                };
    }

    /// <summary>
    /// Drops the closure graphs nothing needs: one a quest bound to it that is not Done reads needs, and one whose governed unit still has a
    /// close attempt unsettled or a live close node, or a record that does not read, so the runtime disarms once the last bound quest is Done
    /// and settled. Called after the pass's moves are kept.
    /// </summary>
    public void Prune()
    {
        if (closer is null || governed() is not { } units || !ClosureGraphs.Any(records.Home)) return;
        var (all, unreadable) = records.All();
        if (unreadable.Count > 0) return;
        var needed = quests().Values.Where(quest => quest.Closure is { Length: > 0 } && quest.Status != "Done").Select(quest => quest.Closure!)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var each in units.Values.Where(each => each.Closes))
        {
            if (all.FirstOrDefault(record => record.Unit == each.Unit.Id) is not { } record) continue;
            var closes = each.Graph.Nodes.Where(node => node.Kind == NodeKinds.QuestClose).Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
            if (record.Attempts.Any(attempt => attempt.State != AttemptStates.Settled)
                || record.Statuses.Any(status => closes.Contains(status.Key) && NodeStates.Live.Contains(status.Value.State)))
            {
                needed.Add(each.GraphDigest);
            }
        }

        ClosureGraphs.Prune(records.Home, needed.Contains, clock(), KeptGrace);
    }

    /// <summary>A close attempt that closed what it named.</summary>
    private static bool Closed(NodeAttempt attempt) => attempt is { State: AttemptStates.Settled, Outcome: WorkStates.Reached };

    /// <summary>
    /// What a closure graph's unit owes before it is evaluated: a close found closed only in part, said once, naming each quest not
    /// done on the submission it closed; and the person's own done, which ends the graph as overridden, stopping every live node, said once. A
    /// close still waiting for its answer is reconciled first, and a chain's close is never the person's.
    /// </summary>
    public void Settle(GovernedUnit unitGoverned, List<WorkPerformed> done, List<string> said)
    {
        if (unitGoverned.Unit.Records.Quest is not { } quest) return;
        var unit = unitGoverned.Unit.Id;
        var now = clock();
        var all = records.All().Records;
        if (all.FirstOrDefault(record => record.Unit == unit) is { } own)
        {
            foreach (var attempt in own.Attempts.Where(attempt => attempt.Plugin == QuestClosing.Sender && Closed(attempt)))
            {
                foreach (var lost in attempt.Close!.Quests.Where(named => Lost(named) && own.History.All(entry => entry.Why != $"{QuestClosing.Partial} #{named.Id}")))
                {
                    var (refused, _) = records.Edit(unit, record =>
                        (record with { History = [.. record.History, new NodeHistory(now, $"{QuestClosing.Partial} #{lost.Id}", WorkPerformer.StatusOf(record, attempt.Node))] }, true));
                    if (refused is not null) continue;
                    said.Add($"work  {unit}: partially closed: #{lost.Id} is no longer done on the submission its close judged, since its take was lost "
                             + "to another machine, which holds its work now. The others stay done, and Daoris never closes it over that take; stop the "
                             + $"close node `{attempt.Node}` to leave it.");
                }
            }
        }

        if (quest.Status != "Done" || !QuestClosing.Judge(quest, quests()).Overridden) return;
        // A close of the submission the quest stands on now that closed it, still waits for its answer, or is held with no refusal kept (its
        // door may have closed it) is the door's, never the person's; one kept for an earlier submission speaks for none.
        var naming = QuestClosing.Naming(all, quest).ToList();
        if (naming.Any(attempt => Closed(attempt) || attempt.State is AttemptStates.Recorded or AttemptStates.Unknown or AttemptStates.Sent
                                  || attempt is { State: AttemptStates.Held, Outcome: not WorkStates.Failed }))
        {
            return;
        }

        // The person closed every quest the close would close. Its pending nodes are stopped, an attempt in flight still reconciled.
        var (_, stopped) = records.Edit<List<string>>(unit, record =>
        {
            var names = new List<string>();
            var next = record;
            foreach (var node in unitGoverned.Graph.Nodes)
            {
                var status = WorkPerformer.StatusOf(next, node.Id);
                if (!NodeStates.Live.Contains(status.State)) continue;
                var moved = NodeLifecycle.Apply(unitGoverned.Declared[node.Id], status, new NodeMove(NodeStates.Stopped, NodeCause.Person) { Words = "closed by you before its end" });
                if (!moved.Moved) continue;
                next = WorkPerformer.Kept(next, moved, QuestClosing.Overridden, now);
                // The milestone reads the override from its entries, so a stop that cleared nothing keeps one too.
                if (moved.Cleared is null) next = next with { History = [.. next.History, new NodeHistory(now, QuestClosing.Overridden, status)] };
                names.Add(node.Id);
            }

            return (names.Count == 0 ? null : next, names);
        });
        if (stopped is not { Count: > 0 }) return;
        done.AddRange(stopped.Select(node => new WorkPerformed(unit, node, PerformedWhat.Applied, NodeStates.Stopped)));
        said.Add($"work  {unit}: #{quest.Id} closed by you before its end, so its graph ends as overridden; stopped: {string.Join(", ", stopped)}.");

        // A quest the close named reads lost: not done, and not on the submission the close judged (a rebase forgot it with its take).
        bool Lost(QuestCloseItem named) =>
            quests().TryGetValue(named.Id, out var standing) && standing.Status != "Done"
            && !(standing.Submission is { } current && current.Machine == named.Machine && current.Sequence == named.Sequence);
    }
}
