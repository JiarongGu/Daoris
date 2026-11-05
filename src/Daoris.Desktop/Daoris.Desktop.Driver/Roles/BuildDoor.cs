namespace Daoris.Driver;

/// <summary>
/// Whether this machine's starts may drive a repository that has not adopted Daoris, the one answer the loop's planner, the window, the
/// terminal's two and set-up all read (AGENTPROVIDER1 s-i): where any entry of Build's order may (<see cref="Adoption"/>), against the adapters
/// of the look asking, so a plugin's harness counts.
/// </summary>
public static class BuildDoor
{
    /// <summary>
    /// An adapter nobody knows may not: the stricter answer plans nothing more than it should, and the spawn names the unknown
    /// adapter itself. An order that does not read may not either, as the start that reads it holds.
    /// </summary>
    public static bool Drives(string home, DriverConfig config, AdapterSet adapters)
    {
        IReadOnlyList<ActorEffective> order;
        try
        {
            order = RoleCatalog.ReadHome(home).Order(ParticipantWork.Build, new RolePlace(), config);
        }
        catch (DriverException)
        {
            order = [];
        }

        return Adoption.Drives(order.Select(entry => entry.Agent), adapters.AbilitiesOf);
    }
}
