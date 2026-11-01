using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A delivery's id, name and purpose in English (SETUPUX1a): the terminal's words and the page's fallback. The page names a delivery from its
/// own catalogue by <paramref name="Id"/>, in every locale it ships; the workflow's name is another thing.
/// </summary>
public sealed record ShippedDelivery(string Id, string Name, string For);

/// <summary>
/// One default workflow Daoris ships: its id and the ids it was known by, its name and what it is for in English (the page names it from its
/// own catalogue by the id), its delivery (the <c>land</c> options a place's quick set-up applies, <see cref="SetupPresets"/>) with its id, name
/// and purpose, whether that set-up offers it, and its graph as a version saves it, compact, with its digest.
/// </summary>
public sealed record WorkflowDefault(
    string Id, int Order, IReadOnlyList<string> Aliases, string Name, string For, SetupLand Delivery, ShippedDelivery DeliveryWords,
    bool SetUp, string GraphJson, string Digest)
{
    /// <summary>The graph as saved, fresh each time, for a version to take.</summary>
    public JsonObject Graph() => JsonNode.Parse(GraphJson)!.AsObject();

    /// <summary>Where a version copied from it came from, as its file keeps it.</summary>
    public WorkflowFrom From => new(Id, Digest);
}

/// <summary>
/// The fabric's defaults as shipped data (DEFAULTCONFIG1a; D145's and D173's notes of 2026-11-01): one file each under the driver's
/// <c>Defaults/workflows/</c>, carried inside the assembly, read once and judged by the graph rules as they are read. The graph presets, the
/// quick set-up's deliveries and the default a place follows all read them, so a default is written once.
/// </summary>
public static class WorkflowDefaults
{
    private const string Folder = "Daoris.Driver.Defaults/workflows/";

    private static readonly Lazy<IReadOnlyList<WorkflowDefault>> Shipped = new(Load);

    /// <summary>Every default this build ships, in its order.</summary>
    public static IReadOnlyList<WorkflowDefault> All => Shipped.Value;

    /// <summary>A default by its id or an id it was known by, or null.</summary>
    public static WorkflowDefault? Of(string? id) =>
        id is null ? null : All.FirstOrDefault(each => each.Id == id || each.Aliases.Contains(id, StringComparer.Ordinal));

    /// <summary>
    /// What a version copied from <paramref name="shipped"/> says otherwise: nothing where its graph is the default's; else the person's Accept
    /// moved, each option of its landing and its integration that differs (one left open is <c>none</c>), each gate added, and its
    /// <c>steps</c> where none of these is what changed.
    /// </summary>
    public static IReadOnlyList<string> Differs(WorkflowDefault shipped, NodeGraph graph)
    {
        if (NodeGraphs.Digest(graph) == shipped.Digest) return [];
        var original = NodeGraphs.Read(shipped.GraphJson).Graph!;
        var differs = new List<string>();
        if (LandNodes.Reviewed(graph) != LandNodes.Reviewed(original)) differs.Add($"accept `{(LandNodes.Reviewed(graph) ? SetupLand.You : SetupLand.Automatic)}`");
        foreach (var kind in new[] { NodeKinds.Land, NodeKinds.Integrate })
        {
            var mine = graph.Nodes.FirstOrDefault(node => node.Kind == kind)?.Options ?? new Dictionary<string, string>();
            var theirs = original.Nodes.FirstOrDefault(node => node.Kind == kind)?.Options ?? new Dictionary<string, string>();
            foreach (var name in mine.Keys.Union(theirs.Keys).Order(StringComparer.Ordinal))
            {
                var value = mine.GetValueOrDefault(name);
                if (value == theirs.GetValueOrDefault(name)) continue;
                differs.Add($"{name} `{(value is null ? "none" : NodeOptions.Value(value) as string ?? value)}`");
            }
        }

        differs.AddRange(new[] { NodeKinds.Opinion, NodeKinds.Look }
            .Where(gate => graph.Nodes.Any(node => node.Kind == gate) && original.Nodes.All(node => node.Kind != gate)));
        return differs.Count > 0 ? differs : ["steps"];
    }

    /// <summary>
    /// Whether every path a kind of task keeps to is a document's (the <c>documents</c> default is for such a kind): a text file by its extension,
    /// or anything under a <c>docs</c> or <c>doc</c> folder at the top. No paths is no such kind.
    /// </summary>
    public static bool AreDocuments(IReadOnlyList<string> paths) => paths.Count > 0 && paths.All(path =>
    {
        var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts is [("docs" or "doc"), ..]
            || (parts is [.., var last] && DocumentExtensions.Any(extension => last.EndsWith(extension, StringComparison.OrdinalIgnoreCase)));
    });

    private static readonly string[] DocumentExtensions = [".md", ".mdx", ".markdown", ".rst", ".adoc", ".txt"];

    private static IReadOnlyList<WorkflowDefault> Load()
    {
        var assembly = typeof(WorkflowDefaults).Assembly;
        var read = new List<WorkflowDefault>();
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            var name = resource.Replace('\\', '/');
            if (!name.StartsWith(Folder, StringComparison.Ordinal) || !name.EndsWith(".json", StringComparison.Ordinal)) continue;
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var document = JsonDocument.Parse(stream);
            read.Add(Read(name[Folder.Length..^".json".Length], document.RootElement));
        }

        return [.. read.OrderBy(each => each.Order)];
    }

    /// <summary>One shipped file read whole.</summary>
    /// <exception cref="InvalidOperationException">Where it does not hold a default: the build's own data, which its tests read first.</exception>
    private static WorkflowDefault Read(string file, JsonElement root)
    {
        InvalidOperationException Broken(string why) => new($"the default workflow `{file}` this build ships does not read: {why}");
        string Text(JsonElement at, string name) =>
            at.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
                ? text : throw Broken($"its `{name}` is text.");
        string? Optional(JsonElement at, string name) => at.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        JsonElement Object(string name) =>
            root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? value : throw Broken($"its `{name}` is an object.");

        var id = Text(root, "id");
        if (id != file) throw Broken("its `id` is its file's name.");
        if (!(root.TryGetProperty("order", out var order) && order.TryGetInt32(out var place))) throw Broken("its `order` is a whole number.");
        var delivery = Object("delivery");
        var land = new SetupLand(Text(delivery, "method"), Text(delivery, "accept"), Optional(delivery, "plugin"), Optional(delivery, "request"));
        var deliveryWords = new ShippedDelivery(Text(delivery, "id"), Text(delivery, "name"), Text(delivery, "for"));
        var graph = JsonNode.Parse(Object("graph").GetRawText())!.ToJsonString();
        var (read, problems) = NodeGraphs.Read(graph, onSave: true);
        if (read is null) throw Broken(problems[0].Words);
        if (NodeGraphs.Problems(read) is [var first, ..]) throw Broken(first.Words);
        IReadOnlyList<string> aliases = root.TryGetProperty("aliases", out var listed) && listed.ValueKind == JsonValueKind.Array
            ? [.. listed.EnumerateArray().Select(each => each.GetString() ?? throw Broken("each of its `aliases` is an id."))]
            : [];
        var setUp = root.TryGetProperty("setUp", out var offered) && offered.ValueKind == JsonValueKind.True;
        return new WorkflowDefault(id, place, aliases, Text(root, "name"), Text(root, "for"), land, deliveryWords, setUp, graph, NodeGraphs.Digest(read));
    }
}
