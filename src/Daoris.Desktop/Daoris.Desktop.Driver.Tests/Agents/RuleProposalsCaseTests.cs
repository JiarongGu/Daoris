using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A scope's name is one scope in any case (`PermissionRules.Add` keeps the spelling it already holds), so a proposal naming it in another
/// case is read against the rules that scope holds: a deny made an ask there widens, and waits for the person.
/// </summary>
public sealed class RuleProposalsCaseTests
{
    private static PermissionFile Held(RuleScope scope, string name, RuleList list, string rule) =>
        PermissionRules.Add(
            PermissionRules.Load(Path.Combine(TestHomes.Root, "daoris-no-such-home-" + Guid.NewGuid().ToString("N")[..8])),
            scope, name, list, rule);

    [Theory]
    [InlineData(RuleScope.Repository, "Daoris")]
    [InlineData(RuleScope.Repository, " daoris ")]
    [InlineData(RuleScope.Workspace, "GAMES")]
    public void A_deny_made_an_ask_under_another_case_of_the_scopes_name_widens(RuleScope scope, string asked)
    {
        var file = Held(scope, scope == RuleScope.Workspace ? "games" : "daoris", RuleList.Deny, "Bash(rm:*)");

        var change = new ProposedChange("add", scope, asked, RuleList.Ask, "Bash(rm:*)", null, null);

        Assert.Equal(ChangeEffect.Widens, RuleProposals.Effect(file, change));
    }

    [Fact]
    public void Removing_an_allow_under_another_case_of_the_scopes_name_narrows_and_is_not_already_so()
    {
        var file = Held(RuleScope.Repository, "daoris", RuleList.Allow, "Bash(ls:*)");

        var change = new ProposedChange("remove", RuleScope.Repository, "DAORIS", null, "Bash(ls:*)", null, null);

        Assert.Equal(ChangeEffect.Narrows, RuleProposals.Effect(file, change));
    }
}
