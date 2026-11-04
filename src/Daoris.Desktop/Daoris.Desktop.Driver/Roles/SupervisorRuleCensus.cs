namespace Daoris.Driver;

/// <summary>
/// The roles whose stored policy still sets a move or a bound, which the supervisor's rules replace.
/// </summary>
public static class SupervisorRuleCensus
{
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
