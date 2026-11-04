using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static Daoris.Driver.NodeGraphs;

namespace Daoris.Driver;

/// <summary>Which nodes of a graph can start without a gate having passed, and what each node reaches: the reading the save's protected-order rules judge on.</summary>
internal static class NodeGraphStarts
{
    /// <summary>
    /// The nodes that can start while no look has passed (FABRIC-GRAPH1's review, finding 4): the entries, and each node whose eligibility
    /// groups (<see cref="NodeWires.Groups"/>: all of an <c>allOf</c>, one of an <c>anyOf</c>, every group) and inputs can all be met so. A
    /// reach's wire (<c>after</c>, <c>trigger</c>) or an input is met by a node that starts so and is no look; a failure's or a rejection's
    /// by any node that starts so, a look included, since a look that fails has not passed.
    /// <para>An <c>all</c> join follows <see cref="WireEvaluation"/> (the re-check's finding 4): its <c>after</c> wires are its members and
    /// no group's, and it starts once every member is reached or skipped, or once any one has failed. All its members met so, it starts as
    /// any node does. A member that starts so and may fail (a look among them) starts it too, but only to fail at once while the rest
    /// still wait: then only its failure's and rejection's paths are met, never its pass, so a landing on them passes no look.</para>
    /// <para>A node fails only where its kind's authority lets something fail it, and a join only where a member may (the second re-check,
    /// finding 3: <see cref="MayFail"/>): a join of a look and a person's press has no failed path, so its failure's wire is never met.</para>
    /// </summary>
    internal static HashSet<string> StartsWithNoLook(Dictionary<string, NodeDeclaration> byId, IReadOnlyList<string> starts, IReadOnlyList<Wire> local) =>
        StartsWithout(byId, starts, local, node => node.Kind == NodeKinds.Look);

    /// <summary>
    /// The nodes that can start while no node <paramref name="gate"/> names has passed: <see cref="StartsWithNoLook"/>'s reading, whichever node
    /// is the gate (the look's, and INTEGRATE1o's <c>work.requests</c>, whose own id is the gate it names). A wire <paramref name="closed"/> names
    /// is never met, and closes its target as a route not taken does (DECIDE1d: a decision's other outcomes' <c>when</c> wires).
    /// </summary>
    internal static HashSet<string> StartsWithout(
        Dictionary<string, NodeDeclaration> byId, IReadOnlyList<string> starts, IReadOnlyList<Wire> local, Func<NodeDeclaration, bool> gate, Func<Wire, bool>? closed = null)
    {
        var started = new HashSet<string>(starts, StringComparer.Ordinal);
        var failing = new HashSet<string>(StringComparer.Ordinal);
        var mayFail = MayFail(byId, local);
        // A reach's wire, a `when` among them (DECIDE1b): met by its source's reach, with that outcome, never by its failure.
        bool Met(Wire wire) => closed?.Invoke(wire) != true && wire.Kind switch
        {
            WireKinds.Failed => mayFail.Contains(wire.From!.Node) && (started.Contains(wire.From.Node) || failing.Contains(wire.From.Node)),
            WireKinds.Rejected => started.Contains(wire.From!.Node) || failing.Contains(wire.From!.Node),
            _ => started.Contains(wire.From!.Node) && !gate(byId[wire.From.Node]),
        };

        // DECIDE1b: the nodes a route may leave untaken, as WireEvaluation's NotTaken closes them: behind a `when` from a decision that starts
        // (another outcome closes it), or a wire from such a node, by a data input, an `allOf` member or every member of an `anyOf`. A join's
        // members never close it, and settle it as skips do, so whichever outcome a run takes, a join on its route starts.
        var untaken = new HashSet<string>(StringComparer.Ordinal);
        bool Closes(Wire wire) => closed?.Invoke(wire) == true || untaken.Contains(wire.From!.Node) || (wire.Kind == WireKinds.When && started.Contains(wire.From.Node));
        for (var moved = true; moved;)
        {
            moved = false;
            foreach (var node in byId.Values)
            {
                var join = node.Kind == NodeKinds.All;
                var members = join ? local.Where(wire => wire.Kind == WireKinds.After && wire.To.Node == node.Id).ToList() : [];
                var groups = NodeWires.Groups(node.Id, join ? [.. local.Where(wire => !members.Contains(wire))] : local);
                var inputs = local.Where(wire => wire.Kind == WireKinds.Data && wire.To.Node == node.Id).ToList();
                // A wire id twice is refused above (`wire-twice`); its first is read here, as a group reads it.
                Wire Into(string id) => local.First(wire => wire.Id == id && wire.To.Node == node.Id);
                if (!untaken.Contains(node.Id) && (inputs.Any(Closes)
                    || groups.Any(group => group.Mode == WireGroupModes.AnyOf ? group.Wires.All(id => Closes(Into(id))) : group.Wires.Any(id => Closes(Into(id))))))
                {
                    moved = untaken.Add(node.Id) || moved;
                }

                if (started.Contains(node.Id) || groups.Count + inputs.Count + members.Count == 0 || !inputs.All(Met)) continue;
                bool MetBy(string id) => Met(Into(id));
                if (!groups.All(group => group.Mode == WireGroupModes.AnyOf ? group.Wires.Any(MetBy) : group.Wires.All(MetBy))) continue;
                if (members.All(member => Met(member) || untaken.Contains(member.From!.Node))) moved = started.Add(node.Id) || moved;
                else if (members.Any(member => mayFail.Contains(member.From!.Node) && (started.Contains(member.From.Node) || failing.Contains(member.From.Node))))
                {
                    moved = failing.Add(node.Id) || moved;
                }
            }
        }

        return started;
    }

    /// <summary>
    /// The nodes something may fail (the second re-check, finding 3): each whose kind's authority names who fails it
    /// (<see cref="NodeAuthorities"/>: not git's proofs, nor the person's kinds, a look among them), and each <c>all</c> join one of whose
    /// members may fail, since <see cref="WireEvaluation"/> fails a join only on a member's failure.
    /// </summary>
    private static HashSet<string> MayFail(Dictionary<string, NodeDeclaration> byId, IReadOnlyList<Wire> local)
    {
        var may = byId.Values.Where(node => node.Kind != NodeKinds.All && NodeAuthorities.Of(node) is { FailureBy: not null })
            .Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        for (var moved = true; moved;)
        {
            moved = false;
            foreach (var join in byId.Values.Where(node => node.Kind == NodeKinds.All && !may.Contains(node.Id)))
            {
                if (local.Any(wire => wire.Kind == WireKinds.After && wire.To.Node == join.Id && may.Contains(wire.From!.Node))) moved = may.Add(join.Id) || moved;
            }
        }

        return may;
    }

    /// <summary>The nodes reached from <paramref name="from"/> along <paramref name="wires"/>, forward or back, the starts included.</summary>
    internal static HashSet<string> Reach(IEnumerable<string> from, IReadOnlyList<Wire> wires, bool forward)
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
}
