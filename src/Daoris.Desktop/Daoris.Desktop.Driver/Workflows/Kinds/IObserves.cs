namespace Daoris.Driver;

/// <summary>
/// A kind whose node's outcome is read from records in the pass (D247 §3.3): the wire evaluation asks it of an active or unknown node still
/// bound, and the pass begins its node once its wires fire.
/// </summary>
public interface IObserves
{
    /// <summary>What the records the pass read say of the reading's node: an outcome by its authority, or none yet.</summary>
    NodeObservation Observe(NodeReading reading);

    /// <summary>Whether the pass begins a node of it in <paramref name="governed"/> once its wires fire; where not, it waits ready for its press.</summary>
    bool Begins(GovernedUnit governed) => true;
}
