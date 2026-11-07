namespace Daoris.Driver;

/// <summary>
/// A node whose wires fired this round, as the pass hands it to its kind to start (<see cref="IStarts.StartAsync"/>): the unit, the node, its
/// start and the round's statuses, with the pass's count of sends, its lines, and the loop's pieces a start is made through.
/// </summary>
internal sealed record NodeStarting
{
    public required GovernedUnit Governed { get; init; }

    public required NodeDeclaration Node { get; init; }

    public required WireStart Start { get; init; }

    /// <summary>The round's statuses, the node's own among them.</summary>
    public required IReadOnlyDictionary<string, NodeStatus> Statuses { get; init; }

    public NodeStatus Status => Statuses[Start.Node];

    /// <summary>Whether the unit's plugin nodes bind to its branch's tip read this pass; <see cref="Tip"/> is that tip, null where unread.</summary>
    public bool Tipped { get; init; }

    public string? Tip { get; init; }

    /// <summary>Whether the units were read afresh this pass, so a bound quest's revision is current.</summary>
    public bool ReadThisPass { get; init; }

    public required WorkSends Sends { get; init; }

    public required List<string> Said { get; init; }

    /// <summary>The units read again, for a start that must judge on what is there now.</summary>
    public required Func<CancellationToken, Task<NodeUnitsRead>> Units { get; init; }

    public required WorkPerformer Performer { get; init; }

    public required IWorkRuntimePort Port { get; init; }

    public required NodeClosing Closing { get; init; }

    public required NodeIntegrating Integrating { get; init; }

    public required NodeRequesting Requesting { get; init; }

    /// <summary>Drops what the pass governs, so the unit is judged again on what is there now; its evaluation ends with this start.</summary>
    public required Action Drop { get; init; }

    /// <summary>The node begun: ready to active, its firings written, for its reading or its door to take.</summary>
    public WorkPerformed Begin() => Performer.Begin(Node, Start, Status);

    /// <summary>
    /// The node started as its kind says: on its door where it starts on one; begun where it observes, or where the evaluator reaches it on
    /// its begun binding (an observed row of no reading: the join, the sink); else left ready for its press or its wait. Null where nothing is done.
    /// </summary>
    public async Task<WorkPerformed?> StartAsync(CancellationToken ct) => NodeKit.Of(Node.Kind) switch
    {
        IStarts kind => await kind.StartAsync(this, ct).ConfigureAwait(false),
        IObserves observes => observes.Begins(Governed) ? Begin() : null,
        BuiltInNode { Row.Reach: NodeReach.Observed } => Begin(),
        _ => null,
    };
}
