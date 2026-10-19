using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-GRAPH1c: the shared table of graphs and the problems a save names in them, <c>test/fixtures/graph-problems.json</c>, which the
/// CLI's <c>graphproblems.test.ts</c> reads too (its <c>nodegraphs.ts</c>, the twin of <see cref="NodeWires.Problems"/>,
/// <see cref="NodeGraphs.Problems"/> and <see cref="NodeGraphHomes.OfLanding"/>). A row's graph is written as a file holds it and read by
/// <see cref="NodeGraphs.Parts"/>: a base changed by its <c>remove</c>, <c>replace</c>, <c>add</c> and <c>set</c>, or written whole, or made by
/// <see cref="Chain"/> and <see cref="Dense"/>; its <c>ends</c> are set after reading, where a file cannot say them.
/// </summary>
internal static class GraphProblemsTable
{
    private static readonly string TablePath = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Cli", "test", "fixtures", "graph-problems.json");

    private static JsonObject Table() => JsonNode.Parse(File.ReadAllText(TablePath))!.AsObject();

    /// <summary>The names of a section's rows, in its order.</summary>
    public static TheoryData<string> Names(string section) => new([.. Table()[section]!.AsArray().Select(row => (string)row!["name"]!)]);

    /// <summary>A section's row by its name.</summary>
    public static JsonObject Row(string section, string name) =>
        Table()[section]!.AsArray().Select(row => row!.AsObject()).Single(row => (string)row["name"]! == name);

    /// <summary>Every code the table's rows name.</summary>
    public static IEnumerable<string> Codes() =>
        new[] { "wires", "graphs", "fragments", "unreadable" }.SelectMany(section => Table()[section]!.AsArray())
            .SelectMany(row => row!["problems"]!.AsArray()).Select(problem => (string)problem!["code"]!);

    /// <summary>The problems a row says the save names, in its order.</summary>
    public static IReadOnlyList<(string Code, string? Wire, string? Node, string Words)> Expected(JsonObject row) =>
        [.. row["problems"]!.AsArray().Select(problem => ((string)problem!["code"]!, (string?)problem["wire"], (string?)problem["node"], (string)problem["words"]!))];

    /// <summary>What a save named, as a row holds it.</summary>
    public static IReadOnlyList<(string Code, string? Wire, string? Node, string Words)> Said(IEnumerable<WireProblem> problems) =>
        [.. problems.Select(problem => (problem.Code, problem.Wire, problem.Node, problem.Words))];

    /// <summary>The row's graph, made as the table says, its ends set after reading.</summary>
    public static NodeGraph Graph(JsonObject row)
    {
        var graph = row["chain"] is { } chain ? Chain((int)chain["count"]!, (int)chain["synthetic"]!)
            : row["dense"] is { } dense ? Dense((int)dense)
            : Read(Json(row));
        foreach (var mark in row["ends"]?.AsArray() ?? new JsonArray())
        {
            var end = new WireEnd((string)mark!["node"]!, (string?)mark["port"]) { Unit = (string?)mark["unit"] };
            var id = (string)mark["wire"]!;
            graph = graph with
            {
                Wires = [.. graph.Wires.Select(wire => wire.Id != id ? wire : (string)mark["end"]! == "from" ? wire with { From = end } : wire with { To = end })],
            };
        }

        return graph;
    }

    /// <summary>The graph's JSON as a file holds it: the row's whole graph, or its base with the row's changes made.</summary>
    public static JsonObject Json(JsonObject row)
    {
        var json = (row["graph"] ?? Table()[(string)row["base"]!])!.DeepClone().AsObject();
        foreach (var list in new[] { "nodes", "wires" })
        {
            var held = (json[list] as JsonArray ?? new JsonArray()).Select(each => each!.DeepClone()).ToList();
            var removed = row["remove"]?[list]?.AsArray().Select(id => (string)id!).ToHashSet() ?? [];
            held.RemoveAll(each => removed.Contains((string)each["id"]!));
            foreach (var replaced in row["replace"]?[list]?.AsArray() ?? new JsonArray())
            {
                held[held.FindIndex(each => (string)each["id"]! == (string)replaced!["id"]!)] = replaced!.DeepClone();
            }

            held.AddRange((row["add"]?[list]?.AsArray() ?? new JsonArray()).Select(each => each!.DeepClone()));
            json[list] = new JsonArray([.. held]);
        }

        foreach (var (key, value) in row["set"]?.AsObject() ?? new JsonObject())
        {
            json[key] = value?.DeepClone();
        }

        return json;
    }

    /// <summary>A graph from its JSON as <see cref="NodeGraphs.Read"/> builds it, judged by no one yet.</summary>
    private static NodeGraph Read(JsonObject json)
    {
        var parts = NodeGraphs.Parts(json.ToJsonString(), out var unread) ?? throw new InvalidOperationException(unread!.Words);
        return new NodeGraph(parts.Nodes, parts.Wires, parts.Entries, parts.End ?? "") { SchemaVersion = parts.SchemaVersion, Cleanup = parts.Cleanup ?? GraphCleanup.Held };
    }

    private static NodeDeclaration Pressed(string id) => new(id, NodeKinds.Human, 1, NodeReach.Pressed);

    private static Wire After(string id, string from, string to) => new(id, WireKinds.After, new(from), new(to));

    /// <summary>A chain of <paramref name="count"/> person's tasks, each after the one before: entries its first, its end its last.</summary>
    public static NodeGraph Chain(int count, int synthetic = 0)
    {
        var nodes = Enumerable.Range(0, count).Select(index => Pressed($"h{index}")).ToList();
        var wires = Enumerable.Range(1, count - 1).Select(index => After($"w{index}", $"h{index - 1}", $"h{index}")).ToList();
        for (var index = 0; index < synthetic; index++)
        {
            nodes.Add(Pressed($"s{index}") with { Synthetic = true });
            wires.Add(After($"x{index}", "h0", $"s{index}"));
        }

        return new(nodes, wires, ["h0"], $"h{count - 1}");
    }

    /// <summary>24 person's tasks wired every earlier to every later, up to <paramref name="wires"/> wires: a graph with no cycle.</summary>
    public static NodeGraph Dense(int wires)
    {
        var chain = Chain(24);
        var extra = (from source in Enumerable.Range(0, 24)
                     from target in Enumerable.Range(source + 2, Math.Max(0, 22 - source))
                     select After($"z{source}-{target}", $"h{source}", $"h{target}"))
            .Take(wires - chain.Wires.Count);
        return chain with { Wires = [.. chain.Wires, .. extra] };
    }
}
