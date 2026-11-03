using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>What the workflow step of the migration did: the file as the step leaves it, the workflows it saved, and each change said once.</summary>
public sealed record WorkflowMigrated(DriverConfig Config, IReadOnlyList<string> Saved, IReadOnlyList<string> Said)
{
    /// <summary>Whether the step changed nothing.</summary>
    public bool Empty => Saved.Count == 0 && Said.Count == 0;
}

/// <summary>
/// The workflow step of the release's one migration (D232 point 2): every place converted to a saved graph once, at the release's first start.
/// A saved workflow whose newest version is a list of steps, holds a <c>handed</c>, or a <c>land</c> with an <c>accept</c> gets the next version
/// as a graph; a place that followed the rules (nothing named, or <c>current</c>) follows the home default or a saved copy of what its rules drew;
/// a rule's <c>integrate</c> becomes its line's policy; a converted place's access <c>landing</c> pause and session checks go, since its graph
/// carries them. The home's migration takes the backup, calls <see cref="Convert"/>, and writes <c>driver.json</c> once; this step writes the
/// workflow files it saves. A version the save refuses is left as it is, said.
/// </summary>
public static class WorkflowMigration
{
    private const string Door = "migration";

    /// <summary>The step over <paramref name="config"/> as the home's file holds it and <paramref name="registry"/>, the service's repositories.</summary>
    public static WorkflowMigrated Convert(string home, DriverConfig config, IReadOnlyList<RepoView> registry)
    {
        var store = new PlannedWorkflows(home);
        var old = new MigratedPlaces(config);
        var (repositories, workspaces) = old.Of(registry);
        var places = workspaces.Select(old.Workspace).Concat(repositories.Select(each => old.Repository(each.Repository, each.Workspace))).ToList();
        var saved = new List<string>();
        var said = new List<string>();
        var at = PlannedWorkflows.At;

        bool Planned(string id, string name, string why, JsonObject graph, WorkflowFrom? from)
        {
            var found = store.Load(id);
            try
            {
                var newest = found?.Read.Problem is null ? found?.Read.Versions.LastOrDefault() : null;
                var planned = WorkflowOperations.PlanGraph(home, found, id, newest, name, Door, at, graph, from);
                if (planned.Same) return true;
                store.Write(id, planned.Plan.Result!);
                if (!saved.Contains(id)) saved.Add(id);
                said.Add($"`{id}` v{planned.Next!.Version}: {why}");
                return true;
            }
            catch (ChoiceWords refused)
            {
                said.Add($"`{id}` is left as it is: {refused.Message} Edit it in the workflow editor.");
                return false;
            }
        }

        string? ShapeOfWorkflow(string id) =>
            store.Load(id) is { Read: { Problem: null, Versions: [.., { Graph: not null } newest] } } found && RawGraph(found, newest) is { } graph
                ? MigratedGraphs.Shape(graph) : null;

        // 1. The saved workflows: a steps, `handed` or `land`+`accept` newest becomes the next version of its id.
        foreach (var found in store.List().ToList())
        {
            if (found.Read is not { Problem: null, Verifies: false, Versions: [.., var newest] } read) continue;
            if (ShapeOf(found, newest) is not { } shape || GraphOf(found, newest) is not { } source) continue;
            if (shape == "land-accept")
            {
                if (MigratedGraphs.LiftLandAccept(source, out var why) is { } lifted) Planned(found.Id, read.Name!, "the `accept` is a review before the land", lifted, null);
                else said.Add($"`{found.Id}` v{newest.Version} is left as it is: {why}");
                continue;
            }

            var choosers = Choosing(places, found.Id);
            var resolutions = choosers.Count == 0 ? [(Place: "", Resolution: HandedResolution.Default, IsWorkspace: false)]
                : choosers.Select(each => (each.Place, Resolution: HandedResolution.Of(each), each.IsWorkspace)).ToList();
            var groups = new List<(JsonObject Graph, List<(string Place, bool IsWorkspace)> Places)>();
            var refused = false;
            foreach (var each in resolutions)
            {
                var lifted = MigratedGraphs.LiftHanded(source, each.Resolution, out var why);
                if (lifted is null) { said.Add($"`{found.Id}` v{newest.Version} is left as it is: {why}"); refused = true; break; }
                var text = lifted.ToJsonString();
                var group = groups.FirstOrDefault(known => known.Graph.ToJsonString() == text);
                if (group.Graph is null) groups.Add((lifted, [(each.Place, each.IsWorkspace)]));
                else group.Places.Add((each.Place, each.IsWorkspace));
            }

            if (refused) continue;
            for (var index = 0; index < groups.Count; index++)
            {
                var (graph, chosen) = groups[index];
                if (index == 0)
                {
                    Planned(found.Id, read.Name!, shape == "steps" ? "a list of steps drawn as a graph, its landing a `land`" : "its `handed` is a `land`", graph, null);
                    continue;
                }

                var names = chosen.Where(place => place.Place.Length > 0).Select(place => place.Place).ToList();
                var id = Unique(store, $"{found.Id}-{Slug(chosen[0].Place)}");
                var name = Cut($"{read.Name} ({chosen[0].Place})", 60);
                if (!Planned(id, name, $"`{found.Id}` lands differently where {string.Join(", ", names.Select(n => $"`{n}`"))} follow it, so they take a workflow of their own", graph, null)) continue;
                foreach (var place in chosen) config = WorkflowSelection.Apply(config, Use(id, place.Place, place.IsWorkspace));
            }
        }

        // 2. The places that followed the rules: each follows the home default with nothing written, or a saved copy of what its rules drew.
        var unconverted = places.Where(each => each.Unconverted).ToList();
        if (config.DefaultWorkflow is null && unconverted.Count > 0)
        {
            var shipped = WorkflowDefaults.Of(SetupPresets.Merge)!;
            if ((store.CopyOf(shipped, false, false) ?? SaveCopy(shipped, false, false)) is { } id)
            {
                config = config.WithDefaultWorkflow(id);
                said.Add($"this machine's default is `{id}`");
            }
        }

        string? SaveCopy(WorkflowDefault shipped, bool opinion, bool look)
        {
            var made = WorkflowDefaults.Copy(shipped, opinion, look, taken => store.Load(taken) is not null);
            return Planned(made.Id, made.Name, $"a copy of `{shipped.Id}`", made.Graph, made.From) ? made.Id : null;
        }

        string? TargetOf(MigratedPlace place, JsonObject desired, string shape)
        {
            foreach (var shipped in WorkflowDefaults.All)
            {
                foreach (var (opinion, look) in new[] { (false, false), (true, false), (false, true), (true, true) })
                {
                    if (MigratedGraphs.Shape(WorkflowPresets.WithGraphGates(shipped.Graph(), opinion, look)) != shape) continue;
                    return store.CopyOf(shipped, opinion, look) ?? SaveCopy(shipped, opinion, look);
                }
            }

            var id = $"{Slug(place.Place)}-workflow";
            for (var number = 2; store.Load(id) is not null; number++)
            {
                if (ShapeOfWorkflow(id) == shape) return id;
                id = $"{Slug(place.Place)}-workflow-{number}";
            }

            return Planned(id, Cut($"{place.Place}'s workflow", 60), $"what `{place.Place}`'s rules drew", desired, null) ? id : null;
        }

        foreach (var place in unconverted)
        {
            var desired = MigratedGraphs.OfRules(place);
            var shape = MigratedGraphs.Shape(desired);
            var resolved = MigratedPlaces.Follows(config, place.IsWorkspace ? null : place.Place, place.IsWorkspace ? place.Place : place.Workspace);
            if (resolved is not (null or MigratedPlaces.Current) && ShapeOfWorkflow(resolved) == shape) continue;
            var homeShape = config.DefaultWorkflow is { } home2 ? ShapeOfWorkflow(home2) : null;
            var explicitCurrent = (place.IsWorkspace ? config.WorkspaceWorkflows : config.Workflows).TryGetValue(place.Place, out var held) && held.Default == MigratedPlaces.Current;
            // A place that named `current` where the home default draws what its rules drew is cleared to the default.
            string? target = null;
            if (!(homeShape == shape && explicitCurrent) && (target = TargetOf(place, desired, shape)) is null) continue;
            var kinds = !place.IsWorkspace && !config.Workflows.ContainsKey(place.Place) && config.WorkspaceWorkflows.TryGetValue(place.Workspace, out var shared) ? shared.Kinds : [];
            config = WorkflowSelection.Apply(config, Use(target, place.Place, place.IsWorkspace));
            said.Add(target is null ? $"`{place.Place}` follows this machine's default" : $"`{place.Place}` follows `{target}`");
            foreach (var kind in kinds.Where(each => each.Value.Workflow is not null))
            {
                config = WorkflowSelection.Apply(config, new WorkflowUse(kind.Value.Workflow, place.Place, null, kind.Key) { InKnown = true, In = place.Workspace });
            }
        }

        // 3. What a converted place's graph now carries goes from the file, and a rule's `integrate` becomes its line's policy.
        foreach (var place in places)
        {
            if (place.Unconverted) config = Removed(config, place);
            if (place.Own && place.Rule?.Integrate is { } integrate && !(place.IsWorkspace ? config.WorkspaceLinePolicies : config.LinePolicies).ContainsKey(place.Place))
            {
                var policy = integrate with { Request = null, Conflicts = null };
                config = place.IsWorkspace ? config.WithWorkspaceLinePolicy(place.Place, policy) : config.WithLinePolicy(place.Place, policy);
                said.Add($"`{place.Place}`'s line policy is its rule's `integrate`");
            }
        }

        if (old.AnyRule) said.Add("the landing rules are gone from driver.json: each place lands by its workflow's `land`");
        var real = WorkflowPorts.Store(home);
        foreach (var id in saved) real.Write(id, store.Load(id)!.File!);
        return new(WithoutOldKeys(config), saved, said);
    }

    /// <summary>The keys no build reads any more: the landing rules and the work cycles.</summary>
    private static readonly string[] OldKeys = ["landings", "workspaceLandings", "workCycles", "workspaceWorkCycles"];

    /// <summary>
    /// The file with the old keys taken out. A save keeps every key it does not own from the file on disk, so the text is the one written
    /// whole: its source file is cleared, and the migration's write replaces the file, its backup taken first.
    /// </summary>
    private static DriverConfig WithoutOldKeys(DriverConfig config)
    {
        var emptied = config with
        {
            Landings = new Dictionary<string, LandingRule>(StringComparer.OrdinalIgnoreCase),
            WorkspaceLandings = new Dictionary<string, LandingRule>(StringComparer.OrdinalIgnoreCase),
            WorkCycles = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase),
            WorkspaceWorkCycles = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase),
        };
        if (config.Source is not { } source || JsonNode.Parse(source) is not JsonObject root || !OldKeys.Any(root.ContainsKey)) return emptied;
        foreach (var key in OldKeys) root.Remove(key);
        return emptied with { Source = root.ToJsonString(), SourceFile = null };
    }

    private static WorkflowUse Use(string? workflow, string place, bool workspace) =>
        new(workflow, workspace ? null : place, workspace ? place : null, null);

    /// <summary>The places whose default choice resolves to <paramref name="workflow"/>, repositories first: the places its rule reaches.</summary>
    private static List<MigratedPlace> Choosing(IReadOnlyList<MigratedPlace> places, string workflow) =>
        [.. places.Where(each => each.Follows == workflow).OrderBy(each => each.IsWorkspace).ThenBy(each => each.Place, StringComparer.OrdinalIgnoreCase)
            .Where(each => !each.IsWorkspace || places.All(other => other.IsWorkspace || other.Workspace != each.Place || other.Follows != workflow))];

    /// <summary>A converted place's access <c>landing</c> pause and its own session checks, which its graph's nodes carry now.</summary>
    private static DriverConfig Removed(DriverConfig config, MigratedPlace place)
    {
        var next = config;
        if (place.OwnChecks) next = next with { SessionChecks = Without(next.SessionChecks, place.Place) };
        if (place.AccessLanding)
        {
            var map = place.IsWorkspace ? next.WorkspaceAccess : next.Access;
            if (map.GetValueOrDefault(place.Place) is { } level)
            {
                var pauses = level.Pauses.Where(pair => pair.Key != AccessKey.Landing).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                var rewritten = pauses.Count == 0 && !level.Full ? Without(map, place.Place) : With(map, place.Place, new AccessSetting { Full = level.Full, Pauses = pauses });
                next = place.IsWorkspace ? next with { WorkspaceAccess = rewritten } : next with { Access = rewritten };
            }
        }

        return next;
    }

    /// <summary>The shape of a version if it is an old one, else null: read from the file's own text, since no reader takes it any more.</summary>
    private static string? ShapeOf(WorkflowFileFound found, NamedVersion version)
    {
        if (RawVersion(found, version) is not { } raw) return null;
        if (raw["steps"] is JsonArray) return "steps";
        if (raw["graph"]?["nodes"] is not JsonArray nodes) return null;
        if (nodes.Any(node => (string?)node?["kind"] == MigratedGraphs.Handed)) return "handed";
        return nodes.Any(node => (string?)node?["kind"] == NodeKinds.Land && node?["options"]?[MigratedGraphs.Accept] is not null) ? "land-accept" : null;
    }

    /// <summary>A version's graph as JSON: a list of steps drawn as <see cref="WorkflowGraphs.Saved"/> draws it, a graph as its file holds it.</summary>
    private static JsonObject? GraphOf(WorkflowFileFound found, NamedVersion version)
    {
        if (RawVersion(found, version) is not { } raw) return null;
        if (raw["steps"] is JsonArray)
        {
            using var document = JsonDocument.Parse(raw["steps"]!.ToJsonString());
            return WorkflowNamed.ReadSteps(document.RootElement.Clone(), version.Version).Steps is { } steps ? WorkflowGraphs.Saved(steps) : null;
        }

        return raw["graph"]?.DeepClone() as JsonObject;
    }

    private static JsonObject? RawGraph(WorkflowFileFound found, NamedVersion version) => RawVersion(found, version)?["graph"] as JsonObject;

    private static JsonObject? RawVersion(WorkflowFileFound found, NamedVersion version) =>
        found.File?["versions"]?.AsArray().FirstOrDefault(each => (int?)each?["version"] == version.Version) as JsonObject;

    private static string Slug(string name)
    {
        var slug = new string([.. name.ToLowerInvariant().Select(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '-')]).Trim('-');
        return slug.Length == 0 ? "place" : Cut(slug, 28);
    }

    private static string Cut(string text, int limit) => text.Length <= limit ? text : text[..limit].TrimEnd('-', ' ');

    private static string Unique(PlannedWorkflows store, string id)
    {
        var free = Cut(id, 40).TrimEnd('-');
        for (var number = 2; store.Load(free) is not null; number++) free = Cut(id, 36).TrimEnd('-') + $"-{number}";
        return free;
    }

    private static IReadOnlyDictionary<string, T> Without<T>(IReadOnlyDictionary<string, T> map, string key) =>
        map.Where(pair => !string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, T> With<T>(IReadOnlyDictionary<string, T> map, string key, T value)
    {
        var next = new Dictionary<string, T>(map, StringComparer.OrdinalIgnoreCase);
        var spelled = next.Keys.FirstOrDefault(each => string.Equals(each, key, StringComparison.OrdinalIgnoreCase)) ?? key;
        next[spelled] = value;
        return next;
    }
}
