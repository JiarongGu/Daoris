namespace Daoris.Driver;

/// <summary>
/// A commit moved onto a target ref by a declared method, and nothing else: into the line through its one door, into a branch by one
/// compare-and-set. <c>behind</c> is the one way a moved target reaches back, to the rebase its commit comes from. Arriving: declared with its
/// ports, options, outcomes and waits before the palette offers it, so the change that performs it leaves the contracts as they are.
/// </summary>
[NodeKind(NodeKinds.Merge, NodeGroup.Git, NodeKinds.Resolve, Arriving = true)]
internal sealed class MergeNode : BuiltInNode
{
    public const string Commit = "commit";

    public const string Ref = "ref";

    public const string Merged = "merged";

    public const string Held = "held";

    public const string Behind = "behind";

    public const string Completed = "completed";

    /// <summary>A merge commit with the repository's hooks; one commit of the work's change; the ref moved to the commit.</summary>
    public static readonly IReadOnlyList<string> Methods = ["merge", "squash", "fast-forward"];

    /// <summary>Reached by git's answer or the line's request, and failed by a hook's refusal, a branch it may not make, or a branch moved by another hand.</summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.Merge, NodeReach.Performed, NodeSource.Git, NodeSource.Git, false, PersonSkips: false, MayEnd: true, Evidenced: true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Input;

    public override IReadOnlyList<NodeOptionField> Options { get; } =
        [NodeOptionFields.Choice("method", Methods), NodeOptionFields.Text("target"), NodeOptionFields.Text("message")];

    public override IReadOnlyList<string> Outcomes { get; } = [Merged, Held, Behind, Completed];

    public override bool TakesOutcomes(IReadOnlyList<string> outcomes) => outcomes.All(outcome => Outcomes.Contains(outcome, StringComparer.Ordinal));

    public override IReadOnlyDictionary<string, string> Inputs { get; } = new Dictionary<string, string>(StringComparer.Ordinal) { [Commit] = WireValues.Commit };

    public override IReadOnlyDictionary<string, string> Outputs { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal) { [Commit] = WireValues.Commit, [Ref] = WireValues.Ref };

    public override IReadOnlyDictionary<string, IReadOnlyList<string>> OutputsOn { get; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        [Commit] = [Merged, Held], [Ref] = [Merged, Held],
    };

    /// <summary>
    /// Its request in the line's integration, a target branch a tree has checked out, the checkout's own uncommitted work, and a completed branch
    /// it could not end. A head's wait on its operation is its rebase's.
    /// </summary>
    public override IReadOnlyList<string> Waits { get; } = ["integration", "checked-out", "root-dirty", "branch-kept"];

    public override string? Title => "The merge";

    public override NodeLook Look => NodeLook.Landing;

    public override IReadOnlyList<string> Places => PlaceKind.GitAlone;
}
