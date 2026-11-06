namespace Daoris.Driver;

/// <summary>
/// The unit's step in the line's integration (INTEGRATE1n; the integration design §17.4): its landed branch asked of the one request door and
/// waited on (<see cref="IntegrateStart"/>), reached and failed by the integration record. A <c>land</c> of the method <c>integrate</c> brings it.
/// </summary>
[NodeKind(NodeKinds.Integrate, NodeGroup.Landing, NodeKinds.Land)]
internal sealed class IntegrateNode : BuiltInNode, IStarts
{
    /// <summary>
    /// Never skipped, since a skip satisfies <c>after</c> and would read as integrated where nothing was; the person's door is the stop, which the
    /// record bounds (F50, row n2).
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

    /// <summary>A graph-only kind, kept by its node's id for the comparison's graph part (<see cref="Involvement.Lowers"/>).</summary>
    public override NodePart Part(NodeDeclaration node) => NodePart.Automatic(node.Kind) with { Lowered = NodeKinds.Integrate };

    public override string Offered => $"tail:{LandNodes.Integrate}";

    public override string? Title => "The integration";

    public override NodeLook Look => NodeLook.Other;

    public override bool ReadsTheLanding => true;

    public override bool EndsByTheLine => true;

    /// <summary>The one door a unit's <c>integrate</c> asks (the integration design §17.4): the one handed, else the home's own.</summary>
    public INodeDoor? Door(NodeDoorWiring wiring) => wiring.Integration ?? NodeRuntime.HomeDoor(wiring.Home);

    /// <summary>A waited node, begun through its adapter: the unit's request asked of the one door, by its key.</summary>
    public async Task<WorkPerformed?> StartAsync(NodeStarting starting, CancellationToken ct) =>
        await starting.Integrating.IntegrateAsync(starting.Governed, starting.Node, starting.Start, starting.Statuses, starting.Said, ct).ConfigureAwait(false);
}
