namespace Daoris.Driver;

/// <summary>A <c>return</c> the runtime starts: the run's unit, the node, its attempt, the person's choices and the versions they name.</summary>
public sealed record ReturnStart(WorkUnit Unit, NodeDeclaration Node, int Attempt, IReadOnlyDictionary<string, string> Choices) : NodeDoorStart
{
    public IReadOnlyDictionary<string, ReturnShown> Bound { get; init; } = new Dictionary<string, ReturnShown>();

    /// <summary>The record naming the copy it brings back (<see cref="EffectRecord"/>).</summary>
    public ReviewRecord? Reviewed { get; init; }

    /// <summary>Once per node, attempt and choices made: a path chosen again on new versions starts it again.</summary>
    public override string Key =>
        $"{Unit.Id}\n{Node.Id}\n{Attempt}\n{Choices.Count}\n{string.Join("\n", Bound.OrderBy(each => each.Key, StringComparer.Ordinal).Select(each => $"{each.Key}@{each.Value}"))}";

    /// <summary>The start of <paramref name="node"/> standing at <paramref name="status"/>, with the choices <paramref name="record"/> marks.</summary>
    public static ReturnStart Of(WorkUnit unit, NodeDeclaration node, NodeStatus status, NodeRecord record, StepAuthority authority) =>
        new(unit, node, status.Attempt, ReturnNode.Choices(record, node.Id, status.Attempt))
        {
            Authority = authority, Bound = ReturnNode.Bound(record, node.Id, status.Attempt),
        };
}

/// <summary>The door a run's <c>return</c> is performed through: the folder's merge, one return at a time.</summary>
public interface IReturnDoor : INodeDoor
{
    /// <summary>Starts the return of <paramref name="start"/> off the pass; true where this call started one.</summary>
    bool Start(ReturnStart start);
}
