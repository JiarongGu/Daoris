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

    private static readonly LandingRule Branch = new(LandingForm.Branch, "quest/{slug}-{quest}");

    private static readonly LandingIntegrate Declared = new(Gates: LandingIntegrate.Declared);

    /// <summary>
    /// INTEGRATE1d (the integration design §4.1, §11 row d): the refusals of a rule carrying <c>integrate</c>, each by the words its
    /// sentence says, and the rules that integrate. <c>wholeSet</c> is whether a whole-set verification runs on the line.
    /// </summary>
    public static TheoryData<string, LandingRule, bool, string?> IntegrateRules() => new()
    {
        { "a branch rule integrating by every absent key", Branch with { Integrate = new LandingIntegrate() }, false, null },
        { "beside a plugin: a pull request is its platform's integration", Branch with { Plugin = "example.pr", Integrate = new LandingIntegrate() }, false, "beside a plugin" },
        { "on a merge rule: an older build merges it ungated", new LandingRule(LandingForm.Merge) { Integrate = new LandingIntegrate() }, false, "never a merge rule" },
        { "beside the rule's own squash", Branch with { Squash = true, Integrate = new LandingIntegrate() }, false, "`squash` is refused beside `integrate`" },
        { "the integrate's own squash", Branch with { Integrate = new LandingIntegrate(Commit: LandingIntegrate.Squash) }, false, null },
        { "requested at done without the rule's automatic acceptance", Branch with { Integrate = Declared with { Request = LandingIntegrate.Done } }, false, "`autoAccept`" },
        { "requested at done with no gates", Branch with { AutoAccept = true, Integrate = new LandingIntegrate(Request: LandingIntegrate.Done) }, false, "`request: done` needs `gates: declared`" },
        { "requested at done, accepted automatically, with declared gates", Branch with { AutoAccept = true, Integrate = Declared with { Request = LandingIntegrate.Done } }, false, null },
        { "requested by the steward with no gates", Branch with { Integrate = new LandingIntegrate(Request: LandingIntegrate.Steward, Gates: LandingIntegrate.NoGates) }, false, "`request: steward` needs `gates: declared`" },
        { "requested by the steward with declared gates", Branch with { Integrate = Declared with { Request = LandingIntegrate.Steward } }, false, null },
        { "an unconfirmed failure a member reaches landed where no whole-set verification runs", Branch with { Integrate = Declared with { Reached = LandingIntegrate.Land } }, false, "no whole-set verification" },
        { "an unconfirmed failure no member reaches landed where none runs", Branch with { Integrate = Declared with { Unreached = LandingIntegrate.Land } }, false, "no whole-set verification" },
        { "an unconfirmed failure landed where a whole-set verification runs", Branch with { Integrate = Declared with { Reached = LandingIntegrate.Land } }, true, null },
        { "an unconfirmed failure held", Branch with { Integrate = Declared with { Reached = LandingIntegrate.Hold, Unreached = LandingIntegrate.Split } }, false, null },
        { "a way of landing a member neither build knows", Branch with { Integrate = new LandingIntegrate(Commit: "octopus") }, false, "`integrate.commit` is `merge`, `rebase` or `squash`, and `octopus` is none." },
        { "a requester neither build knows", Branch with { Integrate = new LandingIntegrate(Request: "anyone") }, false, "`integrate.request` is" },
        { "an order neither build knows", Branch with { Integrate = new LandingIntegrate(Order: "random") }, false, "`integrate.order` is" },
        { "gates neither build knows", Branch with { Integrate = new LandingIntegrate(Gates: "reached") }, false, "`integrate.gates` is" },
        { "an unconfirmed failure's way neither build knows", Branch with { Integrate = new LandingIntegrate(Reached: "retry") }, false, "`integrate.unconfirmed.reached` is" },
        { "a conflict's home neither build knows", Branch with { Integrate = new LandingIntegrate(Conflicts: "agent") }, false, "`integrate.conflicts` is" },
        { "an overlap's way neither build knows", Branch with { Integrate = new LandingIntegrate(Overlap: "block") }, false, "`integrate.overlap` is" },
        { "no member in a candidate", Branch with { Integrate = new LandingIntegrate(Batch: 0) }, false, "`integrate.batch`" },
        { "more members than a candidate holds", Branch with { Integrate = new LandingIntegrate(Batch: 9) }, false, "`integrate.batch`" },
        { "the most members a candidate holds", Branch with { Integrate = Declared with { Batch = 8 } }, false, null },
        { "a line git would refuse", Branch with { Integrate = new LandingIntegrate(Line: "bad..line") }, false, "`integrate.line`" },
        { "a line by name", Branch with { Integrate = new LandingIntegrate(Line: "trunk") }, false, null },
    };

    [Theory]
    [MemberData(nameof(IntegrateRules))]
    public void A_rule_carrying_integrate_is_refused_as_the_design_names(string name, LandingRule rule, bool wholeSet, string? says)
    {
        var problem = LandingKey.Problem(rule, wholeSet);

        if (says is null) Assert.True(problem is null, $"{name}: {problem}");
        else Assert.True(problem?.Contains(says, StringComparison.Ordinal) == true, $"{name}: {problem ?? "(none)"}");
        // The landing's own judgement reads no whole-set verification: `land` is refused there until the line's is known.
        if (!wholeSet) Assert.Equal(problem, LandingRules.Problem(rule));
    }

    [Fact]
    public void An_integrate_absent_reads_the_design_s_absent_column_and_land_splits_where_no_whole_set_verification_runs()
    {
        Assert.Equal(
            new LandingIntegrate(LandingIntegrate.Owner, LandingIntegrate.Merge, 1, LandingIntegrate.Arrival, LandingIntegrate.NoGates,
                LandingIntegrate.Split, LandingIntegrate.Split, LandingIntegrate.Session, LandingIntegrate.Warn, null),
            new LandingIntegrate().Resolved());
        // Eight members where gates run, one where none.
        Assert.Equal(8, Declared.Resolved().Batch);
        Assert.Equal(3, (Declared with { Batch = 3 }).Resolved().Batch);

        var landing = Declared with { Reached = LandingIntegrate.Land, Unreached = LandingIntegrate.Hold };
        Assert.Equal(LandingIntegrate.Land, landing.Unconfirmed(reached: true, wholeSet: true));
        Assert.Equal(LandingIntegrate.Split, landing.Unconfirmed(reached: true, wholeSet: false));
        Assert.Equal(LandingIntegrate.Hold, landing.Unconfirmed(reached: false, wholeSet: false));
        Assert.Equal(LandingIntegrate.Split, Declared.Unconfirmed(reached: false, wholeSet: true));
    }

    [Fact]
    public void A_rule_s_integrate_is_read_and_written_as_kept_only_its_keys_set_in_the_design_s_order_before_the_cycle()
    {
        const string text = """{"landings":{"web":{"form":"branch","pattern":"quest/{quest}","autoAccept":true,"cycle":{"nodes":[]},"integrate":{"overlap":"wait","unconfirmed":{"reached":"hold"},"gates":"declared","request":"done","later":1}}}}""";
        using var document = JsonDocument.Parse(text);

        var map = LandingKey.RuleMap(document.RootElement, "landings");

        Assert.Equal(new LandingIntegrate(Request: LandingIntegrate.Done, Gates: LandingIntegrate.Declared, Reached: LandingIntegrate.Hold, Overlap: LandingIntegrate.Wait), map["web"].Integrate);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            LandingKey.WriteRules(writer, "landings", map);
            writer.WriteEndObject();
        }

        Assert.Equal(
            """{"landings":{"web":{"form":"branch","pattern":"quest/{quest}","autoAccept":true,"integrate":{"request":"done","gates":"declared","unconfirmed":{"reached":"hold"},"overlap":"wait"},"cycle":{"nodes":[]}}}}""",
            System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
    }

    /// <summary>
    /// INTEGRATE1d's proof of an older build (the design §11 row d): a build that knows no <c>integrate</c> reads a rule carrying it as
    /// the branch rule it is, the key passed over as every unknown key in a rule is (the keeps table's <c>reviewer</c> and
    /// <c>later</c>), so it makes the landing branch and merges nothing. This build reads the same rule, and only <c>integrate</c> besides.
    /// </summary>
    [Fact]
    public void An_older_build_reads_a_rule_carrying_integrate_as_the_branch_rule_it_is()
    {
        const string carrying = """{"form":"branch","pattern":"quest/{slug}-{quest}","tidy":true,"autoAccept":true,"integrate":{"request":"done","gates":"declared","batch":4}}""";
        const string older = """{"form":"branch","pattern":"quest/{slug}-{quest}","tidy":true,"autoAccept":true,"reviewer":"kim"}""";
        using var withIntegrate = JsonDocument.Parse(carrying);
        using var asOlder = JsonDocument.Parse(older);

        var read = LandingKey.RuleOf(withIntegrate.RootElement, out var unread);
        var olderRead = LandingKey.RuleOf(asOlder.RootElement, out _);

        Assert.Null(unread);
        Assert.Equal(LandingForm.Branch, olderRead!.Form);
        Assert.Null(LandingKey.Problem(olderRead));
        Assert.Equal(olderRead, read! with { Integrate = null });
        Assert.Equal(new LandingIntegrate(Request: LandingIntegrate.Done, Batch: 4, Gates: LandingIntegrate.Declared), read.Integrate);
    }

    [Theory]
    [InlineData("""{"form":"branch","pattern":"quest/{quest}","plugin":"example.pr","integrate":{}}""", "beside a plugin")]
    [InlineData("""{"form":"merge","integrate":{"request":"owner"}}""", "never a merge rule")]
    [InlineData("""{"form":"branch","pattern":"quest/{quest}","squash":true,"integrate":{}}""", "`squash`")]
    [InlineData("""{"form":"branch","pattern":"quest/{quest}","integrate":{"request":"done","gates":"declared"}}""", "`autoAccept`")]
    [InlineData("""{"form":"branch","pattern":"quest/{quest}","integrate":true}""", "`integrate` is an object")]
    [InlineData("""{"form":"branch","pattern":"quest/{quest}","integrate":{"batch":"8"}}""", "`integrate.batch`")]
    [InlineData("""{"form":"branch","pattern":"quest/{quest}","integrate":{"unconfirmed":"split"}}""", "`integrate.unconfirmed` is an object")]
    [InlineData("""{"form":"branch","pattern":"quest/{quest}","integrate":{"commit":5}}""", "`integrate.commit` is")]
    public void An_integrate_that_does_not_read_or_is_refused_is_passed_over_and_the_rest_of_the_rule_stands(string text, string says)
    {
        using var document = JsonDocument.Parse(text);

        var rule = LandingKey.RuleOf(document.RootElement, out var unread);

        Assert.Null(unread);
        Assert.NotNull(rule);
        Assert.Null(rule.Integrate);
        Assert.Contains(says, LandingKey.IntegrateRefused(document.RootElement));
    }

    [Fact]
    public void An_integrate_that_lands_unconfirmed_failures_is_kept_as_written_where_it_is_read_and_one_that_reads_says_nothing()
    {
        using var document = JsonDocument.Parse("""{"form":"branch","pattern":"quest/{quest}","integrate":{"gates":"declared","unconfirmed":{"unreached":"land"}}}""");

        Assert.Equal(LandingIntegrate.Land, LandingKey.RuleOf(document.RootElement, out _)!.Integrate!.Unreached);
        Assert.Null(LandingKey.IntegrateRefused(document.RootElement));
        using var none = JsonDocument.Parse("""{"form":"branch","pattern":"quest/{quest}","integrate":null}""");
        Assert.Null(LandingKey.RuleOf(none.RootElement, out _)!.Integrate);
        Assert.Null(LandingKey.IntegrateRefused(none.RootElement));
    }

    /// <summary>A door with no field for <c>integrate</c> (Setup's row, Ask Daoris, the terminal) keeps the rule's, as it keeps a cycle; and judges it.</summary>
    [Fact]
    public void A_door_with_no_field_for_integrate_keeps_the_rule_s_and_one_its_new_form_cannot_carry_refuses_the_edit()
    {
        var config = DriverConfig.Empty.WithLanding("engine", Branch with { AutoAccept = true, Integrate = Declared with { Request = LandingIntegrate.Done } });

        var edited = config.WithLanding("engine", new LandingRule(LandingForm.Branch, "work/{quest}", AutoAccept: true));

        Assert.Equal(Declared with { Request = LandingIntegrate.Done }, edited.Landings["engine"].Integrate);
        Assert.Equal("work/{quest}", edited.Landings["engine"].Pattern);
        Assert.Contains("never a merge rule", Assert.Throws<DriverException>(() => config.WithLanding("engine", LandingRule.Merge)).Message);
        Assert.Contains("`autoAccept`", Assert.Throws<DriverException>(() => config.WithLanding("engine", Branch)).Message);
        // Cleared by its own setter, which leaves the rest of the rule as it is.
        var cleared = config.WithLandingIntegrate("engine", null);
        Assert.Null(cleared.Landings["engine"].Integrate);
        Assert.True(cleared.Landings["engine"].AutoAccept);
        Assert.Contains("no landing rule", Assert.Throws<DriverException>(() => config.WithLandingIntegrate("shop-api", Declared)).Message);
    }

    /// <summary>
    /// <c>land</c> is set only where a whole-set verification runs on the line (the design §4.5): here, the level's own landed route
    /// naming a verification pipeline whose gates the person accepted (D192 point 7).
    /// </summary>
    [Fact]
    public void Landing_an_unconfirmed_failure_is_set_only_where_the_level_s_own_landed_route_verifies_the_line()
    {
        var landing = Declared with { Reached = LandingIntegrate.Land };
        var withRule = DriverConfig.Empty.WithLanding("engine", Branch).WithWorkspaceLanding("shop", Branch);
        var route = new WorkflowChoice(null, []) { Landed = new WorkflowLanded("verify-line", "0123456789abcdef0123456789abcdef01234567") };

        Assert.Contains("no whole-set verification", Assert.Throws<DriverException>(() => withRule.WithLandingIntegrate("engine", landing)).Message);
        var unaccepted = withRule with { Workflows = new Dictionary<string, WorkflowChoice>(StringComparer.OrdinalIgnoreCase) { ["engine"] = route with { Landed = route.Landed! with { Gates = null } } } };
        Assert.Throws<DriverException>(() => unaccepted.WithLandingIntegrate("engine", landing));
        var current = withRule with { Workflows = new Dictionary<string, WorkflowChoice>(StringComparer.OrdinalIgnoreCase) { ["engine"] = route with { Landed = new WorkflowLanded(WorkflowKey.Current, "none") } } };
        Assert.Throws<DriverException>(() => current.WithLandingIntegrate("engine", landing));
        var verified = withRule with
        {
            Workflows = new Dictionary<string, WorkflowChoice>(StringComparer.OrdinalIgnoreCase) { ["Engine"] = route },
            WorkspaceWorkflows = new Dictionary<string, WorkflowChoice>(StringComparer.OrdinalIgnoreCase) { ["shop"] = route },
        };
        Assert.Equal(LandingIntegrate.Land, verified.WithLandingIntegrate("engine", landing).Landings["engine"].Integrate!.Reached);
        Assert.Equal(LandingIntegrate.Land, verified.WithWorkspaceLandingIntegrate("shop", landing).WorkspaceLandings["shop"].Integrate!.Reached);
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
