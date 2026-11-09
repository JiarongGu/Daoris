namespace Daoris.Driver;

/// <summary>
/// A door kind's start: the run's own step its work runs under, made from the run, the node and its active status (<c>NodeKept.Authority</c>),
/// and the key its door takes it once by. A start without its authority does not compile.
/// </summary>
public abstract record NodeDoorStart
{
    public required StepAuthority Authority { get; init; }

    /// <summary>What the door takes the start once by while its work runs.</summary>
    public abstract string Key { get; }
}
