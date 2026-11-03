namespace Daoris.Driver;

/// <summary>
/// Whether a home still holds an old cap the supervisor's rules replace: its rules not reached, <c>driver.json</c>'s <c>strikes</c>, or a
/// role's policy with moves or a bound. Once none is left, the old readers kept beside the rules read nothing on this home.
/// </summary>
public static class SupervisorRuleCensus
{
    /// <summary>The old records left: one for a home not reached, one for the key, and one per policy that moves.</summary>
    public static int Count(string home)
    {
        var left = new SupervisorRuleStore(home).Reached() ? 0 : 1;
        if (StrikesHeld(home)) left++;
        return left + PoliciesMoving(home).Count;
    }

    /// <summary>Whether nothing old is left: the end of every read the rules replace.</summary>
    public static bool Ended(string home)
    {
        try
        {
            return Count(home) == 0;
        }
        catch (Exception error) when (error is DriverException or DesktopStoreException or Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Whether this home's <c>driver.json</c> holds <c>strikes</c>.</summary>
    public static bool StrikesHeld(string home)
    {
        var path = DaorisHome.Entry(home, HomeEntry.DriverConfig);
        return File.Exists(path) && DriverConfig.Load(path).StrikesHeld;
    }

    /// <summary>Each role whose stored policy sets a move or a bound, applied or stale, with its policy as read.</summary>
    public static IReadOnlyList<(string Role, StaffPolicyRead Read)> PoliciesMoving(string home)
    {
        var ledger = new StaffLedger(home);
        var moving = new List<(string, StaffPolicyRead)>();
        foreach (var slot in RoleCatalog.ReadHome(home).Slots.Where(slot => slot.Role is not null))
        {
            var read = ledger.Policy(slot.Id);
            if (read.Policy is { } policy && (policy.Moves.Count > 0 || policy.Most is not null)) moving.Add((slot.Id, read));
        }

        return moving;
    }
}
