namespace Daoris.Driver;

/// <summary>
/// Another repository asked, as a step (INTEGRATE1p; the integration design §17.5): a quest published to <c>to</c> under the request's key and waited
/// on (<see cref="RequestStart"/>), reached and failed by that commitment as a <c>wait.unit</c> is.
/// </summary>
[NodeKind(NodeKinds.Request, NodeGroup.Others)]
internal sealed class RequestNode : BuiltInNode, IStarts, IPressed
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

    /// <summary>The service client its quest is published through.</summary>
    public INodeDoor? Door(NodeDoorWiring wiring) => wiring.Requests;

    /// <summary>A run's request, published under its key once its start is standing say-so; on a unit it waits ready.</summary>
    public async Task<WorkPerformed?> StartAsync(NodeStarting starting, CancellationToken ct) => starting.Governed.Scope is null
        ? null
        : await starting.Integrating.RequestAsync(starting.Governed, starting.Node, starting.Start, starting.Status, starting.Said, ct).ConfigureAwait(false);

    /// <summary>Its send, and its asking again or going on without it when declined (INTEGRATE1p).</summary>
    public IReadOnlyList<string> Takes { get; } = [NodeWordActs.Send, NodeWordActs.AskAgain, NodeWordActs.GoOn];

    public string Refusal => RunNodeWords.NotRequest;

    /// <summary>
    /// A ready request under <c>start: you</c> waits for the person's send; its decline waits on the person, unless its workflow said
    /// <c>declined: stop</c> (C33).
    /// </summary>
    public IReadOnlyList<string> Acts(NodeDeclaration node, NodeStatus status) =>
        status.State == NodeStates.Ready && NodeOptions.Text(node, "start") != RequestOptions.Automatic ? [NodeWordActs.Send]
        : status.State == NodeStates.Failed && status.Code == RequestOptions.DeclinedCode && NodeOptions.Text(node, "declined") != RequestOptions.Stop
            ? [NodeWordActs.AskAgain, NodeWordActs.GoOn]
            : [];
}
