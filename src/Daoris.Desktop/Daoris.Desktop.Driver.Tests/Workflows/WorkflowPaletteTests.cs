using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The palette is built from the code's own tables, so a kind, a wire or an option added there appears here with no second list.</summary>
public sealed class WorkflowPaletteTests
{
    private static readonly WorkflowPalette Bare = WorkflowPalette.Build([]);

    [Fact]
    public void Every_built_in_kind_is_present_with_its_authority_and_its_option_fields()
    {
        Assert.Equal(NodeKit.Kinds.Where(kind => !NodeKit.Built.IsArriving(kind)), Bare.Kinds.Select(each => each.Kind));
        foreach (var kind in Bare.Kinds)
        {
            var authority = NodeKit.Row(kind.Kind)!;
            Assert.Equal((authority.Reach, authority.ReachBy, authority.FailureBy, authority.PersonJudges, authority.PersonSkips, authority.MayEnd),
                (kind.Reach, kind.ReachBy, kind.FailureBy, kind.PersonJudges, kind.PersonSkips, kind.MayEnd));
            Assert.Equal(NodeKit.Of(kind.Kind)!.Options, kind.Options);
        }

        var land = Bare.Kinds.Single(each => each.Kind == NodeKinds.Land).Options;
        Assert.Equal(LandNodes.Methods, land.Single(each => each.Name == "method").Choices);
        Assert.DoesNotContain(land, each => each.Name == "accept");
        var integrate = Bare.Kinds.Single(each => each.Kind == NodeKinds.Integrate).Options;
        Assert.Equal(LandingIntegrate.Requests, integrate.Single(each => each.Name == "request").Choices);
        Assert.Equal(LandingIntegrate.ConflictChoices, integrate.Single(each => each.Name == "conflicts").Choices);
        Assert.Equal(RequestOptions.Untils, Bare.Kinds.Single(each => each.Kind == NodeKinds.Request).Options.Single(each => each.Name == "until").Choices);
    }

    [Fact]
    public void Wire_kinds_are_listed_with_what_may_leave_them()
    {
        Assert.Equal(WireKinds.All, Bare.Wires.Select(each => each.Kind));
        var failed = Bare.Wires.Single(each => each.Kind == WireKinds.Failed).From!;
        Assert.DoesNotContain(NodeKinds.Claimed, failed);
        Assert.Contains(NodeKinds.Check, failed);
        var rejected = Bare.Wires.Single(each => each.Kind == WireKinds.Rejected).From!;
        // A review's changes are its rejection. The kinds stand in the kit's order: the picker's groups, then their marks' order.
        Assert.Equal([NodeKinds.Review, NodeKinds.Look, NodeKinds.GoAhead, NodeKinds.Human], rejected);
        Assert.Null(Bare.Wires.Single(each => each.Kind == WireKinds.After).From);
        Assert.Equal(NodeKinds.WaitUnit, Bare.Wires.Single(each => each.Kind == WireKinds.Wait).To);
        // A person's task and a monitor are the built-in decisions, and a wait's source is another unit's, never a node of this graph.
        Assert.Equal([NodeKinds.Human, NodeKinds.Monitor], Bare.Wires.Single(each => each.Kind == WireKinds.When).From!);
        Assert.Empty(Bare.Wires.Single(each => each.Kind == WireKinds.Wait).From!);
    }

    [Fact]
    public void Each_kind_and_step_says_the_wires_that_may_leave_it_and_the_work_a_role_takes_it_by()
    {
        PaletteKind Kind(string kind) => Bare.Kinds.Single(each => each.Kind == kind);
        Assert.Equal([RoleWork.Design, RoleWork.Build], Kind(NodeKinds.Claimed).RoleTakes);
        Assert.Equal([RoleWork.Review], Kind(NodeKinds.Opinion).RoleTakes);
        Assert.Empty(Kind(NodeKinds.Land).RoleTakes);
        Assert.Contains(WireKinds.Rejected, Kind(NodeKinds.Review).Leaves);
        Assert.Contains(WireKinds.Back, Kind(NodeKinds.Review).Leaves);
        Assert.Contains(WireKinds.Failed, Kind(NodeKinds.Check).Leaves);
        Assert.DoesNotContain(WireKinds.Rejected, Kind(NodeKinds.Claimed).Leaves);
        Assert.All(Bare.Kinds.Where(each => each.Kind is not (NodeKinds.Monitor or NodeKinds.Human)), each => Assert.True(each.Outcomes.Count == 0 && !each.Leaves.Contains(WireKinds.When)));
        Assert.Equal([PullRequestReading.Merged, PullRequestReading.Closed], Kind(NodeKinds.Monitor).Outcomes);
        Assert.Contains(WireKinds.When, Kind(NodeKinds.Monitor).Leaves);
        Assert.All(Bare.Kinds, each => Assert.DoesNotContain(WireKinds.Wait, each.Leaves));

        var find = new NodeDeclaration("find", "local-release/find", 1, NodeReach.Performed)
        {
            Outcomes = ["release", "none"], Outputs = new Dictionary<string, string> { ["release"] = "local-release/release@1" }, Evidence = "local-release/receipt@1",
            OutputsOn = new Dictionary<string, IReadOnlyList<string>> { ["release"] = ["release"] },
        };
        var step = Assert.Single(Assert.Single(WorkflowPalette.Build([("local-release", [find])]).Plugins).Steps);
        Assert.Equal((1, "local-release/receipt@1"), (step.Version, step.Evidence));
        Assert.Equal(["release", "none"], step.Outcomes);
        // The output a step carries on one outcome alone is said, so a graph composed from the palette keeps it there.
        Assert.Equal(["release"], Assert.Single(step.OutputsOn, each => each.Key == "release").Value);
        Assert.Empty(Assert.Single(WorkflowPalette.Build([("local-release", [find with { OutputsOn = new Dictionary<string, IReadOnlyList<string>>() }])]).Plugins).Steps[0].OutputsOn);
        Assert.Contains(WireKinds.When, step.Leaves);
        Assert.Contains(WireKinds.Failed, step.Leaves);
        Assert.DoesNotContain(WireKinds.Rejected, step.Leaves);
    }

    /// <summary>A step's title and about reach the palette as its manifest states them, and a step without them carries none.</summary>
    [Fact]
    public void A_step_s_title_and_about_are_carried_and_a_step_without_carries_none()
    {
        var titled = new NodeDeclaration("find", "local-release/find", 1, NodeReach.Performed)
        {
            Title = AuthoredText.FromTags([new("en", "Find a pending release"), new("zh", "查找待发版本")]),
            About = AuthoredText.Of("Looks for a release."),
        };
        var bare = new NodeDeclaration("release", "local-release/release", 1, NodeReach.Performed);

        var steps = Assert.Single(WorkflowPalette.Build([("local-release", [titled, bare])]).Plugins).Steps;

        Assert.Equal((titled.Title, titled.About), (steps[0].Title, steps[0].About));
        Assert.Equal((null, null), (steps[1].Title, steps[1].About));
        var wire = System.Text.Json.JsonSerializer.SerializeToNode(steps[0], new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase })!;
        Assert.Equal("查找待发版本", (string?)wire["title"]!["zh"]);
        Assert.Equal("Looks for a release.", (string?)wire["about"]);
    }

    [Fact]
    public void A_plugins_declared_steps_are_listed_under_it()
    {
        var step = new NodeDeclaration("deploy", "acme/deploy", 2, NodeReach.Performed) { Outputs = new Dictionary<string, string> { ["preview"] = "acme.deploy/preview@1" } };
        var palette = WorkflowPalette.Build([("acme", [step])]);

        var plugin = Assert.Single(palette.Plugins);
        Assert.Equal("acme", plugin.Plugin);
        var listed = Assert.Single(plugin.Steps);
        Assert.Equal(("acme/deploy", NodeReach.Performed), (listed.Kind, listed.Reach));
        Assert.Equal("acme.deploy/preview@1", listed.Outputs["preview"]);
    }
}
