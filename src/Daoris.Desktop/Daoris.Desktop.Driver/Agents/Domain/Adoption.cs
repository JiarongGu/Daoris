namespace Daoris.Driver;

/// <summary>
/// The judge of driving a repository that has not adopted Daoris (D70; the agent-provider design §7 rows 1 and 2), over door ids
/// alone: a door may where its servers ride the session's wire, since such a repository has no <c>.mcp.json</c> of Daoris's for a
/// door that inherits its servers to find. Pure: the doors' abilities are handed in.
/// </summary>
/// <remarks>
/// An id nobody knows reads as a door that may not, the stricter answer, until s-i refuses it: the plan then plans nothing more than
/// it should and the spawn names the unknown id itself.
/// </remarks>
public static class Adoption
{
    /// <summary>Whether <paramref name="door"/> may drive a repository that has not adopted Daoris.</summary>
    public static bool Unadopted(string door, Func<string, DoorAbilities?> abilities) => Unadopted(abilities(door));

    /// <summary>Whether a door of these abilities may drive a repository that has not adopted Daoris; none known may not.</summary>
    public static bool Unadopted(DoorAbilities? door) => door is { ServersOnWire: true };

    /// <summary>
    /// Whether any of <paramref name="doors"/>, Build's candidates in their order, may drive a repository that has not adopted Daoris:
    /// the plan's answer for the machine, which the start then holds each candidate to.
    /// </summary>
    public static bool Drives(IEnumerable<string> doors, Func<string, DoorAbilities?> abilities) =>
        doors.Any(door => Unadopted(door, abilities));
}
