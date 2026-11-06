namespace Daoris.Driver;

/// <summary>
/// A door's abilities read through the old seam (AGENTPROVIDER1 row g): a contract door's from its record, and an adapter that is no
/// contract door, a plugin's or a test's, from what it declares and its wire. Row t takes it away with the seam.
/// </summary>
public static class AdapterAbilities
{
    /// <summary>What <paramref name="adapter"/>'s door can do (<see cref="DoorAbilities"/>).</summary>
    public static DoorAbilities Abilities(this ISessionAdapter adapter) => adapter is LegacyDoor legacy
        ? DoorAbilities.Of(legacy.Door)
        : DoorAbilities.Of(adapter.Transport, adapter.Interactive, adapter.Resumes, adapter.TakesSettings, adapter.StructuredOutput() is not null);

    /// <summary>The abilities of the door named <paramref name="name"/> in <paramref name="adapters"/>, null where none is.</summary>
    public static DoorAbilities? AbilitiesOf(this AdapterSet adapters, string name)
    {
        try
        {
            return adapters.Resolve(name).Abilities();
        }
        catch (DriverException)
        {
            return null;
        }
    }
}
