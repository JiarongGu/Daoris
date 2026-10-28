namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's conversation placed (HELPSTART1, <see cref="ChatRunner.PlaceHelpAsync"/>): its record open in its workspace's room
/// folder (ASKSCOPE1b), its agent and account chosen, nothing spawned yet.
/// </summary>
/// <param name="SessionId">The record's id — null when nothing was placed, and <paramref name="Message"/> says why.</param>
/// <param name="Message">The ledger's sentence, or why nothing was placed.</param>
public sealed record HelpPlaced(string? SessionId, string Message)
{
    /// <summary>
    /// The workspace it was placed in (ASKSCOPE1a, D158's ASKSCOPE1 note point 1): the one its window was on at the press, kept
    /// by its record; <c>default</c> where the press named none.
    /// </summary>
    public string Workspace { get; init; } = RemoteTarget.DefaultWorkspace;

    internal ISessionAdapter? Adapter { get; init; }

    internal HarnessSelection? Selection { get; init; }

    /// <summary>The entry of Ask Daoris's order it was placed on (MODELROLE1d, D205 §4.1), which its run asks; null runs as before.</summary>
    internal ActorEffective? Chosen { get; init; }

    /// <summary>Its pending start (MODELROLE1d2, D205 §3.5(b)), which its run holds until it is dispatched or exhausted; null for none.</summary>
    internal ChatPending? Pending { get; init; }
}
