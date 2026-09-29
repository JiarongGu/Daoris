using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// SESS3 (the owner, 2026-09-29: "there is no way to send additional info in middle of the session"):
/// what a person tells a driven session while it works is held, and handed over where the protocol door
/// allows — the next prompt of the same session when the turn ends, or at once by stopping the turn.
/// </summary>
public sealed class DrivenInboxTests
{
    private static ChatMessage Said(string text) => new(text, []);

    [Fact]
    public void What_is_held_is_taken_in_the_order_it_was_said_and_the_page_is_told()
    {
        var told = new List<ChatQueue>();
        var inbox = new DrivenInbox(told.Add);

        Assert.True(inbox.Hold(Said("the budget is in level.json")));
        Assert.True(inbox.Hold(Said("and cap it at 64 KiB")));

        Assert.Equal(["the budget is in level.json", "and cap it at 64 KiB"], inbox.State.Queued.Select(m => m.Text));
        Assert.True(inbox.State.Taking);
        Assert.Equal("the budget is in level.json", inbox.TakeOrClose()?.Text);
        Assert.Equal("and cap it at 64 KiB", inbox.TakeOrClose()?.Text);
        Assert.Equal(["the budget is in level.json", "and cap it at 64 KiB"], told.Last(state => state.Queued.Count == 2).Queued.Select(m => m.Text));
    }

    /// <summary>
    /// 🔴 Taking and closing are one step: a word said between "nothing held" and "closed" would otherwise
    /// be kept by an inbox nobody reads again, and the person would be told it was sent.
    /// </summary>
    [Fact]
    public void Nothing_held_closes_it_and_a_word_said_after_is_refused_rather_than_lost()
    {
        var told = new List<ChatQueue>();
        var inbox = new DrivenInbox(told.Add);

        Assert.Null(inbox.TakeOrClose());

        Assert.False(inbox.Hold(Said("too late")));
        Assert.Equal(ChatQueue.Idle, inbox.State);
        Assert.Equal(ChatQueue.Idle, told.Last());
    }

    [Fact]
    public async Task Sending_now_stops_the_turn_so_what_is_held_goes_next_and_nothing_is_withdrawn()
    {
        var stopped = 0;
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(() => { stopped++; return Task.CompletedTask; });
        inbox.Hold(Said("stop: the tests are in /spec, not /test"));

        var stop = await inbox.SendNowAsync();

        Assert.True(stop.Cancelled);
        Assert.Empty(stop.Withdrawn);
        Assert.Equal(1, stopped);
        // Still held: the turn's end is what hands it over.
        Assert.Equal("stop: the tests are in /spec, not /test", inbox.TakeOrClose()?.Text);
    }

    /// <summary>With nothing held, stopping the turn would end the session's work for no word at all: the session's own stop is for that.</summary>
    [Fact]
    public async Task Sending_now_with_nothing_held_stops_nothing()
    {
        var stopped = 0;
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(() => { stopped++; return Task.CompletedTask; });

        Assert.Equal(TurnStop.Nothing, await inbox.SendNowAsync());
        Assert.Equal(0, stopped);
    }

    [Fact]
    public void Closing_it_whatever_is_held_hands_back_what_never_reached_the_session()
    {
        var inbox = new DrivenInbox(_ => { });
        inbox.Hold(Said("one"));
        inbox.Hold(Said("two"));

        Assert.Equal(["one", "two"], inbox.Close().Select(m => m.Text));
        Assert.Empty(inbox.Close());
        Assert.False(inbox.Hold(Said("three")));
    }

    [Fact]
    public void The_registry_hands_a_sessions_inbox_to_the_page_and_forgets_it_when_it_closes()
    {
        var processes = new SessionProcesses();
        var changes = new List<(string Session, ChatQueue Queue)>();
        processes.HeldChanged += (session, queue) => changes.Add((session, queue));

        var inbox = processes.OpenInbox("s1a2b3c4");
        Assert.Same(inbox, processes.InboxOf("s1a2b3c4"));
        Assert.True(inbox.Hold(Said("the level file moved")));
        Assert.Contains(changes, change => change.Session == "s1a2b3c4" && change.Queue.Queued.Count == 1);

        inbox.TakeOrClose();
        inbox.TakeOrClose();

        Assert.Null(processes.InboxOf("s1a2b3c4"));
        Assert.Null(processes.InboxOf("someone-else"));
    }
}
