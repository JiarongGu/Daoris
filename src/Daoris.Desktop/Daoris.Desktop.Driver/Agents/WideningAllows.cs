namespace Daoris.Driver;

/// <summary>
/// Where an allow supervisor may decide a widening proposed to the rules, and what stays the person's. A deny asks for no allow, so a
/// widening that undoes one the person made, or switches off a default that denies, is never a supervisor's; and while
/// <c>no-publish</c> is on, no rule that would let a publish through is either.
/// </summary>
public static class WideningAllows
{
    public const string Machine = "machine";

    public const string RemovesDeny = "removes-deny";
    public const string SwitchesOffDenial = "switches-off-denial";
    public const string LetsPublishThrough = "lets-publish-through";

    /// <summary>
    /// The place whose supervisor answers a widening: its rule's reach, the machine for a machine rule and for any default's switch
    /// (one machine-wide list), a workspace or a repository for its own.
    /// </summary>
    public static string PlaceOf(ProposedChange change) => change.Action == "default"
        ? Machine
        : change.Scope switch
        {
            RuleScope.Workspace => $"workspace:{change.Name?.Trim()}",
            RuleScope.Repository => $"repository:{change.Name?.Trim()}",
            _ => Machine,
        };

    /// <summary>
    /// Why <paramref name="change"/> stays the person's under <paramref name="rules"/> as they stand, or null where an allow supervisor may
    /// decide it. A change that is not a widening is not asked of anyone.
    /// </summary>
    public static string? Reserved(PermissionFile rules, ProposedChange change)
    {
        if (change.Action == "default")
        {
            var shipped = PermissionRules.Defaults.FirstOrDefault(each => each.Id == change.Default);
            return shipped is not null && change.On == false && (shipped.List != RuleList.Allow || shipped.Hook is not null) ? SwitchesOffDenial : null;
        }

        var rule = RuleProposals.Rule(change);
        if (RuleProposals.Sits(rules, change, rule) == RuleList.Deny && (change.Action == "remove" || change.List != RuleList.Deny)) return RemovesDeny;
        var noPublish = PermissionRules.Defaults.First(each => each.Id == PermissionRules.NoPublishId);
        return change.Action == "add" && change.List is RuleList.Allow or RuleList.Ask && PermissionRules.IsOn(rules, noPublish) && PublishForms.Matches(rule)
            ? LetsPublishThrough
            : null;
    }
}
