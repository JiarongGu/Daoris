using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The brainstorm policy as <c>driver.json</c> keeps it (FOUNDATION1 mod-iii): the settings' own reading and writing, which the
/// brainstorm rules answer through, so the key is read and kept alike wherever a rule is named.
/// </summary>
public sealed class BrainstormKeyTests
{
    [Fact]
    public void The_rules_answer_through_the_key_for_their_names_and_limits()
    {
        Assert.Equal(BrainstormKey.Ask, BrainstormRules.Ask);
        Assert.Equal(BrainstormKey.Modes, BrainstormRules.Modes);
        Assert.Equal(BrainstormKey.MostAgents, BrainstormRules.MostAgents);
        Assert.Same(BrainstormKey.NotMinutes, BrainstormRules.NotMinutes);
        Assert.Equal(BrainstormKey.Ask, BrainstormRule.Default.Mode);
    }

    [Fact]
    public void A_rule_is_read_and_kept_whole_and_the_rules_read_it_alike()
    {
        const string text = """{"mode":"off","agents":["codex-acp","dsh"],"minutes":{"pass":5,"synthesis":6,"challenge":7,"request":90}}""";
        using var document = JsonDocument.Parse(text);

        var (rule, problem) = BrainstormKey.Read(document.RootElement);

        Assert.Null(problem);
        Assert.Equal("off", rule!.Mode);
        Assert.Equal(["codex-acp", "dsh"], rule.Agents);
        Assert.Equal(new BrainstormMinutes(5, 6, 7, 90), rule.Minutes);
        var through = BrainstormRules.Read(document.RootElement).Rule!;
        Assert.Equal(rule.Mode, through.Mode);
        Assert.Equal(rule.Agents, through.Agents);
        Assert.Equal(rule.Minutes, through.Minutes);
        Assert.Equal(text, BrainstormRules.ToJson(rule));
    }

    [Theory]
    [InlineData("""[]""")]
    [InlineData("""{"mode":"always"}""")]
    [InlineData("""{"agents":["a","A"]}""")]
    [InlineData("""{"agents":["a","b","c","d","e"]}""")]
    [InlineData("""{"minutes":{"pass":0}}""")]
    [InlineData("""{"minutes":{"synthesis":30,"challenge":30,"request":60}}""")]
    public void A_rule_that_does_not_hold_says_its_first_problem_alike_through_the_rules(string text)
    {
        using var document = JsonDocument.Parse(text);

        var (rule, problem) = BrainstormKey.Read(document.RootElement);

        Assert.Null(rule);
        Assert.NotNull(problem);
        Assert.Equal(problem, BrainstormRules.Read(document.RootElement).Problem);
    }

    [Fact]
    public void A_map_keeps_the_first_readable_rule_of_a_name_written_twice_and_skips_one_that_does_not_read()
    {
        using var document = JsonDocument.Parse("""{"workspaceBrainstorms":{" a ":{"mode":"ask"},"A":{"mode":"off"},"b":{"mode":"never"},"":{"mode":"ask"}}}""");

        var map = BrainstormKey.Map(document.RootElement, "workspaceBrainstorms");

        Assert.Equal(["a"], map.Keys);
        Assert.Equal("ask", map["a"].Mode);
    }
}
