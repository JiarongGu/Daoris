namespace Daoris.Driver;

/// <summary>
/// The landing gate's assembly (XAGENT1f, WORKFLOW1f, FABRIC-CUTOVER1d; the second-agent design §7), which the workflows own because it
/// reads the run's process, judges its workflow part and its graph's, and hands that one process to the review and the second opinion:
/// the workplace's <see cref="TreeGates"/> keeps the public calls and its own parts (the second opinion, the paths a chain changed and the
/// fork point they read from), and asks this through <see cref="IWorkplaceGateWorkflows"/>. Each step is what <see cref="TreeGates"/> did
/// before it was a port, in the same order.
/// </summary>
internal static class TreeGateWorkflows
{
    /// <summary>The whole landing gate for a tree (see <see cref="TreeGates.GateAsync"/>).</summary>
    internal static async Task<LandingGate> GateAsync(
        TreeGates gates, string path, string? quest, IOpinionWorld world, string? session, CancellationToken ct, WorkflowProcess? process,
        string? tip, string? run)
    {
        var context = gates.Context;
        var full = Path.GetFullPath(path);
        var (workspace, repository) = context.OwnerOf(full);
        // WORKFLOW1f (the workflow design §2.3, §4.6): the run's bound version where it names one, the rules live where it does not,
        // read once and handed to every part, so the look, the opinion and the landing decide by one process.
        var config = context.Config();
        process ??= await ProcessAsync(gates, config, repository, workspace, quest, world, ct, run).ConfigureAwait(false);
        var workflow = await WorkflowAsync(gates, full, process, session, ct, tip).ConfigureAwait(false);
        var review = await ReviewGate.ReadAsync(world, config, full, repository, workspace, quest, ct, process, tip, run, context.Home, session)
            .ConfigureAwait(false);
        var opinion = await gates.OpinionAsync(full, repository, workspace, quest, session, world, review, OpinionRules.Landing, ct, process, tip, run)
            .ConfigureAwait(false);
        var gate = new LandingGate(opinion, review) { Workflow = workflow };
        return await GraphAsync(gates, gate, full, repository, process, tip, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The graph's part of the gate for a run bound to a graph version (FABRIC-CUTOVER1d; D190 point 4, the cutover design §4): the run's record
    /// read as the runtime keeps it, with no lease and never written, and judged against the door's tree, the commit it would land (the tip it
    /// pinned, else its <c>HEAD</c>) and the opinion and look parts it read a moment before (<see cref="GraphGate"/>). Where it lets the work go,
    /// the gate is pinned to the commit <c>handed</c> is bound to, so every landing path lands that commit or nothing. Any other run's gate is
    /// returned as it is.
    /// </summary>
    private static async Task<LandingGate> GraphAsync(
        TreeGates gates, LandingGate gate, string full, string repository, WorkflowProcess process, string? tip, CancellationToken ct)
    {
        var context = gates.Context;
        if (process is not { Graph: { } graph, Run: { } run, Binding.Digest: { } digest, Problem: null, Unread: null }) return gate;
        var commit = tip ?? await ReviewGit.HeadAsync(full, ct).ConfigureAwait(false);
        var branches = context.Grown.Entries();
        var unit = branches.LastOrDefault(entry => string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetFullPath(context.TreeAt(entry.Workspace, entry.Repository, Path.GetFileName(entry.Branch))),
                Path.TrimEndingDirectorySeparator(full), StringComparison.OrdinalIgnoreCase))?.Unit;
        var judged = GraphGate.Judge(new GraphGateFacts(run, graph, digest, new NodeRecordFiles(context.Home, () => false).Read(RunUnits.IdOf(run)))
        {
            Unit = unit, Commit = commit, Branches = branches, OpinionLetsGo = gate.Opinion.LetsGo, ReviewLetsGo = gate.Review.LetsGo,
        });
        return gate with { Graph = judged, Tip = judged.LetsGo ? judged.Commit : gate.Tip };
    }

    /// <summary>
    /// The process a quest's work here follows (WORKFLOW1f, <see cref="WorkflowProcesses"/>): its run's bound version, or the rules live. A
    /// conversation's work, which serves no quest, reads its chat run's (CHATFLOW1c, the chat-flow design §4.2), so the view and the gate
    /// decide by one process; one with no chat run named reads the rules live.
    /// </summary>
    /// <param name="run">The chat run's key, <c>chat-&lt;first&gt;</c>, where the work serves no quest; null for none.</param>
    internal static async Task<WorkflowProcess> ProcessAsync(
        TreeGates gates, DriverConfig config, string repository, string? workspace, string? quest, IReviewWorld world, CancellationToken ct, string? run) =>
        quest is not null
            ? await WorkflowProcesses.ReadAsync(gates.Context.Home, config, world, quest, repository, workspace, ct).ConfigureAwait(false)
            : ChatRuns.IsChatRun(run)
                ? WorkflowProcesses.Read(gates.Context.Home, config, repository, workspace, run)
                : WorkflowProcess.Current(config, repository, workspace);

    /// <summary>
    /// The run's own part of the gate for a tree (WORKFLOW1f, the workflow design §4.4): the process judged, and where its kind chose
    /// a workflow that asks less of the person, the paths the chain changed read from git, from where its work leaves the line to its
    /// tip. Git is asked only there.
    /// </summary>
    /// <param name="tip">The commit to judge at, the branch's where a push is judged (FABRIC-CLOSE1h16); null reads the tree's <c>HEAD</c>.</param>
    internal static async Task<WorkflowGateState> WorkflowAsync(
        TreeGates gates, string tree, WorkflowProcess process, string? session, CancellationToken ct = default, string? tip = null)
    {
        var context = gates.Context;
        KindPathsRead? paths = null;
        if (WorkflowGate.ChecksPaths(process))
        {
            var (otherwise, steps) = WorkflowProcesses.Otherwise(context.Home, context.Config(), process.Repository, process.Binding!.Workspace);
            var lowered = steps is null
                ? [$"what {otherwise} asks of you could not be read here, so it is taken as more"]
                : Involvement.Lowers(process.Steps, steps);
            paths = new KindPathsRead(lowered, otherwise, lowered.Count == 0 || process.Binding.Kept is not null ? [] : await gates.ChangedAsync(tree, ct, tip).ConfigureAwait(false));
        }

        return WorkflowGate.Judge(process, paths) with { Session = session };
    }
}

/// <summary>The run's own part of a tree's gate, for a caller that holds the trees (the run's view reads it beside its own).</summary>
public static class SessionTreeWorkflows
{
    /// <summary>The run's own part of the gate for <paramref name="tree"/> (see <c>TreeGateWorkflows.WorkflowAsync</c>).</summary>
    public static Task<WorkflowGateState> WorkflowAsync(
        this SessionTrees trees, string tree, WorkflowProcess process, string? session, CancellationToken ct = default) =>
        TreeGateWorkflows.WorkflowAsync(trees.Gates, tree, process, session, ct);
}
