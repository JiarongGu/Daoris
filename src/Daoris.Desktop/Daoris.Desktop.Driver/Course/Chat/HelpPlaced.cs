namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's conversation placed (<see cref="ChatRunner.PlaceHelpAsync"/>): its record open in its workspace's room
/// folder, its agent and account chosen, nothing spawned yet.
/// </summary>
/// <param name="SessionId">The record's id — null when nothing is placed, and <paramref name="Message"/> says why.</param>
/// <param name="Message">The ledger's sentence, or why nothing is placed.</param>
public sealed record HelpPlaced(string? SessionId, string Message)
{
    /// <summary>
    /// The workspace it is placed in: the one its window sat on at the press, kept
    /// by its record; <c>default</c> where the press named none.
    /// </summary>
    public string Workspace { get; init; } = RemoteTarget.DefaultWorkspace;

    internal AgentDoor? Adapter { get; init; }

    internal HarnessSelection? Selection { get; init; }

    /// <summary>The entry of Ask Daoris's order it is placed on, which its run asks; null for none.</summary>
    internal ActorEffective? Chosen { get; init; }

    /// <summary>Its pending start, which its run holds until it is dispatched or exhausted; null for none.</summary>
    internal ChatPending? Pending { get; init; }
}
