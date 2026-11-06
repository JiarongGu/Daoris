namespace Daoris.Driver;

/// <summary>
/// Another repository asked, as a step (INTEGRATE1p; the integration design §17.5): a quest published to <c>to</c> under the request's key and waited
/// on (<see cref="RequestStart"/>), reached and failed by that commitment as a <c>wait.unit</c> is.
/// </summary>
[NodeKind(NodeKinds.Request, NodeGroup.Others)]
internal sealed class RequestNode : BuiltInNode
{
    /// <summary>
    /// The commitment's, as <c>wait.unit</c>'s: the quest's done, or its taking under <c>until: taken</c>, and its decline. No skip of an active node:
    /// after a decline the person's <i>Go on without it…</i> is the failed node's own move.
    /// </summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.Request, NodeReach.Waited, NodeSource.Commitment, NodeSource.Commitment, false, PersonSkips: false, true, true);

    /// <summary>It binds the run, as <c>wait.unit</c> does; the quest it published is named by its attempt's key.</summary>
    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Run;

    public override IReadOnlyList<NodeOptionField> Options { get; } =
    [
        NodeOptionFields.Text("to", required: true),
        NodeOptionFields.Text("title", required: true),
        NodeOptionFields.Text("body", required: true),
        NodeOptionFields.Choice("start", RequestOptions.Starts),
        NodeOptionFields.Choice("until", RequestOptions.Untils),
        NodeOptionFields.Choice("declined", RequestOptions.Declines),
    ];

    public override string? Problem(NodeDeclaration node) => RequestOptions.Problem(node);

    public override NodePart Part(NodeDeclaration node) => NodePart.Automatic(node.Kind) with { Lowered = NodeKinds.Request };

    public override string? Title => "The quest to another repository";

    public override NodeLook Look => NodeLook.Other;
}
