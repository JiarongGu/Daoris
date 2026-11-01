using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTSPLACE1a2 (the Agents-place design §2 point 3 and §4; the land node design §7): the delivery presets are `land` options, never stored. A
/// place's delivery is read from what its work follows, Current's rule or a version's steps, and matched to the preset of its method, with the
/// options that differ said as its own.
/// </summary>
public sealed class SetupPresetsTests
{
    private const string Plugin = "example.github-pull-request";

    private static SetupLand Current(DriverConfig config, string repository = "engine") =>
        SetupPresets.LandOf(Involvement.StepsOf(WorkflowCurrent.Derive(config, repository, "aurora", [])));

    [Fact]
    public void The_five_presets_are_the_land_node_designs_table()
    {
        Assert.Equal(
            [
                ("not-driven", "Not driven", (SetupLand?)null),
                ("merge-after-accept", "Merge after you accept", new SetupLand("merge", "you")),
                ("branch-to-push", "A branch to push", new SetupLand("branch", "you", "none")),
                ("pull-request", "A pull request", new SetupLand("pull-request", "you")),
                ("integrated", "Integrated into the line", new SetupLand("integrate", "you", "none", "owner")),
            ],
            SetupPresets.Table.Select(preset => (preset.Id, preset.Name, preset.Land)));
    }

    /// <summary>DEFAULTCONFIG1a: a place's quick set-up offers the defaults the data marks for it, each with the delivery the data says.</summary>
    [Fact]
    public void The_presets_after_not_driven_are_the_defaults_the_set_up_offers()
    {
        Assert.Equal(
            WorkflowDefaults.All.Where(each => each.SetUp).Select(each => (each.Id, each.DeliveryWords.Name, (SetupLand?)each.Delivery)),
            SetupPresets.Table.Skip(1).Select(preset => (preset.Id, preset.Name, preset.Land)));
        Assert.All([SetupPresets.Merge, SetupPresets.Branch, SetupPresets.PullRequest, SetupPresets.Integrated],
            id => Assert.True(WorkflowDefaults.Of(id)?.SetUp, id));
    }

    [Fact]
    public void Current_s_land_is_read_from_its_rule_as_the_land_node_design_derives_it()
    {
        var config = DriverConfig.Empty;
        Assert.Equal(new SetupLand("merge", "you"), Current(config));
        Assert.Equal(new SetupLand("branch", "you", "none"), Current(config.WithLanding("engine", new LandingRule(LandingForm.Branch, "f/{quest}"))));
        Assert.Equal(new SetupLand("branch", "automatic", "none"),
            Current(config.WithLanding("engine", new LandingRule(LandingForm.Branch, "f/{quest}", AutoAccept: true))));
        Assert.Equal(new SetupLand("pull-request", "you", Plugin), Current(config.WithLanding("engine", new LandingRule(LandingForm.Branch, "f/{quest}", Plugin: Plugin))));
        Assert.Equal(new SetupLand("integrate", "you", "none", "owner"),
            Current(config.WithLanding("engine", new LandingRule(LandingForm.Branch, "f/{quest}") { Integrate = new LandingIntegrate() })));
    }

    [Fact]
    public void A_place_matches_the_preset_of_its_method_and_says_what_differs_as_its_own()
    {
        Assert.Equal(new SetupMatch("not-driven", []), SetupPresets.Match(driven: false, new SetupLand("merge", "you")));
        Assert.Equal(new SetupMatch("merge-after-accept", []), SetupPresets.Match(driven: true, new SetupLand("merge", "you")));
        Assert.Equal(new SetupMatch("pull-request", []), SetupPresets.Match(driven: true, new SetupLand("pull-request", "you", Plugin)));
        var own = SetupPresets.Match(driven: true, new SetupLand("integrate", "automatic", "none", "done"));
        Assert.Equal("integrated", own.Preset);
        Assert.Equal(["accept `automatic`", "request `done`"], own.Own);
        Assert.Equal(new SetupMatch(null, ["method `push`"]), SetupPresets.Match(driven: true, new SetupLand("push", "you")));
    }

    [Fact]
    public void A_preset_s_rule_keeps_what_the_preset_does_not_say()
    {
        var standing = new LandingRule(LandingForm.Branch, "work/{ticket}", Tidy: true, Key: "[A-Z]+-\\d+") { Cycle = "{\"nodes\":[]}" };

        var pullRequest = SetupPresets.RuleOf(new SetupLand("pull-request", "you", Plugin), standing, policy: null);
        var merged = SetupPresets.RuleOf(new SetupLand("merge", "you"), standing, policy: null);
        var branch = SetupPresets.RuleOf(new SetupLand("branch", "you", "none"), LandingRule.Merge, policy: null);
        var integrated = SetupPresets.RuleOf(new SetupLand("integrate", "you", "none", "owner"), standing, new LandingIntegrate(Batch: 4));
        var asked = SetupPresets.RuleOf(new SetupLand("integrate", "you", "none", "owner"), standing, new LandingIntegrate(Request: "steward", Gates: "declared"));

        Assert.Equal(standing with { Plugin = Plugin }, pullRequest);
        Assert.Equal(LandingRule.Merge with { Tidy = true, Cycle = standing.Cycle }, merged);
        Assert.Equal(new LandingRule(LandingForm.Branch, SetupPresets.Pattern), branch);
        // The line's policy is kept, and an absent request is already the owner's, so nothing is written for it.
        Assert.Equal(standing with { Integrate = new LandingIntegrate(Batch: 4) }, integrated);
        Assert.Equal(standing with { Integrate = new LandingIntegrate(Request: "owner", Gates: "declared") }, asked);
        Assert.All(new[] { pullRequest, merged, branch, integrated, asked }, rule => Assert.Null(LandingKey.Problem(rule)));
    }

    [Theory]
    [InlineData("github.com", "github")]
    [InlineData("github.example.com", "github")]
    [InlineData("gitlab.com", "gitlab")]
    [InlineData("ssh.dev.azure.com", "azure")]
    [InlineData("example.visualstudio.com", "azure")]
    [InlineData("bitbucket.org:22", "bitbucket")]
    [InlineData("git.example.com", null)]
    [InlineData(null, null)]
    public void A_remote_s_platform_is_read_from_its_host(string? host, string? platform) => Assert.Equal(platform, SetupPresets.PlatformOf(host));

    [Fact]
    public void A_pull_request_is_opened_by_the_rule_s_own_plugin_else_the_one_named_for_the_remote_s_platform()
    {
        var catalog = new Catalog(new Entry(Plugin, [HookPoints.Land]), new Entry("example.notes", ["quest/consider"]), new Entry("other.lands", [HookPoints.Land]));

        Assert.Equal(Plugin, SetupPresets.PluginFor("github.com", named: null, catalog));
        Assert.Equal("other.lands", SetupPresets.PluginFor("github.com", named: "other.lands", catalog));
        Assert.Null(SetupPresets.PluginFor("gitlab.com", named: null, catalog));
        Assert.Null(SetupPresets.PluginFor(null, named: null, catalog));
        Assert.Null(SetupPresets.PluginFor("github.com", named: null, new Catalog(new Entry(Plugin, [HookPoints.Land]) { Enabled = false })));
    }

    [Fact]
    public void The_delivery_preselected_keeps_a_match_else_reads_the_declaration_then_the_remote()
    {
        var merge = new SetupMatch("merge-after-accept", []);
        Assert.Equal("merge-after-accept", SetupPresets.Suggest(merge, chosen: true, declaresIntegration: true, plugin: Plugin));
        Assert.Equal("integrated", SetupPresets.Suggest(merge, chosen: false, declaresIntegration: true, plugin: Plugin));
        Assert.Equal("pull-request", SetupPresets.Suggest(new SetupMatch("not-driven", []), chosen: false, declaresIntegration: false, plugin: Plugin));
        Assert.Equal("branch-to-push", SetupPresets.Suggest(new SetupMatch("branch-to-push", ["accept `automatic`"]), chosen: true, declaresIntegration: true, plugin: Plugin));
        Assert.Equal("merge-after-accept", SetupPresets.Suggest(new SetupMatch(null, ["method `push`"]), chosen: true, declaresIntegration: false, plugin: null));
    }

    /// <summary>SETUPUX1a: the suggestion says why by a code the page words, with the driver's sentence beside it.</summary>
    [Fact]
    public void The_suggestion_says_why_by_a_code_with_the_drivers_sentence()
    {
        var merge = new SetupMatch("merge-after-accept", []);
        (string Preset, string Code) Said(SetupMatch match, bool chosen, bool declares, string? plugin)
        {
            var (preset, why) = SetupPresets.Suggestion(match, chosen, declares, plugin);
            return (preset, why.Code);
        }

        Assert.Equal(("merge-after-accept", SuggestionWhy.Kept), Said(merge, chosen: true, declares: true, Plugin));
        Assert.Equal(("integrated", SuggestionWhy.Integration), Said(merge, chosen: false, declares: true, Plugin));
        Assert.Equal(("pull-request", SuggestionWhy.Plugin), Said(merge, chosen: false, declares: false, Plugin));
        Assert.Equal(("merge-after-accept", SuggestionWhy.Default), Said(merge, chosen: false, declares: false, plugin: null));
        Assert.All(new[] { SuggestionWhy.Kept, SuggestionWhy.Integration, SuggestionWhy.Plugin, SuggestionWhy.Default },
            code => Assert.False(string.IsNullOrWhiteSpace(SuggestionWhy.Of(code).Said), code));
    }

    internal sealed record Entry(string Id, IReadOnlyList<string> Points) : IWorkplacePluginEntry
    {
        public bool Enabled { get; init; } = true;

        public string? Problem { get; init; }
    }

    internal sealed class Catalog(params Entry[] entries) : IWorkplacePluginCatalog
    {
        public IWorkplacePluginEntry? Find(string plugin) => entries.FirstOrDefault(entry => string.Equals(entry.Id, plugin, StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<IWorkplacePluginEntry> Contributing => [.. entries.Where(entry => entry.Enabled && entry.Problem is null)];
    }
}
