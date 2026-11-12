namespace Daoris.Driver;

/// <summary>
/// A failed plugin step's <i>Try again</i>, the person's press on the run's step: the table's failed to ready by the person, a new attempt
/// under the next number, which the step's plugin is sent as any start is. Every plugin step takes it (<see cref="PluginStep"/>), a step of
/// any other kind refuses it.
/// </summary>
public sealed class PluginStepRetry : IPressed
{
    /// <summary>The one unit every plugin step answers its acts with.</summary>
    public static PluginStepRetry Unit { get; } = new();

    public IReadOnlyList<string> Takes { get; } = [NodeWordActs.TryAgain];

    public string Refusal => RunNodeWords.NotPluginStep;

    /// <summary>Offered where the step failed: a failure kept for the person, or one a retry would come to later.</summary>
    public IReadOnlyList<string> Acts(NodeDeclaration node, NodeStatus status) => status.State == NodeStates.Failed ? [NodeWordActs.TryAgain] : [];

    public NodeMove? Move(NodeDeclaration node, NodeWord word, NodeStatus status, NodeScope scope) =>
        word.Act == NodeWordActs.TryAgain ? new NodeMove(NodeStates.Ready, NodeCause.Person) : null;
}
