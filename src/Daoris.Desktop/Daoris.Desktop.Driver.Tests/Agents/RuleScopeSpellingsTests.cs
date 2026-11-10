using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A scope name a hand edit holds in two spellings is acted on and read in both, as a session's composition takes both.</summary>
public sealed class RuleScopeSpellingsTests
{
    private const string Rule = "Bash(make test:*)";

    private static PermissionFile TwoSpellings() => new(
        RuleLists.Empty,
        new Dictionary<string, RuleLists>(),
        new Dictionary<string, RuleLists>
        {
            ["Engine"] = new([Rule], [], []),
            ["engine"] = new([Rule], [], []),
        },
        []);

    [Fact]
    public void A_removal_takes_the_rule_out_of_every_spelling_of_the_scope()
    {
        var removed = PermissionRules.Remove(TwoSpellings(), RuleScope.Repository, "engine", Rule);

        Assert.DoesNotContain(Rule, PermissionRules.Compose(removed, null, "engine").Allow);
    }

    [Fact]
    public void A_rule_that_sits_in_the_second_spelling_alone_is_found()
    {
        var file = new PermissionFile(
            RuleLists.Empty, new Dictionary<string, RuleLists>(),
            new Dictionary<string, RuleLists> { ["Engine"] = RuleLists.Empty, ["engine"] = new([Rule], [], []) }, []);
        var change = new ProposedChange("add", RuleScope.Repository, "ENGINE", RuleList.Allow, Rule, null, null);

        Assert.Equal(RuleList.Allow, RuleProposals.Sits(file, change, Rule));
    }
}
