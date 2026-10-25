using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The commit rule as <c>driver.json</c> keeps it (FOUNDATION1 mod-iii): the settings' own reading and writing, which the commit rules
/// answer through, so the key is read and kept alike wherever a rule is named.
/// </summary>
public sealed class CommitKeyTests
{
    [Fact]
    public void The_rules_answer_through_the_key_for_their_names_and_limit()
    {
        Assert.Equal(CommitKey.AsYouGo, CommitRules.AsYouGo);
        Assert.Equal(CommitKey.Modes, CommitRules.Modes);
        Assert.Equal(CommitKey.ConventionLimit, CommitRules.ConventionLimit);
        Assert.Equal(CommitKey.AsYouGo, CommitRule.Default.Mode);
    }

    [Fact]
    public void A_rule_is_read_trimmed_and_kept_as_written_and_the_rules_read_it_alike()
    {
        using var document = JsonDocument.Parse("""{"mode":"at-end","convention":"  one paragraph  "}""");

        var (rule, problem) = CommitKey.Read(document.RootElement);

        Assert.Null(problem);
        Assert.Equal(new CommitRule("at-end", "one paragraph"), rule);
        Assert.Equal("""{"mode":"at-end","convention":"one paragraph"}""", CommitRules.ToJson(rule!));
        Assert.Equal(rule, CommitRules.Read(document.RootElement).Rule);
    }

    [Theory]
    [InlineData("""[]""")]
    [InlineData("""{"mode":"sometimes"}""")]
    [InlineData("""{"convention":7}""")]
    [InlineData("""{"convention":"two\nlines"}""")]
    public void A_rule_that_does_not_hold_says_its_first_problem_alike_through_the_rules(string text)
    {
        using var document = JsonDocument.Parse(text);

        var (rule, problem) = CommitKey.Read(document.RootElement);

        Assert.Null(rule);
        Assert.NotNull(problem);
        Assert.Equal(problem, CommitRules.Read(document.RootElement).Problem);
    }

    [Fact]
    public void A_map_keeps_the_first_readable_rule_of_a_name_written_twice()
    {
        using var document = JsonDocument.Parse("""{"commits":{" a ":{"mode":"ask"},"A":{"mode":"at-end"},"b":{"mode":"never"}}}""");

        var map = CommitKey.Map(document.RootElement, "commits");

        Assert.Equal(["a"], map.Keys);
        Assert.Equal("ask", map["a"].Mode);
    }
}
