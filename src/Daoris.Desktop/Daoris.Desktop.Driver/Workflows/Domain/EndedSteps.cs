namespace Daoris.Driver;

/// <summary>
/// A step of a run that has ended as it is read. A run's end leaves the steps it did not finish as they stand on the record, so a step the record
/// holds under way (ready, active, invalidated) is never read as work being done: nothing of an ended graph moves until its chain comes back.
/// Read at the reading, never written to the record.
/// </summary>
public static class EndedSteps
{
    /// <summary>The state and detail <paramref name="state"/> is read as: a settled one as it is, one the record holds under way as not reached.</summary>
    public static (string State, string Detail) Read(string recorded, string state, string detail) =>
        recorded is NodeStates.Ready or NodeStates.Active or NodeStates.Invalidated
            ? (WorkflowRunStates.NotReached, WorkflowGraphDetails.Blocked)
            : (state, detail);
}
