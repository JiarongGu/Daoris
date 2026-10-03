using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// SESS3: what a person tells a driven session while it works is held, and handed over where the protocol door
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

    /// <summary>
    /// STEER1 (D136): the record shows what the person said at once, with when it reaches the session — and a word said
    /// before the door is known is told when it is, in the order said. Each word gets an id that pairs it with the same
    /// words where the session takes them.
    /// </summary>
    [Fact]
    public void Each_word_is_told_at_once_with_when_it_reaches_the_session_and_one_said_early_when_the_door_is_known()
    {
        var told = new List<(string Text, string? Id, DrivenReach Reach)>();
        var inbox = new DrivenInbox(_ => { });
        inbox.OnSaid((message, reach) => told.Add((message.Text, message.Id, reach)));

        Assert.True(inbox.Hold(Said("said while it opened")));
        Assert.Empty(told);

        inbox.Attach(() => Task.CompletedTask);
        Assert.True(inbox.Hold(Said("said while it works")));

        Assert.Equal(
            [("said while it opened", "said-1", DrivenReach.TurnEnd), ("said while it works", "said-2", DrivenReach.TurnEnd)],
            told);
        // The turn-end door still holds them for the turn's end.
        Assert.Equal(["said while it opened", "said while it works"], inbox.State.Queued.Select(m => m.Text));
        Assert.Equal("said-1", inbox.TakeOrClose()?.Id);
    }

    /// <summary>
    /// 🔴 STEER1 (D136): where the agent takes words during a turn, each goes to it at once — held words too, the moment the
    /// session's first prompt is on the wire, and never before it — and nothing waits in the inbox. The run's next step is
    /// each word's answer, in the order sent, and only when none is on its way does the inbox close.
    /// </summary>
    [Fact]
    public async Task On_a_door_that_takes_words_mid_turn_each_word_goes_at_once_and_the_inbox_closes_only_after_every_answer()
    {
        var sent = new List<string>();
        var answers = new Dictionary<string, TaskCompletionSource<string>>();
        var told = new List<DrivenReach>();
        var inbox = new DrivenInbox(_ => { });
        inbox.OnSaid((_, reach) => told.Add(reach));

        Assert.True(inbox.Hold(Said("held before it opened")));
        inbox.Attach(() => Task.CompletedTask, message =>
        {
            sent.Add(message.Text);
            var answer = new TaskCompletionSource<string>();
            answers[message.Text] = answer;
            return answer.Task;
        });
        // Told at once, and held until the target is on the wire.
        Assert.Equal([DrivenReach.NextStep], told);
        Assert.Empty(sent);

        inbox.Flow();
        Assert.Equal(["held before it opened"], sent);

        Assert.True(inbox.Hold(Said("and the budget is 64 KiB")));
        Assert.Equal(["held before it opened", "and the budget is 64 KiB"], sent);
        Assert.Equal([DrivenReach.NextStep, DrivenReach.NextStep], told);
        Assert.Empty(inbox.State.Queued);
        Assert.True(inbox.State.Taking);

        var first = inbox.NextOrClose();
        Assert.Null(first?.Held);
        answers["held before it opened"].SetResult("end_turn");
        Assert.Equal("end_turn", await first!.Answer!);

        var second = inbox.NextOrClose();
        answers["and the budget is 64 KiB"].SetResult("end_turn");
        Assert.Equal("end_turn", await second!.Answer!);

        Assert.Null(inbox.NextOrClose());
        Assert.False(inbox.Hold(Said("too late")));
        Assert.Equal(2, sent.Count);
    }

    /// <summary>
    /// 🔴 STEER1 (D136): on a door that takes words at once nothing is ever held, and a stop while a word is on its way
    /// would answer it cancelled while the agent may still act on it (steer evidence §3), so sending now stops nothing.
    /// </summary>
    [Fact]
    public async Task Sending_now_on_a_door_that_takes_words_mid_turn_stops_nothing()
    {
        var stopped = 0;
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(() => { stopped++; return Task.CompletedTask; }, _ => new TaskCompletionSource<string>().Task);
        inbox.Hold(Said("said before its first prompt left"));
        Assert.Equal(TurnStop.Nothing, await inbox.SendNowAsync());

        inbox.Flow();
        inbox.Hold(Said("the tests are in /spec"));

        Assert.Equal(TurnStop.Nothing, await inbox.SendNowAsync());
        Assert.Equal(0, stopped);
    }

    /// <summary>A word already sent is the session's, so closing hands back only what never left.</summary>
    [Fact]
    public void Closing_a_door_that_takes_words_mid_turn_hands_back_nothing_it_sent()
    {
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(() => Task.CompletedTask, _ => Task.FromException<string>(new DriverException("the agent went away")));
        inbox.Flow();
        inbox.Hold(Said("sent"));

        Assert.Empty(inbox.Close());
        Assert.Null(inbox.NextOrClose());
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
