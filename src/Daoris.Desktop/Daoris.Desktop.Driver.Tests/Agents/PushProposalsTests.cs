using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A proposal to allow or ask a git push can never open one: the fixed `no-push` deny always wins and only git access's Write opens a
/// push, so it is refused at the proposal, naming the place's Write and the verb that sets it. A deny naming a push still narrows.
/// </summary>
public sealed class PushProposalsTests
{
    private static PermissionFile Empty() =>
        PermissionRules.Load(Path.Combine(TestHomes.Root, "daoris-no-such-home-" + Guid.NewGuid().ToString("N")[..8]));

    [Theory]
    [InlineData(RuleScope.Repository, "reports", RuleList.Allow, "Bash(git push:*)", "daoris git access reports write=ask")]
    [InlineData(RuleScope.Repository, "reports", RuleList.Ask, "Bash(git -C ../reports push origin main)", "daoris git access reports write=ask")]
    [InlineData(RuleScope.Workspace, "games", RuleList.Allow, "Bash(git -c http.extraheader=x push:*)", "daoris git access --workspace games write=ask")]
    [InlineData(RuleScope.Machine, null, RuleList.Allow, "Bash(GIT push --force)", "daoris git access --machine write=ask")]
    public void A_rule_opening_a_push_is_refused_naming_the_places_write_and_its_verb(RuleScope scope, string? name, RuleList list, string rule, string verb)
    {
        var change = new ProposedChange("add", scope, name, list, rule, null, null);

        var refused = Assert.Throws<DriverException>(() => RuleProposals.Effect(Empty(), change));

        Assert.Contains(verb, refused.Message, StringComparison.Ordinal);
        Assert.Contains("Write", refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RuleList.Deny, "Bash(git push:*)")]
    [InlineData(RuleList.Allow, "Bash(git stash push:*)")]
    [InlineData(RuleList.Allow, "Bash(git commit:*)")]
    [InlineData(RuleList.Allow, "Bash(npm run push-docs)")]
    public void A_deny_of_a_push_or_a_rule_naming_none_is_judged_as_any_rule(RuleList list, string rule)
    {
        Assert.Null(PushProposals.Refusal(new ProposedChange("add", RuleScope.Repository, "reports", list, rule, null, null)));
    }
}
