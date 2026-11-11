using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Ask Daoris's conversation between its record and its agent (<see cref="HelpStarting"/>): the words said to it while
/// it starts wait as its queue, opening, and go to its door in the order said once it opens; what the door does not take, or all
/// of them where it could not start, is handed back for its record to keep. Each conversation starts as its own.
/// The route's own test is <c>DriverModuleHelpStartTests</c>.
/// </summary>
public sealed class HelpStartingTests
{
    [Fact]
    public void Words_wait_as_its_queue_opening_and_go_to_its_door_in_the_order_said()
    {
        var starting = new HelpStarting();
        starting.Placed("h1");

        Assert.True(starting.Hold("h1", "first", [new ChatUpload("plan.md", [1, 2])], "Where the person is now: Sessions."));
        Assert.True(starting.Hold("H1", "second", [], null));
        Assert.False(starting.Hold("h2", "elsewhere", [], null));
        Assert.Null(starting.Queue("h2"));

        var queue = starting.Queue("h1")!;
        Assert.True(queue.Opening);
        Assert.False(queue.Taking);
        Assert.Equal(["first", "second"], queue.Queued.Select(message => message.Text));
        Assert.Equal(["plan.md"], queue.Queued[0].Files.Select(file => file.Name));

        var said = new List<HeldWord>();
        var left = starting.Over("h1", word =>
        {
            said.Add(word);
            return word.Text != "second";
        });

        Assert.Equal(["first", "second"], said.Select(word => word.Text));
        Assert.Equal("Where the person is now: Sessions.", said[0].Preface);
        Assert.Equal(["second"], left.Select(word => word.Text));
        Assert.Null(starting.Queue("h1"));
        Assert.False(starting.Hold("h1", "after", [], null));
    }

    [Fact]
    public void A_start_that_failed_hands_every_word_back_and_one_not_starting_hands_none()
    {
        var starting = new HelpStarting();
        Assert.Empty(starting.Over("h1", null));

        starting.Placed("h1");
        starting.Hold("h1", "are you there?", [], null);

        Assert.Equal(["are you there?"], starting.Over("h1", null).Select(word => word.Text));
        Assert.Empty(starting.Over("h1", null));
    }

    // 🔴 A new conversation's first word never joins another that is starting.
    [Fact]
    public void Each_conversation_starts_as_its_own_and_holds_its_own_words()
    {
        var starting = new HelpStarting();
        starting.Placed("h1");
        starting.Placed("h2");

        Assert.True(starting.Hold("h1", "the first conversation's", [], null));
        Assert.True(starting.Hold("h2", "the new conversation's", [], null));

        Assert.Equal(["the first conversation's"], starting.Over("h1", null).Select(word => word.Text));
        Assert.Equal(["the new conversation's"], starting.Queue("h2")!.Queued.Select(message => message.Text));
    }
}
