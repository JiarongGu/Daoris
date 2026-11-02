using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>A run bound at the home, with whether its unit is still open (its quest, or a quest of its chain, is <c>Open</c> or <c>Taken</c>) and that quest's id.</summary>
public sealed record LegacyRun(WorkflowRunBinding Binding, bool Open, string? Quest);

/// <summary>What the conversion reads on this machine: the driver's file as it stands, the registry, the saved workflows and the bound runs.</summary>
public sealed record ConvertWorld(DriverConfig Config, string Home, IReadOnlyList<RepoView> Registry, IWorkflowStore Store, IReadOnlyList<LegacyRun> Runs);

/// <summary>A saved workflow whose newest version holds an old shape: <c>steps</c>, <c>handed</c> or <c>land-accept</c>.</summary>
public sealed record LegacyVersionRow(string Workflow, int Version, string Shape);

/// <summary>A place that follows Current (item 4) with the old shapes it holds of its own (item 5), and whether a work cycle keeps it there (item 6).</summary>
public sealed record LegacyPlaceRow(string Place, bool IsWorkspace, string Workspace, IReadOnlyList<string> Shapes, bool WorkCycle, bool OpenUnits);

/// <summary>A run still open on an old engine (item 7), with the door that ends it.</summary>
public sealed record LegacyRunRow(string Run, string Repository, string Workflow, int? Version, string Engine, string Door);

/// <summary>A rule no place follows any more (item 8), or one a place's open Current unit keeps in force.</summary>
public sealed record LegacyLeftover(string Place, bool IsWorkspace, IReadOnlyList<string> Keys, string Why);

/// <summary>The census (WORKFLOWCLEAN1 1e; the cleanup design §4.1): everything old that the home still holds, in the design's eight items.</summary>
public sealed record LegacyCensus(
    IReadOnlyList<LegacyVersionRow> Versions, IReadOnlyList<LegacyPlaceRow> Places, IReadOnlyList<LegacyRunRow> Runs, IReadOnlyList<LegacyLeftover> Leftovers)
{
    /// <summary>The steps newests (item 1).</summary>
    public IReadOnlyList<LegacyVersionRow> Steps => [.. Versions.Where(each => each.Shape == "steps")];

    /// <summary>The <c>handed</c> newests (item 2).</summary>
    public IReadOnlyList<LegacyVersionRow> Handed => [.. Versions.Where(each => each.Shape == "handed")];

    /// <summary>The <c>land</c> newests saved with an <c>accept</c> (item 3).</summary>
    public IReadOnlyList<LegacyVersionRow> LandAccept => [.. Versions.Where(each => each.Shape == "land-accept")];

    /// <summary>The places a work cycle keeps on Current (item 6).</summary>
    public IReadOnlyList<LegacyPlaceRow> WorkCycles => [.. Places.Where(each => each.WorkCycle)];

    /// <summary>Whether items 1 to 8 are all empty: nothing old is left to convert, hold or end.</summary>
    public bool Ended => Versions.Count == 0 && Places.Count == 0 && Runs.Count == 0 && Leftovers.Count == 0;
}

/// <summary>The census, pure: it reads the world and writes nothing.</summary>
public static class WorkflowLegacy
{
    /// <summary>The terminal word that ends an open unit, which a run's row carries as its door.</summary>
    public static string DoorOf(string? quest) => quest is null ? "daoris-driver quest abandon <quest>" : $"daoris-driver quest abandon {quest}";

    /// <summary>
    /// The shape of a version if it is an old one, else null. A <c>land</c> saved with an <c>accept</c> does not read as a graph (it is held, never
    /// landing), so it is found in the file's own text.
    /// </summary>
    public static string? ShapeOf(WorkflowFileFound found, NamedVersion version)
    {
        if (version.Steps is not null) return "steps";
        if (version.Graph is { } graph) return graph.Nodes.Any(node => node.Kind == NodeKinds.Handed) ? "handed" : null;
        return RawGraph(found, version)?["nodes"] is JsonArray nodes
            && nodes.Any(node => (string?)node?["kind"] == NodeKinds.Land && node?["options"]?[LandNodes.AcceptOption] is not null) ? "land-accept" : null;
    }

    private static JsonObject? RawGraph(WorkflowFileFound found, NamedVersion version) =>
        found.File?["versions"]?.AsArray().FirstOrDefault(each => (int?)each?["version"] == version.Version)?["graph"] as JsonObject;

    /// <summary>The places this home has: its registry's repositories and the workspaces they and the file name, each once.</summary>
    public static (IReadOnlyList<(string Repository, string Workspace)> Repositories, IReadOnlyList<string> Workspaces) PlacesOf(DriverConfig config, IReadOnlyList<RepoView> registry)
    {
        var repositories = registry.Select(row => (row.Repository, RemoteTarget.Workspace(row.Workspace))).ToList();
        foreach (var name in config.Landings.Keys.Concat(config.Workflows.Keys).Concat(config.SessionChecks.Keys).Concat(config.Access.Keys).Concat(config.WorkCycles.Keys))
        {
            if (repositories.All(each => !string.Equals(each.Repository, name, StringComparison.OrdinalIgnoreCase))) repositories.Add((name, RemoteTarget.Workspace(null)));
        }

        var workspaces = repositories.Select(each => each.Item2)
            .Concat(config.WorkspaceLandings.Keys).Concat(config.WorkspaceWorkflows.Keys).Concat(config.WorkspaceAccess.Keys).Concat(config.WorkspaceWorkCycles.Keys)
            .Select(RemoteTarget.Workspace).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
        return ([.. repositories.OrderBy(each => each.Repository, StringComparer.OrdinalIgnoreCase)], workspaces);
    }

    /// <summary>The engine an open run is on: <c>current</c>, <c>steps</c> or <c>handed</c>; null for a run on a graph that lands by its <c>land</c>.</summary>
    public static string? EngineOf(IWorkflowStore store, WorkflowRunBinding binding)
    {
        if (binding.IsCurrent) return WorkflowSelection.Current;
        if (store.Load(binding.Workflow) is not { } found || found.Read.Versions.FirstOrDefault(each => each.Version == binding.Version) is not { } version) return null;
        return ShapeOf(found, version) is { } shape && shape is "steps" or "handed" ? shape : null;
    }

    /// <summary>
    /// Items 1 to 8 over <paramref name="world"/>. <paramref name="held"/> names the places an applied conversion left their rules at because a
    /// Current unit was open then: once none is open they are leftovers a later plan offers to remove.
    /// </summary>
    public static LegacyCensus Census(ConvertWorld world, IReadOnlySet<string>? held = null)
    {
        var config = world.Config;
        var versions = new List<LegacyVersionRow>();
        foreach (var found in world.Store.List())
        {
            if (found.Read is not { Problem: null, Verifies: false } read || read.Versions.Count == 0) continue;
            var newest = read.Versions[^1];
            if (ShapeOf(found, newest) is { } shape) versions.Add(new(found.Id, newest.Version, shape));
        }

        var runs = new List<LegacyRunRow>();
        foreach (var run in world.Runs.Where(each => each.Open))
        {
            if (EngineOf(world.Store, run.Binding) is { } engine) runs.Add(new(run.Binding.Run, run.Binding.Repository, run.Binding.Workflow, run.Binding.Version, engine, DoorOf(run.Quest)));
        }

        var (repositories, workspaces) = PlacesOf(config, world.Registry);
        bool Open(string place, bool isWorkspace) => world.Runs.Any(run => run.Open && run.Binding.IsCurrent
            && string.Equals(isWorkspace ? RemoteTarget.Workspace(run.Binding.Workspace) : run.Binding.Repository, place, StringComparison.OrdinalIgnoreCase));
        var places = new List<LegacyPlaceRow>();
        var leftovers = new List<LegacyLeftover>();
        var facts = workspaces.Select(workspace => LegacyReader.Workspace(config, workspace)).Concat(repositories.Select(each => LegacyReader.Repository(config, each.Repository, each.Item2)));
        foreach (var each in facts)
        {
            var open = Open(each.Place, each.IsWorkspace);
            if (each.OnCurrent)
            {
                places.Add(new(each.Place, each.IsWorkspace, each.Workspace, each.Shapes, each.WorkCycle, open));
                continue;
            }

            var keys = each.RemovableKeys;
            if (keys.Count == 0) continue;
            if (open) leftovers.Add(new(each.Place, each.IsWorkspace, keys, "a Current unit is open here and reads these rules until it ends."));
            else if (held?.Contains(Held(each)) == true) leftovers.Add(new(each.Place, each.IsWorkspace, keys, "kept for a unit that has ended; the plan offers to remove them."));
            else leftovers.Add(new(each.Place, each.IsWorkspace, keys, "no place follows these rules any more."));
        }

        return new(versions, places, runs, leftovers);
    }

    /// <summary>The key a held place goes by in a receipt: <c>repository:name</c> or <c>workspace:name</c>.</summary>
    public static string Held(LegacyFacts facts) => (facts.IsWorkspace ? "workspace:" : "repository:") + facts.Place.ToLowerInvariant();

    /// <summary>A saved workflow's newest graph as JSON: a steps version drawn as <see cref="WorkflowGraphs.Saved"/> saves it, a graph version as its file holds it.</summary>
    public static JsonObject? GraphOf(WorkflowFileFound found, NamedVersion version)
    {
        if (version.Steps is { } steps) return WorkflowGraphs.Saved(steps);
        return RawGraph(found, version)?.DeepClone() as JsonObject;
    }
}
