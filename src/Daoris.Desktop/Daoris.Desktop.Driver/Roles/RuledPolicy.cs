namespace Daoris.Driver;

/// <summary>
/// A role's policy on a home whose rules are reached: its moves and their bound are the supervisor's rules, so a press setting them is
/// refused naming the rules' door, and a preset's limit move and bound become rules naming the role, written beside the policy in its
/// one transaction. On a home not reached nothing here applies.
/// </summary>
public static class RuledPolicy
{
    /// <summary>
    /// The policy saved on a reached home, the rules a preset brings with it at the role's place, or the refusal of a move set through the
    /// policy door.
    /// </summary>
    public static (StaffPolicy? Policy, IReadOnlyList<SupervisorRule> Rules, RoleRefusal? Refused) On(RoleRecord role, RolePolicyPress press, StaffPolicy? resolved)
    {
        if (resolved is null || (resolved.Moves.Count == 0 && resolved.Most is null)) return (resolved, [], null);
        var place = PlaceOf(role.Scope);
        if (press.Preset is not null)
        {
            return (resolved with { Moves = new Dictionary<string, StaffMove>(), Most = null }, SupervisorRuleBook.FromPolicy(resolved, role.Id, place), null);
        }

        return (null, [], new RoleRefusal(StaffPolicy.Code,
            $"on this home a line's moves to another choice and their bound are the supervisor's rules, so `{role.Id}`'s `moves` and `most` were not saved: "
            + $"set them with `daoris role rules --place {place} --roles {role.Id} --set <count>=<at>:<do>`."));
    }

    /// <summary>The supervisor's place a role's scope is: the machine, a workspace or a repository.</summary>
    public static string PlaceOf(RoleScope scope) => scope.Kind == RoleScope.Machine ? SupervisorRules.Machine : $"{scope.Kind}:{scope.Id}";

    /// <summary>The refusal of the <c>strikes</c> doors on a reached home, where the failures rule decides; null on a home not reached.</summary>
    public static string? StrikesRefused(string home) =>
        new SupervisorRuleStore(home).Reached()
            ? "on this home the failed sessions that park a quest are the supervisor's rule, so `strikes` was not changed: set it with "
              + "`daoris role rules --place machine --set failures=<n>:hold`."
            : null;
}
