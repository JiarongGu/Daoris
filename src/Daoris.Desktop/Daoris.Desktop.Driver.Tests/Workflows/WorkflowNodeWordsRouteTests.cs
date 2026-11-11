using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris workflow &lt;act&gt; &lt;run&gt; &lt;node&gt;</c> from the typed words: every act the node door takes is a verb of the
/// <c>workflow</c> group, named by its usage, and an Accept typed at the terminal reaches the running Daoris as the page's
/// <c>WORKFLOW_NODE</c> press with the act <c>accept</c>.
/// </summary>
public sealed class WorkflowNodeWordsRouteTests
{
    [Fact]
    public void Every_act_the_node_door_takes_routes_under_workflow_and_its_usage_names_it()
    {
        var usage = DriverCommand.UsageOf("workflow")!;
        foreach (var act in NodeWordActs.All)
        {
            var route = CommandTree.Route(["workflow", act, "q7", "go"]);
            Assert.True(route.Kind == TreeRouteKind.Run, $"`daoris workflow {act}` routes as {route.Kind}: {route.Said}");
            Assert.Equal(["workflow", act, "q7", "go"], route.Words);
            Assert.Matches($@"workflow ([\w-]+\|)*{Regex.Escape(act)}[ |]", usage);
        }
    }

    [Fact]
    public async Task An_accept_typed_at_the_terminal_is_the_pages_node_press_with_act_accept()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Confirming = true;
        string[] lines = ["workflow: your accept of `review` on run q7 is kept; Daoris's next look applies it."];
        standIn.Data = new { said = new TreesSaid(lines, [], 0).ToWire() };
        var output = new StringWriter();

        var route = CommandTree.Route(["workflow", "accept", "#q7", "review", "looks", "right"]);
        Assert.Equal(TreeRouteKind.Run, route.Kind);
        var words = route.Words.Skip(1).ToList();
        Assert.True(WorkflowNodeCommand.Asks(words));
        var asked = WorkflowNodeCommand.Read(words, out var problem);
        Assert.True(asked is not null, problem);

        var exit = await WorkflowNodeCommand.RunAsync(asked, new NodeRecordFiles(home.Home, () => true), output, home.World("workflow"));

        Assert.Equal(0, exit);
        Assert.Equal(lines[0] + Environment.NewLine, output.ToString());
        var press = Assert.Single(standIn.Presses);
        Assert.Equal("WORKFLOW_NODE", press.GetProperty("type").GetString());
        Assert.Equal("""{"run":"q7","node":"review","act":"accept","words":"looks right","said":true}""", press.GetProperty("payload").GetRawText());
    }
}
