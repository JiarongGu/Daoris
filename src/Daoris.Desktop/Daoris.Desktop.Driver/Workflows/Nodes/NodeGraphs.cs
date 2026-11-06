using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static Daoris.Driver.NodeGraphStarts;

namespace Daoris.Driver;
/// <summary>
/// A graph's validation when saved, its digest, and its reading from JSON (FABRIC-GRAPH1; the fabric events design §E7). Pure: it judges
/// declarations, evaluates nothing (<see cref="WireEvaluation"/>) and stores nothing (<see cref="NodeGraphHomes"/> reads the homes).
/// </summary>
/// <remarks>
/// The choices §E7 left open, made here (D173's FABRIC-GRAPH1 note): synthetic nodes and every wire to or from one are counted apart,
/// three nodes and four wires to each authored node; Current's landing nodes are the graph's own, counted; an entry takes no wire from
/// another node; and the cleanup's wait is refused for an end at or before
/// the line. The protected order before the landing is the kit's (<see cref="ProtectedOrder"/>).
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

    /// <summary>
    /// Everything a save refuses in <paramref name="graph"/>, in a stable order; empty when it holds. The schema and the bounds; each
    /// node's declaration; the wires' (<see cref="NodeWires.Problems"/>); the entries; the end; each node no entry reaches;
    /// the kinds' (<see cref="NodeKit.Problems"/>: the landing's, the integration's and the closure's among them); the cleanup.
    /// </summary>
    /// <param name="graph">The graph judged.</param>
    /// <param name="saved">True for a graph a person authored and saves (a named graph version, a cycle): the rules only an authored graph is held to, the
    /// observed nodes of one plugin step (FABRIC-GRAPH1g). False for a graph read back from a file that kept it.</param>
    /// <param name="onSave">True where the graph is being saved now, rather than read from a file that kept it: INTEGRATE1n's F47 (a node that
    /// observes the landing, with a way to it that passes no <c>handed</c>) is refused then alone, since a stored version that read before is
    /// never made unreadable by a rule added after it (<c>compatible-builds.md</c>). Current's own graphs are judged on save, as they are drawn.</param>
    public static IReadOnlyList<WireProblem> Problems(NodeGraph graph, bool saved = true, bool onSave = true)
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
            if (NodeDeclarations.Problem(node) is { } field)
            {
                problems.Add(NodeKit.Of(node.Kind)?.DeclarationProblem(node, field)
                    ?? new(GraphRefusal.Declaration, null, node.Id, $"node `{node.Id}`: its `{field}` does not hold for a `{node.Kind}`."));
            }
            // DECIDE1d: judged with the graph, so the declarations' own checks are left as they were.
            else if (NodeOutcomes.OutputsOnProblem(node) is not null) Say(GraphRefusal.Declaration, null, node.Id, $"node `{node.Id}`: its `outputsOn` does not hold for a `{node.Kind}`.");
        }

        problems.AddRange(NodeWires.Problems(graph.Nodes, graph.Wires));

        var standing = GraphStanding.Of(graph);
        var (byId, local, starts) = (standing.ById, standing.Local, standing.Starts);
        if (graph.Entries.Count == 0) Say(GraphRefusal.NoEntry, null, null, "the graph has no entry, so nothing in it would start.");
        var entered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in graph.Entries)
        {
            if (!byId.ContainsKey(entry)) Say(GraphRefusal.Entry, null, entry, $"entry `{entry}` names no node of this graph.");
            else if (!entered.Add(entry)) Say(GraphRefusal.Entry, null, entry, $"entry `{entry}` is named twice.");
            else
            {
                foreach (var wire in local.Where(wire => wire.To.Node == entry))
                {
                    Say(GraphRefusal.EntryWired, wire.Id, entry, $"wire `{wire.Id}`: `{entry}` is an entry, which starts the graph and waits on nothing in it.");
                }
            }
        }

        // DECIDE1b: the end need not lie on every route; a run whose routes not taken leave nothing to start has ended (NodeRuntime.End).
        var end = !string.IsNullOrWhiteSpace(graph.End) && byId.TryGetValue(graph.End, out var named) ? named : null;
        if (end is null)
        {
            Say(GraphRefusal.End, null, string.IsNullOrWhiteSpace(graph.End) ? null : graph.End, string.IsNullOrWhiteSpace(graph.End)
                ? "the graph names no end." : $"the end `{graph.End}` names no node of this graph.");
        }
        else if (NodeAuthorities.Of(end) is { MayEnd: false })
        {
            Say(GraphRefusal.EndKind, null, end.Id, $"`{end.Id}` is a `{end.Kind}`, which is never an end: {NodeKit.Of(end.Kind)!.NeverEndBecause}");
        }

        if (starts.Count > 0)
        {
            var reached = Reach(starts, local, forward: true);
            foreach (var node in byId.Values.Where(node => !reached.Contains(node.Id)))
            {
                Say(GraphRefusal.Unreachable, null, node.Id, $"node `{node.Id}`: no entry reaches it, so it would never start.");
            }

            // DECIDE1d: a `data` wire takes an output its decision may carry on some outcomes alone. Each other outcome is judged as the run takes it,
            // its decision's `when` wires of the rest never passing: a target that can start so would wait for ever on an output never carried.
            foreach (var wire in local.Where(wire => wire.Kind == WireKinds.Data))
            {
                var source = byId[wire.From!.Node];
                if (wire.From.Port is not { } port || !source.OutputsOn.TryGetValue(port, out var carried)) continue;
                bool Closed(string outcome, Wire other) => other.Kind == WireKinds.When && other.From!.Node == source.Id && other.Outcome != outcome;
                if (source.Outcomes.FirstOrDefault(outcome => !carried.Contains(outcome, StringComparer.Ordinal)
                    && StartsWithout(byId, starts, local, _ => false, other => Closed(outcome, other)).Contains(wire.To.Node)) is { } uncarried)
                {
                    var on = string.Join(" or ", carried.Select(outcome => $"`{outcome}`"));
                    Say(GraphRefusal.UncarriedOutput, wire.Id, wire.To.Node, $"wire `{wire.Id}`: `{source.Id}` carries `{port}` only when it says {on}, and `{wire.To.Node}` can start "
                        + $"after it says `{uncarried}`, with no `{port}` to take: put `{wire.To.Node}` on the route of a `when` naming {on}.");
                }
            }
        }

        // D247: the kinds' rules, which the kit runs: the protected order the gates share, then each kind's own of where its node stands (a push
        // after a branch landing, a monitor on a step that reports a state, a tidy on a monitor's merged route, a hold before the landing, an
        // integrate after its landing, a close at the end), and what a saved graph's landing needs of the kinds that read it.
        problems.AddRange(NodeKit.Problems(standing, graph, saved && onSave));

        // FABRIC-GRAPH1g: §C4's second rule names the one observed node of an event's plugin step, so two of a plugin's step in one graph are
        // reached by neither (`not-named`, counted only). Said when saved, naming both.
        var firstObserved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes.Where(node => saved && node.Reach == NodeReach.Observed && NodeKit.IsPlugin(node.Kind)))
        {
            if (!firstObserved.TryAdd(node.Kind, node.Id))
            {
                Say(GraphRefusal.ObservedTwice, null, node.Id, $"node `{node.Id}` and `{firstObserved[node.Kind]}` are both observed nodes of `{node.Kind}`: an event of that step names one node, "
                    + "so neither would be reached.");
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
        else if (cleanup.Until == GraphCleanupUntil.End && end is not null && NodeKit.Of(end.Kind)?.EndsByTheLine == true)
        {
            Say(GraphRefusal.Cleanup, null, end.Id, $"the end `{end.Id}` is at or before the line, so its cleanup goes at `held`: the wait is for an end after the line.");
        }

        return problems;
    }

    public static string Digest(NodeGraph graph) => NodeGraphPlaces.Digest(graph);

    public static IReadOnlyDictionary<string, string> Places(NodeGraph graph) => NodeGraphPlaces.Places(graph);

    public const string PlacePrefix = NodeGraphPlaces.PlacePrefix;

    /// <summary>
    /// A whole graph from its JSON (a named graph, a bound quest's closure): the graph and what its save refuses, or no graph and the one
    /// <see cref="GraphRefusal.Unreadable"/> problem naming where it is not a graph's shape. Never read in part.
    /// </summary>
    /// <param name="json">The graph's JSON.</param>
    /// <param name="onSave">True where the graph is being saved (a version added, a cycle set): INTEGRATE1n's F47 refusal is the save's alone, so
    /// a stored graph that read before still reads (see <see cref="Problems"/>).</param>
    public static (NodeGraph? Graph, IReadOnlyList<WireProblem> Problems) Read(string json, bool onSave = false)
    {
        if (Parts(json, out var unread) is not { } parts) return (null, [unread!]);
        var graph = new NodeGraph(parts.Nodes, parts.Wires, parts.Entries, parts.End ?? "") { SchemaVersion = parts.SchemaVersion, Cleanup = parts.Cleanup ?? GraphCleanup.Held };
        var problems = Problems(graph, onSave: onSave);
        return (problems.Count == 0 ? graph : null, problems);
    }

    internal static GraphParts? Parts(string json, out WireProblem? unread) => NodeGraphReading.Parts(json, out unread);
}
