namespace Daoris.Driver;

/// <summary>
/// The landing gate, as <see cref="TreeGates"/> over the context (PART10k): built once here, with one-line public calls for the
/// loop and the doors. PART10l folds this part into the front.
/// </summary>
public sealed partial class SessionTrees
{
    private TreeGates? _gates;

    /// <summary>The landing gate's parts for a tree (XAGENT1f, WORKFLOW1f).</summary>
    private TreeGates Gates => _gates ??= new(_context);

    /// <inheritdoc cref="TreeGates.GateAsync"/>
    public Task<LandingGate> GateAsync(
        string path, string? quest, IOpinionWorld world, string? session = null, CancellationToken ct = default, WorkflowProcess? process = null) =>
        Gates.GateAsync(path, quest, world, session, ct, process);

    /// <inheritdoc cref="TreeGates.ProcessAsync"/>
    public Task<WorkflowProcess> ProcessAsync(
        DriverConfig config, string repository, string? workspace, string? quest, IReviewWorld world, CancellationToken ct = default) =>
        Gates.ProcessAsync(config, repository, workspace, quest, world, ct);

    /// <inheritdoc cref="TreeGates.WorkflowAsync"/>
    public Task<WorkflowGateState> WorkflowAsync(string tree, WorkflowProcess process, string? session, CancellationToken ct = default) =>
        Gates.WorkflowAsync(tree, process, session, ct);

    /// <inheritdoc cref="TreeGates.OpinionAsync"/>
    public Task<OpinionGateState> OpinionAsync(
        string tree, string repository, string workspace, string? quest, string? session, IOpinionWorld world, ReviewGateState? review,
        string occasion, CancellationToken ct = default, WorkflowProcess? process = null) =>
        Gates.OpinionAsync(tree, repository, workspace, quest, session, world, review, occasion, ct, process);

    /// <inheritdoc cref="TreeGates.ForkPointAsync"/>
    public Task<string?> ForkPointAsync(string tree, CancellationToken ct = default) => Gates.ForkPointAsync(tree, ct);
}
