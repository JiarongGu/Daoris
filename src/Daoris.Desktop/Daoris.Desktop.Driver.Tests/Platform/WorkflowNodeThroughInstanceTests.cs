using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CUTOVER1f (RPC1e's door, the terminal RPC design §7.2 and §7.3): <c>daoris-driver workflow skip|stop</c> through the running Daoris,
/// the person's press, sent to the instance, which shows it in its window and runs it on their Confirm; the terminal prints the lines the route
/// answers (<c>said</c>, the alone path's own words) and keeps nothing here. Seen failing first.
/// </summary>
public sealed class WorkflowNodeThroughInstanceTests
{
    [Theory]
    [InlineData("skip", "nobody raises it", """{"run":"q7","node":"go","act":"skip","words":"nobody raises it","said":true}""")]
    [InlineData("stop", null, """{"run":"q7","node":"go","act":"stop","said":true}""")]
    public async Task A_word_through_the_instance_waits_for_the_window_and_prints_the_lines_it_answers(string act, string? words, string payload)
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Confirming = true;
        string[] lines = [$"workflow: your {act} of `go` on run q7 is kept; Daoris's next look applies it."];
        standIn.Data = new { said = new TreesSaid(lines, [], 0).ToWire() };
        var output = new StringWriter();

        var exit = await WorkflowNodeCommand.RunAsync(new(act, "q7", "go", words), new NodeRecordFiles(home.Home, () => true), output, home.World("workflow"));

        Assert.Equal(0, exit);
        Assert.Equal(lines[0] + Environment.NewLine, output.ToString());
        Assert.Equal($"workflow: waiting for you to confirm this in Daoris's window…{Environment.NewLine}", home.Error.ToString());
        var press = Assert.Single(standIn.Presses);
        Assert.Equal("WORKFLOW_NODE", press.GetProperty("type").GetString());
        Assert.Equal(payload, press.GetProperty("payload").GetRawText());
        Assert.False(File.Exists(NodeRecordFiles.PathOf(home.Home, RunUnits.IdOf("q7"))));
    }

    /// <summary>The person's refusal in the window is exit 1, and nothing is kept here.</summary>
    [Fact]
    public async Task A_word_the_person_refuses_in_the_window_is_exit_1_and_keeps_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Confirming = true;
        standIn.Press = StandInPress.Refuse;
        standIn.Code = InstanceDoor.Refused;
        var output = new StringWriter();

        var exit = await WorkflowNodeCommand.RunAsync(new("stop", "q7", "go", null), new NodeRecordFiles(home.Home, () => true), output, home.World("workflow"));

        Assert.Equal(1, exit);
        Assert.Equal("", output.ToString());
        Assert.Contains($"({InstanceDoor.Refused})", home.Error.ToString());
        Assert.False(File.Exists(NodeRecordFiles.PathOf(home.Home, RunUnits.IdOf("q7"))));
    }
}
