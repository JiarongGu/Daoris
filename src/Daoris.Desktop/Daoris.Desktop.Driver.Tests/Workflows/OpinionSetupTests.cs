using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A workflow given a second opinion can run it: the copy takes the shipped opinion role, naming none, and a choice of it where the repository declares no
/// reviewers says the one press that declares them, rather than a run later drawing the node as unable to start.
/// </summary>
public sealed class OpinionSetupTests
{
    private const string Declared = """{"opinions":{"web-app":{"reviewers":["codex-acp"],"on":["landing"]}}}""";

    private static NodeGraph Copy()
    {
        var json = WorkflowPresets.WithGraphGates(WorkflowDefaults.All.First(each => WorkflowPresets.GraphOf(each.Id) is not null).Graph(), opinion: true, look: false);
        return NodeGraphs.Read(json.ToJsonString(), onSave: true).Graph!;
    }

    [Fact]
    public void The_copy_with_an_opinion_takes_the_shipped_opinion_role_naming_none()
    {
        var node = Copy().Nodes.Single(each => each.Kind == NodeKinds.Opinion);

        Assert.Equal(StepRoles.Opinion, StepRoles.Of(node));
        Assert.Null(node.Role);
        Assert.False(node.Options.ContainsKey(NodeOptionFields.Role));
    }

    [Fact]
    public void A_choice_where_no_reviewers_are_declared_says_the_press_that_declares_them()
    {
        var reach = IntegrateChoice.Of(DriverConfig.Parse("{}"), "made", 1, Copy(), "web-app", "work", null);

        var said = Assert.Single(reach.Cannots);
        Assert.Contains("daoris place opinion web-app --agents", said);
    }

    [Fact]
    public void A_choice_where_reviewers_are_declared_has_nothing_to_say()
    {
        Assert.Empty(IntegrateChoice.Of(DriverConfig.Parse(Declared), "made", 1, Copy(), "web-app", "work", null).Cannots);
    }
}
