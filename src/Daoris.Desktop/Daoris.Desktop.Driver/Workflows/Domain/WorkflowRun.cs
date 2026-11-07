namespace Daoris.Driver;

/// <summary>A chain's work in one repository, read as steps.</summary>
/// <param name="Quests">The run's quests, in the chain's order.</param>
public sealed record WorkflowRun(string Repository, string Workspace, CurrentWorkflow Current, IReadOnlyList<string> Quests, IReadOnlyList<WorkflowRunStep> Steps)
{
    public WorkflowProcess? Process { get; init; }
    public WorkflowGateState? WorkflowGate { get; init; }
    public string? Problem { get; init; }

    /// <summary>The <see cref="Problem"/> as a code and its values for the page to word; null where it is the driver's sentence only.</summary>
    public BindingProblem? Coded { get; init; }

    /// <summary>The ask that asked the chain, or null.</summary>
    public string? Ask { get; init; }

    /// <summary>The run's newest session: the one its doors attend.</summary>
    public string? Session { get; init; }

    /// <summary>
    /// A chat run's key, <c>chat-&lt;first&gt;</c>; null for a quest's run. Its checks are not yet enforced for a conversation,
    /// which the view says.
    /// </summary>
    public string? ChatRun { get; init; }

    /// <summary>
    /// The checks a chat run's conversation is held to at an execution, read as the execution reads them (<see cref="SessionChecks.Of"/>): its
    /// graph's <c>claimed</c> where a workflow governs it, else its repository's rule; null for a quest's run and where none is set.
    /// </summary>
    public string? ChatChecks { get; init; }

    /// <summary>
    /// The run bound to a graph version, drawn from its record: its wires, its subjects and what its record says of it;
    /// null for a run that steps govern.
    /// </summary>
    public WorkflowGraphRun? Graph { get; init; }

    /// <summary>A verification run's commit, branch, verdict and gates; null for a chain's or a chat's run.</summary>
    public WorkflowRunLine? Line { get; init; }

    /// <summary>
    /// The first unsettled step; null where all settled or no graph could be read. Problem distinguishes the latter. A graph run stands
    /// where its drawing says (<see cref="WorkflowGraphRun.At"/>), since a node only a failure leads to is never settled.
    /// </summary>
    public string? At => Graph is { } graph ? graph.At : Steps.FirstOrDefault(step => !WorkflowRunStates.Settled(step.State))?.Step.Id;
}
