using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A proposal to allow or ask a rule that a deny would beat is refused at the proposal, naming the deny and where it is set (the person's
/// own deny, or the <c>no-push</c> default once switched on); with no such deny the approval is kept, a push included.
/// </summary>
public sealed class DeadApprovalsTests
{
    private static PermissionFile Empty() =>
        PermissionRules.Load(Path.Combine(TestHomes.Root, "daoris-no-such-home-" + Guid.NewGuid().ToString("N")[..8]));

    [Theory]
    [InlineData(RuleList.Allow, "Bash(git push:*)")]
    [InlineData(RuleList.Ask, "Bash(git -C ../reports push origin main)")]
    [InlineData(RuleList.Allow, "Bash(GIT push --force)")]
    public void A_push_approval_is_kept_while_no_push_is_off_as_it_is_by_default(RuleList list, string rule)
    {
        var change = new ProposedChange("add", RuleScope.Repository, "reports", list, rule, null, null);

        Assert.Null(DeadApprovals.Refusal(Empty(), change));
        Assert.Equal(ChangeEffect.Widens, RuleProposals.Effect(Empty(), change));
    }

    [Theory]
    [InlineData(RuleScope.Repository, "reports", RuleList.Allow, "Bash(git push:*)")]
    [InlineData(RuleScope.Workspace, "games", RuleList.Ask, "Bash(git -c http.extraheader=x push:*)")]
    [InlineData(RuleScope.Machine, null, RuleList.Allow, "Bash(GIT push --force)")]
    public void A_push_approval_is_refused_naming_no_push_where_the_person_switched_it_on(RuleScope scope, string? name, RuleList list, string rule)
    {
        var file = PermissionRules.SwitchDefault(Empty(), PermissionRules.NoPushId, on: true);
        var change = new ProposedChange("add", scope, name, list, rule, null, null);

        var refused = Assert.Throws<DriverException>(() => RuleProposals.Effect(file, change));

        Assert.Contains("`no-push`", refused.Message, StringComparison.Ordinal);
        Assert.Contains("Settings", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_approval_the_persons_own_deny_would_beat_is_refused_naming_that_deny_and_its_scope()
    {
        var file = PermissionRules.Add(Empty(), RuleScope.Repository, "reports", RuleList.Deny, "Bash(npm publish:*)");
        var change = new ProposedChange("add", RuleScope.Machine, null, RuleList.Allow, "Bash(npm publish:*)", null, null);

        var refused = Assert.Throws<DriverException>(() => RuleProposals.Effect(file, change));

        Assert.Contains("`reports`'s rules", refused.Message, StringComparison.Ordinal);
        Assert.Contains("a deny beats", refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RuleList.Deny, "Bash(git push:*)")]
    [InlineData(RuleList.Allow, "Bash(git stash push:*)")]
    [InlineData(RuleList.Allow, "Bash(git commit:*)")]
    public void A_deny_or_a_rule_no_deny_beats_is_judged_as_any_rule(RuleList list, string rule)
    {
        var file = PermissionRules.SwitchDefault(Empty(), PermissionRules.NoPushId, on: true);

        Assert.Null(DeadApprovals.Refusal(file, new ProposedChange("add", RuleScope.Repository, "reports", list, rule, null, null)));
    }
}
