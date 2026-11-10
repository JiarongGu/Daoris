using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>A place's supervisor rules before and after a press, as the rules' own fields, which the page words from its catalogue.</summary>
public sealed record PressRules(string Place, IReadOnlyList<PressRule> Before, IReadOnlyList<PressRule> After)
{
    public static PressRules Of(string place, IReadOnlyList<SupervisorRule> before, IReadOnlyList<SupervisorRule> after) =>
        new(place, [.. before.Select(PressRule.Of)], [.. after.Select(PressRule.Of)]);
}

/// <summary>One supervisor rule as the card carries it: its count, where it acts, what it does, how long it waits and the roles it reaches.</summary>
public sealed record PressRule(string Count, int At, string Do, int? UpTo, IReadOnlyList<string>? Roles)
{
    public static PressRule Of(SupervisorRule rule) => new(rule.Count, rule.At, rule.Do, rule.UpTo, rule.Roles);
}

/// <summary>A supervisor set by a press: its names as written, its scope, and its record after the press and before it (none for a new one).</summary>
public sealed record PressSupervisor(string Id, IReadOnlyList<string> Names, PressScope Scope, PressHold Hold, PressHold? Before);

/// <summary>Where a role holds: <c>machine</c>, or a workspace or repository by its id.</summary>
public sealed record PressScope(string Kind, string? Id);

/// <summary>An appointed role's record as the page words it: the kinds it decides, its granted acts, its holder and its window.</summary>
public sealed record PressHold(string Kind, IReadOnlyList<string> Acts, string? Holder, int Window, IReadOnlyList<string> Decides)
{
    public static PressHold Of(RoleHold hold) => new(hold.Kind, hold.Acts, hold.Holder, hold.Window, hold.Decides);
}
