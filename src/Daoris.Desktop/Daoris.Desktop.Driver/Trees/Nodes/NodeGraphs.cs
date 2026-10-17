using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>When a graph's cleanup goes (§C3's <c>wait</c>, the graph's <c>cleanup</c>, §E7).</summary>
public static class GraphCleanupUntil
{
    /// <summary>At <i>held</i>: the line holds the work. The default, today's.</summary>
    public const string Held = "held";

    /// <summary>At the graph's end, for an end after the line, with an optional bound in days.</summary>
    public const string End = "end";
}

/// <summary>A graph's cleanup (§E7): when it goes, and for an end after the line, the bound in days (1-90, §C2).</summary>
public sealed record GraphCleanup(string Until, int? Days = null)
{
    /// <summary>At <i>held</i>, with no bound: today's, and what a graph naming none takes.</summary>
    public static readonly GraphCleanup Held = new(GraphCleanupUntil.Held);
}

/// <summary>
/// A graph (FABRIC-GRAPH1; the fabric events design §E7): <c>{ schemaVersion, nodes, wires, entries, end, cleanup }</c>. Its nodes are
/// <see cref="NodeDeclaration"/>s (NODE1), its wires <see cref="Wire"/>s (WIRE1a); <see cref="WireEvaluation"/> evaluates it (WIRE1b).
/// Each list is a copy of what was set, so a digest taken of it never moves with a caller's collection (NODE1d's rule).
/// </summary>
/// <param name="Entries">The nodes evaluation starts from: none takes a wire from another node of the graph.</param>
/// <param name="End">The node whose reach ends the work (§C2's <i>end</i>); a failure sink may end without reaching it.</param>
public sealed record NodeGraph(IReadOnlyList<NodeDeclaration> Nodes, IReadOnlyList<Wire> Wires, IReadOnlyList<string> Entries, string End)
{
    public IReadOnlyList<NodeDeclaration> Nodes { get; init; } = Nodes.ToImmutableArray();

    public IReadOnlyList<Wire> Wires { get; init; } = Wires.ToImmutableArray();

    public IReadOnlyList<string> Entries { get; init; } = Entries.ToImmutableArray();

    /// <summary>The graph's schema: <see cref="NodeGraphs.SchemaVersion"/>. D157's versions are schema 1, decoded and never saved as graphs.</summary>
    public int SchemaVersion { get; init; } = NodeGraphs.SchemaVersion;

    public GraphCleanup Cleanup { get; init; } = GraphCleanup.Held;
}

/// <summary>Why a save refuses a graph (§E7), beside the wires' own <see cref="WireRefusal"/> codes, which a graph's save names too.</summary>
public static class GraphRefusal
{
    /// <summary>What the file holds is not a graph's shape: a field of the wrong type, a list that is no list, or no JSON at all.</summary>
    public const string Unreadable = "unreadable";

    /// <summary>A schema this build does not read.</summary>
    public const string SchemaVersion = "schema-version";

    /// <summary>More authored nodes than <see cref="NodeGraphs.MaxNodes"/>.</summary>
    public const string NodesBound = "nodes-bound";

    /// <summary>More authored wires than <see cref="NodeGraphs.MaxWires"/>.</summary>
    public const string WiresBound = "wires-bound";

    /// <summary>More synthetic nodes, or wires to or from one, than the authored nodes allow.</summary>
    public const string SyntheticBound = "synthetic-bound";

    /// <summary>A node whose own declaration does not hold (<see cref="NodeDeclarations.Problem"/>).</summary>
    public const string Declaration = "declaration";

    /// <summary>A cycle after the landing declaring a node of a landing's kind, which the landing rule holds already.</summary>
    public const string LandingKind = "landing-kind";

    /// <summary>A cycle's wire into the landing's nodes: the cycle comes after the landing, and the rules' gates come before it.</summary>
    public const string IntoLanding = "into-landing";

    /// <summary>A graph with no entry: nothing would start.</summary>
    public const string NoEntry = "no-entry";

    /// <summary>An entry naming no node, or named twice.</summary>
    public const string Entry = "entry";

    /// <summary>An entry another node of the graph wires into: an entry starts the graph, and waits on nothing in it.</summary>
    public const string EntryWired = "entry-wired";

    /// <summary>No end, or an end naming no node.</summary>
    public const string End = "end";

    /// <summary>An end of a kind that may not be one: the agent's claim (§C2, §E4).</summary>
    public const string EndKind = "end-kind";

    /// <summary>A node no entry reaches.</summary>
    public const string Unreachable = "unreachable";

    /// <summary>D157 §2.2's protected order broken: the look comes before the landing.</summary>
    public const string ProtectedOrder = "protected-order";

    /// <summary>A graph holding <c>quest.close</c> whose end is not it (§E5: in a graph that holds it, it is the end).</summary>
    public const string CloseEnd = "close-end";

    /// <summary>A node after <c>quest.close</c>: only its failure's path leaves it.</summary>
    public const string AfterClose = "after-close";

    /// <summary>Canary 40: the quest's own done needed before its close, which a closure graph never writes before its end.</summary>
    public const string ClosureDeadlock = "closure-deadlock";

    /// <summary>A cleanup of no kind, a bound outside 1-90 days or at <i>held</i>, or a wait for an end at or before the line.</summary>
    public const string Cleanup = "cleanup";
}

/// <summary>
/// A graph's validation when saved, its digest, and its reading from JSON (FABRIC-GRAPH1; the fabric events design §E7). Pure: it judges
/// declarations, evaluates nothing (<see cref="WireEvaluation"/>) and stores nothing (<see cref="NodeGraphHomes"/> reads the homes).
/// </summary>
/// <remarks>
/// The choices §E7 left open, made here (D173's FABRIC-GRAPH1 note): synthetic nodes and every wire to or from one are counted apart,
/// three nodes and four wires to each authored node; Current's landing nodes are the graph's own, counted; an entry takes no wire from
/// another node; the protected order is the look's before every landing; a <c>quest.close</c>'s only way out is its <c>failed</c>
/// wire; and the cleanup's wait is refused for an end at or before the line.
/// </remarks>
public static class NodeGraphs
{
    /// <summary>The schema a graph is saved in (§E7's example). D157's named versions are schema 1.</summary>
    public const int SchemaVersion = 2;

    /// <summary>The authored nodes a graph holds at most: D157's <see cref="WorkflowKindTable.MaxSteps"/>.</summary>
    public const int MaxNodes = WorkflowKindTable.MaxSteps;

    /// <summary>The authored wires a graph holds at most (§E7).</summary>
    public const int MaxWires = 96;

    /// <summary>The synthetic nodes a graph holds at most, for each authored node (§E7: three per authored step).</summary>
    public const int SyntheticPerNode = 3;

    /// <summary>
    /// The wires to or from a synthetic node a graph holds at most, for each authored node: a decoded member's wire into its join and its
    /// failure's into a sink, and a join's out and its own failure's, three to a step at worst, so a maximal D157 version always decodes.
    /// </summary>
    public const int SyntheticWiresPerNode = 4;

    /// <summary>The kinds an end is at or before the line by: the cleanup waits for no such end (§C2, §C3).</summary>
    private static readonly HashSet<string> AtOrBeforeTheLine =
        new([NodeKinds.Claimed, NodeKinds.Handed, NodeKinds.Pushed, NodeKinds.PullRequest, NodeKinds.Line], StringComparer.Ordinal);

    /// <summary>
    /// Everything a save refuses in <paramref name="graph"/>, in a stable order; empty when it holds. The schema and the bounds; each
    /// node's declaration; the wires' (<see cref="NodeWires.Problems"/>); the entries; the end; each node no entry reaches; the protected
    /// order; the closure's rules; the cleanup.
    /// </summary>
    public static IReadOnlyList<WireProblem> Problems(NodeGraph graph)
    {
        var problems = new List<WireProblem>();
        void Say(string code, string? wire, string? node, string words) => problems.Add(new(code, wire, node, words));

        if (graph.SchemaVersion != SchemaVersion)
        {
            Say(GraphRefusal.SchemaVersion, null, null, $"the graph is schema {graph.SchemaVersion}, and this build reads schema {SchemaVersion}.");
        }

        var synthetic = graph.Nodes.Where(node => node.Synthetic).Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var authored = graph.Nodes.Count(node => !node.Synthetic);
        var syntheticNodes = graph.Nodes.Count - authored;
        var syntheticWires = graph.Wires.Count(wire => synthetic.Contains(wire.To.Node) || (wire.From is { } from && synthetic.Contains(from.Node)));
        if (authored > MaxNodes) Say(GraphRefusal.NodesBound, null, null, $"the graph has {authored} nodes, and holds at most {MaxNodes}, synthetic ones apart.");
        if (graph.Wires.Count - syntheticWires > MaxWires)
        {
            Say(GraphRefusal.WiresBound, null, null, $"the graph has {graph.Wires.Count - syntheticWires} wires, and holds at most {MaxWires}, a synthetic node's apart.");
        }

        if (syntheticNodes > SyntheticPerNode * authored || syntheticWires > SyntheticWiresPerNode * authored)
        {
            Say(GraphRefusal.SyntheticBound, null, null, $"the graph has {syntheticNodes} synthetic nodes and {syntheticWires} wires to or from them, and {authored} authored "
                + $"nodes allow at most {SyntheticPerNode * authored} and {SyntheticWiresPerNode * authored}.");
        }

        foreach (var node in graph.Nodes)
        {
            if (NodeDeclarations.Problem(node) is { } field) Say(GraphRefusal.Declaration, null, node.Id, $"node `{node.Id}`: its `{field}` does not hold for a `{node.Kind}`.");
        }

        problems.AddRange(NodeWires.Problems(graph.Nodes, graph.Wires));

        var byId = new Dictionary<string, NodeDeclaration>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes) byId.TryAdd(node.Id, node);
        var local = graph.Wires
            .Where(wire => wire.Kind != WireKinds.Wait && WireKinds.All.Contains(wire.Kind) && wire.From is { Unit: null } from && byId.ContainsKey(from.Node)
                && wire.To.Unit is null && byId.ContainsKey(wire.To.Node))
            .ToList();

        var starts = new List<string>();
        if (graph.Entries.Count == 0) Say(GraphRefusal.NoEntry, null, null, "the graph has no entry, so nothing in it would start.");
        foreach (var entry in graph.Entries)
        {
            if (!byId.ContainsKey(entry)) Say(GraphRefusal.Entry, null, entry, $"entry `{entry}` names no node of this graph.");
            else if (starts.Contains(entry, StringComparer.Ordinal)) Say(GraphRefusal.Entry, null, entry, $"entry `{entry}` is named twice.");
            else
            {
                starts.Add(entry);
                foreach (var wire in local.Where(wire => wire.To.Node == entry))
                {
                    Say(GraphRefusal.EntryWired, wire.Id, entry, $"wire `{wire.Id}`: `{entry}` is an entry, which starts the graph and waits on nothing in it.");
                }
            }
        }

        var end = !string.IsNullOrWhiteSpace(graph.End) && byId.TryGetValue(graph.End, out var named) ? named : null;
        if (end is null)
        {
            Say(GraphRefusal.End, null, string.IsNullOrWhiteSpace(graph.End) ? null : graph.End, string.IsNullOrWhiteSpace(graph.End)
                ? "the graph names no end." : $"the end `{graph.End}` names no node of this graph.");
        }
        else if (NodeAuthorities.Of(end) is { MayEnd: false })
        {
            Say(GraphRefusal.EndKind, null, end.Id, $"`{end.Id}` is a `{end.Kind}`, which is never an end: it is reached on a claim, and an end on evidence.");
        }

        if (starts.Count > 0)
        {
            var reached = Reach(starts, local, forward: true);
            foreach (var node in byId.Values.Where(node => !reached.Contains(node.Id)))
            {
                Say(GraphRefusal.Unreachable, null, node.Id, $"node `{node.Id}`: no entry reaches it, so it would never start.");
            }
        }

        // D157 §2.2's protected order: the gates come before the landing. Of them only the look is a node yet (§E4), so a look must be
        // reached before every landing starts: through wires a reach satisfies, never a failure's or a rejection's path.
        var ordered = local.Where(wire => wire.Kind is WireKinds.After or WireKinds.Trigger or WireKinds.Data).ToList();
        var landings = byId.Values.Where(node => node.Kind == NodeKinds.Handed).ToList();
        foreach (var look in byId.Values.Where(node => node.Kind == NodeKinds.Look))
        {
            var after = Reach([look.Id], ordered, forward: true);
            if (landings.FirstOrDefault(landing => !after.Contains(landing.Id)) is { } early)
            {
                Say(GraphRefusal.ProtectedOrder, null, look.Id, $"the look `{look.Id}` comes before the landing `{early.Id}` (D157 §2.2's order: holds, opinion, look, landing).");
            }
        }

        // §E5: in a graph that holds `quest.close`, it is the end, nothing comes after it but its failure's path, and nothing before it waits on
        // the quest's own done, which a closure graph never writes before its end (canary 40).
        var deadlocked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var close in byId.Values.Where(node => node.Kind == NodeKinds.QuestClose))
        {
            if (end is not null && close.Id != end.Id)
            {
                Say(GraphRefusal.CloseEnd, null, close.Id, $"`{close.Id}` closes the quest, so it is the graph's end, never `{end.Id}`: the quest would read done while its work still moves.");
            }

            foreach (var wire in local.Where(wire => wire.From!.Node == close.Id && wire.Kind != WireKinds.Failed))
            {
                Say(GraphRefusal.AfterClose, wire.Id, close.Id, $"wire `{wire.Id}`: nothing comes after `{close.Id}`, which closes the quest; only its failure's path leaves it.");
            }

            foreach (var claim in Reach([close.Id], local, forward: false).Select(id => byId[id]).Where(node => node.Kind == NodeKinds.Claimed && deadlocked.Add(node.Id)))
            {
                Say(GraphRefusal.ClosureDeadlock, null, claim.Id, $"`{claim.Id}` waits on the quest's own done before `{close.Id}` closes it, and a closure graph "
                    + "writes no done before its end: the agent submits as `ready` instead.");
            }
        }

        var cleanup = graph.Cleanup;
        if (cleanup.Until is not (GraphCleanupUntil.Held or GraphCleanupUntil.End))
        {
            Say(GraphRefusal.Cleanup, null, null, $"the cleanup waits `{cleanup.Until}`, and waits for `{GraphCleanupUntil.Held}` or `{GraphCleanupUntil.End}`.");
        }
        else if (cleanup.Days is { } days && (cleanup.Until == GraphCleanupUntil.Held || days is < 1 or > 90))
        {
            Say(GraphRefusal.Cleanup, null, null, cleanup.Until == GraphCleanupUntil.Held
                ? "a bound in days is for a cleanup waiting on the end; at `held` the line's proof is the wait."
                : $"the cleanup's bound is {days} days, and is 1 to 90.");
        }
        else if (cleanup.Until == GraphCleanupUntil.End && end is not null && AtOrBeforeTheLine.Contains(end.Kind))
        {
            Say(GraphRefusal.Cleanup, null, end.Id, $"the end `{end.Id}` is at or before the line, so its cleanup goes at `held`: the wait is for an end after the line.");
        }

        return problems;
    }

    /// <summary>The nodes reached from <paramref name="from"/> along <paramref name="wires"/>, forward or back, the starts included.</summary>
    private static HashSet<string> Reach(IEnumerable<string> from, IReadOnlyList<Wire> wires, bool forward)
    {
        var next = wires.ToLookup(wire => forward ? wire.From!.Node : wire.To.Node, wire => forward ? wire.To.Node : wire.From!.Node, StringComparer.Ordinal);
        var reached = new HashSet<string>(from, StringComparer.Ordinal);
        var queue = new Queue<string>(reached);
        while (queue.TryDequeue(out var node))
        {
            foreach (var to in next[node])
            {
                if (reached.Add(to)) queue.Enqueue(to);
            }
        }

        return reached;
    }

    /// <summary>
    /// The graph's digest (§E2's <c>graphDigest</c>, which an acceptance binds): <c>gph-</c> and 32 hex of a SHA-256 over every declaration,
    /// nodes and wires in id order, entries in order of their names and each node's ports and options by name, so the order they were
    /// written in never moves it and any declaration changed does.
    /// </summary>
    public static string Digest(NodeGraph graph)
    {
        var text = new StringBuilder();
        void Line(object? value) => text.Append(value switch
        {
            null => "\\0",
            bool flag => flag ? "1" : "0",
            IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()!.Replace("\\", "\\\\").Replace("\n", "\\n"),
        }).Append('\n');
        void Map(IReadOnlyDictionary<string, string> map)
        {
            Line(map.Count);
            foreach (var (name, value) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                Line(name);
                Line(value);
            }
        }

        void End(WireEnd? end)
        {
            Line(end?.Node);
            Line(end?.Port);
            Line(end?.Unit);
        }

        Line("graph");
        Line(graph.SchemaVersion);
        Line(graph.Nodes.Count);
        foreach (var node in graph.Nodes.OrderBy(node => node.Id, StringComparer.Ordinal).ThenBy(node => node.Kind, StringComparer.Ordinal))
        {
            Line("node");
            Line(node.Id);
            Line(node.Kind);
            Line(node.Version);
            Line(node.Reach);
            Line(node.Synthetic);
            Line(node.Evidence);
            Line(node.Retry?.MaxAttempts);
            Line(node.Retry is { } retry ? string.Join(',', retry.DelaysSeconds) : null);
            Line(node.TimeoutSeconds);
            Line(node.Rejectable);
            Line(node.Skippable);
            Map(node.Inputs);
            Map(node.Outputs);
            Map(node.Options);
        }

        Line(graph.Wires.Count);
        foreach (var wire in graph.Wires.OrderBy(wire => wire.Id, StringComparer.Ordinal).ThenBy(wire => wire.Kind, StringComparer.Ordinal))
        {
            Line("wire");
            Line(wire.Id);
            Line(wire.Kind);
            End(wire.From);
            End(wire.To);
            Line(wire.Waits?.Unit);
            Line(wire.Waits?.Revision);
            Line(wire.Waits?.Node);
            Line(wire.Waits?.End);
            Line(wire.Waits?.Commitment);
            Line(wire.Group?.Id);
            Line(wire.Group?.Mode);
        }

        Line(graph.Entries.Count);
        foreach (var entry in graph.Entries.Order(StringComparer.Ordinal)) Line(entry);
        Line(graph.End);
        Line(graph.Cleanup.Until);
        Line(graph.Cleanup.Days);
        return "gph-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..32];
    }

    /// <summary>
    /// A whole graph from its JSON (a <c>workCycles</c> entry, a named graph): the graph and what its save refuses, or no graph and the one
    /// <see cref="GraphRefusal.Unreadable"/> problem naming where it is not a graph's shape. Never read in part.
    /// </summary>
    public static (NodeGraph? Graph, IReadOnlyList<WireProblem> Problems) Read(string json)
    {
        if (Parts(json, out var unread) is not { } parts) return (null, [unread!]);
        var graph = new NodeGraph(parts.Nodes, parts.Wires, parts.Entries, parts.End ?? "") { SchemaVersion = parts.SchemaVersion, Cleanup = parts.Cleanup ?? GraphCleanup.Held };
        var problems = Problems(graph);
        return (problems.Count == 0 ? graph : null, problems);
    }

    /// <summary>What a graph's or a fragment's JSON declares, each part as written; an absent list is empty, an absent end or cleanup null.</summary>
    internal sealed record GraphParts(int SchemaVersion, List<NodeDeclaration> Nodes, List<Wire> Wires, List<string> Entries, string? End, GraphCleanup? Cleanup);

    /// <summary>The parts of <paramref name="json"/>, or null with the problem saying where it is not a graph's shape.</summary>
    internal static GraphParts? Parts(string json, out WireProblem? unread)
    {
        unread = null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new Unread("the graph", "an object");
            return new GraphParts(
                Number(root, "schemaVersion", "the graph") ?? SchemaVersion,
                [.. List(root, "nodes", "the graph").Select((node, index) => Node(node, $"nodes[{index}]"))],
                [.. List(root, "wires", "the graph").Select((wire, index) => WireOf(wire, $"wires[{index}]"))],
                [.. List(root, "entries", "the graph").Select((entry, index) => entry.ValueKind == JsonValueKind.String ? entry.GetString()! : throw new Unread($"entries[{index}]", "a node's id"))],
                Text(root, "end", "the graph"),
                root.TryGetProperty("cleanup", out var cleanup) ? CleanupOf(cleanup) : null);
        }
        catch (JsonException error)
        {
            unread = new(GraphRefusal.Unreadable, null, null, $"the graph is not JSON: {error.Message}");
        }
        catch (Unread error)
        {
            unread = new(GraphRefusal.Unreadable, null, null, error.Message);
        }

        return null;
    }

    /// <summary>A place in the file that is not what a graph holds there.</summary>
    private sealed class Unread(string where, string what) : Exception($"{where} is not a graph's shape: it holds no {what} there.");

    private static IEnumerable<JsonElement> List(JsonElement parent, string name, string where) =>
        !parent.TryGetProperty(name, out var list) || list.ValueKind == JsonValueKind.Null ? []
        : list.ValueKind == JsonValueKind.Array ? list.EnumerateArray().ToList()
        : throw new Unread($"{where}'s `{name}`", "list");

    private static string? Text(JsonElement parent, string name, string where) =>
        !parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null ? null
        : value.ValueKind == JsonValueKind.String ? value.GetString()
        : throw new Unread($"{where}'s `{name}`", "text");

    private static int? Number(JsonElement parent, string name, string where) =>
        !parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null ? null
        : value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number
        : throw new Unread($"{where}'s `{name}`", "whole number");

    private static bool? Flag(JsonElement parent, string name, string where) =>
        !parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null ? null
        : value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean()
        : throw new Unread($"{where}'s `{name}`", "`true` or `false`");

    private static Dictionary<string, string> Names(JsonElement parent, string name, string where, bool anyValue = false)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return map;
        if (value.ValueKind != JsonValueKind.Object) throw new Unread($"{where}'s `{name}`", "object");
        foreach (var property in value.EnumerateObject())
        {
            map[property.Name] = anyValue ? NodeGraphHomes.Compact(property.Value)
                : property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString()! : throw new Unread($"{where}'s `{name}.{property.Name}`", "schema");
        }

        return map;
    }

    private static NodeDeclaration Node(JsonElement node, string where)
    {
        if (node.ValueKind != JsonValueKind.Object) throw new Unread(where, "node");
        // A synthetic node is decoding's alone (§E7): a file declaring one would escape the authored bound.
        if (node.TryGetProperty("synthetic", out _)) throw new Unread($"{where}'s `synthetic`", "field a person's graph declares");
        var retry = node.TryGetProperty("retry", out var declared) && declared.ValueKind != JsonValueKind.Null ? RetryOf(declared, $"{where}'s `retry`") : null;
        return new NodeDeclaration(
            Text(node, "id", where) ?? throw new Unread($"{where}'s `id`", "text"),
            Text(node, "kind", where) ?? throw new Unread($"{where}'s `kind`", "text"),
            Number(node, "version", where) ?? throw new Unread($"{where}'s `version`", "whole number"),
            Text(node, "reach", where) ?? throw new Unread($"{where}'s `reach`", "text"))
        {
            Inputs = Names(node, "inputs", where),
            Outputs = Names(node, "outputs", where),
            Options = Names(node, "options", where, anyValue: true),
            Evidence = Text(node, "evidence", where),
            Retry = retry,
            TimeoutSeconds = Number(node, "timeoutSeconds", where),
            Rejectable = Flag(node, "rejectable", where) ?? false,
            Skippable = Flag(node, "skippable", where) ?? true,
        };
    }

    private static NodeRetry RetryOf(JsonElement retry, string where)
    {
        if (retry.ValueKind != JsonValueKind.Object) throw new Unread(where, "object");
        return new NodeRetry(
            Number(retry, "maxAttempts", where) ?? throw new Unread($"{where}'s `maxAttempts`", "whole number"),
            [.. List(retry, "delaysSeconds", where).Select(delay => delay.ValueKind == JsonValueKind.Number && delay.TryGetInt32(out var seconds) ? seconds : throw new Unread($"{where}'s `delaysSeconds`", "whole number"))]);
    }

    private static Wire WireOf(JsonElement wire, string where)
    {
        if (wire.ValueKind != JsonValueKind.Object) throw new Unread(where, "wire");
        var kind = Text(wire, "kind", where) ?? throw new Unread($"{where}'s `kind`", "text");
        var to = Text(wire, "to", where) ?? throw new Unread($"{where}'s `to`", "node");
        WaitTarget? waits = null;
        WireEnd? from = null;
        if (wire.TryGetProperty("from", out var source) && source.ValueKind != JsonValueKind.Null)
        {
            if (source.ValueKind == JsonValueKind.String) from = EndOf(source.GetString()!, kind);
            else if (source.ValueKind == JsonValueKind.Object)
            {
                waits = new WaitTarget(Text(source, "unit", $"{where}'s `from`") ?? "", Text(source, "revision", $"{where}'s `from`") ?? "")
                {
                    Node = Text(source, "node", $"{where}'s `from`"),
                    End = Flag(source, "end", $"{where}'s `from`") ?? false,
                    Commitment = Text(source, "commitment", $"{where}'s `from`"),
                };
            }
            else throw new Unread($"{where}'s `from`", "node, or what a wait waits on");
        }

        WireGroup? group = null;
        if (wire.TryGetProperty("group", out var declared) && declared.ValueKind != JsonValueKind.Null)
        {
            if (declared.ValueKind != JsonValueKind.Object) throw new Unread($"{where}'s `group`", "object");
            group = new WireGroup(Text(declared, "id", $"{where}'s `group`") ?? "", Text(declared, "mode", $"{where}'s `group`") ?? "");
        }

        return new Wire(Text(wire, "id", where) ?? "", kind, from, EndOf(to, kind)) { Waits = waits, Group = group };
    }

    /// <summary>A wire's end as the file names it: a node, or on a <c>data</c> wire its port after the last dot (<c>qa.evidence</c>).</summary>
    private static WireEnd EndOf(string named, string kind) =>
        kind == WireKinds.Data && named.LastIndexOf('.') is > 0 and var dot && dot < named.Length - 1 ? new(named[..dot], named[(dot + 1)..]) : new(named);

    private static GraphCleanup CleanupOf(JsonElement cleanup)
    {
        if (cleanup.ValueKind != JsonValueKind.Object) throw new Unread("the graph's `cleanup`", "object");
        return new GraphCleanup(Text(cleanup, "until", "the graph's `cleanup`") ?? GraphCleanupUntil.Held, Number(cleanup, "days", "the graph's `cleanup`"));
    }
}

/// <summary>
/// Which graph governs a unit, and from where (FABRIC-GRAPH1).
/// </summary>
/// <param name="Kind">The unit's kind (<see cref="WorkUnitKind"/>).</param>
/// <param name="Source">A <see cref="LandingSource"/>: the repository's, its workspace's, or the default.</param>
/// <param name="Cycle">The governing cycle's JSON text as the file holds it, compact, or null where none is set.</param>
/// <param name="Graph">The graph, or null where it does not hold, or no graph governs (a kind with no cycle set: today's).</param>
/// <param name="Problems">What the governing cycle refuses; empty where it holds. A cycle that does not hold is said, never run.</param>
public sealed record GraphHome(string Kind, string Source, string? Cycle, NodeGraph? Graph, IReadOnlyList<WireProblem> Problems)
{
    public bool Readable => Problems.Count == 0;
}

/// <summary>
/// A value <c>driver.json</c> keeps under the cycle's homes that no unit reads (<see cref="NodeGraphHomes.Strays"/>): where it is, from the
/// file's top (<c>workCycles</c>, the owner, the kind), its code, <c>kind</c> or <c>shadowed</c>, and the sentence that says it.
/// </summary>
public sealed record CycleStray(IReadOnlyList<string> At, string Code, string Words)
{
    /// <summary>A kind with no work cycle: only <c>quest</c> and <c>ask</c> have one, a branch's being its landing rule's.</summary>
    public const string Kind = "kind";

    /// <summary>A name an earlier entry of the same map spells in another case: the first written governs.</summary>
    public const string Shadowed = "shadowed";
}

/// <summary>
/// Current's graphs, derived from the rules (FABRIC-GRAPH1; the fabric events design §E7, §C2, §D7): a branch's from its landing rule,
/// the landing's own nodes first and the rule's <see cref="LandingRule.Cycle"/> after them; a quest's or an ask's from
/// <see cref="DriverConfig.WorkCycles"/>, else <see cref="DriverConfig.WorkspaceWorkCycles"/>. Inherited as D164's amendment inherits a
/// rule: a repository's own replaces its workspace's whole. Nothing set is today exactly. The CLI's twin is <c>cycles.ts</c>, and both
/// read <c>test/fixtures/graph-homes.json</c>.
/// </summary>
public static class NodeGraphHomes
{
    /// <summary>The kinds <c>workCycles</c> keys: a branch's cycle is its landing rule's alone, so no kind has two homes (§D7).</summary>
    public static IReadOnlyList<string> WorkCycleKinds { get; } = [WorkUnitKind.Quest, WorkUnitKind.Ask];

    private static readonly string[] Landed = [NodeKinds.Handed, NodeKinds.Line];

    private static readonly string[] Pulled = [NodeKinds.Handed, NodeKinds.Pushed, NodeKinds.PullRequest, NodeKinds.Line];

    private static readonly JsonWriterOptions CompactWriting = new() { Indented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// The landing's own graph, as its rule reaches: <c>handed</c>, then <c>pushed</c> and <c>pull-request</c> where a branch rule names
    /// the plugin that pushes, then <c>line</c>, each after the one before; entered at <c>handed</c>, ended at <c>line</c>, cleaned up
    /// at <i>held</i>. A rule with no cycle is this, today's behaviour (§C2's default).
    /// </summary>
    public static NodeGraph Landing(LandingRule rule)
    {
        var kinds = rule.Form == LandingForm.Branch && rule.Plugin is not null ? Pulled : Landed;
        return new NodeGraph(
            [.. kinds.Select(kind => new NodeDeclaration(kind, kind, 1, NodeAuthorities.BuiltIn[kind].Reach))],
            [.. kinds.Skip(1).Select((kind, index) => new Wire($"landing/{kinds[index]}-{kind}", WireKinds.After, new(kinds[index]), new(kind)))],
            [NodeKinds.Handed], NodeKinds.Line);
    }

    /// <summary>
    /// A branch's graph under <paramref name="rule"/>: the landing's (<see cref="Landing"/>), and its <c>cycle</c> after it, whose wires
    /// may leave the landing's nodes and never enter them, whose entries start beside <c>handed</c>, and whose end and cleanup are the
    /// graph's (the default <c>line</c> and <i>held</i>).
    /// </summary>
    public static GraphHome OfLanding(LandingRule rule, string source)
    {
        var landing = Landing(rule);
        if (rule.Cycle is null) return new(WorkUnitKind.Branch, source, null, landing, []);
        if (NodeGraphs.Parts(rule.Cycle, out var unread) is not { } parts) return new(WorkUnitKind.Branch, source, rule.Cycle, null, [unread!]);

        var problems = new List<WireProblem>();
        var held = landing.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var node in parts.Nodes.Where(node => Pulled.Contains(node.Kind, StringComparer.Ordinal)))
        {
            problems.Add(new(GraphRefusal.LandingKind, null, node.Id, $"node `{node.Id}` is a `{node.Kind}`, a landing's node, which the landing rule holds already: wire from it."));
        }

        // A wire into the landing is said here and left out of the graph judged below, so it is not said again as an entry's wire.
        bool Into(Wire wire) => wire.To.Unit is null && held.Contains(wire.To.Node);
        problems.AddRange(parts.Wires.Where(Into).Select(wire => new WireProblem(GraphRefusal.IntoLanding, wire.Id, wire.To.Node,
            $"wire `{wire.Id}`: the cycle comes after the landing, and `{wire.To.Node}` is the landing's; what comes before it is the rules' gates.")));

        var graph = new NodeGraph([.. landing.Nodes, .. parts.Nodes], [.. landing.Wires, .. parts.Wires.Where(wire => !Into(wire))], [.. landing.Entries, .. parts.Entries], parts.End ?? landing.End)
        {
            SchemaVersion = parts.SchemaVersion,
            Cleanup = parts.Cleanup ?? GraphCleanup.Held,
        };
        problems.AddRange(NodeGraphs.Problems(graph));
        return new(WorkUnitKind.Branch, source, rule.Cycle, problems.Count == 0 ? graph : null, problems);
    }

    /// <summary>A branch's graph in <paramref name="repository"/>: its effective landing rule's (<see cref="LandingRules.Choose"/>), cycle and all.</summary>
    public static GraphHome Current(DriverConfig config, string repository, string? workspace)
    {
        var landing = LandingRules.Choose(config, repository, workspace);
        return OfLanding(landing.Rule, landing.Source);
    }

    /// <summary>
    /// The graph governing a unit of <paramref name="kind"/> in <paramref name="repository"/>: a branch's is <see cref="Current"/>'s; a quest's
    /// or an ask's the repository's own for that kind, else its workspace's (a repository in no workspace is in <c>default</c>'s), else
    /// none, today's. A repository naming one kind takes its workspace's for another.
    /// </summary>
    public static GraphHome Work(DriverConfig config, string repository, string? workspace, string kind)
    {
        if (kind == WorkUnitKind.Branch) return Current(config, repository, workspace);
        if (WorkCycleKinds.Contains(kind))
        {
            if (config.WorkCycles.TryGetValue(repository.Trim(), out var own) && own.TryGetValue(kind, out var mine)) return Whole(kind, LandingSource.Repository, mine);
            if (config.WorkspaceWorkCycles.TryGetValue(RemoteTarget.Workspace(workspace), out var shared) && shared.TryGetValue(kind, out var theirs))
            {
                return Whole(kind, LandingSource.Workspace, theirs);
            }
        }

        return new(kind, LandingSource.Default, null, null, []);
    }

    /// <summary>
    /// What the file keeps under the cycle's homes that no unit reads (FABRIC-GRAPH1's review, findings 2 and 3), each kept as written and
    /// said here: a kind with no work cycle under <c>workCycles</c> or <c>workspaceWorkCycles</c>, a branch's included; and a name an earlier
    /// entry of <c>landings</c>, <c>workspaceLandings</c> or the work cycles spells in another case, where the first written governs. The CLI's
    /// <c>cycleStrays</c> says the same, row for row (<c>graph-homes.json</c>'s <c>strays</c>).
    /// </summary>
    public static IReadOnlyList<CycleStray> Strays(DriverConfig config) => [];

    private static GraphHome Whole(string kind, string source, string cycle)
    {
        var (graph, problems) = NodeGraphs.Read(cycle);
        return new(kind, source, cycle, graph, problems);
    }

    /// <summary>
    /// <paramref name="json"/> as the config keeps it: compact, its keys in the order written, its text unescaped where JSON allows, so the
    /// file says what the person wrote. Throws <see cref="JsonException"/> where it is not JSON.
    /// </summary>
    public static string Compact(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Compact(document.RootElement);
    }

    /// <summary><paramref name="element"/>'s JSON, compact.</summary>
    public static string Compact(JsonElement element)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, CompactWriting)) element.WriteTo(writer);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
