namespace Daoris.Driver;

/// <summary>
/// A run's active node after its rounds, as the keeping hands it to its kind (<see cref="IStarts.Keep"/>): the record it stands on and the doors,
/// for a door that is idempotent by node and tip to take it once.
/// </summary>
internal sealed record NodeKept(GovernedUnit Governed, RunScope Scope, NodeDeclaration Node, NodeStatus Status, NodeRecord Record, NodeDoors Doors)
{
    /// <summary>The landing the run's <c>land</c> made, which the steps past it read; null where none is made yet.</summary>
    public LandedBranch? Landed => NodeEvaluations.Landed(Governed, Record.Statuses) is { Unit: var unit } ? NodeObserving.LandingOf(unit) : null;
}
