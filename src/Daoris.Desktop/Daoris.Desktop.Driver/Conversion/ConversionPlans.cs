using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The two choices the conversion asks of the person (the cleanup design §4.2): what a <c>land</c> saved with <c>accept: automatic</c> becomes, and
/// whether a place whose access setup composed the automatic switch keeps a review. Each is the default that lowers nothing.
/// </summary>
public sealed record ConversionChoices(string Composed = ConversionChoices.AsLanded, string LandAutomatic = ConversionChoices.WithReview)
{
    /// <summary>A place the access setup landed with no press is converted with no review, as it landed.</summary>
    public const string AsLanded = "none";

    /// <summary>…or keeps the person's review.</summary>
    public const string KeepReview = "review";

    /// <summary>A <c>land</c> saved with <c>automatic</c> gets the person's review: it never landed, so the default lowers nothing.</summary>
    public const string WithReview = "review";

    /// <summary>…or has none, as it was saved, which lowers the person's part.</summary>
    public const string AsSaved = "none";

    public bool KeepsReview => Composed == KeepReview;

    public bool ReviewsAutomatic => LandAutomatic != AsSaved;

    /// <summary>What is wrong with the choices, or null.</summary>
    public string? Problem =>
        Composed is not (AsLanded or KeepReview) ? $"`composed` is `{AsLanded}` or `{KeepReview}`."
        : LandAutomatic is not (WithReview or AsSaved) ? $"`landAutomatic` is `{WithReview}` or `{AsSaved}`."
        : null;
}

/// <summary>A version to save: a copy of a shipped default, a new workflow, or the next version of one that exists.</summary>
/// <param name="Mode"><c>append</c> to <see cref="Id"/>, or <c>new</c>.</param>
/// <param name="Shape">What it converts: <c>steps</c>, <c>handed</c>, <c>land-accept</c>, <c>current</c>, <c>copy</c> or <c>default</c>.</param>
/// <param name="Base">For an append, the version it was made against.</param>
/// <param name="Digest">The planned version's graph digest, by which a save is idempotent.</param>
public sealed record ConvertSave(
    string Id, string Name, string Mode, string Shape, string Said, JsonObject Graph, WorkflowFrom? From, int? Base, string Digest, IReadOnlyList<string> Places);

/// <summary>A place's choice made: the workflow it follows (null clears it), for a kind where one is named.</summary>
public sealed record ConvertUse(string Place, bool IsWorkspace, string? Workflow, string? Kind, string Said);

/// <summary>A line's policy moved from a landing rule's <c>integrate</c>.</summary>
public sealed record ConvertPolicy(string Place, bool IsWorkspace, LandingIntegrate Policy);

/// <summary>The old keys removed at a place: <c>landings</c>, <c>access.landing</c>, <c>sessionChecks</c>.</summary>
public sealed record ConvertRemoval(string Place, bool IsWorkspace, IReadOnlyList<string> Keys);

/// <summary>Something the plan keeps as it is, with why: a work cycle, a version the save refuses, a unit still open.</summary>
public sealed record ConvertKept(string Code, string Subject, string Said);

/// <summary>The conversion plan (D227 point 3): every operation, said once, with the digest the person's press names.</summary>
public sealed record ConversionPlan(
    string Digest, ConversionChoices Choices, IReadOnlyList<ConvertSave> Saves, IReadOnlyList<ConvertUse> Uses, string? DefaultWorkflow,
    IReadOnlyList<ConvertPolicy> Policies, IReadOnlyList<ConvertRemoval> Removals, IReadOnlyList<ConvertKept> Kept, IReadOnlyList<string> Lowers,
    IReadOnlyList<string> Held, LegacyCensus Census)
{
    /// <summary>Whether the plan writes nothing: what is left is only kept, held or counted.</summary>
    public bool Empty => Saves.Count == 0 && Uses.Count == 0 && DefaultWorkflow is null && Policies.Count == 0 && Removals.Count == 0;
}

/// <summary>The conversion planned (WORKFLOWCLEAN1 1e; the cleanup design §4.2): pure over the world it is handed, written nowhere.</summary>
public static class ConversionPlans
{
    private const string Door = "screen";

    /// <summary>The plan over <paramref name="world"/>, with <paramref name="held"/> the places an earlier conversion left their rules at.</summary>
    public static ConversionPlan Plan(ConvertWorld world, ConversionChoices choices, IReadOnlySet<string>? held = null)
    {
        var config = world.Config;
        var store = new PlannedWorkflows(world.Store, world.Home);
        var census = WorkflowLegacy.Census(world, held);
        var (repositories, workspaces) = WorkflowLegacy.PlacesOf(config, world.Registry);
        var facts = workspaces.Select(each => LegacyReader.Workspace(config, each))
            .Concat(repositories.Select(each => LegacyReader.Repository(config, each.Repository, each.Workspace))).ToList();
        var saves = new List<ConvertSave>();
        var uses = new List<ConvertUse>();
        var policies = new List<ConvertPolicy>();
        var removals = new List<ConvertRemoval>();
        var kept = new List<ConvertKept>();
        var lowers = new List<string>();
        var holding = new List<string>();
        var at = PlannedWorkflows.At;

        // A version the save refuses is kept as it is, with the save's own words: never forced.
        bool Planned(string id, string name, string mode, string shape, string said, JsonObject graph, WorkflowFrom? from, IReadOnlyList<string> places)
        {
            var found = store.Load(id);
            try
            {
                var newest = found?.Read.Problem is null ? found?.Read.Versions[^1] : null;
                var planned = WorkflowOperations.PlanGraph(world.Home, found, id, newest, name, Door, at, graph, from);
                if (planned.Same) return true;
                saves.Add(new(id, name, mode, shape, said, graph, from, newest?.Version, planned.Next!.Digest!, places));
                store.Write(id, planned.Plan.Result!);
                return true;
            }
            catch (ChoiceWords refused)
            {
                kept.Add(new("refused", id, $"`{id}` is kept as it is: {refused.Message} Edit it in the workflow editor."));
                return false;
            }
        }

        string? ShapeOfWorkflow(string id) =>
            store.Load(id) is { Read: { Problem: null, Versions: [.., { Graph: not null } newest] } } found && WorkflowLegacy.GraphOf(found, newest) is { } graph
                ? LegacyGraphs.Shape(graph) : null;

        // 1. The saved workflows: a steps, `handed` or `land`+`accept` newest becomes the next version of its id.
        foreach (var row in census.Versions)
        {
            var found = store.Load(row.Workflow)!;
            var newest = found.Read.Versions[^1];
            if (WorkflowLegacy.GraphOf(found, newest) is not { } source) continue;
            if (row.Shape == "land-accept")
            {
                var lifted = LegacyGraphs.LiftLandAccept(source, choices.ReviewsAutomatic, out var why);
                if (lifted is null) { kept.Add(new("refused", row.Workflow, $"`{row.Workflow}` v{newest.Version} is kept as it is: {why}")); continue; }
                var automatic = source["nodes"]!.AsArray().First(node => (string?)node!["kind"] == NodeKinds.Land)!["options"]!["accept"]?.ToString() == "automatic";
                if (automatic && !choices.ReviewsAutomatic) lowers.Add($"`{row.Workflow}` lands with no review, as it was saved; it never landed, so nothing was lowered before.");
                Planned(row.Workflow, found.Read.Name!, "append", row.Shape,
                    automatic ? (choices.ReviewsAutomatic ? "its `automatic` landing gets your review: it never landed" : "its `automatic` landing has no review, as saved") : "the `accept` is a review before the land",
                    lifted, null, Choosing(config, facts, row.Workflow).Select(each => each.Place).ToList());
                continue;
            }

            var choosers = Choosing(config, facts, row.Workflow);
            var resolutions = choosers.Count == 0 ? [(Place: "", Resolution: HandedResolution.Default, IsWorkspace: false)]
                : choosers.Select(each => (each.Place, Resolution: HandedResolution.Of(each), each.IsWorkspace)).ToList();
            var groups = new List<(JsonObject Graph, List<(string Place, bool IsWorkspace)> Places)>();
            var refused = false;
            foreach (var each in resolutions)
            {
                var lifted = LegacyGraphs.LiftHanded(source, each.Resolution, out var why);
                if (lifted is null) { kept.Add(new("refused", row.Workflow, $"`{row.Workflow}` v{newest.Version} is kept as it is: {why}")); refused = true; break; }
                var text = lifted.ToJsonString();
                var group = groups.FirstOrDefault(known => known.Graph.ToJsonString() == text);
                if (group.Graph is null) groups.Add((lifted, [(each.Place, each.IsWorkspace)]));
                else group.Places.Add((each.Place, each.IsWorkspace));
            }

            if (refused) continue;
            for (var at2 = 0; at2 < groups.Count; at2++)
            {
                var (graph, places) = groups[at2];
                var names = places.Where(place => place.Place.Length > 0).Select(place => place.Place).ToList();
                if (at2 == 0)
                {
                    Planned(row.Workflow, found.Read.Name!, "append", row.Shape, row.Shape == "steps" ? "a list of steps drawn as a graph, its landing a `land`" : "its `handed` is a `land`", graph, null, names);
                    continue;
                }

                var id = Unique(store, $"{row.Workflow}-{Slug(places[0].Place)}");
                var name = Cut($"{found.Read.Name} ({places[0].Place})", 60);
                if (!Planned(id, name, "new", row.Shape, $"`{row.Workflow}` lands differently where {string.Join(", ", names.Select(n => $"`{n}`"))} follow it, so they take a version of their own", graph, null, names)) continue;
                uses.AddRange(places.Select(place => new ConvertUse(place.Place, place.IsWorkspace, id, null, $"`{place.Place}` follows `{id}`, which lands as its rule landed it")));
            }
        }

        // 2. The places on Current: each follows the home default with nothing written, or a saved copy of what its rules drew.
        var working = config;
        string? defaultWrite = null;
        var current = facts.Where(each => each.OnCurrent).ToList();
        if (config.DefaultWorkflow is null && current.Count > 0)
        {
            var shipped = WorkflowDefaults.Of(SetupPresets.Merge)!;
            var id = store.CopyOf(shipped, false, false) ?? SaveCopy(shipped, false, false);
            if (id is not null)
            {
                defaultWrite = id;
                working = working.WithDefaultWorkflow(id);
            }
        }

        string? SaveCopy(WorkflowDefault shipped, bool opinion, bool look)
        {
            var made = WorkflowDefaults.Copy(shipped, opinion, look, taken => store.Load(taken) is not null);
            return Planned(made.Id, made.Name, "new", "copy", $"a copy of `{shipped.Id}`", made.Graph, made.From, []) ? made.Id : null;
        }

        string? TargetOf(LegacyFacts place, JsonObject desired, string shape)
        {
            foreach (var shipped in WorkflowDefaults.All)
            {
                foreach (var (opinion, look) in new[] { (false, false), (true, false), (false, true), (true, true) })
                {
                    if (LegacyGraphs.Shape(WorkflowPresets.WithGraphGates(shipped.Graph(), opinion, look)) != shape) continue;
                    return store.CopyOf(shipped, opinion, look) ?? SaveCopy(shipped, opinion, look);
                }
            }

            var id = $"{Slug(place.Place)}-workflow";
            for (var number = 2; store.Load(id) is { } taken; number++)
            {
                if (ShapeOfWorkflow(id) == shape) return id;
                id = $"{Slug(place.Place)}-workflow-{number}";
            }

            return Planned(id, Cut($"{place.Place}'s workflow", 60), "new", "current", $"what `{place.Place}`'s old rules drew", desired, null, [place.Place]) ? id : null;
        }

        foreach (var place in current)
        {
            if (place.WorkCycle)
            {
                kept.Add(new("work-cycle", place.Place, $"`{place.Place}` stays on Current: its quests close by its work cycle, which a saved workflow does not run yet (FABRIC-CUTOVER1h)."));
                // The home default would take it from Current, so the place is pinned there by an explicit choice, as the landed route's `current` is.
                var cycled = place.IsWorkspace ? config.WorkspaceWorkCycles.ContainsKey(place.Place) : config.WorkCycles.ContainsKey(place.Place);
                if (defaultWrite is not null && cycled)
                {
                    working = WorkflowPart.PinnedToCurrent(working, place.Place, place.IsWorkspace);
                    uses.Add(new(place.Place, place.IsWorkspace, WorkflowSelection.Current, null, $"`{place.Place}` stays on Current, as its work cycle needs"));
                }

                continue;
            }

            var desired = LegacyGraphs.OfCurrent(place, choices.KeepsReview);
            var shape = LegacyGraphs.Shape(desired);
            var resolved = place.IsWorkspace ? WorkflowSelection.Resolve(working, null, place.Place, null) : WorkflowSelection.Resolve(working, place.Place, place.Workspace, null);
            var fine = resolved.Workflow != WorkflowSelection.Current && ShapeOfWorkflow(resolved.Workflow) == shape;
            if (!fine)
            {
                var homeShape = working.DefaultWorkflow is { } home ? ShapeOfWorkflow(home) : null;
                var explicitCurrent = (place.IsWorkspace ? config.WorkspaceWorkflows : config.Workflows).TryGetValue(place.Place, out var held2) && held2.Default == WorkflowSelection.Current;
                string? target = null;
                if (homeShape == shape && explicitCurrent) target = null;
                else if ((target = TargetOf(place, desired, shape)) is null) continue;

                var kinds = !place.IsWorkspace && !config.Workflows.ContainsKey(place.Place) && config.WorkspaceWorkflows.TryGetValue(place.Workspace, out var shared) ? shared.Kinds : [];
                var edit = new WorkflowUse(target, place.IsWorkspace ? null : place.Place, place.IsWorkspace ? place.Place : null, null);
                working = WorkflowSelection.Apply(working, edit);
                uses.Add(new(place.Place, place.IsWorkspace, target, null, target is null ? $"`{place.Place}` follows the home default again" : $"`{place.Place}` follows `{target}`"));
                foreach (var kind in kinds.Where(each => each.Value.Workflow is not null))
                {
                    working = WorkflowSelection.Apply(working, new WorkflowUse(kind.Value.Workflow, place.Place, null, kind.Key) { InKnown = true, In = place.Workspace });
                    uses.Add(new(place.Place, false, kind.Value.Workflow, kind.Key, $"`{place.Place}` keeps its workspace's `{kind.Key}` mapping"));
                }
            }

            var keys = place.RemovableKeys;
            if (keys.Count == 0) continue;
            if (census.Places.Any(each => each.Place == place.Place && each.IsWorkspace == place.IsWorkspace && each.OpenUnits))
            {
                holding.Add(WorkflowLegacy.Held(place));
                kept.Add(new("open-unit", place.Place, $"`{place.Place}`'s rules stay in force for its open Current unit, and a later plan offers to remove them once it ends."));
                continue;
            }

            removals.Add(new(place.Place, place.IsWorkspace, keys));
            if (place.Own && place.Rule?.Integrate is { } integrate && (place.IsWorkspace ? config.WorkspaceLinePolicies : config.LinePolicies).ContainsKey(place.Place) is false)
            {
                policies.Add(new(place.Place, place.IsWorkspace, integrate with { Request = null, Conflicts = null }));
            }
        }

        // 3. Rules an earlier conversion left for an open unit, once it has ended.
        foreach (var place in facts.Where(each => !each.OnCurrent && each.RemovableKeys.Count > 0 && held?.Contains(WorkflowLegacy.Held(each)) == true))
        {
            if (census.Leftovers.Any(each => each.Place == place.Place && each.IsWorkspace == place.IsWorkspace && each.Why.StartsWith("a Current unit", StringComparison.Ordinal))) continue;
            removals.Add(new(place.Place, place.IsWorkspace, place.RemovableKeys));
            if (place.Own && place.Rule?.Integrate is { } integrate && !(place.IsWorkspace ? config.WorkspaceLinePolicies : config.LinePolicies).ContainsKey(place.Place))
            {
                policies.Add(new(place.Place, place.IsWorkspace, integrate with { Request = null, Conflicts = null }));
            }
        }

        foreach (var run in census.Runs)
        {
            kept.Add(new("bound-run", run.Run, $"run `{run.Run}` in `{run.Repository}` ends on the old engine ({run.Engine}); end its unit with `{run.Door}`."));
        }

        return new(Digest(world, facts, choices, saves, uses, defaultWrite, policies, removals, census), choices, saves, uses, defaultWrite, policies, removals, kept, lowers, holding, census);
    }

    /// <summary>The places whose default choice resolves to <paramref name="workflow"/>, repositories first: the places its rule reaches.</summary>
    private static List<LegacyFacts> Choosing(DriverConfig config, IReadOnlyList<LegacyFacts> facts, string workflow) =>
        [.. facts.Where(each => each.Follows == workflow).OrderBy(each => each.IsWorkspace).ThenBy(each => each.Place, StringComparer.OrdinalIgnoreCase)
            .Where(each => !each.IsWorkspace || facts.All(other => other.IsWorkspace || other.Workspace != each.Place || other.Follows != workflow))];

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

    private static string Digest(
        ConvertWorld world, IReadOnlyList<LegacyFacts> facts, ConversionChoices choices, IReadOnlyList<ConvertSave> saves, IReadOnlyList<ConvertUse> uses,
        string? defaultWrite, IReadOnlyList<ConvertPolicy> policies, IReadOnlyList<ConvertRemoval> removals, LegacyCensus census)
    {
        var read = new JsonArray();
        foreach (var each in facts)
        {
            var rule = each.Rule is null ? null : new JsonArray(each.Rule.Form, each.Rule.Pattern, each.Rule.Plugin, each.Rule.AutoAccept, each.Rule.Key, each.Rule.Squash, each.Rule.Tidy, each.Rule.Cycle,
                each.Rule.Integrate is null ? null : System.Text.Json.JsonSerializer.Serialize(each.Rule.Integrate));
            read.Add(new JsonArray(each.Place, each.IsWorkspace, each.Follows, each.Level, rule, each.Own, each.Composed, each.Opinion, each.Look, each.Checks, each.WorkCycle, each.AccessLanding));
        }

        var workflows = new JsonObject();
        foreach (var found in world.Store.List().OrderBy(each => each.Id, StringComparer.Ordinal))
        {
            workflows[found.Id] = found.Read is { Problem: null, Versions: [.., var newest] } ? $"{newest.Version}:{newest.Digest}" : "unread";
        }

        var operations = new JsonObject
        {
            ["saves"] = new JsonArray([.. saves.Select(each => (JsonNode?)new JsonArray(each.Id, each.Mode, each.Digest, each.Base))]),
            ["uses"] = new JsonArray([.. uses.Select(each => (JsonNode?)new JsonArray(each.Place, each.IsWorkspace, each.Workflow, each.Kind))]),
            ["default"] = defaultWrite,
            ["policies"] = new JsonArray([.. policies.Select(each => (JsonNode?)new JsonArray(each.Place, each.IsWorkspace, System.Text.Json.JsonSerializer.Serialize(each.Policy)))]),
            ["removals"] = new JsonArray([.. removals.Select(each => (JsonNode?)new JsonArray(each.Place, each.IsWorkspace, string.Join(',', each.Keys)))]),
            ["runs"] = new JsonArray([.. census.Runs.Select(each => (JsonNode?)each.Run)]),
        };
        var whole = new JsonObject
        {
            ["facts"] = read,
            ["workflows"] = workflows,
            ["shipped"] = new JsonArray([.. WorkflowDefaults.All.Select(each => (JsonNode?)$"{each.Id}:{each.Digest}")]),
            ["default"] = world.Config.DefaultWorkflow,
            ["choices"] = new JsonArray(choices.Composed, choices.LandAutomatic),
            ["operations"] = operations,
        };
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(whole.ToJsonString())));
    }
}
