namespace Daoris.Driver;

/// <summary>A step of a run waiting for the person's Accept, Approve or Reject, and the landing acts a supervisor may make on it.</summary>
/// <param name="Attempt">The attempt the step waits on, which the decision's subject names.</param>
/// <param name="Acts">The landing acts the step offers, unqualified: <c>accept</c> of a review, <c>approve</c> and <c>reject</c> of an approval.</param>
public sealed record LandingStep(string Run, string Node, int Attempt, string Repository, IReadOnlyList<string> Acts)
{
    /// <summary>The run's nodes that hold it for the person alone, a go-ahead among them: no supervisor decides a step of a run one holds.</summary>
    public bool GoAheadOpen { get; init; }

    /// <summary>The epoch of the step's tenure the listing read: a decision keeps it, and its release names it, so a hand-over between refuses it.</summary>
    public int? Epoch { get; init; }

    public IReadOnlyList<string> Offered(IReadOnlyList<string> covered) => [.. covered.Where(Acts.Contains)];
}

/// <summary>The steps of the home's runs a landing supervisor may decide, read from the runs' node records as the pass reads them.</summary>
public static class LandingSteps
{
    /// <summary>
    /// Every step of every run bound to a graph version that waits for the person's press a landing supervisor makes, and whose holder the
    /// supervisor's word stands in for (<see cref="SettleDoor.Admit"/> of the line: a step the person holds, or not cast yet), with its epoch.
    /// </summary>
    public static IReadOnlyList<LandingStep> Waiting(string home)
    {
        var records = new NodeRecordFiles(home, () => false);
        var steps = new List<LandingStep>();
        foreach (var run in new GraphRuns(records).Read())
        {
            if (run.Ended is not null || run.Binding.Problem is not null || run.Version.Graph is not { } graph) continue;
            if (records.Read(run.Unit) is not { } record) continue;
            var open = graph.Nodes.Any(node => NodeKit.Of(node.Kind)?.HoldsRun == true && WorkPerformer.StatusOf(record, node.Id).State == NodeStates.Active);
            var holding = StepTenureFiles.HoldingOf(home, run.Run, record);
            foreach (var node in graph.Nodes)
            {
                var status = WorkPerformer.StatusOf(record, node.Id);
                if (status.State != NodeStates.Active || NodeKit.Of(node.Kind)?.LandingActs(node) is not { Count: > 0 } acts) continue;
                var tenure = holding.GetValueOrDefault(node.Id);
                if (SettleDoor.Admit(tenure, new SettleBy(SettleBy.Line, tenure?.Epoch)) is not null) continue;
                steps.Add(new LandingStep(run.Run, node.Id, status.Attempt, run.Binding.Repository, acts) { GoAheadOpen = open, Epoch = tenure?.Epoch });
            }
        }

        return steps;
    }

    /// <summary>The step of <paramref name="run"/> named by the decision's subject, if it still waits on that attempt.</summary>
    public static LandingStep? Find(string home, string run, string node, int attempt) =>
        Waiting(home).FirstOrDefault(step => step.Run == run && step.Node == node && step.Attempt == attempt);
}
