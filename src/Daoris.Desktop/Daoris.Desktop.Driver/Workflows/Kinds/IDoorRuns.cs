namespace Daoris.Driver;

/// <summary>A door built on the kit's runner (<see cref="NodeDoorRuns"/>), whose every start's work runs under that start's authority.</summary>
internal interface IDoorRuns : INodeDoor
{
    NodeDoorRuns Runs { get; }
}
