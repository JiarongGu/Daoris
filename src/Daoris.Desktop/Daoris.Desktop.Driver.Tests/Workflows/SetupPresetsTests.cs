using System.Text.Json.Nodes;
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

    [Fact]
    public void The_five_presets_are_the_land_node_designs_table()
    {
        Assert.Equal(
            [
                ("not-driven", "Not driven", (SetupLand?)null),
                ("merge-after-accept", "Merge after you accept", new SetupLand("merge", "you")),
                ("branch-to-push", "A branch to push", new SetupLand("branch", "you")),
                ("pull-request", "A pull request", new SetupLand("pull-request", "you", "github-pull-request")),
                ("integrated", "Integrated into the line", new SetupLand("integrate", "you", null, "owner")),
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
    public void A_place_matches_the_preset_of_its_method_and_says_what_differs_as_its_own()
    {
        Assert.Equal(new SetupMatch("not-driven", []), SetupPresets.Match(driven: false, new SetupLand("merge", "you")));
        Assert.Equal(new SetupMatch("merge-after-accept", []), SetupPresets.Match(driven: true, new SetupLand("merge", "you")));
        Assert.Equal(new SetupMatch("pull-request", []), SetupPresets.Match(driven: true, new SetupLand("pull-request", "you", Plugin)));
        var own = SetupPresets.Match(driven: true, new SetupLand("integrate", "automatic", null, "done"));
        Assert.Equal("integrated", own.Preset);
        Assert.Equal(["accept `automatic`", "request `done`"], own.Own);
        Assert.Equal(new SetupMatch(null, ["method `push`"]), SetupPresets.Match(driven: true, new SetupLand("push", "you")));
    }

    /// <summary>WORKFLOWCLEAN1 1a: a delivery is what the graph says, and a preset is a saved copy's, never a rule written beside it.</summary>
    [Theory]
    [InlineData("merge-after-accept", "merge", "you", null, null)]
    [InlineData("merge-automatic", "merge", "automatic", null, null)]
    [InlineData("documents", "merge", "automatic", null, null)]
    [InlineData("release", "merge", "you", null, null)]
    [InlineData("branch-to-push", "branch", "you", null, null)]
    [InlineData("pull-request", "pull-request", "you", "github-pull-request", null)]
    [InlineData("integrated", "integrate", "you", null, "owner")]
    [InlineData("integrated-no-press", "integrate", "automatic", null, "done")]
    public void A_shipped_default_s_delivery_is_derived_from_its_graph(string id, string method, string accept, string? plugin, string? request)
    {
        var shipped = WorkflowDefaults.Of(id)!;

        Assert.Equal(new SetupLand(method, accept, plugin, request), shipped.Delivery);
        // A file that restates no delivery is worded by the default that owns its shape.
        Assert.Equal(WorkflowDefaults.All.First(each => each.Delivery == shipped.Delivery).Id, shipped.DeliveryWords.Id);
    }

    [Fact]
    public void A_copy_of_a_default_is_numbered_around_a_person_s_own_workflow_and_stamped_with_where_it_came_from()
    {
        var shipped = WorkflowDefaults.Of("merge-after-accept")!;

        var plain = WorkflowDefaults.Copy(shipped, opinion: false, look: false);
        var gated = WorkflowDefaults.Copy(shipped, opinion: true, look: true, taken: id => id is "merge-after-accept-opinion-look");

        Assert.Equal("merge-after-accept", plain.Id);
        Assert.Equal(shipped.From, plain.From);
        Assert.Equal(shipped.Digest, NodeGraphs.Digest(NodeGraphs.Read(plain.Graph.ToJsonString()).Graph!));
        Assert.Equal("merge-after-accept-opinion-look-2", gated.Id);
        Assert.Contains(NodeKinds.Opinion, gated.Graph["nodes"]!.AsArray().Select(node => (string?)node!["kind"]));
        Assert.Contains(NodeKinds.Look, gated.Graph["nodes"]!.AsArray().Select(node => (string?)node!["kind"]));
    }

    [Fact]
    public void A_land_left_open_resolves_from_derived_defaults_and_never_from_a_rule()
    {
        var land = NodeGraphs.Read(WorkflowDefaults.Of("pull-request")!.GraphJson).Graph!.Nodes.First(node => node.Kind == NodeKinds.Land);
        var written = land with { Options = new Dictionary<string, string>(land.Options) { ["pattern"] = "\"work/{ticket}\"" } };

        var open = LandNodes.Resolve(land);
        var named = LandNodes.Resolve(written);

        Assert.Equal(LandNodes.DefaultPattern, open["pattern"]);
        Assert.False(open.ContainsKey("plugin"));
        Assert.False(open.ContainsKey("key"));
        // A written pattern stands, and one that says {ticket} resolves the default key.
        Assert.False(named.ContainsKey("pattern"));
        Assert.Equal(BranchPatterns.DefaultKey, named["key"]);
        Assert.Empty(LandNodes.Resolve(NodeGraphs.Read(WorkflowDefaults.Of("merge-after-accept")!.GraphJson).Graph!.Nodes.First(node => node.Kind == NodeKinds.Land)));
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
    public void A_pull_request_is_opened_by_the_plugin_named_for_the_remote_s_platform_that_performs_steps()
    {
        var catalog = new Catalog(new Entry(Plugin, [HookPoints.Perform]), new Entry("example.notes", ["quest/consider"]), new Entry("other.lands", [HookPoints.State]));

        Assert.Equal(Plugin, SetupPresets.PluginFor("github.com", catalog));
        Assert.Null(SetupPresets.PluginFor("gitlab.com", catalog));
        Assert.Null(SetupPresets.PluginFor(null, catalog));
        Assert.Null(SetupPresets.PluginFor("github.com", new Catalog(new Entry(Plugin, [HookPoints.Perform]) { Enabled = false })));
    }

    /// <summary>G8, D240 point 5: the delivery is preselected from a match and the declaration alone; a plugin serving the platform suggests nothing.</summary>
    [Fact]
    public void The_delivery_preselected_keeps_a_match_else_reads_the_declaration_and_never_suggests_a_pull_request()
    {
        var merge = new SetupMatch("merge-after-accept", []);
        Assert.Equal("merge-after-accept", SetupPresets.Suggest(merge, chosen: true, declaresIntegration: true));
        Assert.Equal("integrated", SetupPresets.Suggest(merge, chosen: false, declaresIntegration: true));
        Assert.Equal("merge-after-accept", SetupPresets.Suggest(new SetupMatch("not-driven", []), chosen: false, declaresIntegration: false));
        Assert.Equal("branch-to-push", SetupPresets.Suggest(new SetupMatch("branch-to-push", ["accept `automatic`"]), chosen: true, declaresIntegration: true));
        Assert.Equal("merge-after-accept", SetupPresets.Suggest(new SetupMatch(null, ["method `push`"]), chosen: true, declaresIntegration: false));
        Assert.DoesNotContain(SetupPresets.PullRequest, new[] { true, false }.SelectMany(chosen => new[] { true, false }
            .Select(declares => SetupPresets.Suggest(new SetupMatch("not-driven", []), chosen, declares))));
    }

    /// <summary>SETUPUX1a: the suggestion says why by a code the page words, with the driver's sentence beside it.</summary>
    [Fact]
    public void The_suggestion_says_why_by_a_code_with_the_drivers_sentence()
    {
        var merge = new SetupMatch("merge-after-accept", []);
        (string Preset, string Code) Said(SetupMatch match, bool chosen, bool declares)
        {
            var (preset, why) = SetupPresets.Suggestion(match, chosen, declares);
            return (preset, why.Code);
        }

        Assert.Equal(("merge-after-accept", SuggestionWhy.Kept), Said(merge, chosen: true, declares: true));
        Assert.Equal(("integrated", SuggestionWhy.Integration), Said(merge, chosen: false, declares: true));
        Assert.Equal(("merge-after-accept", SuggestionWhy.Default), Said(merge, chosen: false, declares: false));
        Assert.All(new[] { SuggestionWhy.Kept, SuggestionWhy.Integration, SuggestionWhy.Default },
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
