namespace Daoris.Driver;

/// <summary>The window's reader of which held dones the risk policy gives to a role, so no row of <i>What needs you</i> offers them to the person (RISKGATE1c2).</summary>
public static class QuestRoleHolds
{
    /// <summary>The ids of <paramref name="quests"/>' held dones (a departure, evidence read and not found) the policy at their place classes <c>role</c>.</summary>
    public static IReadOnlySet<string> Of(DriverConfig config, IEnumerable<QuestView> quests) => QuestDepartures.RoleHeld(config, quests);

    /// <summary>As <see cref="Of(DriverConfig, IEnumerable{QuestView})"/>, over the quests whose view still reads as the row's quest: same id, hold and last move.</summary>
    public static IReadOnlySet<string> Of(DriverConfig config, IEnumerable<QuestView> quests, IEnumerable<(string Id, string? Hold, DateTimeOffset? Updated)> rows)
    {
        var read = rows.ToList();
        return QuestDepartures.RoleHeld(config, quests.Where(quest => read.Contains((quest.Id, quest.Hold, quest.Updated))));
    }
}
