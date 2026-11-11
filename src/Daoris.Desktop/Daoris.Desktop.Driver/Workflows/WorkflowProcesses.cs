using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The process each gate reads for a chain's work in a repository: the run's bound version where it names one, the rules live where it does not.
/// </summary>
/// <remarks>
/// <para><b>One read per decision.</b> The landing gate reads it once (<see cref="SessionTreeGates.GateAsync"/>) and hands it to every
/// part, the landing among them; the look's automatic landing and the second opinions owed read it once per entry, and hand it on.
/// Never a second copy per door.</para>
///
/// <para><b>A step chooses among what is declared, never beyond it</b>: <see cref="WorkflowProcess.Of"/>, the rule.</para>
///
/// <para><b>Never swapped</b>: a named workflow that cannot be read here holds its run, saying why, and nothing else is read
/// in its place.</para>
///
/// <para><b>Reads over ports</b>: the reads, written against <see cref="IWorkflowStore"/> and
/// <see cref="IWorkflowRunStore"/>; the rules they apply are <see cref="WorkflowProcess"/>'s, in Workflows' Domain, which every
/// gate calls by its own name (<see cref="WorkflowProcess.Current"/>, <see cref="WorkflowProcess.Of"/>). A caller that holds
/// only the home reads by it.</para>
/// </remarks>
public static class WorkflowProcesses
{
    /// <summary>
    /// The process of a run: its binding read from the home, and its graph version from the store by the number the run
    /// kept, which must still be the version it bound (a version is never edited). Work with no binding follows no workflow and is held; a
    /// workflow that cannot be read here holds the run with why. A graph version is read from its gate nodes, its
    /// <c>claimed</c> node's options kept for its sessions (<see cref="WorkflowProcess.Claimed"/>).
    /// </summary>
    /// <param name="run">The run: the chain's first quest in the repository (<see cref="WorkflowRunBindings.RunOf"/>); null for none.</param>
    /// <param name="accepted">
    /// What a door read of the repository's accepted integration declaration: handed to the pure check of a graph's
    /// <c>integrate</c>, whose <c>done</c> and <c>steward</c> need a gate named. Null where the caller did not read it, and then that one fact is
    /// not judged; the facts the config carries always are.
    /// </param>
    public static WorkflowProcess Read(
        string home, DriverConfig config, string repository, string? workspace, string? run, AcceptedGates? accepted = null) =>
        Read(WorkflowPorts.Store(home), WorkflowPorts.Runs(home), config, repository, workspace, run, accepted);

    /// <summary>The process of a run, its binding read from <paramref name="runs"/> and its version from <paramref name="store"/>.</summary>
    /// <param name="run">The run: the chain's first quest in the repository (<see cref="WorkflowRunBindings.RunOf"/>); null for none.</param>
    /// <param name="accepted">The accepted declaration's gates as a door read them; null where not read.</param>
    public static WorkflowProcess Read(
        IWorkflowStore store, IWorkflowRunStore runs, DriverConfig config, string repository, string? workspace, string? run,
        AcceptedGates? accepted = null)
    {
        if (run is not null && runs.Read(run) is { } binding) return Bound(store, config, repository, workspace, run, binding, accepted);
        var declared = WorkflowProcess.Declared(config, repository, workspace) with { Run = run };
        // A damaged record may name gates we cannot safely replace.
        return run is not null && runs.Bound(run)
            ? declared with { Unread = "its stored run binding could not be read." }
            : declared with { Problem = WorkflowProcess.NoWorkflow(repository) };
    }

    /// <summary>
    /// The process of a run whose binding its start's bind came to: read from that binding, so
    /// neither the service's quests nor the run's file is read again for the start, and its first session is told and takes what its run
    /// is bound to. No binding, where the bind named no run, follows no workflow.
    /// </summary>
    /// <param name="run">The run the bind named; null for none.</param>
    /// <param name="binding">Its binding as the bind kept or read it; null where it named no run.</param>
    public static WorkflowProcess Bound(
        string home, DriverConfig config, string repository, string? workspace, string? run, WorkflowRunBinding? binding, AcceptedGates? accepted = null) =>
        Bound(WorkflowPorts.Store(home), config, repository, workspace, run, binding, accepted);

    /// <summary>The process of a run from its binding, a named workflow's version read from <paramref name="store"/>.</summary>
    public static WorkflowProcess Bound(
        IWorkflowStore store, DriverConfig config, string repository, string? workspace, string? run, WorkflowRunBinding? binding,
        AcceptedGates? accepted = null)
    {
        var declared = WorkflowProcess.Declared(config, repository, workspace) with { Run = run };
        if (binding is null) return declared with { Problem = WorkflowProcess.NoWorkflow(repository) };

        // Held before any step: no opinion is asked for a run whose workflow cannot be read.
        var held = declared with { Binding = binding, Opinion = null };
        if (binding.Problem is { } problem) return held with { Problem = problem, Coded = binding.Coded };
        if (binding.Version is not { } number) return Held(held, BindingProblem.ForNoVersion(binding.Workflow));
        var found = WorkflowNamed.IsId(binding.Workflow) ? store.Load(binding.Workflow) : null;
        if (found is null) return Held(held, BindingProblem.ForNotSaved(binding.Workflow));
        if (found.Read.Problem is { } unread) return Held(held, BindingProblem.ForUnread(binding.Workflow, unread));
        var version = found.Read.Versions.FirstOrDefault(each => each.Version == number);
        if (version is null) return Held(held, BindingProblem.ForVersionUnread(binding.Workflow, number, "that version is no longer saved here."));
        if (version is not { Graph: { } graph, Digest: { } identity })
        {
            return Held(held, BindingProblem.ForVersionUnread(binding.Workflow, number, version.Problem ?? "it is no graph"));
        }

        if (!string.Equals(identity, binding.Digest, StringComparison.Ordinal))
        {
            return held with
            {
                Problem = $"`{binding.Workflow}` v{number} is not the version its run was bound to: its graph changed since, and a version is never edited.",
            };
        }

        return Graphed(config, repository, workspace, run, binding, number, graph, held, accepted);
    }

    private static WorkflowProcess Held(WorkflowProcess held, BindingProblem problem) => held with { Problem = problem.Said, Coded = problem };

    /// <summary>
    /// A graph version read as a process: its <c>land</c> carried as the intent it binds
    /// (<see cref="LandNodes.RuleOf"/>) and its gates read from the node options (<see cref="GraphGates"/>), each choosing among what the
    /// repository declares, and the graph carried beside, as its file holds it and never decoded, which the landing's graph part and the run's
    /// view read from the run's record. Its steps are its person part (<see cref="Involvement.StepsOf(NodeGraph)"/>). A graph with no <c>land</c>
    /// that ends at a <c>review</c> is work in place, accepted there and landed by nothing (<see cref="WorkflowProcess.InPlace"/>); any other
    /// lands nothing, so no chain's work follows it; a gate node whose option does not read holds the run, saying which; so does a graph that
    /// closes its quests (<c>quest.close</c>), whose close this build does not make for a run yet. Its
    /// <c>claimed</c> node's options are its sessions' either way. A graph whose <c>integrate</c> or plugin nodes the
    /// repository's declarations cannot carry (<see cref="IntegrateCarry.Cannots"/>) holds the run too, each case its sentence.
    /// </summary>
    private static WorkflowProcess Graphed(
        DriverConfig config, string repository, string? workspace, string? run, WorkflowRunBinding binding, int number, NodeGraph graph,
        WorkflowProcess held, AcceptedGates? accepted)
    {
        var claimed = graph.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Claimed)?.Options ?? new Dictionary<string, string>();
        held = held with { Claimed = claimed, Graph = graph };
        var landNode = graph.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Land);
        var inPlace = landNode is null && graph.Nodes.Any(node => node.Id == graph.End && node.Kind == NodeKinds.Review);
        if (landNode is null && !inPlace)
        {
            // A verification pipeline's graph lands nothing, so no chain's work follows it.
            return held with { Problem = $"`{binding.Workflow}` v{number}'s graph holds no landing (`{NodeKinds.Land}`), so no chain's work lands by it." };
        }

        // Over what the place declares: the held process carries no opinion, and a gate node chooses among the declared reviewers.
        var gates = GraphGates.Of(graph, WorkflowProcess.Declared(config, repository, workspace));
        if (gates.Problem is { } unread) return held with { Problem = $"`{binding.Workflow}` v{number} cannot be read here: v{number}, {unread}" };
        if (graph.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.QuestClose) is { } close)
        {
            return held with
            {
                Problem = $"`{binding.Workflow}` v{number}'s graph closes its quests at `{close.Id}` (`{NodeKinds.QuestClose}`), and this build "
                    + "does not close a run's quests yet, so it cannot start a run of it.",
            };
        }

        if (landNode is null)
        {
            return WorkflowProcess.InPlace(config, repository, workspace, binding, gates) with
            {
                Run = run, Steps = Involvement.StepsOf(graph), Claimed = claimed, Graph = graph,
            };
        }

        var bound = LandNodes.LandingOf(landNode, LandNodes.Reviewed(graph), binding.Land);
        var process = WorkflowProcess.Of(config, repository, workspace, binding, gates, bound) with
        {
            Run = run, Steps = Involvement.StepsOf(graph), Claimed = claimed, Graph = graph,
        };
        var cannots = IntegrateCarry.Cannots(repository, graph, config.LinePolicyOf(repository, workspace), process.Landing, accepted);
        return cannots.Count == 0 ? process : process with { Problem = string.Join(' ', cannots) };
    }

    /// <summary>
    /// The process of a quest's work in <paramref name="repository"/>: its run named by the chain's first quest there,
    /// read from the service's quests. Where they do not answer, the quest's own id is tried, since a chain's first quest names its
    /// run; and where that names none, the work is held unread only if a named workflow is bound to any run here, since one might
    /// govern it.
    /// </summary>
    public static Task<WorkflowProcess> ReadAsync(
        string home, DriverConfig config, IReviewWorld world, string quest, string repository, string? workspace, CancellationToken ct = default) =>
        ReadAsync(WorkflowPorts.Store(home), WorkflowPorts.Runs(home), config, world, quest, repository, workspace, ct);

    /// <summary>The process of a quest's work in <paramref name="repository"/>, its chain read from the quests <paramref name="quests"/> answers.</summary>
    public static Task<WorkflowProcess> ReadAsync(
        string home, DriverConfig config, Func<CancellationToken, Task<IReadOnlyList<QuestView>>> quests, string quest, string repository,
        string? workspace, CancellationToken ct = default) =>
        ReadAsync(WorkflowPorts.Store(home), WorkflowPorts.Runs(home), config, quests, quest, repository, workspace, ct);

    /// <summary>The process of a quest's work in <paramref name="repository"/>, its run's binding and version read through the ports.</summary>
    public static Task<WorkflowProcess> ReadAsync(
        IWorkflowStore store, IWorkflowRunStore runs, DriverConfig config, IReviewWorld world, string quest, string repository,
        string? workspace, CancellationToken ct = default) =>
        ReadAsync(store, runs, config, world.QuestsAsync, quest, repository, workspace, ct);

    /// <summary>The process of a quest's work in <paramref name="repository"/>, its chain read from the quests <paramref name="quests"/> answers.</summary>
    public static async Task<WorkflowProcess> ReadAsync(
        IWorkflowStore store, IWorkflowRunStore runs, DriverConfig config, Func<CancellationToken, Task<IReadOnlyList<QuestView>>> quests,
        string quest, string repository, string? workspace, CancellationToken ct = default)
    {
        var id = quest.TrimStart('#');
        try
        {
            var chain = ReviewGate.ChainOf(await quests(ct).ConfigureAwait(false), id);
            return Read(store, runs, config, repository, workspace, WorkflowRunBindings.RunOf(chain, repository) ?? id);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or InvalidOperationException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            var own = Read(store, runs, config, repository, workspace, WorkflowRunBindings.IsRunId(id) ? id : null);
            if (own.Binding is not null || !runs.AnyNamed(repository)) return own;
            return own with { Unread = $"its chain could not be read, so whether a named workflow governs it is not known: {error.Message.TrimEnd().TrimEnd('.')}." };
        }
    }

    /// <summary>
    /// The process a concluded record's run follows, for the second opinion owed at its done: read for its tree's owner where this
    /// home opened the tree, else the quest's repository in the workspace the record names. Never throws: a run that could not be read is none.
    /// </summary>
    public static async Task<WorkflowProcess?> ConcludedAsync(
        string home, DriverConfig config, IReviewWorld world, QuestView quest, string tree, string? workspace, CancellationToken ct = default)
    {
        try
        {
            var trees = new SessionTrees(home);
            var (where, repository) = trees.Holds(tree) ? trees.Owner(tree) : (RemoteTarget.Workspace(workspace), quest.To);
            return await ReadAsync(home, config, world, quest.Id, repository, where, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or DriverException)
        {
            return null;
        }
    }

    /// <summary>
    /// The workflow a run would follow without its task's kind: resolved again with the task's kind left out, as
    /// the choices stand now. Its newest graph version's person part, read from its gate and go-ahead nodes
    /// (<see cref="Involvement.StepsOf(NodeGraph)"/>); null where none is chosen or it does not read.
    /// </summary>
    /// <returns>Its name as said, and its steps, or null steps where they could not be read.</returns>
    /// <param name="kind">A kind its workspace declares, to read what a task of that kind would follow; null for none.</param>
    public static (string Name, IReadOnlyList<NamedStep>? Steps) Otherwise(
        string home, DriverConfig config, string repository, string? workspace, string? kind = null) =>
        Otherwise(WorkflowPorts.Store(home), config, repository, workspace, kind);

    /// <summary>The workflow a run would follow without its task's kind, a named one read from <paramref name="store"/>.</summary>
    /// <param name="kind">A kind its workspace declares, to read what a task of that kind would follow; null for none.</param>
    public static (string Name, IReadOnlyList<NamedStep>? Steps) Otherwise(
        IWorkflowStore store, DriverConfig config, string repository, string? workspace, string? kind = null)
    {
        var selected = WorkflowSelection.Resolve(config, repository, workspace, kind is null ? null : new WorkflowTaskChoice(kind, null));
        if (selected.Workflow == WorkflowSelection.Current) return ("no workflow", null);
        var found = WorkflowNamed.IsId(selected.Workflow) ? store.Load(selected.Workflow) : null;
        var newest = found?.Read.Versions.LastOrDefault(each => each.Graph is not null);
        return newest is null ? ($"`{selected.Workflow}`", null) : ($"`{selected.Workflow}` v{newest.Version}", Involvement.StepsOf(newest.Graph!));
    }
}
