namespace Daoris.Driver;

/// <summary>
/// A person's task: a manual operation the run waits on until the person presses <i>Done</i>, or an
/// approval they <i>Approve</i> or <i>Reject</i>, as a release pipeline's manual action. Its outcome is written in the file, so a flowchart
/// routes on it with a <c>when</c> wire: <c>done</c> for a task, <c>approved</c> for an approval, whose refusal is the graph's <c>rejected</c>
/// wire as a review's changes are; one declaring none is a task reached with no outcome. One declaring two or more of its own is a per-run
/// decision, answered by <i>decide</i> (<see cref="HumanDecisions"/>).
/// </summary>
[NodeKind(NodeKinds.Human, NodeGroup.You, NodeKinds.GoAhead)]
internal sealed class HumanNode : BuiltInNode, IObserves, IPressed
{
    /// <summary>A task's outcome.</summary>
    public static readonly IReadOnlyList<string> Task = [Done];

    /// <summary>An approval's outcome: its refusal is a state, not an outcome.</summary>
    public static readonly IReadOnlyList<string> Approval = [Approved];

    private const string Done = "done", Approved = "approved";

    public override NodeAuthority Row { get; } = new(NodeKinds.Human, NodeReach.Pressed, NodeSource.Person, null, true, true, true, false) { Reanswers = true };

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Run;

    /// <summary>Its outcomes are a task's, an approval's or a decision's own; one declaring none is a task reached with no outcome.</summary>
    public override bool TakesOutcomes(IReadOnlyList<string> outcomes) =>
        outcomes.SequenceEqual(Task) || outcomes.SequenceEqual(Approval) || HumanDecisions.Takes(outcomes);

    /// <summary>A decision routes its answer: one no <c>when</c> wire leaves on any of its outcomes decides nothing, and is refused.</summary>
    public override IEnumerable<WireProblem> Problems(NodeGraph graph, NodeDeclaration node)
    {
        if (!HumanDecisions.Is(node)) yield break;
        if (graph.Wires.Any(wire => wire.Kind == WireKinds.When && wire.From is { Unit: null } from && from.Node == node.Id
            && node.Outcomes.Contains(wire.Outcome ?? "", StringComparer.Ordinal))) yield break;
        yield return new WireProblem(GraphRefusal.DecisionUnwired, null, node.Id,
            $"node `{node.Id}`: a decision routes its answer, and no `{WireKinds.When}` wire leaves it on any of its outcomes.");
    }

    public override NodePart Part(NodeDeclaration node) => new(NodeKinds.Human, WorkflowParticipation.You, WorkflowExecutor.Daoris);

    /// <summary>A step added from the palette is a task, reached with <c>done</c>; the editor turns it into an approval by declaring <c>approved</c>.</summary>
    public override IReadOnlyList<string> Outcomes => Task;

    public override string? Title => "Your task";

    /// <summary>A task with no means of its own is the owner's where it names no role (its role named on the step is the one way after HOLDDOOR1).</summary>
    public override string Role => StepRoles.Owner;

    /// <summary>A landing supervisor's Approve and Reject of an approval; a task a person does by hand is theirs alone.</summary>
    public override IReadOnlyList<string> LandingActs(NodeDeclaration node) =>
        node.Outcomes.SequenceEqual(Approval) ? [NodeWordActs.Approve, NodeWordActs.Reject] : [];

    public override NodeLook Look => NodeLook.You;

    /// <summary>Begun on a run alone, as a go-ahead is: the run then waits on the person. On a unit it waits ready.</summary>
    public bool Begins(GovernedUnit governed) => governed.Scope is not null;

    /// <summary>Nothing but the person's press reaches it, applied from the run's record by the pass: no record says more.</summary>
    public NodeObservation Observe(NodeReading reading) =>
        new(reading.Unit.Id, reading.Node.Id, reading.Node.Kind, null, "it waits for your press") { Attempt = reading.Binding.Attempt };

    public IReadOnlyList<string> Takes { get; } = [NodeWordActs.Done, NodeWordActs.Approve, NodeWordActs.Reject, NodeWordActs.Decide];

    public string Refusal => RunNodeWords.NotTask;

    /// <summary>A waiting task offers its Done; a waiting approval its Approve and Reject; a decision its decide, waiting or answered, until its run ends.</summary>
    public IReadOnlyList<string> Acts(NodeDeclaration node, NodeStatus status) =>
        status.State == NodeStates.Active || (status.State == NodeStates.Reached && HumanDecisions.Is(node)) ? Presses(node) : [];

    /// <summary>
    /// Done and Approve reach it on the person's word at the binding it waits on, with the outcome the press names and their note; Reject
    /// rejects it on that attempt with their note, as a review's changes do, and the graph's <c>rejected</c> wire routes it. A decide answers a
    /// decision (<see cref="HumanDecisions.Move"/>).
    /// </summary>
    public NodeMove? Move(NodeDeclaration node, NodeWord word, NodeStatus status, NodeScope scope) =>
        word.Act == NodeWordActs.Decide ? HumanDecisions.Move(word, status)
        : word.Act == NodeWordActs.Reject
        ? new NodeMove(NodeStates.Rejected, NodeCause.Person) { Source = word.Source, Attempt = status.Attempt, Words = word.Words }
        : new NodeMove(NodeStates.Reached, NodeCause.Authority)
        {
            Source = word.Source, Attempt = status.Attempt, Acceptance = status.Binding is { } bound ? bound with { Outcome = Outcome(node, word.Act) } : null,
            Words = word.Words,
        };

    /// <summary>The presses <paramref name="node"/> waits for once active: a decision's decide, an approval's two, else a task's Done.</summary>
    public static IReadOnlyList<string> Presses(NodeDeclaration node) =>
        HumanDecisions.Is(node) ? [NodeWordActs.Decide]
        : node.Outcomes.SequenceEqual(Approval) ? [NodeWordActs.Approve, NodeWordActs.Reject] : [NodeWordActs.Done];

    /// <summary>The outcome <paramref name="act"/> reaches <paramref name="node"/> with: the one it names among those declared, else none.</summary>
    public static string? Outcome(NodeDeclaration node, string act) => act switch
    {
        NodeWordActs.Approve when node.Outcomes.Contains(Approved, StringComparer.Ordinal) => Approved,
        NodeWordActs.Done when node.Outcomes.Contains(Done, StringComparer.Ordinal) => Done,
        _ => null,
    };
}
