namespace Daoris.Driver;

/// <summary>An <c>apply</c> the runtime starts: the run's unit, the node, its attempt and the review record whose plan it applies.</summary>
public sealed record ApplyStart(WorkUnit Unit, NodeDeclaration Node, int Attempt) : NodeDoorStart
{
    /// <summary>The record its review accepted, or the run's work read at its start where no review feeds it (<see cref="EffectRecord"/>).</summary>
    public ReviewRecord? Record { get; init; }

    /// <summary>Once per node and attempt: a stale plan's way back starts the next attempt.</summary>
    public override string Key => $"{Unit.Id}\n{Node.Id}\n{Attempt}";
}

/// <summary>The door a run's <c>apply</c> is performed through: the system's adapter, one applier at a time.</summary>
public interface IApplyDoor : INodeDoor
{
    /// <summary>Starts the apply of <paramref name="start"/> off the pass; true where this call started one.</summary>
    bool Start(ApplyStart start);
}
