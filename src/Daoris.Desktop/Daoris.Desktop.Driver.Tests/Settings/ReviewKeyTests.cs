using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The review rule as <c>driver.json</c> keeps it (FOUNDATION1 mod-iii): the settings' own reading and writing, which the review rules
/// answer through, so the key is read and kept alike wherever a rule is named.
/// </summary>
public sealed class ReviewKeyTests
{
    [Fact]
    public void The_rules_answer_through_the_key_for_their_names()
    {
        Assert.Equal(ReviewKey.Kinds, ReviewRules.Kinds);
        Assert.Equal(ReviewKey.Production, ReviewRules.Production);
        Assert.Equal(ReviewKey.NameProblem("prod"), ReviewRules.NameProblem("prod"));
        Assert.True(ReviewKey.ReadsAsProduction("live-site"));
        Assert.Equal(ReviewKey.ReadsAsProduction("dev"), ReviewRules.ReadsAsProduction("dev"));
    }

    [Fact]
    public void A_rule_is_read_and_kept_as_written_and_the_rules_read_it_alike()
    {
        const string text = """{"required":true,"environments":[{"name":"local","kind":"local","procedure":"docs/run.md","address":"http://localhost:4200","run":"npm start"},{"name":"dev","kind":"deployed","procedure":"docs/dev.md"}]}""";
        using var document = JsonDocument.Parse(text);

        var (rule, problem) = ReviewKey.Read(document.RootElement);

        Assert.Null(problem);
        Assert.True(rule!.Required);
        Assert.Equal(["local", "dev"], rule.Environments.Select(each => each.Name));
        Assert.Equal(text, ReviewRules.ToJson(rule));
        Assert.Equal(text, ReviewRules.ToJson(ReviewRules.Read(document.RootElement).Rule!));
    }

    [Fact]
    public void False_is_none_and_is_kept_as_false()
    {
        using var document = JsonDocument.Parse("false");

        var (rule, problem) = ReviewKey.Read(document.RootElement);

        Assert.Null(problem);
        Assert.True(rule!.IsNone);
        Assert.Equal("false", ReviewRules.ToJson(rule));
    }

    [Theory]
    [InlineData("""[]""")]
    [InlineData("""{"environments":[]}""")]
    [InlineData("""{"environments":[{"name":"prod","kind":"deployed","procedure":"a.md"}]}""")]
    [InlineData("""{"environments":[{"name":"local","kind":"local","procedure":"a.md"}]}""")]
    [InlineData("""{"environments":[{"name":"dev","kind":"deployed","procedure":"../a.md"}]}""")]
    public void A_rule_that_does_not_hold_says_its_first_problem_alike_through_the_rules(string text)
    {
        using var document = JsonDocument.Parse(text);

        var (rule, problem) = ReviewKey.Read(document.RootElement);

        Assert.Null(rule);
        Assert.NotNull(problem);
        Assert.Equal(problem, ReviewRules.Read(document.RootElement).Problem);
    }

    [Fact]
    public void A_repository_map_keeps_none_and_a_workspace_map_does_not()
    {
        using var document = JsonDocument.Parse("""{"reviews":{"a":false},"workspaceReviews":{"a":false}}""");

        Assert.True(ReviewKey.Map(document.RootElement, "reviews", allowNone: true)["a"].IsNone);
        Assert.Empty(ReviewKey.Map(document.RootElement, "workspaceReviews", allowNone: false));
    }
}
