using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The landing rule as <c>driver.json</c> keeps it (FOUNDATION1 mod-iii): the settings' own shape, judgements and reading, which the
/// landing's rules answer through, so a rule is judged and read alike wherever it is named.
/// </summary>
public sealed class LandingKeyTests
{
    [Fact]
    public void The_landing_answers_through_the_key_for_its_names_and_limits()
    {
        Assert.Equal(LandingKey.Placeholders, LandingRules.Placeholders);
        Assert.Equal(LandingKey.DefaultKey, LandingRules.DefaultKey);
        Assert.Equal(LandingKey.KeyLimit, LandingRules.KeyLimit);
        Assert.Equal(LandingKey.SharedEscapes, LandingRules.SharedEscapes);
        Assert.Equal(LandingForm.Merge, LandingRule.Merge.Form);
    }

    [Theory]
    [InlineData("merge", null, null, false)]
    [InlineData("branch", "feature/{quest}-{slug}", null, false)]
    [InlineData("branch", "feature/{ticket}", "[A-Z]+-\\d+", false)]
    [InlineData("merge", null, "x", true)]
    [InlineData("branch", "feature/nothing", null, true)]
    [InlineData("branch", "feature/{quest}", "(?<=a)b", true)]
    [InlineData("rebase", null, null, true)]
    public void A_rule_is_judged_alike_through_the_landing(string form, string? pattern, string? key, bool refused)
    {
        var rule = new LandingRule(form, pattern, Key: key);

        Assert.Equal(refused, LandingKey.Problem(rule) is not null);
        Assert.Equal(LandingKey.Problem(rule), LandingRules.Problem(rule));
    }

    [Fact]
    public void A_pattern_expands_and_is_tried_alike_through_the_landing()
    {
        var names = new LandingNames("0fda18", "sample-work", "engine", "s1a2b3c4");

        Assert.Equal("feature/0fda18-sample-work", LandingKey.Expand("feature/{quest}-{slug}", names));
        Assert.Equal(LandingKey.Expand("x/{ticket}", names), LandingRules.Expand("x/{ticket}", names));
        Assert.Equal(LandingKey.Problem("{slug}"), LandingRules.Problem("{slug}"));
        Assert.Equal(LandingKey.KeyProblem("\\1"), LandingRules.KeyProblem("\\1"));
    }

    [Fact]
    public void A_map_reads_the_first_rule_of_a_name_that_lands_and_writes_each_as_kept()
    {
        const string text = """{"landings":{"a":{"form":"branch","pattern":"feature/{quest}","plugin":"example.pr","tidy":true},"A":{"form":"merge"},"b":{"form":"merge","plugin":"x"},"c":{"form":"merge","pattern":"ignored"}}}""";
        using var document = JsonDocument.Parse(text);

        var map = LandingKey.RuleMap(document.RootElement, "landings");

        Assert.Equal(["a", "c"], map.Keys.Order());
        Assert.True(map["a"].Tidy);
        Assert.Null(map["c"].Pattern);

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            LandingKey.WriteRules(writer, "landings", map);
            writer.WriteEndObject();
        }

        Assert.Equal("""{"landings":{"a":{"form":"branch","pattern":"feature/{quest}","plugin":"example.pr","tidy":true},"c":{"form":"merge"}}}""",
            System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
    }

    [Fact]
    public void A_key_a_rule_refuses_is_dropped_and_the_rest_of_the_rule_stands()
    {
        using var document = JsonDocument.Parse("""{"form":"branch","pattern":"feature/{ticket}","key":"(?<=a)b"}""");

        var rule = LandingKey.RuleOf(document.RootElement, out var unread);

        Assert.Null(unread);
        Assert.Null(rule!.Key);
        Assert.Equal("feature/{ticket}", rule.Pattern);
    }

    [Fact]
    public void A_cycle_is_kept_as_written_in_a_rule_and_a_value_that_is_no_rule_says_why()
    {
        using var document = JsonDocument.Parse("""{"form":"merge","cycle":{ "nodes": [] }}""");

        Assert.Equal("""{"nodes":[]}""", LandingKey.RuleOf(document.RootElement, out _)!.Cycle);
        using var number = JsonDocument.Parse("7");
        Assert.Null(LandingKey.RuleOf(number.RootElement, out var unread));
        Assert.NotNull(unread);
        using var nothing = JsonDocument.Parse("""{"pattern":"x"}""");
        Assert.Null(LandingKey.RuleOf(nothing.RootElement, out var named));
        Assert.Contains("form", named);
    }
}
