namespace Daoris.Driver;

/// <summary>
/// The six wire kinds (FABRIC-WIRE1a; the fabric events design §E3). A wire is typed and carries no expression, script or command
/// (§E9): its kind is its condition.
/// </summary>
public static class WireKinds
{
    /// <summary>The target becomes eligible when the source has a current <c>reached</c> acceptance; a skipped source satisfies it too (§E2).</summary>
    public const string After = "after";

    /// <summary>
    /// Names a source output, or its evidence, and a target input: it needs a current <c>reached</c>, compatible schemas and an immutable
    /// output reference. A skipped source produces no outputs, so it never satisfies one (§E2).
    /// </summary>
    public const string Data = "data";

    /// <summary>Gives a <c>wait.unit</c> node what it waits for: another unit's named node or its end, at an agreed revision (§E6).</summary>
    public const string Wait = "wait";

    /// <summary>The source's accepted reach, a typed transition receipt, starts the target once for that receipt and binding.</summary>
    public const string Trigger = "trigger";

    /// <summary>The path taken once the source's declared retries are exhausted.</summary>
    public const string Failed = "failed";

    /// <summary>The path taken on the person's recorded rejection of the source.</summary>
    public const string Rejected = "rejected";

    public static readonly IReadOnlyList<string> All = [After, Data, Wait, Trigger, Failed, Rejected];

    /// <summary>
    /// The kinds that make a node eligible, which its <c>allOf</c> and <c>anyOf</c> groups gather. A <c>data</c> or <c>wait</c> wire is an
    /// input, mandatory whatever the groups say (§E3).
    /// </summary>
    public static readonly IReadOnlyList<string> Eligibility = [After, Trigger, Failed, Rejected];
}

/// <summary>How a group of a node's incoming eligibility wires is satisfied (§E3). Groups combine by all.</summary>
public static class WireGroupModes
{
    /// <summary>Every wire of the group.</summary>
    public const string AllOf = "allOf";

    /// <summary>One wire of the group, its witness: by wire-id order when several qualify at once (WIRE1b).</summary>
    public const string AnyOf = "anyOf";

    public static readonly IReadOnlyList<string> All = [AllOf, AnyOf];
}

/// <summary>
/// One end of a wire: a node of this graph, and on a <c>data</c> wire its port, an output (or <see cref="Evidence"/>) at the source and an
/// input at the target (<c>qa.evidence</c>, <c>deploy.approval</c>).
/// </summary>
/// <param name="Node">The node's id in this graph.</param>
/// <param name="Port">The port's name, on a <c>data</c> wire alone.</param>
public sealed record WireEnd(string Node, string? Port = null)
{
    /// <summary>The source port that is a reached node's evidence reference, beside its declared outputs (§E2, <i>ports are references</i>).</summary>
    public const string Evidence = "evidence";

    /// <summary>
    /// Another unit's node, never this graph's: refused on every wire, since a dependency on other work, another repository's above all,
    /// ends at a local <c>wait.unit</c> through a commitment (§E3, §E9).
    /// </summary>
    public string? Unit { get; init; }
}

/// <summary>
/// What a <c>wait</c> wire gives its <c>wait.unit</c> (§E6): another unit's named node or its end, at an agreed revision, never a newer
/// one silently. Another repository's work is waited on through an accepted commitment, which the adapter checks (WAITUNIT1).
/// </summary>
/// <param name="Unit">The unit waited on.</param>
/// <param name="Revision">The revision agreed.</param>
public sealed record WaitTarget(string Unit, string Revision)
{
    /// <summary>The unit's node waited on, or null with <see cref="End"/>.</summary>
    public string? Node { get; init; }

    /// <summary>Whether the unit's end is waited on, in place of a node.</summary>
    public bool End { get; init; }

    /// <summary>The quest commitment it is waited through, where the unit is another repository's.</summary>
    public string? Commitment { get; init; }
}

/// <summary>The group an eligibility wire joins at its target: an id, one node's, and its mode (<see cref="WireGroupModes"/>).</summary>
public sealed record WireGroup(string Id, string Mode);

/// <summary>
/// One wire (FABRIC-WIRE1a; §E3): <c>{ id, kind, from, to, group? }</c>. A <c>wait</c> wire's source is a <see cref="WaitTarget"/> in place
/// of a node. The id is the wire's identity: an <c>anyOf</c>'s witness is chosen by it, and a trigger's de-duplication keys on it (WIRE1b).
/// </summary>
/// <param name="Id">Its id in its graph.</param>
/// <param name="Kind">One of <see cref="WireKinds"/>.</param>
/// <param name="From">The source; null on a <c>wait</c> wire.</param>
/// <param name="To">The target, a node of this graph.</param>
public sealed record Wire(string Id, string Kind, WireEnd? From, WireEnd To)
{
    /// <summary>What a <c>wait</c> wire waits on; null on every other kind.</summary>
    public WaitTarget? Waits { get; init; }

    /// <summary>The group it joins at its target, on an eligibility wire; none is a group of its own, satisfied by it alone.</summary>
    public WireGroup? Group { get; init; }
}

/// <summary>One thing a save refuses (§E3, <i>refused when saved</i>): a stable code, the wire or node it is about, and its words.</summary>
/// <param name="Code">One of <see cref="WireRefusal"/>.</param>
/// <param name="Wire">The wire's id, where it is about one.</param>
/// <param name="Node">The node's id, where it is about one.</param>
/// <param name="Words">What is wrong, in a sentence, names in backticks.</param>
public sealed record WireProblem(string Code, string? Wire, string? Node, string Words);

/// <summary>
/// One of a node's incoming eligibility groups: a declared group, or a wire with none, a group of its own (<see cref="Id"/> null). Its
/// wires are in wire-id order, the order an <c>anyOf</c>'s witness is chosen in (WIRE1b).
/// </summary>
public sealed record WireGroupOf(string Node, string? Id, string Mode, IReadOnlyList<string> Wires);

/// <summary>Why a save refuses a wire set (§E3), as a code a test holds and a page words.</summary>
public static class WireRefusal
{
    /// <summary>A wire without an id.</summary>
    public const string NoId = "no-id";

    /// <summary>Two wires with one id: the id is what a witness and a trigger's receipt are keyed on.</summary>
    public const string WireTwice = "wire-twice";

    /// <summary>Two nodes with one id: a wire's reference to it would name either.</summary>
    public const string NodeTwice = "node-twice";

    /// <summary>A kind that is none of the six.</summary>
    public const string UnknownKind = "unknown-kind";

    /// <summary>The ends do not fit the kind: a source, a wait target or a port where the kind has none, or none where it needs one.</summary>
    public const string Shape = "shape";

    /// <summary>An end on another unit's node: a dependency on other work ends at a local <c>wait.unit</c>, through a commitment.</summary>
    public const string ForeignNode = "foreign-node";

    /// <summary>An end naming no node of this graph.</summary>
    public const string MissingNode = "missing-node";

    /// <summary>A <c>data</c> wire naming an output, evidence or input its node does not declare.</summary>
    public const string MissingPort = "missing-port";

    /// <summary>A <c>data</c> wire whose output's schema is not the input's.</summary>
    public const string IncompatibleSchema = "incompatible-schema";

    /// <summary>A <c>wait</c> wire into a node that is not a <c>wait.unit</c>.</summary>
    public const string NotWaitUnit = "not-wait-unit";

    /// <summary>A wait target without its unit or revision, or naming both or neither of a node and the end.</summary>
    public const string WaitTarget = "wait-target";

    /// <summary>A <c>wait.unit</c> with no <c>wait</c> wire: it would wait on nothing.</summary>
    public const string NoWait = "no-wait";

    /// <summary>A <c>wait.unit</c> with a second <c>wait</c> wire: each waits on one thing, and several are joined by groups.</summary>
    public const string WaitTwice = "wait-twice";

    /// <summary>A declared input with no <c>data</c> wire: every input is mandatory.</summary>
    public const string InputUnfed = "input-unfed";

    /// <summary>A second <c>data</c> wire into one input: alternative producers need an explicitly typed choice input.</summary>
    public const string InputTwice = "input-twice";

    /// <summary>A group on a <c>data</c> or <c>wait</c> wire, which is an input, mandatory whatever the groups say.</summary>
    public const string GroupedInput = "grouped-input";

    /// <summary>A group without an id, of a mode that is neither <c>allOf</c> nor <c>anyOf</c>, or with both modes among its wires.</summary>
    public const string GroupMode = "group-mode";

    /// <summary>A group gathering wires into more than one node: a group is one node's.</summary>
    public const string GroupSplit = "group-split";

    /// <summary>A <c>failed</c> wire from a node nothing can fail: not yet is pending, so it would never be taken.</summary>
    public const string NeverFails = "never-fails";

    /// <summary>A <c>rejected</c> wire from a node no person may reject: it would never be taken.</summary>
    public const string NeverRejected = "never-rejected";

    /// <summary>A local dependency cycle, a node's own <c>trigger</c> loop included.</summary>
    public const string Cycle = "cycle";
}

/// <summary>
/// The wires' validation when a graph is saved (FABRIC-WIRE1a; the fabric events design §E3): each wire's shape and kind, the groups,
/// the mandatory data inputs, and every refusal §E3 names. Pure: it judges declarations and wires, evaluates nothing (WIRE1b) and
/// stores nothing (GRAPH1). A declaration's own checks are <see cref="NodeDeclarations.Problem"/>.
/// </summary>
public static class NodeWires
{
    /// <summary>
    /// Everything a save refuses in <paramref name="wires"/> over <paramref name="nodes"/>, in a stable order; empty when it holds. The
    /// nodes' ids first; then each wire in the order given, by its first problem (its id, kind, shape, ends, ports, source and group);
    /// then each node's inputs and wait, in the order given; then each local cycle, by its first node.
    /// </summary>
    public static IReadOnlyList<WireProblem> Problems(IReadOnlyList<NodeDeclaration> nodes, IReadOnlyList<Wire> wires)
    {
        var problems = new List<WireProblem>();
        var byId = new Dictionary<string, NodeDeclaration>(StringComparer.Ordinal);
        var distinct = new List<NodeDeclaration>();
        foreach (var node in nodes)
        {
            if (byId.TryAdd(node.Id, node)) distinct.Add(node);
            else problems.Add(new(WireRefusal.NodeTwice, null, node.Id, $"node `{node.Id}`: another node has this id, so a wire naming it would name either."));
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var groups = new Dictionary<string, Wire>(StringComparer.Ordinal);
        foreach (var wire in wires)
        {
            if (Problem(wire, byId, ids, groups) is { } problem) problems.Add(problem);
        }

        foreach (var node in distinct)
        {
            foreach (var input in node.Inputs.Keys)
            {
                var feeding = wires.Where(wire => wire.Kind == WireKinds.Data && Into(wire, node.Id) && wire.To.Port == input).ToList();
                if (feeding.Count == 0)
                {
                    problems.Add(new(WireRefusal.InputUnfed, null, node.Id, $"node `{node.Id}`: its input `{input}` has no `data` wire; every input is mandatory."));
                }

                problems.AddRange(feeding.Skip(1).Select(wire => new WireProblem(WireRefusal.InputTwice, wire.Id, node.Id,
                    $"wire `{wire.Id}`: `{node.Id}`'s input `{input}` is fed by `{feeding[0].Id}` already; alternative producers need an explicitly typed choice input.")));
            }

            if (node.Kind != NodeKinds.WaitUnit) continue;
            var waits = wires.Where(wire => wire.Kind == WireKinds.Wait && Into(wire, node.Id)).ToList();
            if (waits.Count == 0)
            {
                problems.Add(new(WireRefusal.NoWait, null, node.Id, $"node `{node.Id}`: a `wait.unit` waits on what a `wait` wire names, and none does."));
            }

            problems.AddRange(waits.Skip(1).Select(wire => new WireProblem(WireRefusal.WaitTwice, wire.Id, node.Id,
                $"wire `{wire.Id}`: `{node.Id}` waits on what `{waits[0].Id}` names already; a `wait.unit` waits on one thing, and several are joined by groups.")));
        }

        problems.AddRange(Cycles(byId, wires));
        return problems;
    }

    /// <summary>
    /// <paramref name="node"/>'s incoming eligibility groups (§E3: groups combine by all), each declared group once and each wire with none
    /// a group of its own, an <c>allOf</c> of one. Each group's wires are in wire-id order, an <c>anyOf</c>'s witness order (WIRE1b), and the
    /// groups in the order of their first wire. A <c>data</c> or <c>wait</c> wire is no member: an input is mandatory whatever the groups say.
    /// </summary>
    public static IReadOnlyList<WireGroupOf> Groups(string node, IReadOnlyList<Wire> wires) =>
        [.. wires
            .Where(wire => WireKinds.Eligibility.Contains(wire.Kind) && Into(wire, node))
            .GroupBy(wire => wire.Group is { Id: { } id } && !string.IsNullOrWhiteSpace(id) ? "group\n" + id : "wire\n" + wire.Id, StringComparer.Ordinal)
            .Select(group =>
            {
                var members = group.Select(wire => wire.Id).Order(StringComparer.Ordinal).ToList();
                var declared = group.First().Group;
                return declared is not null && !string.IsNullOrWhiteSpace(declared.Id)
                    ? new WireGroupOf(node, declared.Id, declared.Mode, members)
                    : new WireGroupOf(node, null, WireGroupModes.AllOf, members);
            })
            .OrderBy(group => group.Wires[0], StringComparer.Ordinal)];

    /// <summary>
    /// The schema a <c>data</c> wire takes from <paramref name="port"/> on <paramref name="node"/>: a declared output's, or its evidence's
    /// (its declared <see cref="NodeDeclaration.Evidence"/>, else an observed built-in's record); null where it has no such port.
    /// </summary>
    public static string? SourceSchema(NodeDeclaration node, string port) =>
        port == WireEnd.Evidence
            ? node.Evidence ?? (BuiltInNodes.Observed.Contains(node.Kind) ? BuiltInNodes.EvidenceSchema(node.Kind) : null)
            : node.Outputs.TryGetValue(port, out var schema) ? schema : null;

    /// <summary>Whether <paramref name="wire"/> ends at this graph's <paramref name="node"/>.</summary>
    private static bool Into(Wire wire, string node) => wire.To is { Unit: null } to && to.Node == node;

    /// <summary>One wire's first problem, or null; a sound grouped wire's group is noted at its first wire, for the next to agree with.</summary>
    private static WireProblem? Problem(Wire wire, Dictionary<string, NodeDeclaration> nodes, HashSet<string> ids, Dictionary<string, Wire> groups)
    {
        var id = wire.Id;
        WireProblem Refuse(string code, string? node, string words) => new(code, id, node, $"wire `{id}`: {words}");

        if (string.IsNullOrWhiteSpace(id)) return new(WireRefusal.NoId, id, null, "a wire has no id: its id is what a witness and a trigger's receipt are keyed on.");
        if (!ids.Add(id)) return Refuse(WireRefusal.WireTwice, null, "another wire has this id; each wire's id is its own.");
        if (!WireKinds.All.Contains(wire.Kind))
        {
            return Refuse(WireRefusal.UnknownKind, null, $"`{wire.Kind}` is no wire kind; a wire is `after`, `data`, `wait`, `trigger`, `failed` or `rejected`.");
        }

        if (Shape(wire) is { } shape) return Refuse(WireRefusal.Shape, null, shape);
        foreach (var end in new[] { wire.From, wire.To })
        {
            if (end is { Unit: not null }) return Refuse(WireRefusal.ForeignNode, end.Node, $"`{end.Node}` is another unit's node; a dependency on other work ends at a local `wait.unit`, through a commitment.");
        }

        foreach (var end in new[] { wire.From, wire.To })
        {
            if (end is not null && !nodes.ContainsKey(end.Node)) return Refuse(WireRefusal.MissingNode, end.Node, $"there is no node `{end.Node}` in this graph.");
        }

        var to = nodes[wire.To.Node];
        var from = wire.From is { } source ? nodes[source.Node] : null;
        switch (wire.Kind)
        {
            case WireKinds.Wait:
                if (to.Kind != NodeKinds.WaitUnit)
                {
                    return Refuse(WireRefusal.NotWaitUnit, to.Id, $"a `wait` wire gives a `wait.unit` what it waits on, and `{to.Id}` is a `{to.Kind}`.");
                }

                if (wire.Waits is not { } waits || string.IsNullOrWhiteSpace(waits.Unit) || string.IsNullOrWhiteSpace(waits.Revision)
                    || string.IsNullOrWhiteSpace(waits.Node) != waits.End)
                {
                    return Refuse(WireRefusal.WaitTarget, to.Id, $"`{to.Id}` waits on a unit at its agreed revision, and on one of its nodes or its end.");
                }

                break;

            case WireKinds.Data:
                var output = wire.From!.Port!;
                var input = wire.To.Port!;
                if (SourceSchema(from!, output) is not { } produced)
                {
                    return Refuse(WireRefusal.MissingPort, from!.Id, output == WireEnd.Evidence
                        ? $"`{from.Id}` declares no evidence to pass on."
                        : $"`{from.Id}` has no output `{output}`.");
                }

                if (!to.Inputs.TryGetValue(input, out var taken)) return Refuse(WireRefusal.MissingPort, to.Id, $"`{to.Id}` has no input `{input}`.");
                if (!string.Equals(produced, taken, StringComparison.Ordinal))
                {
                    return Refuse(WireRefusal.IncompatibleSchema, to.Id, $"`{from.Id}`'s `{output}` is `{produced}`, and `{to.Id}`'s input `{input}` takes `{taken}`.");
                }

                break;

            case WireKinds.Failed when NodeAuthorities.Of(from!) is { FailureBy: null }:
                return Refuse(WireRefusal.NeverFails, from!.Id, $"nothing can fail `{from.Id}`, a `{from.Kind}`: not yet is pending, so this path is never taken.");

            case WireKinds.Rejected when NodeAuthorities.Of(from!) is { PersonJudges: false }:
                return Refuse(WireRefusal.NeverRejected, from!.Id, $"no person may reject `{from.Id}`, a `{from.Kind}`, so this path is never taken.");
        }

        if (wire.Group is not { } group) return null;
        if (!WireKinds.Eligibility.Contains(wire.Kind))
        {
            return Refuse(WireRefusal.GroupedInput, to.Id,
                $"a `{wire.Kind}` wire is `{to.Id}`'s input, mandatory whatever the groups say; only `after`, `trigger`, `failed` and `rejected` wires are grouped.");
        }

        if (string.IsNullOrWhiteSpace(group.Id) || !WireGroupModes.All.Contains(group.Mode))
        {
            return Refuse(WireRefusal.GroupMode, to.Id, $"a group into `{to.Id}` has an id, and is `allOf` or `anyOf`.");
        }

        if (!groups.TryGetValue(group.Id, out var first))
        {
            groups[group.Id] = wire;
            return null;
        }

        if (first.To.Node != to.Id)
        {
            return Refuse(WireRefusal.GroupSplit, to.Id, $"group `{group.Id}` gathers wires into `{first.To.Node}`, and a group is one node's, never `{to.Id}`'s as well.");
        }

        return first.Group!.Mode != group.Mode
            ? Refuse(WireRefusal.GroupMode, to.Id, $"group `{group.Id}` into `{to.Id}` is `{first.Group.Mode}` already; one group has one mode.")
            : null;
    }

    /// <summary>What does not fit the kind in a wire's ends, or null.</summary>
    private static string? Shape(Wire wire)
    {
        if (wire.Kind == WireKinds.Wait)
        {
            return wire.From is not null ? "a `wait` wire waits on another unit's work, never on a node of this graph."
                : wire.Waits is null ? "a `wait` wire names what it waits on."
                : wire.To.Port is not null ? "only a `data` wire names ports."
                : null;
        }

        if (wire.Waits is not null) return "only a `wait` wire names what it waits on.";
        if (wire.From is null) return $"a `{wire.Kind}` wire needs its source node.";
        if (wire.Kind == WireKinds.Data)
        {
            return string.IsNullOrWhiteSpace(wire.From.Port) || string.IsNullOrWhiteSpace(wire.To.Port)
                ? "a `data` wire names its source's output and its target's input."
                : null;
        }

        return wire.From.Port is not null || wire.To.Port is not null ? $"only a `data` wire names ports; a `{wire.Kind}` wire joins nodes." : null;
    }

    /// <summary>
    /// The local dependency cycles (§E3), a node's own loop included: over every wire of a kind with a source, both of whose ends are this
    /// graph's nodes. A <c>wait</c> wire's source is other work, so it closes none here. Each cycle once, named by its nodes in id order.
    /// </summary>
    private static IEnumerable<WireProblem> Cycles(Dictionary<string, NodeDeclaration> nodes, IReadOnlyList<Wire> wires)
    {
        var edges = wires
            .Where(wire => WireKinds.All.Contains(wire.Kind) && wire.Kind != WireKinds.Wait && wire.From is { Unit: null } from && nodes.ContainsKey(from.Node)
                && wire.To.Unit is null && nodes.ContainsKey(wire.To.Node))
            .ToList();
        var next = edges.ToLookup(wire => wire.From!.Node, wire => wire.To.Node, StringComparer.Ordinal);

        // Tarjan's strongly connected components: each one of more than a node, or a node wired to itself, is a cycle.
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        var low = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        var onStack = new HashSet<string>(StringComparer.Ordinal);
        var components = new List<List<string>>();

        void Visit(string node)
        {
            index[node] = low[node] = index.Count;
            stack.Push(node);
            onStack.Add(node);
            foreach (var to in next[node])
            {
                if (!index.ContainsKey(to))
                {
                    Visit(to);
                    low[node] = Math.Min(low[node], low[to]);
                }
                else if (onStack.Contains(to))
                {
                    low[node] = Math.Min(low[node], index[to]);
                }
            }

            if (low[node] != index[node]) return;
            var component = new List<string>();
            string member;
            do
            {
                member = stack.Pop();
                onStack.Remove(member);
                component.Add(member);
            }
            while (member != node);
            components.Add(component);
        }

        foreach (var node in nodes.Keys.Order(StringComparer.Ordinal))
        {
            if (!index.ContainsKey(node)) Visit(node);
        }

        foreach (var members in components.Select(component => component.Order(StringComparer.Ordinal).ToList()).OrderBy(members => members[0], StringComparer.Ordinal))
        {
            var inside = edges.Where(wire => members.Contains(wire.From!.Node) && members.Contains(wire.To.Node)).Select(wire => wire.Id).Order(StringComparer.Ordinal).ToList();
            if (members.Count == 1 && inside.Count == 0) continue;
            yield return new(WireRefusal.Cycle, null, members[0],
                $"{(members.Count == 1 ? "node" : "nodes")} {string.Join(", ", members.Select(member => $"`{member}`"))}: "
                + $"{(members.Count == 1 ? "its own wire comes back to it" : "they wait on each other")} through {string.Join(", ", inside.Select(wire => $"`{wire}`"))}; "
                + "a graph's wires never come back, and a return is its adapter's, bounded.");
        }
    }
}
