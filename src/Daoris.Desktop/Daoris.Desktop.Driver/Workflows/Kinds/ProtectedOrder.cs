using static Daoris.Driver.NodeGraphStarts;

namespace Daoris.Driver;

/// <summary>
/// D157 §2.2's protected order before the landing (holds, opinion, look, landing), the one rule the gate kinds share. A kind takes its part by
/// its <see cref="INodeKind.Guard"/>, and the kit runs the rule once per save beside each kind's own (<see cref="NodeKit.Problems"/>): it reads
/// the gates together, which no one node's rules can.
/// </summary>
internal static class ProtectedOrder
{
    private const string Order = "D157 §2.2's order: holds, opinion, look, landing";

    /// <summary>What the order refuses in <paramref name="standing"/>'s graph, each said against the gate it is about.</summary>
    public static IReadOnlyList<WireProblem> Problems(GraphStanding standing)
    {
        var problems = new List<WireProblem>();
        void Say(string code, string node, string words) => problems.Add(new(code, null, node, words));
        var (byId, local, starts, ordered, landings) = (standing.ById, standing.Local, standing.Starts, standing.Ordered, standing.Landings);
        var gates = byId.Values.Where(node => Guard(node) != LandingGuard.None).ToList();
        var passed = gates.Where(node => Guard(node) == LandingGuard.Passed).ToList();

        // Each gate is reached before every landing starts, through wires a reach satisfies, never a failure's or a rejection's path. The holds
        // stay the rules' gate at the landing. FABRIC-CUTOVER1b (the cutover design §5.1): the opinion is not asked to reach a look. DECIDE1b: a
        // decision's route is an order too, so a landing on one outcome's route is judged by the gates on that route.
        var unreached = new HashSet<string>(StringComparer.Ordinal);
        foreach (var gate in gates)
        {
            var after = Reach([gate.Id], ordered, forward: true);
            if (Guard(gate) == LandingGuard.Passed) unreached.UnionWith(landings.Where(landing => !after.Contains(landing.Id)).Select(landing => landing.Id));
            if (landings.FirstOrDefault(landing => !after.Contains(landing.Id)) is { } early)
            {
                Say(GraphRefusal.ProtectedOrder, gate.Id, $"the {gate.Kind} `{gate.Id}` comes before the landing `{early.Id}` ({Order}).");
            }
        }

        // FABRIC-GRAPH1's review, finding 4: a gate that must be passed reaching a landing is not enough; no landing may start before one has
        // passed, whichever member of an `anyOf` a run takes. One problem a landing they all reach, naming the first.
        if (passed.Count > 0)
        {
            var unpassed = StartsWithout(byId, starts, local, node => Guard(node) == LandingGuard.Passed);
            foreach (var landing in landings.Where(landing => !unreached.Contains(landing.Id) && unpassed.Contains(landing.Id)))
            {
                Say(GraphRefusal.ProtectedOrder, passed[0].Id, $"the landing `{landing.Id}` can start with no {passed[0].Kind} passed, though `{passed[0].Id}` reaches it: "
                    + $"every way to it passes a {passed[0].Kind} ({Order}).");
            }
        }

        // FABRIC-GRAPH1i: a skip satisfies `after` (§E2), so a person's skip of the look lets a wire from it pass while the opinion before it is
        // unsettled, and reachability holds the order no further. Where the order holds by reach, each gate is wired straight into each landing,
        // as the decoder wires a version's (FABRIC-GRAPH1h): a wire of the order's kinds, in no `anyOf` group, whose other members would let a
        // way past it. Said only where the order is not already refused, so one fault is not two.
        if (problems.Count > 0) return problems;
        bool Wired(string from, string to) => ordered.Any(wire => wire.From!.Node == from && wire.To.Node == to && wire.Group?.Mode != WireGroupModes.AnyOf);
        foreach (var gate in gates)
        {
            if (landings.FirstOrDefault(landing => !Wired(gate.Id, landing.Id)) is { } next)
            {
                Say(GraphRefusal.ProtectedWire, gate.Id, $"the {gate.Kind} `{gate.Id}` is not wired into the landing `{next.Id}`: a skip of a gate between them would let an "
                    + $"`after` wire pass while `{gate.Id}` is unsettled, so wire `{gate.Id}` into `{next.Id}` ({Order}).");
            }
        }

        return problems;
    }

    private static LandingGuard Guard(NodeDeclaration node) => NodeKit.Of(node.Kind)?.Guard ?? LandingGuard.None;
}
