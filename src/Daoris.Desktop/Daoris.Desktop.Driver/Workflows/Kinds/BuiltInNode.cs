namespace Daoris.Driver;

/// <summary>
/// A kind this build declares (<see cref="NodeKinds"/>), whose contract row is its own and never a declaration's. What a kind does not declare is
/// a step with no options and no checks of its own, offered alone, taken by no role, drawn as its own kind and carried out by Daoris.
/// </summary>
public abstract class BuiltInNode : INodeKind
{
    /// <summary>Who may report each of its outcomes: the one thing a node of it cannot change.</summary>
    public abstract NodeAuthority Row { get; }

    public NodeAuthority Authority(NodeDeclaration node) => Row;

    public abstract RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded);

    public virtual IReadOnlyList<NodeOptionField> Options => [];

    public virtual string? Problem(NodeDeclaration node) => null;

    public virtual WireProblem? DeclarationProblem(NodeDeclaration node, string field) => null;

    public virtual string NeverEndBecause => "it is reached on a claim, and an end on evidence.";

    public virtual IEnumerable<WireProblem> Problems(NodeGraph graph, NodeDeclaration node) => [];

    /// <summary>The outcomes a node of it is reached with, which its declaration writes; none where its kind is no decision.</summary>
    public virtual IReadOnlyList<string> Outcomes => [];

    /// <summary>
    /// Whether a node of it may declare <paramref name="outcomes"/>: where its kind is reached with outcomes, any that keep the names a route is
    /// followed by (<see cref="NodeOutcomes.Problem"/>); a kind whose outcomes are a fixed set of its own says which.
    /// </summary>
    public virtual bool TakesOutcomes(IReadOnlyList<string> outcomes) => Outcomes.Count > 0 && NodeOutcomes.Problem(outcomes) is null;

    public virtual NodePart Part(NodeDeclaration node) => NodePart.Automatic(node.Kind);

    public virtual string Offered => PaletteKind.Alone;

    public virtual IReadOnlyList<string> RoleTakes => [];

    public virtual string? Evidence => null;

    public abstract string? Title { get; }

    public abstract NodeLook Look { get; }

    public virtual bool ReadsTheLanding => false;

    public virtual bool HoldsRun => false;

    public virtual IReadOnlyList<string> LandingActs(NodeDeclaration node) => [];

    public virtual LandingGuard Guard => LandingGuard.None;

    public virtual bool EndsByTheLine => false;

    public virtual IReadOnlyDictionary<string, string> Inputs => NodePorts.None;

    public virtual IReadOnlyDictionary<string, string> Outputs => NodePorts.None;

    public virtual IReadOnlyList<string> Places => PlaceKind.Every;

    /// <summary>Each output carried on some outcomes alone, which a step the editor adds writes on its declaration.</summary>
    public virtual IReadOnlyDictionary<string, IReadOnlyList<string>> OutputsOn => NodeArrivals.NoneOn;

    public virtual NodeArrivals Arrives => NodeArrivals.None;

    public virtual IReadOnlyList<string> Waits => [];

    public virtual WorkflowRunWait? WaitOf(NodeStatus status) => null;
}
