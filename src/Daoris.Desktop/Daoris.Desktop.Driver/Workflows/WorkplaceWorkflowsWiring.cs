using System.Runtime.CompilerServices;

namespace Daoris.Driver;

internal sealed class WorkplaceWorkflowsOperations : IWorkplaceWorkflows, IWorkplaceGateWorkflows
{
    [ModuleInitializer]
    internal static void Install()
    {
        var operations = new WorkplaceWorkflowsOperations();
        WorkplaceWorkflows.Current = operations;
        WorkplaceGateWorkflows.Current = operations;
    }

    public string? AcceptedGates(DriverConfig config, string repository, string? workspace) =>
        WorkflowSelection.ResolveLanded(config, repository, workspace).Gates;

    public Task<string?> DeclarationTextAsync(string root, string blob, CancellationToken ct) => LandedGates.TextAsync(root, blob, ct);

    public async Task<IWorkplaceProcess> ReadProcessAsync(
        string home, DriverConfig config, Func<CancellationToken, Task<IReadOnlyList<QuestView>>> quests, string quest, string repository,
        string? workspace, CancellationToken ct) =>
        await WorkflowProcesses.ReadAsync(home, config, quests, quest, repository, workspace, ct).ConfigureAwait(false);

    public string? BindChatFirst(DriverConfig config, string home, string run, string tree, IWorkplacePluginCatalog plugins, DateTimeOffset at) =>
        ChatRuns.BindFirst(config, home, run, tree, WorkflowCurrent.PluginsOf(plugins), at).Failed;

    public (IReadOnlyList<string> Line, bool Live) ChatPressOf(string session, string recordsJson) => ChatRuns.PressOf(session, recordsJson);

    // The world is the review's IOpinionWorld, which every world the doors and the look hand the gate is (the review's IAutoLandingWorld
    // among them): the second opinion reads its opinions through it.
    public async Task<IWorkplaceGate> GateAsync(
        TreeGates gates, string path, string? quest, IReviewWorld world, string? session, CancellationToken ct, IWorkplaceProcess? process,
        string? tip, string? run) =>
        await TreeGateWorkflows.GateAsync(gates, path, quest, (IOpinionWorld)world, session, ct, (WorkflowProcess?)process, tip, run).ConfigureAwait(false);

    public async Task<IWorkplaceProcess> ProcessAsync(
        TreeGates gates, DriverConfig config, string repository, string? workspace, string? quest, IReviewWorld world, CancellationToken ct, string? run) =>
        await TreeGateWorkflows.ProcessAsync(gates, config, repository, workspace, quest, world, ct, run).ConfigureAwait(false);
}
