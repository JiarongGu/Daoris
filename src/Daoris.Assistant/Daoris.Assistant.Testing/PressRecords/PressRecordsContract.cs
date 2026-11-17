using Daoris.Assistant.Ports;
using Xunit;

namespace Daoris.Assistant.Testing;

/// <summary>
/// The cases every <see cref="IPressRecords"/> passes: <see cref="MemoryPressRecords"/> in the assistant's suite, the modules' press
/// record in theirs.
/// </summary>
public abstract class PressRecordsContract
{
    /// <summary>The presses under test, none made.</summary>
    protected abstract IPressRecords Backend { get; }

    /// <summary>Makes <paramref name="press"/> in the window: the fake is told, the real side records it as a press would.</summary>
    protected abstract void Press(PressMade press);

    [Fact]
    public void A_conversations_presses_are_listed_in_the_order_made_with_what_each_named_and_no_others()
    {
        var quest = new PressMade("press-1", "conversation-1", "quest send", "done")
        {
            Place = new Dictionary<string, string> { ["repository"] = "game" },
            Receipt = "sent the quest to game",
        };
        var other = new PressMade("press-2", "conversation-2", "session stop", "running");
        var asked = new PressMade("press-3", "conversation-1", "branch land", "asked") { Card = "land" };

        Press(quest);
        Press(other);
        Press(asked);

        var made = Backend.Of("conversation-1");
        Assert.Equal(["press-1", "press-3"], made.Select(each => each.Id));
        Assert.Equal(["quest send", "branch land"], made.Select(each => each.Route));
        Assert.Equal(["done", "asked"], made.Select(each => each.Outcome));
        Assert.Equal("game", made[0].Place["repository"]);
        Assert.Equal(("sent the quest to game", (string?)null), (made[0].Receipt, made[0].Card));
        Assert.Equal("land", made[1].Card);
    }

    [Fact]
    public void A_conversation_that_pressed_nothing_has_none()
    {
        Press(new PressMade("press-1", "conversation-1", "quest send", "done"));

        Assert.Empty(Backend.Of("conversation-2"));
    }
}
