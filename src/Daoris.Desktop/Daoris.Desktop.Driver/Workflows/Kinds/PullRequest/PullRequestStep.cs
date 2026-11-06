namespace Daoris.Driver;

/// <summary>
/// Daoris's half of any plugin's <c>pull-request</c> step (G8, D247 point 4): a graph version's person part reads it as the person's merge on the
/// platform, named by the plugin that opens it (<see cref="WorkflowGate.StepsOf"/>). The step itself is its plugin's (<see cref="PluginStep"/>).
/// </summary>
[NodeKind("*/pull-request", NodeGroup.Landing, NodeKinds.Push)]
public sealed record PullRequestStep : PluginStep
{
    public override NodePart Part(NodeDeclaration node) => base.Part(node) with { Lowered = WorkflowKinds.PullRequest };
}
