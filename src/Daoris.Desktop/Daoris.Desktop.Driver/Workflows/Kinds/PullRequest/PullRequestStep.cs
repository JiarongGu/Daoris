namespace Daoris.Driver;

/// <summary>
/// Daoris's half of any plugin's <c>pull-request</c> step: the performed step that opens the pull request of the
/// branch the run's <c>push</c> pushed. Its evidence address is the pull request's, which the runtime writes on the landing record
/// (<see cref="Keep"/>), so the <c>work/state</c> asks have an address and the page one to show; a graph version's person part reads it as the
/// person's merge on the platform, named by the plugin that opens it (<see cref="WorkflowGate.StepsOf"/>), and a <c>monitor</c> watches the state
/// it reports (<see cref="PullRequestReading"/>). The step itself is its plugin's (<see cref="PluginStep"/>).
/// </summary>
[NodeKind("*/pull-request", NodeGroup.Git, NodeKinds.Push)]
public sealed record PullRequestStep : PluginStep, IWatchable
{
    public override NodePart Part(NodeDeclaration node) => base.Part(node) with { Lowered = WorkflowKinds.PullRequest };

    public IReadOnlyList<string> States => PullRequestReading.States;

    public NodeWatched Read(NodeReading reading) => PullRequestReading.Read(reading);

    /// <summary>Whether <paramref name="address"/> is an address a pull request is kept by: an absolute <c>http</c> or <c>https</c> one.</summary>
    public static bool IsAddress(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    /// <summary>
    /// Each of the run's pull-request steps that reached: the pull request its evidence names is written on the landing record with its plugin,
    /// once, so the <c>work/state</c> asks have an address and the page shows the pull request.
    /// </summary>
    internal static void Keep(GovernedUnit governed, NodeRecord record, IPullRequestRecords kept)
    {
        if (NodeEvaluations.Landed(governed, record.Statuses) is not { Unit: var unit } || NodeObserving.LandingOf(unit) is not { } landed) return;
        foreach (var node in governed.Declared.Values)
        {
            if (PullRequestKind.PluginOf(node.Kind) is not { } plugin) continue;
            if (record.Statuses.GetValueOrDefault(node.Id) is not { State: NodeStates.Reached, Acceptance.Evidence.Ref: { } address }) continue;
            if (!IsAddress(address)) continue;
            kept.Keep(governed.Watched.Repository, landed.Branch, plugin, address);
        }
    }
}
