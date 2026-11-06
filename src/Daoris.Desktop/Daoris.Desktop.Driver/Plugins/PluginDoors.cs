namespace Daoris.Driver;

/// <summary>
/// A plugin's manifest converted to the doors it declares (the agent-provider design §4.8, row q). It reads the harness's named fields
/// alone, so a manifest naming one the declaration has no place for (<c>steering</c>, <c>resumes</c>, <c>limits</c>, <c>rules</c>,
/// <c>dialect</c>) reaches nothing, as it never did; Agents' factory makes the door.
/// </summary>
public static class PluginDoors
{
    /// <summary>The declaration <paramref name="harness"/> makes for <paramref name="plugin"/>.</summary>
    public static DeclaredDoor Declare(PluginHarness harness, string plugin) => new(
        harness.Name,
        plugin,
        harness.Command,
        harness.Posture,
        harness.ProfileVariable,
        harness.Package,
        harness.Install,
        harness.VersionArguments,
        harness.AccountOf,
        harness.Product,
        harness.Maker,
        harness.OwnHome,
        harness.FullPosture,
        harness.AskingPosture);

    /// <summary>The door <paramref name="harness"/> makes for <paramref name="plugin"/>, which the registry places by its account key when it joins.</summary>
    public static AgentDoor Door(PluginHarness harness, string plugin) => DeclaredDoors.Door(Declare(harness, plugin));
}
