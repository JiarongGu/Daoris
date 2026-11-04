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
/// another node; the protected order is the look's before every landing, and the opinion's before every look and landing
/// (FABRIC-WORKFLOW2); a <c>quest.close</c>'s only way out is its <c>failed</c>
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
        new([NodeKinds.Claimed, NodeKinds.WorkRequests, NodeKinds.Land, NodeKinds.Pushed, NodeKinds.PullRequest, NodeKinds.Integrate, NodeKinds.Line], StringComparer.Ordinal);

    /// <summary>The kinds that observe the landing record (INTEGRATE1n, F47): none can be reached before a <c>handed</c> has made one.</summary>
    private static readonly string[] OfTheLanding = [NodeKinds.Pushed, NodeKinds.PullRequest, NodeKinds.Line, NodeKinds.Integrate];

    /// <summary>
    /// Everything a save refuses in <paramref name="graph"/>, in a stable order; empty when it holds. The schema and the bounds; each
    /// node's declaration; the wires' (<see cref="NodeWires.Problems"/>); the entries; the end; each node no entry reaches; the protected
    /// order; the closure's rules; the cleanup.
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

        var byId = new Dictionary<string, NodeDeclaration>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes) byId.TryAdd(node.Id, node);
        // DECIDE1b: a `back` is a way back, which NodeWires judges; no rule below reads it, as an entry's wire, a reach or an order.
        var local = graph.Wires
            .Where(wire => wire.Kind is not (WireKinds.Wait or WireKinds.Back) && WireKinds.All.Contains(wire.Kind) && wire.From is { Unit: null } from && byId.ContainsKey(from.Node)
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

        // D157 §2.2's protected order: the gates come before the landing. Of them the opinion (FABRIC-WORKFLOW2)
        // and the look are nodes (§E4), so each must be reached before every landing starts: through wires a reach satisfies, never a
        // failure's or a rejection's path. The holds stay the rules' gate at the landing, no node yet. FABRIC-CUTOVER1b (the cutover design
        // §5.1): the opinion is not asked to reach a look, whose set-up the review gate holds while an agent reads the opinion.
        // DECIDE1b: a decision's route is an order too, so a landing on one outcome's route is judged by the gates on that route.
        var ordered = local.Where(wire => WireKinds.Ordered.Contains(wire.Kind)).ToList();
        var landings = byId.Values.Where(node => node.Kind == NodeKinds.Land).ToList();
        var lands = landings;
        var looks = byId.Values.Where(node => node.Kind == NodeKinds.Look).ToList();
        var unreached = new HashSet<string>(StringComparer.Ordinal);
        // LANDNODE1c2: a `review` is the person's Accept, a gate before every landing as the look is.
        foreach (var gate in byId.Values.Where(node => node.Kind is NodeKinds.Opinion or NodeKinds.Look or NodeKinds.Review))
        {
            var after = Reach([gate.Id], ordered, forward: true);
            var later = landings.Select(landing => ("landing", landing));
            if (gate.Kind == NodeKinds.Look) unreached.UnionWith(landings.Where(landing => !after.Contains(landing.Id)).Select(landing => landing.Id));
            if (later.FirstOrDefault(each => !after.Contains(each.Item2.Id)) is ({ } what, { } early))
            {
                Say(GraphRefusal.ProtectedOrder, null, gate.Id, $"the {gate.Kind} `{gate.Id}` comes before the {what} `{early.Id}` (D157 §2.2's order: holds, opinion, look, landing).");
            }
        }

        // FABRIC-GRAPH1's review, finding 4: every look reaching a landing is not enough; no landing may start before a look has passed,
        // whichever member of an `anyOf` a run takes. One problem a landing the looks all reach, naming the first look.
        if (looks.Count > 0)
        {
            var unlooked = StartsWithNoLook(byId, starts, local);
            foreach (var landing in landings.Where(landing => !unreached.Contains(landing.Id) && unlooked.Contains(landing.Id)))
            {
                Say(GraphRefusal.ProtectedOrder, null, looks[0].Id, $"the landing `{landing.Id}` can start with no look passed, though `{looks[0].Id}` reaches it: every way to it "
                    + "passes a look (D157 §2.2's order: holds, opinion, look, landing).");
            }
        }

        // FABRIC-GRAPH1i: a skip satisfies `after` (§E2), so a person's skip of the look lets a wire from it pass while the opinion before it is
        // unsettled, and reachability holds the order no further. Where the order holds by reach, each gate is wired straight into each later
        // one, as the decoder wires a version's (FABRIC-GRAPH1h): a wire of the order's kinds, in no `anyOf` group, whose other members would
        // let a way past it. Said only where the order is not already refused, so one fault is not two.
        if (!problems.Any(problem => problem.Code == GraphRefusal.ProtectedOrder))
        {
            bool Wired(string from, string to) => ordered.Any(wire => wire.From!.Node == from && wire.To.Node == to && wire.Group?.Mode != WireGroupModes.AnyOf);
            foreach (var gate in byId.Values.Where(node => node.Kind is NodeKinds.Opinion or NodeKinds.Look or NodeKinds.Review))
            {
                var later = landings.Select(landing => ("landing", landing));
                if (later.FirstOrDefault(each => !Wired(gate.Id, each.Item2.Id)) is ({ } what, { } next))
                {
                    Say(GraphRefusal.ProtectedWire, null, gate.Id, $"the {gate.Kind} `{gate.Id}` is not wired into the {what} `{next.Id}`: a skip of a gate between them would let an `after` "
                        + $"wire pass while `{gate.Id}` is unsettled, so wire `{gate.Id}` into `{next.Id}` (D157 §2.2's order: holds, opinion, look, landing).");
                }
            }
        }

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

        foreach (var land in lands)
        {
            var method = NodeOptions.Text(land, "method");
            if (method is not null && LandNodes.Methods.Contains(method, StringComparer.Ordinal))
            {
                var after = Reach([land.Id], ordered, forward: true);
                var pushes = byId.Values.Where(node => node.Kind == NodeKinds.Pushed).ToList();
                var requests = byId.Values.Where(node => node.Kind == NodeKinds.PullRequest).ToList();
                void Tail(string kind, bool brought, List<NodeDeclaration> held)
                {
                    if (!brought)
                    {
                        foreach (var extra in held)
                        {
                            var brings = kind == NodeKinds.Integrate ? LandNodes.Integrate : kind == NodeKinds.Pushed ? LandNodes.Push : LandNodes.PullRequest;
                            Say(GraphRefusal.LandTail, null, extra.Id, $"node `{extra.Id}` is a `{kind}`, which comes with a `land` whose method is `{brings}`: "
                                + "choose that method on the landing and it brings this step.");
                        }
                    }
                    else if (held.Count == 0)
                    {
                        Say(GraphRefusal.LandTail, null, land.Id, $"node `{land.Id}`: a `{method}` land brings a `{kind}` after it, and the graph holds none.");
                    }
                    else
                    {
                        foreach (var late in held.Where(node => !after.Contains(node.Id)))
                        {
                            Say(GraphRefusal.LandTail, null, late.Id, $"node `{late.Id}`: a `{kind}` is the tail of `{land.Id}`, and no way from `{land.Id}` reaches it.");
                        }
                    }
                }

                Tail(NodeKinds.Pushed, method is LandNodes.Push or LandNodes.PullRequest, pushes);
                Tail(NodeKinds.PullRequest, method == LandNodes.PullRequest, requests);
                Tail(NodeKinds.Integrate, method == LandNodes.Integrate, integrates);
                if (method == LandNodes.PullRequest && pushes.Count > 0)
                {
                    var afterPush = Reach(pushes.Select(node => node.Id), ordered, forward: true);
                    foreach (var early in requests.Where(node => !afterPush.Contains(node.Id)))
                    {
                        Say(GraphRefusal.LandTail, null, early.Id, $"node `{early.Id}`: a `{NodeKinds.PullRequest}` comes after the `{NodeKinds.Pushed}` of `{land.Id}`, and no way from it reaches `{early.Id}`.");
                    }
                }
            }

        }

        // INTEGRATE1o (§17.3's graph rules): at most one `work.requests`, after a `claimed` and before every landing. After: a `claimed` of this graph
        // reaches it through wires an order holds. Before: no landing can start with it unreached, by the look's own reading
        // (StartsWithout), so a way to the landing past it is refused as an `anyOf` member or a bare wire alike. Each is said once,
        // against the node it is about.
        var holds = byId.Values.Where(node => node.Kind == NodeKinds.WorkRequests).ToList();
        foreach (var again in holds.Skip(1))
        {
            Say(GraphRefusal.WorkRequestsTwice, null, again.Id, $"node `{again.Id}`: `{holds[0].Id}` and `{again.Id}` are both a `{NodeKinds.WorkRequests}`, and the graph holds the work on its requests once.");
        }

        if (holds.Count > 0)
        {
            var claimed = Reach(byId.Values.Where(node => node.Kind == NodeKinds.Claimed).Select(node => node.Id), ordered, forward: true);
            foreach (var hold in holds.Where(hold => !claimed.Contains(hold.Id)))
            {
                Say(GraphRefusal.WorkRequestsUnclaimed, null, hold.Id, $"node `{hold.Id}`: a `{NodeKinds.WorkRequests}` holds the work a `{NodeKinds.Claimed}` observes, and no `{NodeKinds.Claimed}` of this graph comes before it.");
            }

            foreach (var hold in holds)
            {
                var early = StartsWithout(byId, starts, local, node => node.Id == hold.Id);
                foreach (var landing in landings.Where(landing => early.Contains(landing.Id)))
                {
                    Say(GraphRefusal.WorkRequestsLate, null, hold.Id, $"node `{hold.Id}`: the landing `{landing.Id}` can start before `{hold.Id}` is reached, and a `{NodeKinds.WorkRequests}` holds the work before every landing: "
                        + $"every way to it passes `{hold.Id}`.");
                }
            }
        }

        // LANDNODE1k: each hold once, in a graph that lands, and before every landing by the work.requests reading (StartsWithout), so a hold after
        // the landing or beside it in an `anyOf` is refused alike. Each is said once, against the hold.
        foreach (var same in byId.Values.Where(node => NodeKinds.IsHold(node.Kind)).GroupBy(node => node.Kind, StringComparer.Ordinal))
        {
            var each = same.ToList();
            foreach (var again in each.Skip(1))
            {
                Say(GraphRefusal.HoldTwice, null, again.Id, $"node `{again.Id}`: `{each[0].Id}` and `{again.Id}` are both a `{again.Kind}`, and the graph judges each hold once.");
            }

            foreach (var hold in each)
            {
                if (landings.Count == 0)
                {
                    Say(GraphRefusal.HoldUnlanded, null, hold.Id, $"node `{hold.Id}`: a `{hold.Kind}` judges the work before it lands, and the graph holds no landing.");
                    continue;
                }

                var early = StartsWithout(byId, starts, local, node => node.Id == hold.Id);
                foreach (var landing in landings.Where(landing => early.Contains(landing.Id)))
                {
                    Say(GraphRefusal.HoldLate, null, hold.Id, $"node `{hold.Id}`: the landing `{landing.Id}` can start before `{hold.Id}` is reached, and a `{hold.Kind}` judges the work before it lands: "
                        + $"every way to it passes `{hold.Id}`.");
                }
            }
        }

        // INTEGRATE1n, F47 (§17.4): every kind that observes the landing record is refused when saved where some way to it passes no `land`,
        // since it would wait for ever before a landing. A way is any wire from an entry, a failure's included, and a `land` is passed
        // through whole: its own wires are not followed. Said only of a graph that holds a landing, so one without (a quest's cycle) is as it was.
        // WORKFLOWCLEAN1 1b: the one judge at the one save point; a `land` with no method chooses none, and a stored one still reads.
        if (saved && onSave)
        {
            foreach (var chosen in lands.Where(land => !land.Options.ContainsKey("method")))
            {
                Say(GraphRefusal.LandMethod, null, chosen.Id, $"node `{chosen.Id}`: a `land` is saved with its method chosen: merge, branch, push, pull-request or integrate.");
            }
        }

        if (saved && onSave && landings.Count > 0)
        {
            var landed = landings.Select(landing => landing.Id).ToHashSet(StringComparer.Ordinal);
            var unlanded = Reach(starts, [.. local.Where(wire => !landed.Contains(wire.From!.Node))], forward: true);
            foreach (var node in byId.Values.Where(node => OfTheLanding.Contains(node.Kind, StringComparer.Ordinal) && unlanded.Contains(node.Id)))
            {
                Say(GraphRefusal.BeforeLanding, null, node.Id, $"node `{node.Id}`: {(node.Kind == NodeKinds.Integrate ? "an" : "a")} `{node.Kind}` observes the landing, and a way to it passes no "
                    + $"`{landings[0].Kind}` ({string.Join(", ", landings.Select(landing => $"`{landing.Id}`"))}), so it would wait for ever before one.");
            }
        }

        // FABRIC-GRAPH1g: §C4's second rule names the one observed node of an event's plugin step, so two of a plugin's step in one graph are
        // reached by neither (`not-named`, counted only). Said when saved, naming both.
        var firstObserved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes.Where(node => saved && node.Reach == NodeReach.Observed && NodeKinds.IsPlugin(node.Kind)))
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
        else if (cleanup.Until == GraphCleanupUntil.End && end is not null && AtOrBeforeTheLine.Contains(end.Kind))
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
