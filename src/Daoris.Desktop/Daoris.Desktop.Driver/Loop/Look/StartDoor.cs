namespace Daoris.Driver;

/// <summary>
/// Whether this machine's starts may drive a repository that has not adopted Daoris: where any entry of Build's order may (<see cref="Adoption"/>).
/// The planner asks whether any entry can drive a repository that has not adopted Daoris, and the start passes each entry that
/// cannot. Read against this look's adapters so a plugin's harness counts.
/// </summary>
internal static class StartDoor
{
    /// <summary>
    /// An adapter nobody knows may not: the stricter answer plans nothing more than it should, and the spawn names the unknown
    /// adapter itself. An order that does not read may not either, as the start that reads it holds.
    /// </summary>
    public static bool Of(string home, DriverConfig config, AdapterSet adapters)
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
