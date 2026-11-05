namespace Daoris.Driver;

/// <summary>A chain's work in one repository, read as steps (WORKFLOW1c; D157 point 11, design §5.1).</summary>
/// <param name="Quests">The run's quests, in the chain's order.</param>
public sealed record WorkflowRun(string Repository, string Workspace, CurrentWorkflow Current, IReadOnlyList<string> Quests, IReadOnlyList<WorkflowRunStep> Steps)
{
    public WorkflowProcess? Process { get; init; }
    public WorkflowGateState? WorkflowGate { get; init; }
    public string? Problem { get; init; }

    /// <summary>The ask the chain was asked by, or null.</summary>
    public string? Ask { get; init; }

    /// <summary>The run's newest session: the one its doors attend.</summary>
    public string? Session { get; init; }

    /// <summary>
    /// A chat run's key, <c>chat-&lt;first&gt;</c> (CHATFLOW1c); null for a quest's run. Its checks are not yet enforced for a conversation
    /// (the chat-flow design §4.4), which the view says until CHATFLOW1h.
    /// </summary>
    public string? ChatRun { get; init; }

    /// <summary>
    /// The checks a chat run's conversation is held to at an execution, read as the execution reads them (<see cref="SessionChecks.Of"/>): its
    /// graph's <c>claimed</c> where a workflow governs it, else its repository's rule; null for a quest's run and where none is set.
    /// </summary>
    public string? ChatChecks { get; init; }

    /// <summary>
    /// The run bound to a graph version, drawn from its record (FABRIC-CUTOVER1e): its wires, its subjects and what its record says of it;
    /// null for a run D157's steps govern.
    /// </summary>
    public WorkflowGraphRun? Graph { get; init; }

    /// <summary>A verification run's commit, branch, verdict and gates (FABRIC-PIPELINE1d); null for a chain's or a chat's run.</summary>
    public WorkflowRunLine? Line { get; init; }

    /// <summary>
    /// The first unsettled step; null where all settled or no graph could be read. Problem distinguishes the latter. A graph run stands
    /// where its drawing says (<see cref="WorkflowGraphRun.At"/>), since a node only a failure leads to is never settled.
    /// </summary>
    public string? At => Graph is { } graph ? graph.At : Steps.FirstOrDefault(step => !WorkflowRunStates.Settled(step.State))?.Step.Id;
}
