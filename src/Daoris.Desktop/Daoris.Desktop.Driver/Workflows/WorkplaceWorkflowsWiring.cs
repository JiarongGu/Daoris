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

    public PlaceIntegrate IntegrateOf(string home, DriverConfig config, string repository, string? workspace) =>
        PlaceIntegrates.Of(WorkflowPorts.Store(home), config, repository, workspace);

    public string? AcceptedGates(DriverConfig config, string repository, string? workspace) =>
        WorkflowSelection.ResolveLanded(config, repository, workspace).Gates;

    public Task<string?> DeclarationTextAsync(string root, string blob, CancellationToken ct) => LandedGates.TextAsync(root, blob, ct);

    public async Task<IWorkplaceProcess> ReadProcessAsync(
        string home, DriverConfig config, Func<CancellationToken, Task<IReadOnlyList<QuestView>>> quests, string quest, string repository,
        string? workspace, CancellationToken ct) =>
        await WorkflowProcesses.ReadAsync(home, config, quests, quest, repository, workspace, ct).ConfigureAwait(false);

    public string? BindChatFirst(DriverConfig config, string home, string run, string tree, IWorkplacePluginCatalog plugins, DateTimeOffset at) =>
        ChatRuns.BindFirst(config, home, run, tree, WorkflowSteps.PluginsOf(plugins), at).Failed;

    public (IReadOnlyList<string> Line, bool Live) ChatPressOf(string session, string recordsJson) => ChatRuns.PressOf(session, recordsJson);

    public string? TakenOf(string session, string recordsJson) => ChatRuns.TakenOf(session, recordsJson);

    public void SubmitChatCandidate(string home, string run, string repository, string? workspace, string branch, string tip, DateTimeOffset at, Func<string, bool> landed)
    {
        if (WorkflowRunBindings.Read(home, run) is not { Problem: null } binding || !GraphRun.Names(binding)) return;
        ChatCandidates.Submit(home, run, repository, workspace, branch, tip, at, landed);
    }

    public string? SkippedLook(string home, string run, string? node) =>
        LookReading.SkippedWords(new NodeRecordFiles(home, () => false).Read(RunUnits.IdOf(run)), node);

    public ReviewWord KeepReviewWord(string home, Func<bool>? holdsLease, IWorkplaceProcess process, DateTimeOffset now, string? words = null, string? revision = null)
    {
        if (process is not WorkflowProcess { BoundRun: { } run, Graph: { } graph } || graph.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Review) is not { } review)
        {
            return new ReviewWord(false, false, null, null, null);
        }

        var word = RunNodeWords.Word(new NodeRecordFiles(home, holdsLease), run, review.Id, NodeWordActs.Accept, words, () => now, revision: revision);
        var kept = word.Done || word.Refused == RunNodeWords.Worded;
        // A review that does not wait for the word yet (not active) is no refusal: the word is given again when it does.
        var said = kept || word.Refused == NodeRefusal.NotInTable ? null : RunNodeWords.Said(word);
        return new ReviewWord(true, kept, RunUnits.IdOf(run), review.Id, said) { Moved = word.Refused == ReviewAccepts.Moved ? word.Detail ?? "" : null };
    }

    public void KeepRepair(string home, Func<bool>? holdsLease, string unit, string review, string tip, string turn, int carries, DateTimeOffset at)
    {
        var records = new NodeRecordFiles(home, holdsLease);
        RunRepairs.Keep(records, unit, CarriedRepairs.Named(records.Read(unit), new NodeRepair(review, tip, turn, carries, at)));
    }

    public void TakeRepair(string home, Func<bool>? holdsLease, string unit, string review) =>
        RunRepairs.Take(new NodeRecordFiles(home, holdsLease), unit, review);

    public int RepairCarries(string home, Func<bool>? holdsLease, string unit, string review) =>
        RunRepairs.Of(new NodeRecordFiles(home, holdsLease), unit, review)?.Carries ?? 0;

    public bool FollowsSaved(DriverConfig config, string repository, string? workspace) =>
        WorkflowSelection.Resolve(config, repository, workspace, null).Workflow != WorkflowSelection.Current;

    // The world is the review's IOpinionWorld, which every world the doors and the look hand the gate is (the review's IReviewLandingWorld
    // among them): the second opinion reads its opinions through it.
    public async Task<IWorkplaceGate> GateAsync(
        TreeGates gates, string path, string? quest, IReviewWorld world, string? session, CancellationToken ct, IWorkplaceProcess? process,
        string? tip, string? run) =>
        await TreeGateWorkflows.GateAsync(gates, path, quest, (IOpinionWorld)world, session, ct, (WorkflowProcess?)process, tip, run).ConfigureAwait(false);

    public async Task<IWorkplaceProcess> ProcessAsync(
        TreeGates gates, DriverConfig config, string repository, string? workspace, string? quest, IReviewWorld world, CancellationToken ct, string? run) =>
        await TreeGateWorkflows.ProcessAsync(gates, config, repository, workspace, quest, world, ct, run).ConfigureAwait(false);
}
