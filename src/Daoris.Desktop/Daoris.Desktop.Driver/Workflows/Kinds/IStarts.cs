namespace Daoris.Driver;

/// <summary>
/// A kind whose node is started on a door (D247 §3.5): the pass asks it to start a node whose wires fired, and after a run's rounds to keep an
/// active node going on its door, each once per node and binding. The runtime asks the kind, never names it.
/// </summary>
internal interface IStarts
{
    /// <summary>Its door, built from the loop's wiring; null where the wiring holds none, or where its door is the runtime's own.</summary>
    INodeDoor? Door(NodeDoorWiring wiring);

    /// <summary>The node whose wires fired, started in the pass: what was done, or null where it stays ready.</summary>
    Task<WorkPerformed?> StartAsync(NodeStarting starting, CancellationToken ct);

    /// <summary>A run's active node after the rounds, handed to its door off the pass; nothing where the pass's start was the whole of it.</summary>
    void Keep(NodeKept kept)
    {
    }
}
