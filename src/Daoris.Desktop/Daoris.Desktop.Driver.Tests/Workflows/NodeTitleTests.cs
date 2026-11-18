using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A step of a generic kind reads by its role and its id, never by its kind's title: on the shipped release preset the four
/// tasks (<c>closed</c>, <c>config-needed</c>, <c>config-change</c>, <c>check-release</c>) read each its own. Seen failing first.
/// </summary>
public sealed class NodeTitleTests
{
    [Fact]
    public void The_release_presets_tasks_each_read_by_their_role_and_id()
    {
        var graph = NodeGraphs.Read(WorkflowDefaults.Of("pull-request-release")!.GraphJson, onSave: true).Graph!;
        string TitleOf(string id) => WorkflowRunWords.Title(WorkflowGraphRuns.Cell(graph.Nodes.Single(node => node.Id == id), null));

        string[] expected = ["owner · closed", "release manager · config-needed", "config changer · config-change", "operations · check-release"];
        Assert.Equal(expected, new[] { "closed", "config-needed", "config-change", "check-release" }.Select(TitleOf));
        Assert.Equal("The push", TitleOf("push"));
    }
}
