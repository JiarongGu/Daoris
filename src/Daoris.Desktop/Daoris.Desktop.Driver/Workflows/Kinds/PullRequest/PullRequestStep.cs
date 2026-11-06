namespace Daoris.Driver;

/// <summary>
/// Daoris's half of any plugin's <c>pull-request</c> step (G8, D247 point 4): a graph version's person part reads it as the person's merge on the
/// platform, named by the plugin that opens it (<see cref="WorkflowGate.StepsOf"/>), and a <c>monitor</c> watches the state it reports
/// (<see cref="PullRequestReading"/>). The step itself is its plugin's (<see cref="PluginStep"/>).
/// </summary>
[NodeKind("*/pull-request", NodeGroup.Landing, NodeKinds.Push)]
public sealed record PullRequestStep : PluginStep, IWatchable
{
    public override NodePart Part(NodeDeclaration node) => base.Part(node) with { Lowered = WorkflowKinds.PullRequest };

    public IReadOnlyList<string> States => PullRequestReading.States;

    public NodeWatched Read(NodeReading reading) => PullRequestReading.Read(reading);
}
