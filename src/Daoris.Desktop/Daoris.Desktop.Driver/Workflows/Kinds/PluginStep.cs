namespace Daoris.Driver;

/// <summary>
/// A plugin's step, <c>&lt;plugin&gt;/&lt;step&gt;</c>, as the kit reads it (D247 point 4): its contract row from its declaration (its plugin's
/// believed answer reaches and fails it, a person rejects or skips it as declared), bound by its place after the landing, carried out by its
/// plugin and titled by its step and its plugin. A step name Daoris has a half of reads as that half (<see cref="PullRequestStep"/>).
/// </summary>
public record PluginStep : INodeKind
{
    /// <summary>The step's kind, <c>&lt;plugin&gt;/&lt;step&gt;</c>; empty on a registered half, which <see cref="NodeKit.Find"/> names as it hands it out.</summary>
    public string Kind { get; init; } = "";

    /// <summary>
    /// The node's authority from its declaration. A plugin's answer is named <see cref="NodeSource.Plugin"/> here alone: that the answer came from
    /// the declaring plugin is <see cref="WorkBelief.Completion"/>'s sender check, not the lifecycle's.
    /// </summary>
    public NodeAuthority Authority(NodeDeclaration node) =>
        new(node.Kind, node.Reach, NodeSource.Plugin, NodeSource.Plugin, node.Rejectable, node.Skippable, MayEnd: true, Evidenced: true);

    public RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubjects.Placed(node, afterHanded);

    public IReadOnlyList<NodeOptionField> Options => [];

    /// <summary>None of its own: a plugin's declaration is held to the checks every declaration is.</summary>
    public string? Problem(NodeDeclaration node) => null;

    public virtual NodePart Part(NodeDeclaration node) => new(node.Kind, WorkflowParticipation.Automatic, WorkflowExecutor.Plugin);

    public string Offered => PaletteKind.Alone;

    public IReadOnlyList<string> RoleTakes => [];

    /// <summary>None beyond its declaration's own <see cref="NodeDeclaration.Evidence"/>.</summary>
    public string? Evidence => null;

    public string? Title => Kind.IndexOf('/') is var slash and > 0 ? $"{Kind[(slash + 1)..]} by `{Kind[..slash]}`" : null;

    public NodeLook Look => NodeLook.Plugin;
}
