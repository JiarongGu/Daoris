namespace Daoris.Driver;

/// <summary>
/// What a start in one workspace would run on, and where each part came from (MAP1b) — the wiring
/// panel's answer, never a second judgement. Carries names and versions only: no home, no binary path
/// and no key, so it is safe on any surface the roster already reaches.
/// </summary>
/// <param name="Adapter">The adapter a start spawns — `driver.json`'s choice.</param>
/// <param name="Owner">Whose accounts it runs as (AGT7): the adapter itself, or the tool a door opens onto.</param>
/// <param name="Profile">The account, by its directory name; null is the agent's own sign-in.</param>
/// <param name="ProfileFrom">Which rung chose it.</param>
/// <param name="Version">The version it would run: the probed one, else the pin that was asked for.</param>
/// <param name="VersionFrom">Which rung pinned it; unset is <c>PATH</c>.</param>
/// <param name="Commanded">`driver.json` names the command outright, which outranks any pin.</param>
/// <param name="Refusal">Why a start would be held, in the driver's own words; null when it would run.</param>
public sealed record StartWiring(
    string Adapter, string Owner, string? Profile, ChoiceFrom ProfileFrom,
    string? Version, ChoiceFrom VersionFrom, bool Commanded, string? Refusal)
{
    /// <summary>
    /// The account <see cref="ProfileFrom"/>'s rung named, when the start runs on another account of the person's order
    /// because that one is not ready (TOOL4f, D125 §3.3): <see cref="Profile"/> is then the account the start takes, so
    /// the panel never shows an account a start would not take. Null when nothing rotated.
    /// </summary>
    public string? RotatedFrom { get; init; }
}
