namespace Daoris.Driver;

/// <summary>
/// A <c>push</c> the runtime starts: the run's record and node, the landed branch it pushes at the commit the landing made,
/// and the checkout that holds it. Everything else the push needs is the push act's own to read.
/// </summary>
/// <param name="Unit">The run's own unit, whose record holds the node.</param>
/// <param name="Node">The <c>push</c> node's declaration.</param>
/// <param name="Repository">The run's repository.</param>
/// <param name="Workspace">The workspace the run is in.</param>
/// <param name="Root">The checkout the branch is in.</param>
/// <param name="Branch">The landed branch, as the landing record names it.</param>
/// <param name="Tip">The commit the landing made the branch at, which the push is pinned to.</param>
public sealed record PushStart(string Unit, NodeDeclaration Node, string Repository, string Workspace, string Root, string Branch, string Tip);

/// <summary>
/// The door a run's <c>push</c> is performed through: the person's press of the push act and a workflow's step are one act, so this
/// pushes through <see cref="GitPushActs"/> and nothing else.
/// </summary>
public interface IPushDoor : INodeDoor
{
    /// <summary>
    /// Starts the push of <paramref name="start"/> off the pass; returns at once. True where this call started one, false where one for the same
    /// node and commit is under way.
    /// </summary>
    bool Start(PushStart start);
}
