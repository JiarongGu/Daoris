using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A saved graph that landed by a push or a pull request, as the steps it is now: the <c>land</c> a <c>branch</c>, then
/// <c>push</c>, then the plugin's <c>&lt;plugin&gt;/pull-request</c> step where it opened one. This is the only reader of the methods
/// <c>push</c> and <c>pull-request</c>, of the kinds <c>pushed</c> and <c>pull-request</c>, and of a <c>land</c>'s <c>plugin</c> option.
/// </summary>
/// <remarks>
/// The condition that ends it: no home that saved a workflow before the release that made push and pull request steps can still be started, which
/// is the release after the next one's migration replacing <see cref="HomeMigration.Steps"/>. It goes, and <see cref="PushStepsStep"/> with it, then.
/// </remarks>
public static class PushedGraphs
{
    private const string OldPush = "push";
    private const string OldPullRequest = "pull-request";
    private const string OldPushed = "pushed";

    /// <summary>
    /// The graph converted, or null where it holds nothing to convert. A <c>pull-request</c> whose <c>land</c> named a plugin becomes that plugin's
    /// performed step; where none is named (the bind chose it from the remote's platform) the step is dropped, its wires joined, and
    /// <paramref name="said"/> says the graph needs one named.
    /// </summary>
    public static JsonObject? Convert(JsonObject source, out string? said)
    {
        said = null;
        if (source["nodes"] is not JsonArray nodes) return null;
        var land = nodes.OfType<JsonObject>().FirstOrDefault(node => (string?)node["kind"] == NodeKinds.Land);
        var options = land?["options"] as JsonObject;
        var method = options?["method"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
        var plugin = options?["plugin"] is JsonValue named && named.TryGetValue<string>(out var id) && id != "none" ? id : null;
        var old = nodes.OfType<JsonObject>().Any(node => (string?)node["kind"] is OldPushed or OldPullRequest);
        if (!(method is OldPush or OldPullRequest || options?.ContainsKey("plugin") == true || old)) return null;

        var graph = (JsonObject)source.DeepClone();
        var copy = (JsonArray)graph["nodes"]!;
        if (copy.OfType<JsonObject>().FirstOrDefault(node => (string?)node["kind"] == NodeKinds.Land)?["options"] is JsonObject taken)
        {
            if (method is OldPush or OldPullRequest) taken["method"] = LandNodes.Branch;
            taken.Remove("plugin");
        }

        foreach (var node in copy.OfType<JsonObject>().ToList())
        {
            switch ((string?)node["kind"])
            {
                case OldPushed:
                    node["kind"] = NodeKinds.Push;
                    node["reach"] = "performed";
                    break;
                case OldPullRequest when plugin is not null:
                    node["kind"] = $"{plugin}/{OldPullRequest}";
                    node["reach"] = "performed";
                    break;
                case OldPullRequest:
                    Drop(graph, node);
                    said = $"its `{node["id"]}` opened a pull request through the plugin that served the remote's platform, which no longer binds: name the plugin's step in the workflow editor.";
                    break;
            }
        }

        return graph;
    }

    /// <summary>A node taken out of the graph, each wire into it joined to each wire out of it, and the end moved to what led to it.</summary>
    private static void Drop(JsonObject graph, JsonObject node)
    {
        var id = (string)node["id"]!;
        var wires = graph["wires"] as JsonArray ?? [];
        var into = wires.OfType<JsonObject>().Where(wire => (string?)wire["to"] == id).ToList();
        var outOf = wires.OfType<JsonObject>().Where(wire => (string?)wire["from"] == id).ToList();
        foreach (var wire in into.Concat(outOf)) wires.Remove(wire);
        foreach (var from in into)
        {
            foreach (var to in outOf)
            {
                wires.Add(new JsonObject { ["id"] = $"{(string)from["id"]!}-{(string)to["id"]!}", ["kind"] = "after", ["from"] = (string)from["from"]!, ["to"] = (string)to["to"]! });
            }
        }

        if ((string?)graph["end"] == id && into.Count > 0) graph["end"] = (string)into[0]["from"]!;
        ((JsonArray)graph["nodes"]!).Remove(node);
    }
}
