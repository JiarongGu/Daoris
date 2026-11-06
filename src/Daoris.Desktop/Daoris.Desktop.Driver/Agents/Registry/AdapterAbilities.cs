namespace Daoris.Driver;

/// <summary>
/// A door's abilities read through the old seam (AGENTPROVIDER1 row g), from its record. Row t takes it away with the seam.
/// </summary>
public static class AdapterAbilities
{
    /// <summary>What <paramref name="adapter"/>'s door can do (<see cref="DoorAbilities"/>).</summary>
    public static DoorAbilities Abilities(this ISessionAdapter adapter) => DoorAbilities.Of(LegacyDoor.Of(adapter).Door);

    /// <summary>The abilities of the door named <paramref name="name"/> in <paramref name="adapters"/>, null where none is.</summary>
    public static DoorAbilities? AbilitiesOf(this ProviderRegistry adapters, string name)
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
