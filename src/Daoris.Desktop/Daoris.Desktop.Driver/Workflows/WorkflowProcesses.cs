using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The process each gate reads for a chain's work in a repository (WORKFLOW1f, D157 points 4, 6 and 11; the workflow design §2.3,
/// §2.6, §4.5–§4.6): the run's bound version where it names one, the rules live where it does not.
/// </summary>
/// <remarks>
/// <para><b>One read per decision.</b> The landing gate reads it once (<see cref="SessionTrees.GateAsync"/>) and hands it to every
/// part, the landing among them; the look's automatic landing and the second opinions owed read it once per entry, and hand it on.
/// Never a second copy per door.</para>
///
/// <para><b>A step chooses among what is declared, never beyond it</b> (§2.3): <see cref="WorkflowProcess.Of"/>, the rule.</para>
///
/// <para><b>Never swapped</b> (§4.5): a named workflow that cannot be read here holds its run, saying why, and nothing else is read
/// in its place.</para>
///
/// <para><b>Reads over ports</b> (STRUCT3b, D166; FLAT1b-i): the reads, written against <see cref="IWorkflowStore"/> and
/// <see cref="IWorkflowRunStore"/>; the rules they apply are <see cref="WorkflowProcess"/>'s, in Workflows' Domain, which every
/// gate calls by its own name (<see cref="WorkflowProcess.Current"/>, <see cref="WorkflowProcess.Of"/>). A caller that holds
/// only the home reads by it, as every module did before.</para>
/// </remarks>
public static class WorkflowProcesses
{
    /// <summary>
    /// The process of a run (design §2.6, §4.5): its binding read from the home, and a named workflow's version from the store by the
    /// number the run kept, which must still be the version it bound (a version is never edited). Current, or no binding, is the
    /// rules live; a named workflow that cannot be read here holds the run with why. A graph version is held from the steps' gates, its
    /// <c>claimed</c> node's options kept for its sessions (<see cref="WorkflowProcess.Claimed"/>, FABRIC-PIPELINE1b3).
    /// </summary>
    /// <param name="run">The run: the chain's first quest in the repository (<see cref="WorkflowRunBindings.RunOf"/>); null for none.</param>
    public static WorkflowProcess Read(string home, DriverConfig config, string repository, string? workspace, string? run) =>
        Read(WorkflowPorts.Store(home), WorkflowPorts.Runs(home), config, repository, workspace, run);

    /// <summary>The process of a run, its binding read from <paramref name="runs"/> and its version from <paramref name="store"/>.</summary>
    /// <param name="run">The run: the chain's first quest in the repository (<see cref="WorkflowRunBindings.RunOf"/>); null for none.</param>
    public static WorkflowProcess Read(
        IWorkflowStore store, IWorkflowRunStore runs, DriverConfig config, string repository, string? workspace, string? run)
    {
        if (run is not null && runs.Read(run) is { } binding) return Bound(store, config, repository, workspace, run, binding);
        var current = WorkflowProcess.Current(config, repository, workspace) with { Run = run };
        // Absence keeps legacy work on Current; a damaged record may name gates we cannot safely replace.
        return run is not null && runs.Bound(run) ? current with { Unread = "its stored run binding could not be read." } : current;
    }

    /// <summary>
    /// The process of a run whose binding its start's bind came to (FABRIC-PIPELINE1b3; the cutover design §8): read from that binding, so
    /// neither the service's quests nor the run's file is read again for the start, and its first session is told and takes what its run
    /// was bound to. No binding, where the bind named no run, is the rules live.
    /// </summary>
    /// <param name="run">The run the bind named; null for none.</param>
    /// <param name="binding">Its binding as the bind kept or read it; null where it named no run.</param>
    public static WorkflowProcess Bound(string home, DriverConfig config, string repository, string? workspace, string? run, WorkflowRunBinding? binding) =>
        Bound(WorkflowPorts.Store(home), config, repository, workspace, run, binding);

    /// <summary>The process of a run from its binding, a named workflow's version read from <paramref name="store"/>.</summary>
    public static WorkflowProcess Bound(
        IWorkflowStore store, DriverConfig config, string repository, string? workspace, string? run, WorkflowRunBinding? binding)
    {
        var current = WorkflowProcess.Current(config, repository, workspace) with { Run = run };
        if (binding is null) return current;
        if (binding.IsCurrent) return current with { Binding = binding };

        // Held before any step: no opinion is asked for a run whose workflow cannot be read.
        var held = current with { Binding = binding, Opinion = null };
        if (binding.Problem is { } problem) return held with { Problem = problem };
        if (binding.Version is not { } number) return held with { Problem = $"its run names no version of `{binding.Workflow}`." };
        var found = WorkflowNamed.IsId(binding.Workflow) ? store.Load(binding.Workflow) : null;
        if (found is null) return held with { Problem = $"no workflow `{binding.Workflow}` is saved here any more." };
        if (found.Read.Problem is { } unread) return held with { Problem = $"`{binding.Workflow}` cannot be read: {unread}" };
        var version = found.Read.Versions.FirstOrDefault(each => each.Version == number);
        if (version is null) return held with { Problem = $"`{binding.Workflow}` v{number} is no longer saved here." };
        if (version.Steps is null)
        {
            // FABRIC-PIPELINE1b3 (D192's FABRIC-PIPELINE1b2 note, point 5): a graph version is held from the steps' gates as before, and its
            // `claimed` node's options are its sessions' (the pipeline design §3.1), read from the version as its file holds it, as GraphRuns
            // reads it, never decoded, and only where it is the version its run bound. FABRIC-CUTOVER1e: its graph is carried beside, which the
            // run's view draws from the run's record.
            var unrun = held with { Problem = $"`{binding.Workflow}` v{number} cannot be read here: {version.Unrun}" };
            return version is { Graph: { } graph, Digest: { } identity } && string.Equals(identity, binding.Digest, StringComparison.Ordinal)
                ? unrun with
                {
                    Claimed = graph.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Claimed)?.Options ?? new Dictionary<string, string>(),
                    Graph = graph,
                }
                : unrun;
        }
        if (binding.Digest is { } digest && !string.Equals(digest, version.Digest, StringComparison.Ordinal))
        {
            return held with
            {
                Problem = $"`{binding.Workflow}` v{number} is not the version its run was bound to: its steps changed since, and a version "
                    + "is never edited.",
            };
        }

        return WorkflowProcess.Of(config, repository, workspace, binding, version.Steps) with { Run = run };
    }

    /// <summary>
    /// The process of a quest's work in <paramref name="repository"/> (design §4.5): its run named by the chain's first quest there,
    /// read from the service's quests. Where they do not answer, the quest's own id is tried, since a chain's first quest names its
    /// run; and where that names none, the work is held unread only if a named workflow is bound to any run here, since one might
    /// govern it.
    /// </summary>
    public static Task<WorkflowProcess> ReadAsync(
        string home, DriverConfig config, IReviewWorld world, string quest, string repository, string? workspace, CancellationToken ct = default) =>
        ReadAsync(WorkflowPorts.Store(home), WorkflowPorts.Runs(home), config, world, quest, repository, workspace, ct);

    /// <summary>The process of a quest's work in <paramref name="repository"/>, its run's binding and version read through the ports.</summary>
    public static async Task<WorkflowProcess> ReadAsync(
        IWorkflowStore store, IWorkflowRunStore runs, DriverConfig config, IReviewWorld world, string quest, string repository,
        string? workspace, CancellationToken ct = default)
    {
        var id = quest.TrimStart('#');
        try
        {
            var chain = ReviewGate.ChainOf(await world.QuestsAsync(ct).ConfigureAwait(false), id);
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
    /// The workflow a run would follow without its task's kind (design §4.4): §4.1 resolved again with the task's kind left out, as
    /// the choices stand now. A named workflow's newest version that reads, or Current drawn; null where the named one does not read.
    /// A graph version is a version that reads (FABRIC-CUTOVER1e): its steps are its person part, read from its gate and go-ahead nodes
    /// (<see cref="Involvement.StepsOf(NodeGraph)"/>).
    /// </summary>
    /// <returns>Its name as said, and its steps, or null steps where they could not be read.</returns>
    /// <param name="kind">A kind its workspace declares, to read what a task of that kind would follow (WORKFLOW1i-b's room); null for none.</param>
    public static (string Name, IReadOnlyList<NamedStep>? Steps) Otherwise(
        string home, DriverConfig config, string repository, string? workspace, string? kind = null) =>
        Otherwise(WorkflowPorts.Store(home), config, repository, workspace, kind);

    /// <summary>The workflow a run would follow without its task's kind, a named one read from <paramref name="store"/>.</summary>
    /// <param name="kind">A kind its workspace declares, to read what a task of that kind would follow; null for none.</param>
    public static (string Name, IReadOnlyList<NamedStep>? Steps) Otherwise(
        IWorkflowStore store, DriverConfig config, string repository, string? workspace, string? kind = null)
    {
        var selected = WorkflowSelection.Resolve(config, repository, workspace, kind is null ? null : new WorkflowTaskChoice(kind, null));
        if (selected.Workflow == WorkflowSelection.Current)
        {
            return ("Current", Involvement.StepsOf(WorkflowCurrent.Derive(config, repository, workspace, [])));
        }

        var found = WorkflowNamed.IsId(selected.Workflow) ? store.Load(selected.Workflow) : null;
        var newest = found?.Read.Versions.LastOrDefault(each => each.Steps is not null || each.Graph is not null);
        return newest is null ? ($"`{selected.Workflow}`", null)
            : ($"`{selected.Workflow}` v{newest.Version}", newest.Steps ?? Involvement.StepsOf(newest.Graph!));
    }
}
