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
/// another node; a <c>quest.close</c>'s only way out is its <c>failed</c> wire; and the cleanup's wait is refused for an end at or before
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
    /// node's declaration; the wires' (<see cref="NodeWires.Problems"/>); the entries; the end; each node no entry reaches; the landing's;
    /// the kinds' (<see cref="NodeKit.Problems"/>); the closure's rules; the cleanup.
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
                if (node.Kind == NodeKinds.Land && field == LandNodes.AcceptOption)
                {
                    Say(GraphRefusal.Accept, null, node.Id, $"node `{node.Id}`: a `land` takes no `accept`. Where you accept the work, put *Your review* before it; with no review it lands once the steps before it are done.");
                }
                else
                {
                    Say(GraphRefusal.Declaration, null, node.Id, $"node `{node.Id}`: its `{field}` does not hold for a `{node.Kind}`.");
                }
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
            Say(GraphRefusal.EndKind, null, end.Id, end.Kind == NodeKinds.Stop
                ? $"`{end.Id}` is a `{end.Kind}`, which is never an end: a failure stops the run there, short of its end."
                : $"`{end.Id}` is a `{end.Kind}`, which is never an end: it is reached on a claim, and an end on evidence.");
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

        var (ordered, landings) = (standing.Ordered, standing.Landings);

        // INTEGRATE1n (§17.4's graph rules): at most one `integrate`, after the landing, and a branch no plugin lands: no `pushed` or
        // `pull-request` beside it. Each is said against the node it is about, once.
        var integrates = byId.Values.Where(node => node.Kind == NodeKinds.Integrate).ToList();
        foreach (var again in integrates.Skip(1))
        {
            Say(GraphRefusal.IntegrateTwice, null, again.Id, $"node `{again.Id}`: `{integrates[0].Id}` and `{again.Id}` are both an `{NodeKinds.Integrate}`, and the graph's landing is integrated once.");
        }

        if (integrates.Count > 0)
        {
            if (landings.Count == 0)
            {
                Say(GraphRefusal.IntegrateUnlanded, null, integrates[0].Id, $"node `{integrates[0].Id}`: an `{NodeKinds.Integrate}` integrates the landing a `{NodeKinds.Land}` makes, and the graph holds no `{NodeKinds.Land}`.");
            }
        }

        // LANDNODE1a (the land node design §6): one landing, and the method's tail and no other. LANDNODE1c2: what precedes a `land` decides
        // whether it waits on a person; nothing here refuses one that starts without a press.
        foreach (var again in landings.Skip(1))
        {
            Say(GraphRefusal.LandOnce, null, again.Id, $"node `{again.Id}`: `{landings[0].Id}` and `{again.Id}` are both a landing, and a graph holds one `{NodeKinds.Land}`.");
        }

        foreach (var land in landings)
        {
            var method = NodeOptions.Text(land, "method");
            if (method is not null && LandNodes.Methods.Contains(method, StringComparer.Ordinal))
            {
                // An integrate is the tail of its own method (G8).
                var after = Reach([land.Id], ordered, forward: true);
                if (method != LandNodes.Integrate)
                {
                    foreach (var extra in integrates)
                    {
                        Say(GraphRefusal.LandTail, null, extra.Id, $"node `{extra.Id}` is a `{NodeKinds.Integrate}`, which comes with a `land` whose method is `{LandNodes.Integrate}`: "
                            + "choose that method on the landing and it brings this step.");
                    }
                }
                else if (integrates.Count == 0)
                {
                    Say(GraphRefusal.LandTail, null, land.Id, $"node `{land.Id}`: a `{method}` land brings a `{NodeKinds.Integrate}` after it, and the graph holds none.");
                }
                else
                {
                    foreach (var late in integrates.Where(node => !after.Contains(node.Id)))
                    {
                        Say(GraphRefusal.LandTail, null, late.Id, $"node `{late.Id}`: a `{NodeKinds.Integrate}` is the tail of `{land.Id}`, and no way from `{land.Id}` reaches it.");
                    }
                }
            }
        }

        // D247: the kinds' rules, which the kit runs: the protected order the gates share, then each kind's own of where its node stands (a push
        // after a branch landing, a monitor on a step that reports a state, a tidy on a monitor's merged route, a hold before the landing).
        problems.AddRange(NodeKit.Problems(standing, graph));

        // INTEGRATE1n, F47 (§17.4): every kind that observes the landing record is refused when saved where some way to it passes no `land`,
        // since it would wait for ever before a landing. A way is any wire from an entry, a failure's included, and a `land` is passed
        // through whole: its own wires are not followed. Said only of a graph that holds a landing, so one without (a quest's cycle) is as it was.
        // WORKFLOWCLEAN1 1b: the one judge at the one save point; a `land` with no method chooses none, and a stored one still reads.
        if (saved && onSave)
        {
            foreach (var chosen in landings.Where(land => !land.Options.ContainsKey("method")))
            {
                Say(GraphRefusal.LandMethod, null, chosen.Id, $"node `{chosen.Id}`: a `land` is saved with its method chosen: {string.Join(", ", LandNodes.Methods.SkipLast(1))} or {LandNodes.Methods[^1]}.");
            }
        }

        if (saved && onSave && landings.Count > 0)
        {
            var landed = landings.Select(landing => landing.Id).ToHashSet(StringComparer.Ordinal);
            var unlanded = Reach(starts, [.. local.Where(wire => !landed.Contains(wire.From!.Node))], forward: true);
            foreach (var node in byId.Values.Where(node => NodeKit.Of(node.Kind)?.ReadsTheLanding == true && unlanded.Contains(node.Id)))
            {
                Say(GraphRefusal.BeforeLanding, null, node.Id, $"node `{node.Id}`: {(node.Kind == NodeKinds.Integrate ? "an" : "a")} `{node.Kind}` observes the landing, and a way to it passes no "
                    + $"`{landings[0].Kind}` ({string.Join(", ", landings.Select(landing => $"`{landing.Id}`"))}), so it would wait for ever before one.");
            }
        }

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
