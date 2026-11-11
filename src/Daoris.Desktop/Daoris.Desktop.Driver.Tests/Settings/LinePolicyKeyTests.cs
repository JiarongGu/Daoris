using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A line's policy as <c>driver.json</c> keeps it: the settings' own shape, judgements and reading of how a line is integrated, and the
/// branch patterns a landing names its branch by.
/// </summary>
public sealed class LinePolicyKeyTests
{
    private static readonly LandingIntegrate Declared = new(Gates: LandingIntegrate.Declared);

    public static TheoryData<string, LandingIntegrate, bool, string?> Policies() => new()
    {
        { "every absent key", new LandingIntegrate(), false, null },
        { "who requests is the node's", new LandingIntegrate(Request: LandingIntegrate.Done), false, "a line's policy says how the line is integrated" },
        { "where a conflict goes is the node's", new LandingIntegrate(Conflicts: LandingIntegrate.Session), false, "a line's policy says how the line is integrated" },
        { "the integrate's own squash", new LandingIntegrate(Commit: LandingIntegrate.Squash), false, null },
        { "an unconfirmed failure a member reaches landed where no whole-set verification runs", Declared with { Reached = LandingIntegrate.Land }, false, "no whole-set verification" },
        { "an unconfirmed failure no member reaches landed where none runs", Declared with { Unreached = LandingIntegrate.Land }, false, "no whole-set verification" },
        { "an unconfirmed failure landed where a whole-set verification runs", Declared with { Reached = LandingIntegrate.Land }, true, null },
        { "an unconfirmed failure held", Declared with { Reached = LandingIntegrate.Hold, Unreached = LandingIntegrate.Split }, false, null },
        { "a way of landing a member neither build knows", new LandingIntegrate(Commit: "octopus"), false, "`integrate.commit` is `merge`, `rebase` or `squash`, and `octopus` is none." },
        { "an order neither build knows", new LandingIntegrate(Order: "random"), false, "`integrate.order` is" },
        { "gates neither build knows", new LandingIntegrate(Gates: "reached"), false, "`integrate.gates` is" },
        { "an unconfirmed failure's way neither build knows", new LandingIntegrate(Reached: "retry"), false, "`integrate.unconfirmed.reached` is" },
        { "no member in a candidate", new LandingIntegrate(Batch: 0), false, "`integrate.batch`" },
        { "more members than a candidate holds", new LandingIntegrate(Batch: 9), false, "`integrate.batch`" },
        { "the most members a candidate holds", Declared with { Batch = 8 }, false, null },
        { "a line git would refuse", new LandingIntegrate(Line: "bad..line"), false, "`integrate.line`" },
        { "a line by name", new LandingIntegrate(Line: "trunk"), false, null },
    };

    [Theory]
    [MemberData(nameof(Policies))]
    public void A_policy_is_refused_as_the_design_names(string name, LandingIntegrate policy, bool wholeSet, string? says)
    {
        var problem = LinePolicyKey.PolicyProblem(policy, wholeSet);

        if (says is null) Assert.True(problem is null, $"{name}: {problem}");
        else Assert.True(problem?.Contains(says, StringComparison.Ordinal) == true, $"{name}: {problem ?? "(none)"}");
    }

    [Fact]
    public void An_integrate_absent_reads_the_design_s_absent_column_and_land_splits_where_no_whole_set_verification_runs()
    {
        Assert.Equal(
            new LandingIntegrate(LandingIntegrate.Owner, LandingIntegrate.Merge, 1, LandingIntegrate.Arrival, LandingIntegrate.NoGates,
                LandingIntegrate.Split, LandingIntegrate.Split, LandingIntegrate.Session, LandingIntegrate.Warn, null),
            new LandingIntegrate().Resolved());
        Assert.Equal(8, Declared.Resolved().Batch);
        Assert.Equal(3, (Declared with { Batch = 3 }).Resolved().Batch);

        var landing = Declared with { Reached = LandingIntegrate.Land, Unreached = LandingIntegrate.Hold };
        Assert.Equal(LandingIntegrate.Land, landing.Unconfirmed(reached: true, wholeSet: true));
        Assert.Equal(LandingIntegrate.Split, landing.Unconfirmed(reached: true, wholeSet: false));
        Assert.Equal(LandingIntegrate.Hold, landing.Unconfirmed(reached: false, wholeSet: false));
        Assert.Equal(LandingIntegrate.Split, Declared.Unconfirmed(reached: false, wholeSet: true));
    }

    [Fact]
    public void A_policy_is_read_and_written_as_kept_only_its_keys_set_in_the_design_s_order()
    {
        const string text = """{"linePolicies":{"web":{"overlap":"wait","unconfirmed":{"reached":"hold"},"gates":"declared","later":1}}}""";
        using var document = JsonDocument.Parse(text);

        var map = LinePolicyKey.PolicyMap(document.RootElement, "linePolicies");

        Assert.Equal(new LandingIntegrate(Gates: LandingIntegrate.Declared, Reached: LandingIntegrate.Hold), map["web"]);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            LinePolicyKey.WritePolicies(writer, "linePolicies", map);
            writer.WriteEndObject();
        }

        Assert.Equal("""{"linePolicies":{"web":{"gates":"declared","unconfirmed":{"reached":"hold"}}}}""",
            System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
    }

    [Theory]
    [InlineData("true", "`integrate` is an object")]
    [InlineData("""{"batch":"8"}""", "`integrate.batch`")]
    [InlineData("""{"unconfirmed":"split"}""", "`integrate.unconfirmed` is an object")]
    [InlineData("""{"commit":5}""", "`integrate.commit` is")]
    public void A_policy_that_does_not_read_says_why(string text, string says)
    {
        using var document = JsonDocument.Parse(text);

        Assert.Null(LinePolicyKey.IntegrateOf(document.RootElement, out var problem));
        Assert.Contains(says, problem);
    }

    [Fact]
    public void A_pattern_expands_by_its_placeholders_and_a_bad_one_is_refused()
    {
        var names = new LandingNames("0fda18", "sample-work", "engine", "s1a2b3c4");

        Assert.Equal("feature/0fda18-sample-work", BranchPatterns.Expand("feature/{quest}-{slug}", names));
        Assert.Null(BranchPatterns.Problem("feature/{quest}-{slug}"));
        Assert.NotNull(BranchPatterns.Problem("{slug}"));
        Assert.NotNull(BranchPatterns.KeyProblem("(?<=a)b"));
    }
}
