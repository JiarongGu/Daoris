namespace Daoris.Driver;

/// <summary>A <c>return</c> the runtime starts: the run's unit, whose executors worked in the copy, the node, its attempt and the person's choices.</summary>
public sealed record ReturnStart(WorkUnit Unit, NodeDeclaration Node, int Attempt, IReadOnlyDictionary<string, string> Choices) : NodeDoorStart
{
    /// <summary>Once per node, attempt and choices made: each choice the person marks starts the return again.</summary>
    public override string Key => $"{Unit.Id}\n{Node.Id}\n{Attempt}\n{Choices.Count}";

    /// <summary>The start of <paramref name="node"/> standing at <paramref name="status"/>, with the choices <paramref name="record"/> marks.</summary>
    public static ReturnStart Of(WorkUnit unit, NodeDeclaration node, NodeStatus status, NodeRecord record, StepAuthority authority) =>
        new(unit, node, status.Attempt, ReturnNode.Choices(record, node.Id, status.Attempt)) { Authority = authority };
}

/// <summary>The door a run's <c>return</c> is performed through: the folder's merge, one return at a time.</summary>
public interface IReturnDoor : INodeDoor
{
    /// <summary>Starts the return of <paramref name="start"/> off the pass; true where this call started one.</summary>
    bool Start(ReturnStart start);
}
