using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>WORKFLOWEDIT1a: the palette is built from the code's own tables, so a kind, a wire or an option added there appears here with no second list.</summary>
public sealed class WorkflowPaletteTests
{
    private static readonly WorkflowPalette Bare = WorkflowPalette.Build([]);

    [Fact]
    public void Every_built_in_kind_is_present_with_its_authority_and_its_option_fields()
    {
        Assert.Equal(NodeKinds.BuiltIn, Bare.Kinds.Select(each => each.Kind));
        foreach (var kind in Bare.Kinds)
        {
            var authority = NodeAuthorities.BuiltIn[kind.Kind];
            Assert.Equal((authority.Reach, authority.ReachBy, authority.FailureBy, authority.PersonJudges, authority.PersonSkips, authority.MayEnd),
                (kind.Reach, kind.ReachBy, kind.FailureBy, kind.PersonJudges, kind.PersonSkips, kind.MayEnd));
            Assert.Equal(NodeOptionFields.Of(kind.Kind), kind.Options);
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
        // DECIDE1b: a review's changes are its rejection.
        Assert.Equal([NodeKinds.Human, NodeKinds.Look, NodeKinds.GoAhead, NodeKinds.Review], rejected);
        Assert.Null(Bare.Wires.Single(each => each.Kind == WireKinds.After).From);
        Assert.Equal(NodeKinds.WaitUnit, Bare.Wires.Single(each => each.Kind == WireKinds.Wait).To);
        // WORKFLOWEDIT1b: no built-in kind is a decision, and a wait's source is another unit's, never a node of this graph.
        Assert.Empty(Bare.Wires.Single(each => each.Kind == WireKinds.When).From!);
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
        Assert.All(Bare.Kinds, each => Assert.DoesNotContain(WireKinds.When, each.Leaves));
        Assert.All(Bare.Kinds, each => Assert.DoesNotContain(WireKinds.Wait, each.Leaves));

        var find = new NodeDeclaration("find", "local-release/find", 1, NodeReach.Performed)
        {
            Outcomes = ["release", "none"], Outputs = new Dictionary<string, string> { ["release"] = "local-release/release@1" }, Evidence = "local-release/receipt@1",
        };
        var step = Assert.Single(Assert.Single(WorkflowPalette.Build([("local-release", [find])]).Plugins).Steps);
        Assert.Equal((1, "local-release/receipt@1"), (step.Version, step.Evidence));
        Assert.Equal(["release", "none"], step.Outcomes);
        Assert.Contains(WireKinds.When, step.Leaves);
        Assert.Contains(WireKinds.Failed, step.Leaves);
        Assert.DoesNotContain(WireKinds.Rejected, step.Leaves);
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
