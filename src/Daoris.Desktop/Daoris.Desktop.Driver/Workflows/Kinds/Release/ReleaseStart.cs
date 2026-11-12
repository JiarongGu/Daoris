namespace Daoris.Driver;

/// <summary>A <c>release</c> the runtime starts: the run's unit, the node and its attempt, the checkout the run's repository is worked in, and the run's branches.</summary>
/// <param name="Branches">The session branches the run owns whose trees the release ends.</param>
public sealed record ReleaseStart(WorkUnit Unit, NodeDeclaration Node, int Attempt, string Repository, string Root, IReadOnlyList<string> Branches) : NodeDoorStart
{
    /// <summary>Once per node and attempt.</summary>
    public override string Key => $"{Unit.Id}\n{Node.Id}\n{Attempt}";
}

/// <summary>The door a run's <c>release</c> is performed through: the run's trees ended behind the clean-up's proof.</summary>
public interface IReleaseDoor : INodeDoor
{
    /// <summary>Starts the release of <paramref name="start"/> off the pass; true where this call started one.</summary>
    bool Start(ReleaseStart start);
}
