using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// How the rules stood where a <c>handed</c> left its form or press open: the form and press over the place's rule, and the rule's ticket key
/// and squash, which the lifted <c>land</c> writes.
/// </summary>
public sealed record HandedResolution(string Form, bool AutoAccept, string? Key, bool Squash)
{
    /// <summary>What a place with no rule at all resolves to: the merge, pressed by the person.</summary>
    public static HandedResolution Default { get; } = new(LandingForm.Merge, false, null, false);

    /// <summary>The resolution a place's facts give: its rule, with the composed switch where the access setup made one.</summary>
    public static HandedResolution Of(MigratedPlace? place) =>
        place?.Rule is { } rule ? new(rule.Form, rule.AutoAccept || place.Composed, rule.Key, rule.Squash) : Default;
}

/// <summary>
/// Shapes translated into graphs for the migration. Pure: a graph in, a graph or a refusal out, the rules handed in as facts.
/// A refusal names why the version is left as it is.
/// </summary>
public static class MigratedGraphs
{
    /// <summary>The landing step's kind, which the migration lifts to <c>land</c>.</summary>
    public const string Handed = "handed";

    /// <summary>The <c>land</c> option that held the landing for a press.</summary>
    public const string Accept = "accept";

    private const string Review = "review";

    /// <summary>The graph a place's rules drew, as the shipped defaults draw theirs: the chain, the landing's tail, the gates the rules asked for.</summary>
    public static JsonObject OfRules(MigratedPlace place)
    {
        var rule = place.Rule;
        var form = rule?.Form ?? LandingForm.Merge;
        // A place whose access setup composed the switch landed with no press, and keeps landing so.
        var reviewed = !(rule?.AutoAccept == true || place.Composed);
        // A rule that named a plugin landed a branch and had the plugin push it and open a pull request: a branch, a push and the plugin's step.
        var plugin = form == LandingForm.Branch && rule!.Plugin is { } named && named != "none" ? named : null;
        var method = form != LandingForm.Branch ? LandNodes.Merge
            : plugin is not null ? LandNodes.Branch
            : rule is { Integrate: not null, Squash: false } ? LandNodes.Integrate
            : LandNodes.Branch;

        var land = new JsonObject { ["method"] = method };
        var pattern = method != LandNodes.Merge && rule?.Pattern is { } written && written != LandNodes.DefaultPattern ? written : null;
        if (pattern is not null) land["pattern"] = pattern;
        if (pattern?.Contains("{ticket}", StringComparison.Ordinal) == true && rule?.Key is { } key) land[LandNodes.KeyOption] = key;
        if (rule?.Squash == true && method is LandNodes.Branch) land["squash"] = true;

        var work = new JsonObject();
        if (place.Checks is { } checks) work["checks"] = checks;
        var nodes = new JsonArray(
            Node("work", NodeKinds.Claimed, "observed", work), Node("requests", NodeKinds.WorkRequests, "observed"),
            Node("holds", NodeKinds.QuestHolds, "observed"), Node("content", NodeKinds.ContentHolds, "observed"));
        var chain = new List<string> { "work", "requests", "holds", "content" };
        if (reviewed)
        {
            nodes.Add(Node("review", NodeKinds.Review, "pressed"));
            chain.Add("review");
        }

        nodes.Add(Node("landing", NodeKinds.Land, "performed", land));
        chain.Add("landing");
        var end = "landing";
        if (plugin is not null)
        {
            nodes.Add(Node("push", NodeKinds.Push, "performed"));
            nodes.Add(Node("pull-request", $"{plugin}/pull-request", "performed"));
            chain.AddRange(["push", "pull-request"]);
            end = "pull-request";
        }

        switch (method)
        {
            case LandNodes.Merge:
                nodes.Add(Node("line", NodeKinds.Line, "observed"));
                chain.Add(end = "line");
                break;
            case LandNodes.Integrate:
                var integrate = (rule!.Integrate ?? new LandingIntegrate()).Resolved();
                nodes.Add(Node("integrate", NodeKinds.Integrate, "waited", new JsonObject { ["request"] = integrate.Request, ["conflicts"] = integrate.Conflicts }));
                nodes.Add(Node("line", NodeKinds.Line, "observed"));
                chain.AddRange(["integrate", "line"]);
                end = "line";
                break;
        }

        // A cycle's wires leave the landing or `line`, which every rule's graph drew.
        if (rule?.Cycle is not null && end != "line")
        {
            nodes.Add(Node("line", NodeKinds.Line, "observed"));
            chain.Add(end = "line");
        }

        var wires = new JsonArray();
        for (var at = 1; at < chain.Count; at++) wires.Add(new JsonObject { ["id"] = $"w{at}", ["kind"] = "after", ["from"] = chain[at - 1], ["to"] = chain[at] });
        var graph = new JsonObject { ["schemaVersion"] = 2, ["entries"] = new JsonArray("work"), ["end"] = end, ["nodes"] = nodes, ["wires"] = wires };
        if (place.Opinion || place.Look) graph = WorkflowPresets.WithGraphGates(graph, place.Opinion, place.Look);
        return rule?.Cycle is { } cycle ? Folded(graph, cycle) : graph;
    }

    /// <summary>
    /// A rule's <c>cycle</c> folded after the <c>land</c>: its nodes and wires added, a wire that left <c>handed</c>, the id the rules gave the landing,
    /// re-aimed at the land's own id, its entries beside the graph's, its end and cleanup the graph's.
    /// </summary>
    private static JsonObject Folded(JsonObject graph, string cycle)
    {
        if (JsonNode.Parse(cycle) is not JsonObject fragment) return graph;
        var land = (string)graph["nodes"]!.AsArray().First(node => (string?)node!["kind"] == NodeKinds.Land)!["id"]!;
        foreach (var node in fragment["nodes"] as JsonArray ?? []) graph["nodes"]!.AsArray().Add(node!.DeepClone());
        foreach (var wire in fragment["wires"] as JsonArray ?? [])
        {
            var copy = wire!.DeepClone().AsObject();
            if (copy["from"] is JsonValue from && from.TryGetValue<string>(out var leaving) && leaving == Handed) copy["from"] = land;
            else if (copy["from"] is JsonObject end && (string?)end["node"] == Handed) end["node"] = land;
            graph["wires"]!.AsArray().Add(copy);
        }

        foreach (var entry in fragment["entries"] as JsonArray ?? []) graph["entries"]!.AsArray().Add(entry!.DeepClone());
        if (fragment["end"] is JsonValue ended && ended.TryGetValue<string>(out var last)) graph["end"] = last;
        if (fragment["cleanup"] is { } cleanup) graph["cleanup"] = cleanup.DeepClone();
        return graph;
    }

    private static JsonObject Node(string id, string kind, string reach, JsonObject? options = null)
    {
        var node = new JsonObject { ["id"] = id, ["kind"] = kind, ["version"] = 1, ["reach"] = reach };
        if (options is { Count: > 0 }) node["options"] = options;
        return node;
    }

    /// <summary>
    /// A graph's shape, by what a person means by it: its nodes' kinds with their written options, its wires between kinds, its entries' kinds.
    /// Node ids, wire ids, reach and the end are no part of it, so a drawn graph matches the shipped copy that says the same.
    /// </summary>
    public static string Shape(JsonObject graph)
    {
        var kinds = graph["nodes"]!.AsArray().ToDictionary(node => (string)node!["id"]!, node => (string)node!["kind"]!, StringComparer.Ordinal);
        string KindOf(JsonNode? end) => end is JsonObject o ? kinds.GetValueOrDefault((string?)o["node"] ?? "", "?") : kinds.GetValueOrDefault((string?)end ?? "", "?");
        var tokens = new List<string>();
        foreach (var node in graph["nodes"]!.AsArray())
        {
            var options = node!["options"] as JsonObject;
            var written = options?.Where(pair => pair.Value is not null).OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}={pair.Value!.ToJsonString()}") ?? [];
            tokens.Add($"node {node["kind"]} {string.Join(',', written)}");
        }

        foreach (var wire in graph["wires"]!.AsArray()) tokens.Add($"wire {KindOf(wire!["from"])}>{KindOf(wire["to"])} {(string?)wire["kind"]}");
        foreach (var entry in graph["entries"]?.AsArray() ?? []) tokens.Add($"entry {KindOf(entry)}");
        tokens.Sort(StringComparer.Ordinal);
        return string.Join('\n', tokens);
    }

    /// <summary>
    /// A graph holding a <c>handed</c> lifted to a <c>land</c>: the form and press over <paramref name="resolution"/>, the method from them and the
    /// tail the graph already holds, the pattern kept, the key written where the pattern says <c>{ticket}</c>, a <c>review</c> before it where the
    /// press is the person's. Ids are kept.
    /// </summary>
    /// <returns>The graph, or null with why the version is left.</returns>
    public static JsonObject? LiftHanded(JsonObject source, HandedResolution resolution, out string? refusal)
    {
        refusal = null;
        var graph = (JsonObject)source.DeepClone();
        var nodes = graph["nodes"]!.AsArray();
        var handed = nodes.FirstOrDefault(node => (string?)node!["kind"] == Handed)?.AsObject();
        if (handed is null)
        {
            refusal = "it holds no landing step to lift.";
            return null;
        }

        var options = handed["options"] as JsonObject;
        string? Written(string name) => options?[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
        var form = Written("form") ?? resolution.Form;
        var accept = Written("accept") ?? (resolution.AutoAccept && form == LandingForm.Branch ? "automatic" : "you");
        var plugin = Written("plugin");
        var pattern = Written("pattern");
        if (form == LandingForm.Merge && (accept == "automatic" || pattern is not null || plugin is not null))
        {
            refusal = "its landing merges with no press or names a branch or a plugin, which never ran; choose its landing in the editor.";
            return null;
        }

        var method = form == LandingForm.Merge ? LandNodes.Merge
            : nodes.Any(node => (string?)node!["kind"] == NodeKinds.Integrate) ? LandNodes.Integrate
            : LandNodes.Branch;
        var land = new JsonObject { ["method"] = method };
        if (method != LandNodes.Merge && pattern is not null) land["pattern"] = pattern;
        // Kept for the conversion (PushedGraphs) that makes the plugin's steps.
        if (method == LandNodes.Branch && plugin is not null && plugin != "none") land["plugin"] = plugin;
        if (method != LandNodes.Merge && pattern?.Contains("{ticket}", StringComparison.Ordinal) == true && resolution.Key is { } key) land[LandNodes.KeyOption] = key;
        if (resolution.Squash && method is LandNodes.Branch) land["squash"] = true;
        handed["kind"] = NodeKinds.Land;
        handed["reach"] = "performed";
        handed["options"] = land;
        if (accept == "you") WithReview(graph, (string)handed["id"]!);
        return PushedGraphs.Convert(graph, out _) ?? graph;
    }

    /// <summary>A <c>land</c> saved with an <c>accept</c> (held, never landing): the option taken off, and the person's <c>review</c> before it.</summary>
    public static JsonObject? LiftLandAccept(JsonObject source, out string? refusal)
    {
        refusal = null;
        var graph = (JsonObject)source.DeepClone();
        var land = graph["nodes"]!.AsArray().FirstOrDefault(node => (string?)node!["kind"] == NodeKinds.Land)?.AsObject();
        if (land?["options"] is not JsonObject options || options[Accept] is null)
        {
            refusal = "it holds no `land` with an `accept`.";
            return null;
        }

        if (options["method"] is null)
        {
            refusal = "its `land` leaves the method open, which a saved landing no longer does; choose one in the editor.";
            return null;
        }

        // An `automatic` one never landed, so the person's review lowers nothing.
        options.Remove(Accept);
        WithReview(graph, (string)land["id"]!);
        return PushedGraphs.Convert(graph, out _) ?? graph;
    }

    /// <summary>The person's <c>review</c> wired in before <paramref name="landId"/>, where none stands: from what led in, a gate keeping its own wire to the landing.</summary>
    private static void WithReview(JsonObject graph, string landId)
    {
        var nodes = graph["nodes"]!.AsArray();
        if (nodes.Any(node => (string?)node!["kind"] == NodeKinds.Review)) return;
        var wires = graph["wires"]!.AsArray();
        var id = Review;
        for (var number = 2; nodes.Any(node => (string?)node!["id"] == id); number++) id = $"{Review}-{number}";
        nodes.Add(Node(id, NodeKinds.Review, "pressed"));
        var leads = wires.Where(wire => EndOf(wire!["to"]) == landId).Select(wire => wire!.AsObject()).ToList();
        var gates = new[] { NodeKinds.Opinion, NodeKinds.Look };
        string KindOf(string node) => (string?)nodes.First(each => (string?)each!["id"] == node)!["kind"] ?? "";
        foreach (var lead in leads)
        {
            var from = EndOf(lead["from"]);
            if (from is not null && gates.Contains(KindOf(from)))
            {
                wires.Add(new JsonObject { ["id"] = $"{from}-{id}", ["kind"] = "after", ["from"] = from, ["to"] = id });
            }
            else
            {
                if (lead["to"] is JsonObject target) target["node"] = id;
                else lead["to"] = id;
            }
        }

        wires.Add(new JsonObject { ["id"] = $"{id}-{landId}", ["kind"] = "after", ["from"] = id, ["to"] = landId });
        if (leads.Count == 0 && graph["entries"] is JsonArray entries)
        {
            for (var at = 0; at < entries.Count; at++)
            {
                if ((string?)entries[at] == landId) entries[at] = id;
            }
        }
    }

    private static string? EndOf(JsonNode? end) => end is JsonObject o ? (string?)o["node"] : (string?)end;
}
