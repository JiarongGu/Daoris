namespace Daoris.Driver;

/// <summary>A working session's settle of a step it holds, as its tool asked it: the run, the step, the outcome, its words and evidence.</summary>
/// <param name="Outcome">A decision's answer, one of its outcomes; null for a task's Done.</param>
/// <param name="Evidence">The evidence by name (<see cref="TaskEvidence"/>), as the tool gets it.</param>
public sealed record StepSettleAsk(string Run, string Node, string? Outcome, string? Words, IReadOnlyDictionary<string, string> Evidence);

/// <summary>
/// The working agent's channel for a step it holds: its session's one tool (<see cref="Tool"/>) settles a <c>task</c>
/// step waiting on its holder's word (<see cref="AgentSettles.Tool"/>), its Done or a decision's answer, through the run's node door at the
/// settle door as the agent its channel names, never the person. A decision its agent answers is settled by that answer, no proposal held for a
/// confirmation (<see cref="NodeWord.Decider"/>); at a destination the owner marked the step is the owner role's, and the door refuses the
/// agent. Refused, and nothing kept, where the step does not wait on a task its holder settles, where its outcome is not one it declares,
/// where an evidence it names is missing (<see cref="MissingEvidence"/>), and by the door where the agent does not hold it or its epoch moved.
/// </summary>
public static class StepSettles
{
    /// <summary>The working session's tool, as the connector names it.</summary>
    public const string Tool = "step_settle";

    /// <summary>The step names evidence its settle left out; the detail names each.</summary>
    public const string MissingEvidence = "missing-evidence";

    private const string Accepted = "accepted";

    /// <summary>Keeps the settle <paramref name="ask"/> on the run's record for the next pass, as <paramref name="by"/> speaks, or refuses it.</summary>
    public static NodeWorded Settle(NodeRecordFiles records, StepSettleAsk ask, SettleBy by, Func<DateTimeOffset> clock, IReadOnlyCollection<string>? makers = null)
    {
        var run = ask.Run.Trim().TrimStart('#');
        var node = ask.Node.Trim();
        if (new GraphRuns(records).Find(run) is not { Version.Graph: { } graph } bound) return new(run, node, NodeWordActs.Done, RunNodeWords.NotRun);
        if (graph.Nodes.FirstOrDefault(each => each.Id == node) is not { } declared) return new(run, node, NodeWordActs.Done, RunNodeWords.NotNode);
        var decision = TaskDecisions.Is(declared);
        var act = decision ? NodeWordActs.Decide : NodeWordActs.Done;
        var status = records.Read(bound.Unit) is { } kept ? WorkPerformer.StatusOf(kept, node) : null;
        if (status is null || !AgentSettles.Tool(declared, status)) return new(run, node, act, RunNodeWords.NotWaiting) { State = status?.State };
        if (StepReceipts.Mapped(declared) is { Count: > 0 } receipts)
        {
            return new(run, node, act, StepReceipts.ByReceipt) { Detail = string.Join(", ", receipts.Values.Distinct(StringComparer.Ordinal)), State = status.State };
        }

        var outcome = string.IsNullOrWhiteSpace(ask.Outcome) ? null : ask.Outcome.Trim();
        if (declared.Kind == NodeKinds.Review || TaskNode.Presses(declared).Contains(NodeWordActs.Approve, StringComparer.Ordinal))
        {
            var review = declared.Kind == NodeKinds.Review;
            var answer = outcome switch
            {
                Accepted when review => NodeWordActs.Accept,
                "approved" when !review => NodeWordActs.Approve,
                "changes" when review => NodeWordActs.Changes,
                "rejected" when !review => NodeWordActs.Reject,
                _ => null,
            };
            if (answer is null) return new(run, node, act, RunNodeWords.NotOutcome) { Detail = review ? "accepted, changes" : "approved, rejected" };
            return RunNodeWords.Word(records, run, node, answer, TaskEvidence.Words(ask.Words, ask.Evidence), clock, epoch: by.Epoch, settle: by, workers: makers);
        }

        if (decision ? outcome is null || !declared.Outcomes.Contains(outcome, StringComparer.Ordinal) : outcome is not null && outcome != "done")
        {
            return new(run, node, act, RunNodeWords.NotOutcome) { Detail = string.Join(", ", declared.Outcomes) };
        }

        if (TaskEvidence.Missing(declared, ask.Evidence) is { Count: > 0 } missing)
        {
            return new(run, node, act, MissingEvidence) { Detail = string.Join(", ", missing), State = status.State };
        }

        return RunNodeWords.Word(records, run, node, act, TaskEvidence.Words(ask.Words, ask.Evidence), clock, outcome: decision ? outcome : null, epoch: by.Epoch, settle: by,
            workers: makers);
    }

    /// <summary>What the settle came to, as its session is told it: kept, or refused and why.</summary>
    public static string Said(NodeWorded done) => (done.Refused, done.Act) switch
    {
        (null, _) => $"Your settle of `{done.Node}` on run #{done.Run} is kept; Daoris's next look applies it and hands you the next step it holds for you.",
        (StepReceipts.ByReceipt, _) =>
            $"Your settle of `{done.Node}` on run #{done.Run} is refused: the receipts of your acts settle it ({done.Detail}), journaled as you take them. Nothing was kept.",
        (MissingEvidence, _) => $"Your settle of `{done.Node}` on run #{done.Run} is refused: it names its evidence, and leaves out {done.Detail}. Nothing was kept.",
        (RunNodeWords.NotOutcome, _) => $"Your settle of `{done.Node}` on run #{done.Run} is refused: answer with one of its outcomes ({done.Detail}). Nothing was kept.",
        (RunNodeWords.NotWaiting, _) =>
            $"Your settle of `{done.Node}` on run #{done.Run} is refused: it does not wait on a task its holder settles now ({done.State ?? "not under way"}). Nothing was kept.",
        (SettleDoor.OwnWork, _) when done.Detail?.StartsWith(SettleDoor.NotYet, StringComparison.Ordinal) == true =>
            $"Your settle of `{done.Node}` on run #{done.Run} is not taken: {done.Detail}. Nothing was kept, and it stays open.",
        _ => $"Your settle of `{done.Node}` on run #{done.Run} is refused ({done.Refused}{(done.Detail is { } detail ? $": {detail}" : "")}). Nothing was kept.",
    };
}
