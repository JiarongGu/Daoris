using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A saved workflow's file with its newest graph started by an <c>isolate</c> step (<see cref="PlaceWorkStep"/>): the step a worktree for each
/// session as many at once as the cap allows, its <c>overlap</c> the policy's, written as the graph's one entry wired into its work.
/// </summary>
internal static class IsolatedFiles
{
    private const string Step = "apart";

    /// <summary>The file with a version whose graph starts with the step, or null where the newest already holds one or has no work entry.</summary>
    public static JsonObject? Isolated(JsonObject file, string overlap)
    {
        if (file["versions"] is not JsonArray { Count: > 0 } versions || versions[^1] is not JsonObject newest) return null;
        if (newest["graph"] is not JsonObject graph || With(graph.DeepClone().AsObject(), overlap) is not { } isolated) return null;
        var version = newest.DeepClone().AsObject();
        version["version"] = (newest["version"]?.GetValue<int>() ?? versions.Count) + 1;
        version["at"] = At();
        version["graph"] = isolated;
        var result = file.DeepClone().AsObject();
        result["versions"]!.AsArray().Add(version);
        return result;
    }

    /// <summary>A new workflow <paramref name="id"/> whose one version is <paramref name="file"/>'s newest started by the step.</summary>
    public static JsonObject? Copy(JsonObject file, string id, string overlap)
    {
        if (file["versions"] is not JsonArray { Count: > 0 } versions || versions[^1] is not JsonObject newest) return null;
        if (newest["graph"] is not JsonObject graph || With(graph.DeepClone().AsObject(), overlap) is not { } isolated) return null;
        var version = newest.DeepClone().AsObject();
        version["version"] = 1;
        version["at"] = At();
        version["graph"] = isolated;
        var made = file.DeepClone().AsObject();
        made["id"] = id;
        made["name"] = $"{HomeMigrationRun.Text(file["name"]) ?? id}, worked apart";
        made.Remove("order");
        made["versions"] = new JsonArray(version);
        return made;
    }

    private static string At() => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

    private static JsonObject? With(JsonObject graph, string overlap)
    {
        if (graph["nodes"] is not JsonArray nodes || graph["entries"] is not JsonArray entries) return null;
        if (nodes.Any(node => HomeMigrationRun.Text(node?["kind"]) == NodeKinds.Isolate)) return null;
        var work = entries.Select(HomeMigrationRun.Text).OfType<string>()
            .Where(entry => nodes.Any(node => HomeMigrationRun.Text(node?["id"]) == entry && HomeMigrationRun.Text(node?["kind"]) == NodeKinds.Claimed))
            .ToList();
        if (work.Count != 1 || entries.Count != 1) return null;
        var options = new JsonObject { [IsolateStep.KindOption] = IsolateStep.Worktree };
        if (overlap == IsolateStep.Wait) options[IsolateStep.OverlapOption] = IsolateStep.Wait;
        nodes.Insert(0, new JsonObject { ["id"] = Step, ["kind"] = NodeKinds.Isolate, ["version"] = 1, ["reach"] = NodeReach.Observed, ["options"] = options });
        (graph["wires"] as JsonArray ?? []).Insert(0, new JsonObject { ["id"] = $"{Step}>{work[0]}", ["kind"] = WireKinds.After, ["from"] = Step, ["to"] = work[0] });
        graph["entries"] = new JsonArray(Step);
        return graph;
    }
}
