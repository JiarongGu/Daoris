namespace Daoris.Driver;

/// <summary>
/// The judge of driving a repository that has not adopted Daoris (D70; the agent-provider design §7 rows 1 and 2), over door ids
/// alone: a door may where its servers ride the session's wire, since such a repository has no <c>.mcp.json</c> of Daoris's for a
/// door that inherits its servers to find. Pure: the doors' abilities are handed in.
/// </summary>
/// <remarks>
/// An id nobody knows is refused (<see cref="UnknownDoorException"/>), never read as the pipe.
/// </remarks>
public static class Adoption
{
    /// <summary>Whether <paramref name="door"/> may drive a repository that has not adopted Daoris.</summary>
    /// <exception cref="UnknownDoorException">No door of that id is known.</exception>
    public static bool Unadopted(string door, Func<string, DoorAbilities?> abilities) =>
        Unadopted(abilities(door) ?? throw new UnknownDoorException(door));

    /// <summary>Whether a door of these abilities may drive a repository that has not adopted Daoris; none known may not.</summary>
    public static bool Unadopted(DoorAbilities? door) => door is { ServersOnWire: true };

    /// <summary>
    /// Whether a door of these abilities may drive a repository, <paramref name="adopted"/> or not: an adopted one by its own servers where
    /// the door's inherit its environment or ride its wire, the rest by <see cref="Unadopted(DoorAbilities?)"/>.
    /// </summary>
    public static bool Drives(DoorAbilities? door, bool adopted) =>
        adopted ? door is { ServersOnWire: true } or { ServersInherit: true } : Unadopted(door);

    /// <summary>
    /// Whether any of <paramref name="doors"/>, Build's candidates in their order, may drive a repository that has not adopted Daoris:
    /// the plan's answer for the machine, which the start then holds each candidate to.
    /// </summary>
    /// <exception cref="UnknownDoorException">A door of the order is unknown: every one is asked, so none hides behind an earlier answer.</exception>
    public static bool Drives(IEnumerable<string> doors, Func<string, DoorAbilities?> abilities) =>
        doors.Select(door => Unadopted(door, abilities)).ToList().Contains(true);
}
