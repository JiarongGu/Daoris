namespace Daoris.Driver;

/// <summary>
/// A person's task (§D5; NODEKIT1 HUMAN, D245's second note): a manual operation the run waits on until the person presses <i>Done</i>, or an
/// approval they <i>Approve</i> or <i>Reject</i>, as a release pipeline's manual action. Its outcomes are written in the file, so a flowchart
/// routes on them with <c>when</c> wires: <c>done</c> for a task, <c>approved</c> and <c>rejected</c> for an approval; one declaring none is a task
/// reached with no outcome.
/// </summary>
[NodeKind(NodeKinds.Human, NodeGroup.You, NodeKinds.GoAhead)]
internal sealed class HumanNode : BuiltInNode, IObserves, IPressed
{
    /// <summary>A task's outcome.</summary>
    public static readonly IReadOnlyList<string> Task = [Done];

    /// <summary>An approval's outcomes, in this order.</summary>
    public static readonly IReadOnlyList<string> Approval = [Approved, Rejected];

    private const string Done = "done", Approved = "approved", Rejected = "rejected";

    public override NodeAuthority Row { get; } = new(NodeKinds.Human, NodeReach.Pressed, NodeSource.Person, null, true, true, true, false);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Run;

    /// <summary>Its outcomes are a task's or an approval's, or none.</summary>
    public override string? Problem(NodeDeclaration node) =>
        node.Outcomes.Count == 0 || node.Outcomes.SequenceEqual(Task) || node.Outcomes.SequenceEqual(Approval) ? null : "outcomes";

    public override bool OwnsOutcomes => true;

    public override NodePart Part(NodeDeclaration node) => new(NodeKinds.Human, WorkflowParticipation.You, WorkflowExecutor.Daoris);

    public override string? Title => "Your task";

    public override NodeLook Look => NodeLook.You;

    /// <summary>Begun on a run alone, as a go-ahead is: the run then waits on the person. On a unit it waits ready.</summary>
    public bool Begins(GovernedUnit governed) => governed.Scope is not null;

    /// <summary>Nothing but the person's press reaches it, applied from the run's record by the pass: no record says more.</summary>
    public NodeObservation Observe(NodeReading reading) =>
        new(reading.Unit.Id, reading.Node.Id, reading.Node.Kind, null, "it waits for your press") { Attempt = reading.Binding.Attempt };

    public IReadOnlyList<string> Takes { get; } = [NodeWordActs.Done, NodeWordActs.Approve, NodeWordActs.Reject];

    public string Refusal => RunNodeWords.NotTask;

    /// <summary>A waiting task offers its Done; a waiting approval its Approve and Reject.</summary>
    public IReadOnlyList<string> Acts(NodeDeclaration node, NodeStatus status) => status.State == NodeStates.Active ? Presses(node) : [];

    /// <summary>Each press reaches it on the person's word at the binding it waits on, with the outcome the press names and their note.</summary>
    public NodeMove? Move(NodeDeclaration node, NodeWord word, NodeStatus status, NodeScope scope) => new(NodeStates.Reached, NodeCause.Authority)
    {
        Source = NodeSource.Person, Attempt = status.Attempt, Acceptance = status.Binding is { } bound ? bound with { Outcome = Outcome(node, word.Act) } : null,
        Words = word.Words,
    };

    /// <summary>The presses <paramref name="node"/> waits for once active: an approval's two, else a task's Done.</summary>
    public static IReadOnlyList<string> Presses(NodeDeclaration node) =>
        node.Outcomes.SequenceEqual(Approval) ? [NodeWordActs.Approve, NodeWordActs.Reject] : [NodeWordActs.Done];

    /// <summary>The outcome <paramref name="act"/> reaches <paramref name="node"/> with: the one it names among those declared, else none.</summary>
    public static string? Outcome(NodeDeclaration node, string act) => act switch
    {
        NodeWordActs.Approve when node.Outcomes.Contains(Approved, StringComparer.Ordinal) => Approved,
        NodeWordActs.Reject when node.Outcomes.Contains(Rejected, StringComparer.Ordinal) => Rejected,
        NodeWordActs.Done when node.Outcomes.Contains(Done, StringComparer.Ordinal) => Done,
        _ => null,
    };
}
