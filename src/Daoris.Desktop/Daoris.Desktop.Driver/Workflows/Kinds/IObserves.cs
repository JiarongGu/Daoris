namespace Daoris.Driver;

/// <summary>
/// A kind whose node's outcome is read from records in the pass: the wire evaluation asks it of an active or unknown node still
/// bound, and the pass begins its node once its wires fire.
/// </summary>
public interface IObserves
{
    /// <summary>What the records the pass read say of the reading's node: an outcome by its authority, or none yet.</summary>
    NodeObservation Observe(NodeReading reading);

    /// <summary>Whether the pass begins a node of it in <paramref name="governed"/> once its wires fire; where not, it waits ready for its press.</summary>
    bool Begins(GovernedUnit governed) => true;

    /// <summary>Whether a node of it that failed as <paramref name="status"/> is still read: only a reach is taken from it, as an observed fact settles it whoever made it.</summary>
    bool ObservesFailed(NodeStatus status) => false;

    /// <summary>Whether its record may pass a failed node over once it asks nothing of it, as an opinion its rule does not require.</summary>
    bool PassesOverFailed => false;
}
