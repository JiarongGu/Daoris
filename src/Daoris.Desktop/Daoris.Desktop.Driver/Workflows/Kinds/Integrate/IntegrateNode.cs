namespace Daoris.Driver;

/// <summary>
/// The unit's step in the line's integration: its landed branch asked of the one request door and
/// waited on (<see cref="IntegrateStart"/>), reached and failed by the integration record. A <c>land</c> of the method <c>integrate</c> brings it.
/// </summary>
[NodeKind(NodeKinds.Integrate, NodeGroup.Landing, NodeKinds.Land)]
internal sealed class IntegrateNode : BuiltInNode, IStarts
{
    /// <summary>
    /// Never skipped, since a skip satisfies <c>after</c> and would read as integrated where nothing is; the person's door is the stop, which the
    /// record bounds.
    /// </summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.Integrate, NodeReach.Waited, NodeSource.Integration, NodeSource.Integration, false, PersonSkips: false, true, true);

    /// <summary>The landed branch at the tip <c>handed</c> recorded, wherever the graph wires it.</summary>
    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Landing;

    public override IReadOnlyList<NodeOptionField> Options { get; } =
        [NodeOptionFields.Choice("request", LandingIntegrate.Requests), NodeOptionFields.Choice("conflicts", LandingIntegrate.ConflictChoices)];

    /// <summary>
    /// The first of its options that holds no choice of the install's, by its name: <c>request</c> is one of <see cref="LandingIntegrate.Requests"/>
    /// and <c>conflicts</c> one of <see cref="LandingIntegrate.ConflictChoices"/>, each written as text (the unit's half of the install's policy), so a
    /// cycle, a version and a stored file are held to one check. Any other option is left as written, as a graph's options are opaque to a reader
    /// that does not know them.
    /// </summary>
    public override string? Problem(NodeDeclaration node)
    {
        foreach (var (name, choices) in new (string, IReadOnlyList<string>)[] { ("request", LandingIntegrate.Requests), ("conflicts", LandingIntegrate.ConflictChoices) })
        {
            if (node.Options.ContainsKey(name) && !(NodeOptions.Text(node, name) is { } text && choices.Contains(text, StringComparer.Ordinal))) return name;
        }

        return null;
    }

    /// <summary>
    /// At most one, after the landing, and a tail of a <c>land</c> whose method is <c>integrate</c>: said against the
    /// node it is about, once.
    /// </summary>
    public override IEnumerable<WireProblem> Problems(NodeGraph graph, NodeDeclaration node)
    {
        var standing = GraphStanding.Of(graph);
        var first = standing.ById.Values.First(each => each.Kind == NodeKinds.Integrate);
        if (!ReferenceEquals(first, node))
        {
            yield return new WireProblem(GraphRefusal.IntegrateTwice, null, node.Id,
                $"node `{node.Id}`: `{first.Id}` and `{node.Id}` are both an `{NodeKinds.Integrate}`, and the graph's landing is integrated once.");
        }
        else if (standing.Landings.Count == 0)
        {
            yield return new WireProblem(GraphRefusal.IntegrateUnlanded, null, node.Id,
                $"node `{node.Id}`: an `{NodeKinds.Integrate}` integrates the landing a `{NodeKinds.Land}` makes, and the graph holds no `{NodeKinds.Land}`.");
        }

        foreach (var land in standing.Landings)
        {
            var method = NodeOptions.Text(land, "method");
            if (method is null || !LandNodes.Methods.Contains(method, StringComparer.Ordinal)) continue;
            if (method != LandNodes.Integrate)
            {
                yield return new WireProblem(GraphRefusal.LandTail, null, node.Id, $"node `{node.Id}` is a `{NodeKinds.Integrate}`, which comes with a `land` whose method is `{LandNodes.Integrate}`: "
                    + "choose that method on the landing and it brings this step.");
            }
            else if (!NodeGraphStarts.Reach([land.Id], standing.Ordered, forward: true).Contains(node.Id))
            {
                yield return new WireProblem(GraphRefusal.LandTail, null, node.Id,
                    $"node `{node.Id}`: a `{NodeKinds.Integrate}` is the tail of `{land.Id}`, and no way from `{land.Id}` reaches it.");
            }
        }
    }

    /// <summary>A graph-only kind, kept by its node's id for the comparison's graph part (<see cref="Involvement.Lowers"/>).</summary>
    public override NodePart Part(NodeDeclaration node) => NodePart.Automatic(node.Kind) with { Lowered = NodeKinds.Integrate };

    public override string Offered => $"tail:{LandNodes.Integrate}";

    public override string? Title => "The integration";

    public override NodeLook Look => NodeLook.Other;

    public override bool ReadsTheLanding => true;

    public override bool EndsByTheLine => true;

    /// <summary>The one door a unit's <c>integrate</c> asks: the one handed, else the home's own.</summary>
    public INodeDoor? Door(NodeDoorWiring wiring) => wiring.Integration ?? NodeRuntime.HomeDoor(wiring.Home);

    /// <summary>A waited node, begun through its adapter: the unit's request asked of the one door, by its key.</summary>
    public async Task<WorkPerformed?> StartAsync(NodeStarting starting, CancellationToken ct) =>
        await starting.Integrating.IntegrateAsync(starting.Governed, starting.Node, starting.Start, starting.Statuses, starting.Said, ct).ConfigureAwait(false);
}
