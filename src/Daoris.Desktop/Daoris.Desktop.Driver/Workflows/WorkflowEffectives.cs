using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>The choice that made a route follow its workflow: the level, and the kind with its label and paths where the route is a kind's.</summary>
/// <param name="Level">One of <see cref="WorkflowLevels"/>.</param>
public sealed record EffectiveSource(string Level, string? Kind, string? Label, IReadOnlyList<string> Paths);

/// <summary>A shipped default a saved copy is behind (WORKFLOWCLEAN1h): its id, its English name, and the graph the copy's next version would be.</summary>
public sealed record ShippedNewer(string Default, string Name, JsonNode Graph);

/// <summary>One node of an effective graph and the cell a run answers for it (<see cref="WorkflowGraphRuns.Cell"/>).</summary>
public sealed record EffectiveCell(string Node, WorkflowStep Step);

/// <summary>
/// The workflow a route follows, as the place's chart draws it (SETUPUX1b; D221): which workflow and how it was chosen, its version, or
/// Current's digest, its graph as a version saves it, and each node's cell.
/// </summary>
/// <param name="Route"><c>default</c>, <c>kind:&lt;name&gt;</c> or <c>landed</c>.</param>
/// <param name="Workflow">A saved workflow's id, or <c>current</c>.</param>
/// <param name="Name">The saved workflow's name; null under Current.</param>
/// <param name="Version">The saved version followed (the newest, which a run binds); null under Current.</param>
/// <param name="Digest">The version's digest, or under Current the digest of what it draws.</param>
/// <param name="Schema">1 for a version of steps, drawn as the graph it decodes to; 2 for a graph; null under Current and where nothing is drawn.</param>
/// <param name="Verifies">The landed route follows a verification pipeline; false under Current, which is no verification.</param>
/// <param name="Graph">The graph to draw; null where none is: the landed route under Current, or a workflow that does not read (<paramref name="Problem"/>).</param>
/// <param name="Problem">Why a saved workflow cannot be drawn, in the driver's words.</param>
/// <param name="Newer">The shipped default a copy was made from, where this build ships it changed since (<see cref="ShippedNewer"/>); else null.</param>
public sealed record WorkflowEffective(
    string Route, string Workflow, string? Name, int? Version, string? Digest, int? Schema, EffectiveSource Source, bool Verifies,
    JsonNode? Graph, IReadOnlyList<EffectiveCell> Cells, string? Problem, ShippedNewer? Newer = null)
{
    /// <summary>The limit codes the cells carry, each once, in node order.</summary>
    public IReadOnlyList<string> Limits => [.. Cells.Select(cell => cell.Step.Limit).OfType<string>().Distinct(StringComparer.Ordinal)];
}

/// <summary>
/// The graph a route actually follows (SETUPUX1b; D221, the setup design §2.3, §5 row b): <see cref="WorkflowSelection.Resolve"/> (or
/// <see cref="WorkflowSelection.ResolveLanded"/>) says which workflow, so the identity is the selection's; Current's graph is
/// <see cref="WorkflowOperations.CurrentSource"/>'s, the handler <c>new --from current:&lt;repository&gt;</c> writes through, over
/// <see cref="WorkflowCurrent.Derive"/> for a repository or for a workspace; a saved workflow is its newest version that reads, as a run binds it.
/// Pure: it reads the file and the store and writes nothing.
/// </summary>
public static class WorkflowEffectives
{
    public const string DefaultRoute = "default";
    public const string LandedRoute = "landed";
    public const string KindPrefix = "kind:";

    /// <summary>The route's effective graph.</summary>
    /// <param name="repository">The repository, or null for a workspace's own routes.</param>
    /// <param name="workspace">The repository's workspace, or null for none, read as the default workspace; the workspace itself where <paramref name="repository"/> is null.</param>
    /// <param name="route"><c>default</c>, <c>kind:&lt;name&gt;</c> or <c>landed</c>.</param>
    /// <exception cref="DriverException">The route is none of those, or names a kind its workspace does not declare.</exception>
    public static WorkflowEffective Read(
        DriverConfig config, IWorkflowStore store, string? repository, string? workspace, string route, IReadOnlyList<WorkflowPlugin> plugins)
    {
        string workflow;
        EffectiveSource source;
        var verifies = false;
        if (route == LandedRoute)
        {
            var landed = WorkflowSelection.ResolveLanded(config, repository, workspace);
            (workflow, source, verifies) = (landed.Workflow, new EffectiveSource(landed.Level, null, null, []), landed.Workflow != WorkflowSelection.Current);
        }
        else
        {
            string? kind = null;
            if (route.StartsWith(KindPrefix, StringComparison.Ordinal)) kind = route[KindPrefix.Length..];
            else if (route != DefaultRoute) throw new DriverException($"a route is `default`, `kind:<name>` or `landed` — not `{route}`.");
            var selected = WorkflowSelection.Resolve(config, repository, workspace, kind is null ? null : new WorkflowTaskChoice(kind, null));
            if (selected.Undeclared is not null) throw new DriverException(WorkflowSelection.UndeclaredInWorkspace(RemoteTarget.Workspace(workspace), selected.Undeclared));
            (workflow, source) = (selected.Workflow, new EffectiveSource(selected.Level, selected.Kind, selected.Label, selected.Paths));
        }

        var process = WorkflowProcess.Current(config, repository ?? "", workspace);
        if (workflow == WorkflowSelection.Current)
        {
            // Landed Current means no verification: no graph is drawn (WorkflowSelection.ResolveLanded).
            return route == LandedRoute
                ? new(route, workflow, null, null, null, null, source, false, null, [], null)
                : OfCurrent(config, repository, workspace, route, source, process, plugins);
        }

        return OfSaved(store, workflow, route, source, verifies, process);
    }

    private static WorkflowEffective OfCurrent(
        DriverConfig config, string? repository, string? workspace, string route, EffectiveSource source, WorkflowProcess process, IReadOnlyList<WorkflowPlugin> plugins)
    {
        var drawn = WorkflowCurrent.Derive(config, repository, workspace, plugins);
        var graph = WorkflowOperations.CurrentSource(drawn, repository ?? workspace ?? "", opinion: false, look: false).Graph!;
        var nodes = NodeGraphs.Read(graph.ToJsonString()).Graph?.Nodes ?? [];
        // Current's own cell where it draws one (its ids are its kinds, as the graph's nodes are), else the cell a run answers for the node.
        var cells = nodes.Select(node => new EffectiveCell(
            node.Id, drawn.Steps.FirstOrDefault(step => step.Id == node.Id) ?? WorkflowGraphRuns.Cell(node, process, source.Level))).ToList();
        return new(route, WorkflowSelection.Current, null, null, drawn.Version, null, source, false, graph, cells, null);
    }

    private static WorkflowEffective OfSaved(IWorkflowStore store, string id, string route, EffectiveSource source, bool verifies, WorkflowProcess process)
    {
        WorkflowEffective Unread(string problem) => new(route, id, null, null, null, null, source, verifies, null, [], problem);
        var found = WorkflowNamed.IsId(id) ? store.Load(id) : null;
        if (found is null) return Unread($"no workflow `{id}` is saved here any more.");
        if (found.Read.Problem is { } unread) return Unread($"`{id}` cannot be read: {unread}");
        // The newest version, as a run's bind takes it: one that does not read is the problem every run will hold on, not a reason to draw an older.
        var newest = found.Read.Versions.LastOrDefault();
        if (newest is null) return Unread($"`{id}` keeps no version that reads here.");
        if (newest.Steps is null && newest.Graph is null) return Unread($"`{id}` v{newest.Version} cannot be read here: {newest.Problem}");

        // A graph version is drawn as the file holds it; a version of steps as the graph it decodes to (no run binds that, D190 point 1).
        var (graph, nodes) = newest.Graph is { } held
            ? (found.File!["versions"]![found.Read.Versions.ToList().IndexOf(newest)]!["graph"]!.DeepClone(), held.Nodes)
            : ((JsonNode)WorkflowGraphs.Saved(newest.Steps!), WorkflowGraphs.Graph(newest.Steps!).Nodes);
        var cells = nodes.Select(node => new EffectiveCell(node.Id, WorkflowGraphRuns.Cell(node, process, source.Level))).ToList();
        return new(route, id, found.Read.Name, newest.Version, newest.Digest, newest.Graph is not null ? WorkflowNamed.GraphSchema : 1, source, verifies, graph, cells, null,
            WorkflowDefaults.NewerThan(newest.From, newest.Graph));
    }
}
