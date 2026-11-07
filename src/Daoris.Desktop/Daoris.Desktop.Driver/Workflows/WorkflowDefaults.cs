using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A delivery's id, name and purpose in English: the terminal's words and the page's fallback. The page names a delivery from its
/// own catalogue by <paramref name="Id"/>, in every locale it ships; the workflow's name is another thing.
/// </summary>
public sealed record ShippedDelivery(string Id, string Name, string For);

/// <summary>
/// One default workflow Daoris ships: its id and the ids it answers to, its name and what it is for in English (the page names it from its
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
/// The fabric's defaults as shipped data: one file each under the driver's
/// <c>Defaults/workflows/</c>, carried inside the assembly, read once and judged by the graph rules as they are read. The graph presets, the
/// quick set-up's deliveries and the default a place follows all read them, so a default is written once.
/// </summary>
public static class WorkflowDefaults
{
    private const string Folder = "Daoris.Driver.Defaults/workflows/";

    private static readonly Lazy<IReadOnlyList<WorkflowDefault>> Shipped = new(Load);

    /// <summary>Every default this build ships, in its order.</summary>
    public static IReadOnlyList<WorkflowDefault> All => Shipped.Value;

    /// <summary>A default by its id or an id it answers to, or null.</summary>
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
    /// The shipped default a copy is behind: the one its newest version's <c>from</c> names, where this build
    /// ships a digest other than the one stamped, with the graph the copy's next version would be, which keeps the gates the copy has. Null for a
    /// copy that is up to date, one stamped by none, or one copied from a default this build does not ship.
    /// </summary>
    public static ShippedNewer? NewerThan(WorkflowFrom? from, NodeGraph? copy)
    {
        if (from is null || copy is null || Of(from.Default) is not { } shipped || shipped.Digest == from.Digest) return null;
        var made = Copy(shipped, copy.Nodes.Any(node => node.Kind == NodeKinds.Opinion), copy.Nodes.Any(node => node.Kind == NodeKinds.Look));
        return new ShippedNewer(shipped.Id, shipped.Name, made.Graph);
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

        // A delivery's words are its owner's: the first default by order whose graph derives that shape.
        var ordered = read.OrderBy(each => each.Order).ToList();
        var owners = ordered.Where(each => each.DeliveryWords.Id.Length > 0).ToList();
        if (owners.GroupBy(each => each.Delivery).FirstOrDefault(group => group.Count() > 1) is { } twice)
        {
            throw new InvalidOperationException($"two defaults this build ships word the delivery `{twice.Key.Method}` after `{twice.Key.Accept}`: only its first carries its words.");
        }

        for (var at = 0; at < ordered.Count; at++)
        {
            if (ordered[at].DeliveryWords.Id.Length > 0) continue;
            var owner = owners.FirstOrDefault(each => each.Delivery == ordered[at].Delivery)
                ?? throw new InvalidOperationException($"the default workflow `{ordered[at].Id}` this build ships derives a delivery that no default words.");
            ordered[at] = ordered[at] with { DeliveryWords = owner.DeliveryWords };
        }

        return ordered;
    }

    /// <summary>What a saved copy of a default is made of (<see cref="Copy"/>): its id, name, graph and where it came from.</summary>
    public sealed record WorkflowCopy(string Id, string Name, JsonObject Graph, WorkflowFrom From);

    /// <summary>
    /// A copy of <paramref name="shipped"/> for a home to save: the id <c>merge-after-accept</c>, with
    /// <c>-opinion</c>, <c>-look</c> or <c>-opinion-look</c> after it for the variant the ticks choose, numbered <c>-2</c>, <c>-3</c> from
    /// <paramref name="taken"/> where a person's own workflow holds it; the graph with the gates the ticks add; <c>from</c> stamped.
    /// </summary>
    /// <param name="taken">Whether an id is held by a workflow that is not a copy of this variant (a person's own).</param>
    public static WorkflowCopy Copy(WorkflowDefault shipped, bool opinion, bool look, Func<string, bool>? taken = null)
    {
        var id = shipped.Id + (opinion ? "-opinion" : "") + (look ? "-look" : "");
        var free = id;
        for (var number = 2; taken?.Invoke(free) == true; number++) free = $"{id}-{number}";
        var name = shipped.Name + (opinion ? " + second opinion" : "") + (look ? " + your look" : "");
        return new WorkflowCopy(free, name, WorkflowPresets.WithGraphGates(shipped.Graph(), opinion, look), shipped.From);
    }

    /// <summary>
    /// The land a graph says: the method from its <c>land</c>, <c>you</c> where a <c>review</c> comes before it,
    /// the plugin it names (an integration or a branch left to push is <c>none</c>), and an integration's request.
    /// </summary>
    public static SetupLand DerivedLand(NodeGraph graph)
    {
        var land = graph.Nodes.First(node => node.Kind == NodeKinds.Land);
        var method = NodeOptions.Text(land, "method") ?? LandNodes.Merge;
        var accept = LandNodes.Reviewed(graph) ? SetupLand.You : SetupLand.Automatic;
        // A branch followed by a push and a plugin's pull-request step is the pull-request delivery: the plugin is the step's own.
        var pullRequest = graph.Nodes.FirstOrDefault(node => NodeKit.IsPlugin(node.Kind) &&node.Kind.EndsWith("/pull-request", StringComparison.Ordinal));
        if (method == LandNodes.Branch && pullRequest is not null && graph.Nodes.Any(node => node.Kind == NodeKinds.Push))
        {
            return new SetupLand(SetupLand.PullRequest, accept, NodeGoverning.PluginOf(pullRequest.Kind));
        }

        string? plugin = null;
        var request = method == LandNodes.Integrate
            ? graph.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Integrate) is { } integrate ? NodeOptions.Text(integrate, "request") ?? LandingIntegrate.Owner : LandingIntegrate.Owner
            : null;
        return new SetupLand(method, accept, plugin, request);
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
        var graph = JsonNode.Parse(Object("graph").GetRawText())!.ToJsonString();
        var (read, problems) = NodeGraphs.Read(graph, onSave: true);
        if (read is null) throw Broken(problems[0].Words);
        if (NodeGraphs.Problems(read) is [var first, ..]) throw Broken(first.Words);
        // The delivery is derived from the graph; a file keeps only the words of the delivery it owns.
        var land = DerivedLand(read);
        var deliveryWords = root.TryGetProperty("delivery", out var delivery) && delivery.ValueKind == JsonValueKind.Object
            ? new ShippedDelivery(id, Text(delivery, "name"), Text(delivery, "for"))
            : new ShippedDelivery("", "", "");
        IReadOnlyList<string> aliases = root.TryGetProperty("aliases", out var listed) && listed.ValueKind == JsonValueKind.Array
            ? [.. listed.EnumerateArray().Select(each => each.GetString() ?? throw Broken("each of its `aliases` is an id."))]
            : [];
        var setUp = root.TryGetProperty("setUp", out var offered) && offered.ValueKind == JsonValueKind.True;
        return new WorkflowDefault(id, place, aliases, Text(root, "name"), Text(root, "for"), land, deliveryWords, setUp, graph, NodeGraphs.Digest(read));
    }
}
