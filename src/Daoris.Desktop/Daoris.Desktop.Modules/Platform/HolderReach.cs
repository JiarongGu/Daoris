using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// Whether a role holder's harness may work where the holder does (at one of the two starts kept outside
/// the plan): a holder's roles reach across the machine's repositories, so its place is the machine's room. With no walk to pass it in,
/// a provider not opened there is refused before anything is started, so a provider's reach is never as wide as an absent part.
/// </summary>
public static class HolderReach
{
    /// <summary>Null where the holder's harness may work at the machine's room; else the sentence its refusal says.</summary>
    public static string? Closes(ProviderReach reach) => reach.Closes(HolderTerminal.Harness, ReachPlace.Machine);
}
