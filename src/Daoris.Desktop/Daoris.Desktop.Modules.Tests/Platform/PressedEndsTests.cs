using Xunit;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>An update or a pin Ask Daoris pressed is said in its conversation when it ends, and no other action or asker's is.</summary>
public sealed class PressedEndsTests
{
    private static PipeConfirmation Press(string kind, bool help = true) => new(
        "p1", "driver", "HARNESS_ACTION", [], new PipeAsker("node", 1, "c1", help), DateTimeOffset.UnixEpoch,
        View: new PressView(kind, new Dictionary<string, string> { ["agent"] = "claude-code" }, "daoris agent update claude-code"));

    private static List<(string Session, string Word, string Key)> Run(PipeConfirmation press, PipeConfirmationEnd settled, int exit, string? problem, string action = "update")
    {
        var said = new List<(string, string, string)>();
        Func<string, string, string, bool> tell = (session, word, key) => { said.Add((session, word, key)); return true; };
        var ends = new PressedEnds();
        SettledSaid.Of(() => tell, ends)(press, new PipeEnding(settled));
        said.Clear();
        ends.Ended("claude-code", action, exit, problem, tell);
        return said;
    }

    [Fact]
    public void A_finished_update_is_said_in_the_conversation_that_pressed_it()
    {
        var said = Assert.Single(Run(Press("agent-update-tool"), PipeConfirmationEnd.Confirmed, 0, null));
        Assert.Equal("c1", said.Session);
        Assert.Equal("Daoris: `daoris agent update claude-code` finished.", said.Word);
        Assert.Equal("press-ended/p1", said.Key);
    }

    [Fact]
    public void A_failed_pin_says_why()
    {
        var said = Assert.Single(Run(Press("agent-pin"), PipeConfirmationEnd.Confirmed, 3, null, action: "pin"));
        Assert.Contains("did not finish: it ended with exit code 3", said.Word);
    }

    [Fact]
    public void A_refused_press_another_asker_and_another_action_are_not_said()
    {
        Assert.Empty(Run(Press("agent-update-tool"), PipeConfirmationEnd.Refused, 0, null));
        Assert.Empty(Run(Press("agent-update-tool", help: false), PipeConfirmationEnd.Confirmed, 0, null));
        Assert.Empty(Run(Press("agent-update-tool"), PipeConfirmationEnd.Confirmed, 0, null, action: "login"));
    }
}
