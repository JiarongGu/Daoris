using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The second-opinion rule as <c>driver.json</c> keeps it (FOUNDATION1 mod-iii): the settings' own reading and writing, which the
/// opinion rules answer through, so the key is read and kept alike wherever a rule is named.
/// </summary>
public sealed class OpinionKeyTests
{
    [Fact]
    public void The_rules_answer_through_the_key_for_their_names_and_bound()
    {
        Assert.Equal(OpinionKey.Landing, OpinionRules.Landing);
        Assert.Equal(OpinionKey.Steps, OpinionRules.Steps);
        Assert.Equal(OpinionKey.Occasions, OpinionRules.Occasions);
        Assert.Equal(OpinionKey.DefaultMinutes, OpinionRules.DefaultMinutes);
        Assert.Equal(OpinionKey.DefaultMinutes, new OpinionRule(["a"], ["landing"]).Bound);
    }

    [Fact]
    public void A_rule_is_read_and_kept_as_written_and_the_rules_read_it_alike()
    {
        const string text = """{"on":["landing","steps"],"reviewers":["codex-acp","dsh"],"required":true,"verify":true,"minutes":30,"recheck":false}""";
        using var document = JsonDocument.Parse(text);

        var (rule, problem) = OpinionKey.Read(document.RootElement);

        Assert.Null(problem);
        Assert.Equal(["codex-acp", "dsh"], rule!.Reviewers);
        Assert.Equal(30, rule.Bound);
        Assert.Equal(text, OpinionRules.ToJson(rule));
        Assert.Equal(text, OpinionRules.ToJson(OpinionRules.Read(document.RootElement).Rule!));
    }

    [Theory]
    [InlineData("""[]""")]
    [InlineData("""{}""")]
    [InlineData("""{"reviewers":["a","A"]}""")]
    [InlineData("""{"reviewers":["a"],"on":["often"]}""")]
    [InlineData("""{"reviewers":["a"],"minutes":4}""")]
    public void A_rule_that_does_not_hold_says_its_first_problem_alike_through_the_rules(string text)
    {
        using var document = JsonDocument.Parse(text);

        var (rule, problem) = OpinionKey.Read(document.RootElement);

        Assert.Null(rule);
        Assert.NotNull(problem);
        Assert.Equal(problem, OpinionRules.Read(document.RootElement).Problem);
    }

    [Fact]
    public void A_repository_map_keeps_none_and_a_workspace_map_does_not()
    {
        using var document = JsonDocument.Parse("""{"opinions":{"a":false},"workspaceOpinions":{"a":false}}""");

        Assert.True(OpinionKey.Map(document.RootElement, "opinions", allowNone: true)["a"].IsNone);
        Assert.Empty(OpinionKey.Map(document.RootElement, "workspaceOpinions", allowNone: false));
    }
}
