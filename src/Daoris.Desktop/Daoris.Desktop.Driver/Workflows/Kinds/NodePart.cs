namespace Daoris.Driver;

/// <summary>A node's part on the run view (<see cref="WorkflowGraphRun.Cell"/>).</summary>
/// <param name="Step">The step kind its cell is drawn as: the work, the landing, or its own kind.</param>
/// <param name="Participation">Who takes part (<see cref="WorkflowParticipation"/>).</param>
/// <param name="Executor">Who carries it out (<see cref="WorkflowExecutor"/>).</param>
/// <param name="Press">The person's press on it, or null.</param>
/// <param name="Lowered">
/// The step a graph version's person part reads it as (<see cref="WorkflowGate.StepsOf"/>), or null where it is no part of that comparison.
/// </param>
public sealed record NodePart(string Step, string Participation, string Executor, string? Press = null, string? Lowered = null)
{
    /// <summary>A step Daoris carries out with no one's press, drawn as its own kind.</summary>
    public static NodePart Automatic(string kind) => new(kind, WorkflowParticipation.Automatic, WorkflowExecutor.Daoris);
}
