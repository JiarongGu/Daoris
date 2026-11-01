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
