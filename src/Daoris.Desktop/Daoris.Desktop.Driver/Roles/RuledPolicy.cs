namespace Daoris.Driver;

/// <summary>
/// A role's policy beside the supervisor's rules: a line's moves and their bound are the rules, so a preset's limit move and bound become
/// rules naming the role, written beside the policy in its one transaction.
/// </summary>
public static class RuledPolicy
{
    /// <summary>The rules a press's preset brings, at the role's place; none for no preset, and none for a supervisor, whose turns are no line a rule moves.</summary>
    public static IReadOnlyList<SupervisorRule> Of(RoleRecord role, RolePolicyPress press) =>
        press is { Preset: { } preset, Clear: false } && role.Hold is null ? SupervisorRuleBook.PresetRules(preset, role.Id, PlaceOf(role.Scope)) : [];

    /// <summary>The supervisor's place a role's scope is: the machine, a workspace or a repository.</summary>
    public static string PlaceOf(RoleScope scope) => scope.Kind == RoleScope.Machine ? SupervisorRules.Machine : $"{scope.Kind}:{scope.Id}";
}
