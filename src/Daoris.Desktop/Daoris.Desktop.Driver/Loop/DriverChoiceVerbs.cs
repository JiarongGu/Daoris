namespace Daoris.Driver;

/// <summary>
/// The verbs of <c>daoris-driver driver</c> that the subdomains above the settings own, wired into the terminal's world (FOUNDATION1
/// mod-iii; the design §14.3: a lower subdomain declares a port and the higher implements it, wired by Loop). The settings' command
/// (<see cref="DriverChoicesCommand"/>) names none of them: each host that builds a <see cref="DriverChoicesWorld"/> hands it this set
/// as its <see cref="DriverChoicesWorld.Verbs"/>.
/// </summary>
public static class DriverChoiceVerbs
{
    /// <summary>Every verb the settings' command hands on, each by the subdomain whose acts it drives.</summary>
    public static IReadOnlyList<ChoiceVerb> All { get; } =
    [
        new HelpersVerb(),
    ];
}
