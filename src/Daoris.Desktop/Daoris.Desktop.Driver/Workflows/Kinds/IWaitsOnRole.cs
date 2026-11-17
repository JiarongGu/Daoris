namespace Daoris.Driver;

/// <summary>
/// A kind whose active step can wait on a role: the wait it stands at (one of <see cref="PersonWaits"/>' codes) read from the step and its
/// status, or null where it waits on none now. <see cref="StepWaiting.Read"/> asks it and names no kind; whoever holds the role, a person or an
/// agent alike, is read from the role (D304 point 2, D312), never from the kind.
/// </summary>
public interface IWaitsOnRole
{
    /// <summary>The wait <paramref name="node"/> stands at while active at <paramref name="status"/>, or null.</summary>
    string? Waits(NodeDeclaration node, NodeStatus status);
}
