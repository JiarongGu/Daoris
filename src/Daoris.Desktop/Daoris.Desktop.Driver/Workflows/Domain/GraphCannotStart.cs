namespace Daoris.Driver;

/// <summary>
/// Why a graph run's gate node cannot start, drawn on the node: an <c>opinion</c> its place declares too little for (no reviewers, or reviewers it
/// does not declare), read from the run's process, while the runtime has not begun it. Null for every other node, and once it has begun.
/// </summary>
public static class GraphCannotStart
{
    /// <summary>The words the node is drawn <c>cannot-start</c> with, or null.</summary>
    public static string? Of(NodeDeclaration node, string state, WorkflowProcess? process) =>
        node.Kind == NodeKinds.Opinion && state is NodeStates.Blocked or NodeStates.Ready ? process?.OpinionCannot : null;
}
