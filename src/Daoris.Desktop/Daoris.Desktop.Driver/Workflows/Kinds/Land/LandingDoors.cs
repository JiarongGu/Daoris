namespace Daoris.Driver;

/// <summary>
/// The door over the session trees that performs a <c>land</c> the runtime started: from the node's
/// bound intent alone, off the pass, under the tree's hold and the delivery's key, so a press and a pass land one delivery once.
/// </summary>
public interface ILandingDoor : INodeDoor
{
    /// <summary>
    /// Starts the landing of <paramref name="start"/> off the pass; returns at once. True where this call started one, false where one for the
    /// same node and tip is under way or has been refused at that tip and the tree has not moved since.
    /// </summary>
    bool Start(LandStart start);
}
