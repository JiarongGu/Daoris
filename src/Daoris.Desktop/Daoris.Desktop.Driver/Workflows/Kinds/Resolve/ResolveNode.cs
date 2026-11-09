namespace Daoris.Driver;

/// <summary>
/// A problem a step could not settle, handed to whoever the workflow names (its <c>resolver</c>) with its files and hunks named, bounded per
/// stop, and returned on <c>resolved</c> by a <c>back</c> way to the rebase it came from. Arriving: declared with its ports, options, outcome and
/// waits before the palette offers it, so the change that performs it leaves the contracts as they are.
/// </summary>
[NodeKind(NodeKinds.Resolve, NodeGroup.Git, NodeKinds.Rebase, Arriving = true)]
internal sealed class ResolveNode : BuiltInNode
{
    public const string Problem = "problem";

    public const string Work = "work";

    public const string Resolver = "resolver";

    public const string Resolved = "resolved";

    /// <summary>The work's newest executor, its conversation going on; a new session in a tree of Daoris's own; a task for the person.</summary>
    public static readonly IReadOnlyList<string> Resolvers = ["session", "fresh", "person"];

    /// <summary>Reached by its proof that only the named files changed, and failed where a stop's bound is spent or its resolver stood down.</summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.Resolve, NodeReach.Performed, NodeSource.Git, NodeSource.Git, false, PersonSkips: false, MayEnd: false, Evidenced: true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Input;

    public override IReadOnlyList<NodeOptionField> Options { get; } = [NodeOptionFields.Choice(Resolver, Resolvers)];

    public override IReadOnlyList<string> Outcomes { get; } = [Resolved];

    public override bool TakesOutcomes(IReadOnlyList<string> outcomes) => outcomes.All(outcome => outcome == Resolved);

    public override string NeverEndBecause => "its resolution finishes the rebase its way back returns to.";

    public override IReadOnlyDictionary<string, string> Inputs { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal) { [Problem] = WireValues.Problem, [Work] = WireValues.Work };

    /// <summary>Its resolver at work on a stop, and past the stop's bound the person.</summary>
    public override IReadOnlyList<string> Waits { get; } = ["session", "fresh", "person", "bound"];

    public override string? Title => "The resolution";

    public override NodeLook Look => NodeLook.Line;
}
