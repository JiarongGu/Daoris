using Xunit;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>A settled press nobody waits on is said in Ask Daoris's conversation as Daoris's word, and in no other asker's.</summary>
public sealed class SettledSaidTests
{
    private static PipeConfirmation Press(bool help, string? session = "c1") => new(
        "abc", "driver", "WORK_PAUSE", [], new PipeAsker("node", 1, session, help), DateTimeOffset.UnixEpoch,
        View: new PressView("work-pause", new Dictionary<string, string>(), "daoris work pause alpha"));

    private static List<(string Session, string Word, string Key)> Said(PipeConfirmation press, PipeConfirmationEnd end)
    {
        var said = new List<(string, string, string)>();
        SettledSaid.Of(() => (session, word, key) => { said.Add((session, word, key)); return true; })(press, new PipeEnding(end));
        return said;
    }

    [Theory]
    [InlineData(PipeConfirmationEnd.Confirmed, "confirmed `daoris work pause alpha` and it ran")]
    [InlineData(PipeConfirmationEnd.Refused, "refused `daoris work pause alpha`; nothing ran")]
    [InlineData(PipeConfirmationEnd.Changed, "so nothing ran")]
    public void Ask_Daoris_press_is_said_in_its_conversation(PipeConfirmationEnd end, string part)
    {
        var said = Assert.Single(Said(Press(help: true), end));
        Assert.Equal("c1", said.Session);
        Assert.StartsWith("Daoris: ", said.Word);
        Assert.Contains(part, said.Word);
        Assert.Equal("press-settled/abc", said.Key);
    }

    [Fact]
    public void Another_asker_s_press_and_a_taken_back_one_are_not_said()
    {
        Assert.Empty(Said(Press(help: false), PipeConfirmationEnd.Confirmed));
        Assert.Empty(Said(Press(help: true, session: null), PipeConfirmationEnd.Confirmed));
        Assert.Empty(Said(Press(help: true), PipeConfirmationEnd.Withdrawn));
    }
}
