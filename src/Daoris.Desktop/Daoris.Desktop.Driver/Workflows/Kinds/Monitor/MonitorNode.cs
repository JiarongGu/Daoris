namespace Daoris.Driver;

/// <summary>
/// The watch on the step it follows (D245's note; D247 §4): a task like any other whose state logic is observed, reached with the state that
/// step reports as its outcome, so the graph routes on it with <c>when</c> wires. Its outcomes are its watched step's states, written in the file:
/// a pull request's <c>merged</c> and <c>closed</c>, the one step that reports a state today.
/// </summary>
[NodeKind(NodeKinds.Monitor, NodeGroup.Landing, "*/pull-request")]
internal sealed class MonitorNode : BuiltInNode, IObserves
{
    /// <summary>Reached by the watched step's state, and nothing fails it. Never skipped, since a skip would read as a state; the person's door is the stop.</summary>
    public override NodeAuthority Row { get; } = new(NodeKinds.Monitor, NodeReach.Observed, NodeSource.Watched, null, false, PersonSkips: false, true, true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Landing;

    /// <summary>The states of the step a monitor is first placed after: a pull request's.</summary>
    public override IReadOnlyList<string> Outcomes => PullRequestReading.States;

    /// <summary>A monitor is a decision: its declaration names the outcomes it routes on.</summary>
    public override string? Problem(NodeDeclaration node) => node.Outcomes.Count == 0 ? "outcomes" : null;

    /// <summary>
    /// It stands on one step that reports a state: exactly one node wired straight into it by <c>after</c>, whose kind is <see cref="IWatchable"/>,
    /// and its outcomes are that step's states in their order.
    /// </summary>
    public override IEnumerable<WireProblem> Problems(NodeGraph graph, NodeDeclaration node)
    {
        var follows = WireEvaluation.Members(node.Id, graph.Wires).Distinct(StringComparer.Ordinal).ToList();
        var said = follows.Count switch
        {
            0 => "no step is wired straight into it by `after`",
            > 1 => $"{follows.Count} steps are wired straight into it by `after`, and it watches one",
            _ => graph.Nodes.FirstOrDefault(each => each.Id == follows[0]) is { } watched && NodeKit.Of(watched.Kind) is IWatchable reports
                ? reports.States.SequenceEqual(node.Outcomes, StringComparer.Ordinal) ? null
                    : $"its outcomes are the states `{watched.Id}` reports, {string.Join(" and ", reports.States.Select(state => $"`{state}`"))}, in that order"
                : $"`{follows[0]}` reports no state to watch",
        };
        if (said is not null)
        {
            yield return new WireProblem(GraphRefusal.MonitorWatch, null, node.Id, $"node `{node.Id}`: a `{NodeKinds.Monitor}` watches the one step before it "
                + $"that reports a state, and {said}.");
        }
    }

    public override string? Evidence => NodeObserving.EvidenceSchema(NodeKinds.Monitor);

    public override string? Title => "The pull request's watch";

    public override NodeLook Look => NodeLook.Line;

    public override bool ReadsTheLanding => true;

    public NodeObservation Observe(NodeReading reading) => MonitorReading.Observe(reading);
}
