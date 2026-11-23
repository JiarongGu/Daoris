namespace Daoris.Driver;

/// <summary>The window's reader of which held dones the risk policy gives to a role, so no row of <i>What needs you</i> offers them to the person (RISKGATE1c2).</summary>
public static class QuestRoleHolds
{
    /// <summary>The ids of <paramref name="quests"/>' held dones (a departure, evidence read and not found) the policy at their place classes <c>role</c>.</summary>
    public static IReadOnlySet<string> Of(DriverConfig config, IEnumerable<QuestView> quests) => QuestDepartures.RoleHeld(config, quests);
}
