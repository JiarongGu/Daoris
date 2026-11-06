namespace Daoris.Driver;

/// <summary>
/// A kind this build declares (<see cref="NodeKinds"/>), whose contract row is its own and never a declaration's. What a kind does not declare is
/// a step with no options and no checks of its own, offered alone, taken by no role, drawn as its own kind and carried out by Daoris.
/// </summary>
public abstract class BuiltInNode : INodeKind
{
    /// <summary>Who may report each of its outcomes (§E2's authority table): the one thing a node of it cannot change.</summary>
    public abstract NodeAuthority Row { get; }

    public NodeAuthority Authority(NodeDeclaration node) => Row;

    public abstract RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded);

    public virtual IReadOnlyList<NodeOptionField> Options => [];

    public virtual string? Problem(NodeDeclaration node) => null;

    public virtual NodePart Part(NodeDeclaration node) => NodePart.Automatic(node.Kind);

    public virtual string Offered => PaletteKind.Alone;

    public virtual IReadOnlyList<string> RoleTakes => [];

    public virtual string? Evidence => null;

    public abstract string? Title { get; }

    public abstract NodeLook Look { get; }
}
