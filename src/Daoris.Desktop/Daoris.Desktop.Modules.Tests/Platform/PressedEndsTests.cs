using Daoris.Driver;
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

    private static Func<string, string, string, bool> Tell(List<(string Session, string Word, string Key)> said) =>
        (session, word, key) => { said.Add((session, word, key)); return true; };

    [Fact]
    public void An_update_that_ends_before_the_press_is_registered_is_said_as_it_registers()
    {
        var said = new List<(string Session, string Word, string Key)>();
        var ends = new PressedEnds();
        ends.Ended("claude-code", "update", 3, null, Tell(said));
        Assert.Empty(said);

        SettledSaid.Of(() => Tell(said), ends)(Press("agent-update-tool"), new PipeEnding(PipeConfirmationEnd.Confirmed));

        Assert.Contains(said, each => each.Word.Contains("did not finish: it ended with exit code 3") && each.Key == "press-ended/p1");
    }

    [Fact]
    public void An_end_from_before_the_press_was_asked_is_not_said_of_it()
    {
        var said = new List<(string Session, string Word, string Key)>();
        var ends = new PressedEnds(() => DateTimeOffset.UnixEpoch.AddMinutes(-1));
        ends.Ended("claude-code", "update", 0, null, Tell(said));

        SettledSaid.Of(() => Tell(said), ends)(Press("agent-update-tool"), new PipeEnding(PipeConfirmationEnd.Confirmed));

        Assert.DoesNotContain(said, each => each.Key == "press-ended/p1");
    }

    [Fact]
    public void An_update_that_fails_after_its_confirm_is_said_with_the_reason()
    {
        var said = new List<(string Session, string Word, string Key)>();
        var ends = new PressedEnds();
        SettledSaid.Of(() => Tell(said), ends)(Press("agent-update-tool"), new PipeEnding(PipeConfirmationEnd.Confirmed));
        said.Clear();

        ends.Ended("claude-code", "update", -1, "another action is running", Tell(said));

        Assert.Equal("Daoris: `daoris agent update claude-code` did not finish: another action is running.", Assert.Single(said).Word);
    }

    private const string Answer = """{"data":{"done":true,"quest":"q1","ask":"a1"},"success":true}""";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void An_ask_and_a_quest_a_pressed_verb_published_are_kept_with_the_conversation_and_named_in_its_line(bool answeredFirst)
    {
        var said = new List<(string Session, string Word, string Key)>();
        var kept = new List<(string Session, HelpPublishedEntry Entry)>();
        void Keep(string session, HelpPublishedEntry entry) => kept.Add((session, entry));
        var ends = new PressedEnds();
        var settled = SettledSaid.Of(() => Tell(said), ends, Keep);
        var press = Press("agent-update-tool");

        if (answeredFirst) ends.Answered("p1", Answer, Tell(said), Keep);
        settled(press, new PipeEnding(PipeConfirmationEnd.Confirmed));
        if (!answeredFirst) ends.Answered("p1", Answer, Tell(said), Keep);

        var entry = Assert.Single(kept);
        Assert.Equal(("c1", "a1", "q1", "p1"), (entry.Session, entry.Entry.Ask, entry.Entry.Quest, entry.Entry.Proposal));
        Assert.Contains(said, each => each.Word == "Daoris: `daoris agent update claude-code` published ask `#a1` and quest `#q1`." && each.Key == "press-published/p1");
    }

    [Fact]
    public void An_answer_that_names_neither_keeps_nothing()
    {
        var said = new List<(string Session, string Word, string Key)>();
        var kept = new List<HelpPublishedEntry>();
        var ends = new PressedEnds();
        SettledSaid.Of(() => Tell(said), ends, (_, entry) => kept.Add(entry))(Press("agent-update-tool"), new PipeEnding(PipeConfirmationEnd.Confirmed));
        said.Clear();

        ends.Answered("p1", """{"data":{"done":true},"success":true}""", Tell(said), (_, entry) => kept.Add(entry));

        Assert.Empty(kept);
        Assert.Empty(said);
    }

    [Fact]
    public void A_refused_press_another_asker_and_another_action_are_not_said()
    {
        Assert.Empty(Run(Press("agent-update-tool"), PipeConfirmationEnd.Refused, 0, null));
        Assert.Empty(Run(Press("agent-update-tool", help: false), PipeConfirmationEnd.Confirmed, 0, null));
        Assert.Empty(Run(Press("agent-update-tool"), PipeConfirmationEnd.Confirmed, 0, null, action: "login"));
    }
}
