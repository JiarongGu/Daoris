namespace Daoris.Driver;

/// <summary>
/// What a door that sets a workflow choice says of the repositories it reaches when the version integrates its landing (INTEGRATE1r part r2;
/// the integration design §17.7: <i>the choice's door says the reach</i>; the workflow design §4.7): for each, why its declarations cannot
/// carry the version, and whether its line integrates with no gates. The terminal's <c>use</c> and Ask Daoris's card ask it; a door that
/// reads the accepted declaration does so here, asynchronously, and the pure check (<see cref="IntegrateCarry"/>) is handed the answer.
/// </summary>
internal static class IntegrateChoice
{
    /// <summary>What one repository the choice reaches answers.</summary>
    /// <param name="Cannots">Why its declarations cannot carry the version, a sentence each; empty where they can.</param>
    /// <param name="NoGates">Whether the version integrates its line there with no gates (§17.7, F42).</param>
    internal sealed record Reach(string Repository, IReadOnlyList<string> Cannots, bool NoGates);

    /// <summary>Whether the graph holds a step this check judges: an <c>integrate</c>, or a plugin's push or pull request.</summary>
    internal static bool Matters(NodeGraph graph) =>
        graph.Nodes.Any(node => node.Kind is NodeKinds.Integrate or NodeKinds.Pushed or NodeKinds.PullRequest);

    /// <summary>Whether a fact in the accepted declaration is judged: the graph's <c>integrate</c> asks with <c>done</c> or <c>steward</c>.</summary>
    internal static bool NeedsAccepted(NodeGraph graph) =>
        graph.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Integrate) is { } integrate
        && IntegrateNodes.RequestOf(integrate) is LandingIntegrate.Done or LandingIntegrate.Steward;

    /// <summary>
    /// One repository's answer over <paramref name="config"/>, the version's newest graph as it would be bound there. <paramref name="accepted"/>
    /// is what the door read of its accepted declaration, null where it did not ask (then that one fact is not judged).
    /// </summary>
    internal static Reach Of(
        DriverConfig config, string workflow, int version, NodeGraph graph, string repository, string? workspace, AcceptedGates? accepted)
    {
        var declared = WorkflowProcess.Current(config, repository, workspace);
        var (steps, _) = WorkflowNamed.GateSteps(graph, version, declared.Landing.Rule);
        // A version that does not read here has its own refusal, and a graph with no landing integrates nothing.
        if (steps is null || !steps.Any(step => step.Kind == WorkflowKinds.Landing)) return new(repository, [], false);

        var binding = new WorkflowRunBinding("", repository, RemoteTarget.Workspace(workspace), workflow, WorkflowLevels.Repository, DateTimeOffset.MinValue)
        {
            Version = version,
        };
        var landing = WorkflowProcess.Of(config, repository, workspace, binding, steps).Landing.Rule;
        var integrates = graph.Nodes.Any(node => node.Kind == NodeKinds.Integrate);
        return new(repository, IntegrateCarry.Cannots(repository, graph, declared.Landing.Rule, landing, accepted), integrates && IntegrateCarry.NoGates(declared.Landing.Rule));
    }

    /// <summary>
    /// Each repository the choice reaches, answered; a repository's accepted declaration is read only where the version asks with <c>done</c> or
    /// <c>steward</c>. Empty where the graph holds nothing this judges.
    /// </summary>
    internal static async Task<IReadOnlyList<Reach>> ReachAsync(
        DriverConfig config, string workflow, int version, NodeGraph graph,
        IEnumerable<(string Repository, string? Workspace, string? Root)> reached, CancellationToken ct)
    {
        if (!Matters(graph)) return [];
        var asks = NeedsAccepted(graph);
        var answers = new List<Reach>();
        foreach (var (repository, workspace, root) in reached)
        {
            var accepted = asks ? await ReadAcceptedAsync(config, repository, workspace, root, ct).ConfigureAwait(false) : null;
            answers.Add(Of(config, workflow, version, graph, repository, workspace, accepted));
        }

        return answers;
    }

    /// <summary>
    /// The gates the declaration <paramref name="repository"/>'s integration acceptance names, as it stands at its line's tip: read only where
    /// that blob is the one there (<see cref="IntegrationAcceptance.AcceptedTextAsync"/>), so a declaration the line changed since, or none
    /// accepted, names none. A repository with no checkout here, no line, or a git that cannot say is <see cref="AcceptedGates.NotRead"/>.
    /// </summary>
    internal static async Task<AcceptedGates> ReadAcceptedAsync(DriverConfig config, string repository, string? workspace, string? root, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return AcceptedGates.NotRead($"`{repository}` has no checkout here to read its line from");
        try
        {
            var rule = WorkflowProcess.Current(config, repository, workspace).Landing.Rule;
            var line = rule.Integrate?.Line ?? (await CanonicalLine.ResolveAsync(root, repository, workspace, config, ct).ConfigureAwait(false)).Branch;
            if (line is null || !BranchName.IsValid(line)) return AcceptedGates.NotRead($"`{repository}` has no line to read the declaration at");
            var text = await IntegrationAcceptance.AcceptedTextAsync(config, root, repository, $"refs/heads/{line}", ct).ConfigureAwait(false);
            return new(text is null ? [] : IntegrationDeclaration.Declared(text));
        }
        catch (Exception error) when (!ct.IsCancellationRequested && error is IOException or InvalidOperationException or DriverException
                                          or System.ComponentModel.Win32Exception)
        {
            return AcceptedGates.NotRead(error.Message);
        }
    }

    /// <summary>
    /// What a refused choice says: each repository that cannot carry the version, its sentences after its name, and that nothing was written.
    /// </summary>
    internal static string Refusal(string workflow, int version, IEnumerable<Reach> refused) =>
        $"`{workflow}` v{version} cannot be chosen here: "
        + string.Join(" ", refused.Select(each => $"`{each.Repository}`: {string.Join(" ", each.Cannots)}")) + " Nothing was written.";
}
