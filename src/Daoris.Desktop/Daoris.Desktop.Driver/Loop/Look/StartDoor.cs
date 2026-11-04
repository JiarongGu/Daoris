namespace Daoris.Driver;

/// <summary>
/// The door this machine's starts ride: the protocol door where any entry of Build's order rides it, else the first entry's wire.
/// The planner asks whether any entry can drive a repository that has not adopted Daoris, and the start passes each entry that
/// cannot. Read against this look's adapters so a plugin's harness counts.
/// </summary>
internal static class StartDoor
{
    /// <summary>
    /// An adapter nobody knows is the pipe: the stricter door plans nothing more than it should, and the spawn names the unknown
    /// adapter itself. An order that does not read is the pipe too, as the start that reads it holds.
    /// </summary>
    public static SessionWire Of(string home, DriverConfig config, AdapterSet adapters)
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

        SessionWire? first = null;
        foreach (var entry in order)
        {
            try
            {
                var wire = adapters.Resolve(entry.Agent).Wire;
                if (wire == SessionWire.Acp) return wire;
                first ??= wire;
            }
            catch (DriverException)
            {
                first ??= SessionWire.Pipe;
            }
        }

        return first ?? SessionWire.Pipe;
    }
}
