namespace Daoris.Driver;

/// <summary>
/// The key of the words an Accept says to a session it carries on (LANDBACK1 part 1, <see cref="LandCarry"/>): pure key vocabulary, which
/// <see cref="DaorisKeys"/> keeps under its own names for the readers above the workplace.
/// </summary>
public static class CarryKeys
{
    public const string Kind = "carry";

    /// <summary>
    /// The carry-on's key: the session, what it is carried on for (a dirty tree's status fingerprint, or the tip a conflict was found at),
    /// and which carry-on of the Accept's two it is, so an unchanged tree is told once however often the person presses, and the second
    /// carry-on after a turn that changed nothing is still a word of its own. No quest's fact: <see cref="DaorisKeys.Read"/> names none.
    /// </summary>
    /// <param name="basis">The dirty tree's fingerprint (<see cref="AutoLandingRules.Fingerprint"/>) or <c>conflict-</c> and the tip.</param>
    public static string Of(string session, string basis, int carryOn) => $"{Kind}/{session}/{basis.Replace(':', '-')}/{carryOn}";
}
