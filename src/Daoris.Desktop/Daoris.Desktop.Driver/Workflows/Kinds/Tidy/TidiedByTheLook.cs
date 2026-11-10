namespace Daoris.Driver;

/// <summary>
/// The clean-up of a chain the look's own tidy already ended: its landing gone from the record by its end (on the line, or merged) with
/// nothing tidied on it, and the unit's own session branch gone too. The tidy's door finds no standing landing to write on and ends nothing,
/// so the tidy's reading reaches it from the record as it stands rather than waiting for a write that never comes.
/// </summary>
internal static class TidiedByTheLook
{
    /// <summary>Whether <paramref name="landing"/>, the landing <paramref name="unit"/>'s tidy reads, left that tidy nothing to clean.</summary>
    public static bool Of(LandedBranch? landing, WorkUnit unit) =>
        landing is { GoneAt: not null, RemovedAs: not null, Tidied: null } && unit.Records.Branch is not { GoneAt: null };

    /// <summary>What the reached tidy says.</summary>
    public static string Said(LandedBranch landing) =>
        $"nothing was left to clean: the look's own tidy removed `{landing.Branch}` ({landing.RemovedAs}"
        + (landing.RemovedOn is { } on ? $" on `{on}`" : "") + ") and its chain's session branch with it";
}
