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
    /// QUEUEEDIT1a: a held word is removed by its id, and the record, which showed it waiting, is told it was withdrawn; the
    /// rest keep their order, and the word is never taken.
    /// </summary>
    [Fact]
    public void A_held_word_is_removed_by_its_id_and_the_record_is_told_it_was_withdrawn()
    {
        var inbox = new DrivenInbox(_ => { });
        var withdrawn = new List<string?>();
        inbox.OnWithdrawn(message => withdrawn.Add(message.Id));
        inbox.Attach(interrupt: null);
        inbox.Hold(Said("one"));
        inbox.Hold(Said("two"));
        var first = inbox.State.Queued[0];

        Assert.Equal(QueueEdit.Done, inbox.Edit(first.Id!, text: null));

        Assert.Equal([first.Id], withdrawn);
        Assert.Equal(["two"], inbox.State.Queued.Select(message => message.Text));
        Assert.Equal("two", inbox.TakeOrClose()?.Text);
    }

    /// <summary>QUEUEEDIT1a: an edit replaces the words where they are held, under a new id, which the record is told with the old one withdrawn.</summary>
    [Fact]
    public void A_held_word_is_replaced_in_place_under_a_new_id_and_the_record_is_told_both()
    {
        var inbox = new DrivenInbox(_ => { });
        var told = new List<(string? Id, string Text, DrivenReach Reach)>();
        var withdrawn = new List<string?>();
        inbox.OnSaid((message, reach) => told.Add((message.Id, message.Text, reach)));
        inbox.OnWithdrawn(message => withdrawn.Add(message.Id));
        inbox.Attach(interrupt: null);
        inbox.Hold(new ChatMessage("one", [new KeptFile("a.txt", "kept/a.txt")]));
        inbox.Hold(Said("two"));
        var first = inbox.State.Queued[0];

        Assert.Equal(QueueEdit.Done, inbox.Edit(first.Id!, "one, said better"));

        var now = inbox.State.Queued;
        Assert.Equal(["one, said better", "two"], now.Select(message => message.Text));
        Assert.Equal(["a.txt"], now[0].Files.Select(file => file.Name));
        Assert.NotEqual(first.Id, now[0].Id);
        Assert.Equal([first.Id], withdrawn);
        Assert.Equal((now[0].Id, "one, said better", DrivenReach.TurnEnd), told[^1]);
    }

    /// <summary>QUEUEEDIT1a: a word already sent, or taken, is the session's: answered taken, never an error, and untouched.</summary>
    [Fact]
    public void A_word_the_session_took_is_answered_taken_and_left_alone()
    {
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(interrupt: null);
        inbox.Hold(Said("one"));
        var id = inbox.State.Queued[0].Id!;
        Assert.Equal("one", inbox.TakeOrClose()?.Text);

        Assert.Equal(QueueEdit.Taken, inbox.Edit(id, text: null));
        Assert.Equal(QueueEdit.Taken, inbox.Edit("said-nothing", "words"));
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

    /// <summary>
    /// MSG1d (D137 §5.3): when a word said now reaches the session, which the modules answer the page with: not known until
    /// the door is (a word said while it opens waits for its first prompt either way), at its next step where the door sends
    /// words during a turn, at the turn's end where it does not, and nothing once it has closed.
    /// </summary>
    [Fact]
    public void Its_reach_is_its_doors_once_known_and_none_once_closed()
    {
        var opening = new DrivenInbox(_ => { });
        var holding = new DrivenInbox(_ => { });
        var sending = new DrivenInbox(_ => { });

        holding.Attach(interrupt: null);
        sending.Attach(() => Task.CompletedTask, _ => Task.FromResult("end_turn"));

        Assert.Null(opening.Reach);
        Assert.Equal(DrivenReach.TurnEnd, holding.Reach);
        Assert.Equal(DrivenReach.NextStep, sending.Reach);
        holding.TakeOrClose();
        Assert.Null(holding.Reach);
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

    /// <summary>
    /// 🔴 STEER2a (D136's 2026-10-16 note): *Send now* on a door whose agent steers hands the words to the running turn at once,
    /// told with the reach <c>now</c>; nothing is held, nothing is stopped, and the inbox closes only once the steer is answered.
    /// </summary>
    [Fact]
    public async Task Sending_now_on_a_door_that_steers_hands_the_words_to_the_running_turn_at_once()
    {
        var stopped = 0;
        var steered = new List<string>();
        var told = new List<(string? Id, DrivenReach Reach)>();
        var answer = new TaskCompletionSource<string>();
        var inbox = new DrivenInbox(_ => { });
        inbox.OnSaid((message, reach) => told.Add((message.Id, reach)));
        inbox.Attach(
            () => { stopped++; return Task.CompletedTask; },
            _ => new TaskCompletionSource<string>().Task,
            message => { steered.Add(message.Text); return answer.Task; });
        inbox.Flow();

        var now = await inbox.HoldNowAsync(Said("put MANGO after the words"));

        Assert.Equal(new SentNow("now", true, null), now);
        Assert.Equal(["put MANGO after the words"], steered);
        Assert.Equal([("said-1", DrivenReach.Steered)], told);
        Assert.Empty(inbox.State.Queued);
        Assert.Equal(0, stopped);

        var next = inbox.NextOrClose();
        Assert.Same(answer.Task, next?.Waits);
        answer.SetResult("injected");
        Assert.Null(inbox.NextOrClose());
    }

    /// <summary>
    /// 🔴 STEER2a (a second maker's review): a turn is asked to stop once, however many presses; a turn the run takes after it is
    /// a turn of its own.
    /// </summary>
    [Fact]
    public async Task A_turn_is_asked_to_stop_once_however_often_the_person_presses()
    {
        var stopped = 0;
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(() => { stopped++; return Task.CompletedTask; });

        await inbox.HoldNowAsync(Said("one"));
        await inbox.HoldNowAsync(Said("two"));
        await inbox.SendNowAsync();
        Assert.Equal(1, stopped);

        Assert.Equal("one", inbox.NextOrClose()?.Held?.Text);
        await inbox.SendNowAsync();
        Assert.Equal(2, stopped);
    }

    /// <summary>
    /// 🔴 STEER2a: the run takes no held word while the stop decided for the turn before is still on its way; it waits for it,
    /// so a slow stop can never land on the kept words' own turn.
    /// </summary>
    [Fact]
    public async Task The_run_takes_no_held_word_until_the_stop_for_the_turn_before_is_on_the_wire()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var landed = 0;
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(async () => { await gate.Task; landed++; });

        var pressing = inbox.HoldNowAsync(Said("start with the tests"));
        var waiting = inbox.NextOrClose();

        Assert.Null(waiting?.Held);
        Assert.False(waiting?.Waits?.IsCompleted ?? true);
        gate.SetResult();
        await pressing.WaitAsync(TimeSpan.FromSeconds(10));
        await waiting!.Waits!.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, landed);
        Assert.Equal("start with the tests", inbox.NextOrClose()?.Held?.Text);
    }

    /// <summary>
    /// 🔴 STEER2a (the owner's case): on a door that steers, words pressed while an earlier word is on its way are steered past
    /// it, the person's choice to go first; the earlier word stays on its way, and nothing is stopped. Where the agent cannot
    /// steer they go at the next step behind it, and the answer says <c>behind</c>.
    /// </summary>
    [Fact]
    public async Task Sending_now_past_a_word_on_its_way_steers_where_the_agent_steers_and_goes_behind_where_it_cannot()
    {
        var sent = new List<string>();
        var steered = new List<string>();
        var stopped = 0;
        var steering = new DrivenInbox(_ => { });
        steering.Attach(
            () => { stopped++; return Task.CompletedTask; },
            message => { sent.Add(message.Text); return new TaskCompletionSource<string>().Task; },
            message => { steered.Add(message.Text); return Task.FromResult("injected"); });
        steering.Flow();
        steering.Hold(Said("put PINEAPPLE after the words"));

        Assert.Equal(new SentNow("now", true, null), await steering.HoldNowAsync(Said("stop: the tests are in /spec")));
        Assert.Equal(["put PINEAPPLE after the words"], sent);
        Assert.Equal(["stop: the tests are in /spec"], steered);

        var stepping = new DrivenInbox(_ => { });
        stepping.Attach(
            () => { stopped++; return Task.CompletedTask; },
            message => { sent.Add(message.Text); return new TaskCompletionSource<string>().Task; });
        stepping.Flow();
        stepping.Hold(Said("one more thing"));

        Assert.Equal(new SentNow("next-step", false, SendNowWhy.Behind), await stepping.HoldNowAsync(Said("and MANGO")));
        Assert.Equal(["put PINEAPPLE after the words", "one more thing", "and MANGO"], sent);
        Assert.Equal(0, stopped);
    }

    /// <summary>
    /// STEER2a: before the session's first prompt is on the wire no turn runs to steer: the words are held for right after it,
    /// as any word said then is, and the answer says why the press was not carried out.
    /// </summary>
    [Fact]
    public async Task Sending_now_before_the_first_prompt_left_holds_the_words_and_says_why()
    {
        var steered = 0;
        var inbox = new DrivenInbox(_ => { });

        Assert.Equal(new SentNow(null, false, SendNowWhy.Opening), await inbox.HoldNowAsync(Said("said while it opened")));

        inbox.Attach(() => Task.CompletedTask, _ => new TaskCompletionSource<string>().Task, _ => { steered++; return Task.FromResult("injected"); });
        Assert.Equal(new SentNow("next-step", false, SendNowWhy.Opening), await inbox.HoldNowAsync(Said("said before it left")));

        Assert.Equal(0, steered);
        Assert.Equal(["said while it opened", "said before it left"], inbox.State.Queued.Select(m => m.Text));
    }

    /// <summary>
    /// STEER2a (D90): on a door that takes words only at the turn's end, *Send now* holds the words as before and stops the turn
    /// so they go next; nothing is withdrawn.
    /// </summary>
    [Fact]
    public async Task Sending_now_on_a_turn_end_door_holds_the_words_and_stops_the_turn_so_they_go_next()
    {
        var stopped = 0;
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(() => { stopped++; return Task.CompletedTask; });

        var now = await inbox.HoldNowAsync(Said("stop: the tests are in /spec"));

        Assert.Equal(new SentNow("turn-end", true, null), now);
        Assert.Equal(1, stopped);
        Assert.Equal("stop: the tests are in /spec", inbox.TakeOrClose()?.Text);
    }

    /// <summary>
    /// STEER2a: a door that can neither steer nor stop its turn keeping the session (the native door's run) holds the words as
    /// before, and says so by its code; a next-step door whose agent never said it steers sends them at its next step, as said.
    /// </summary>
    [Fact]
    public async Task Sending_now_where_the_door_has_no_way_holds_or_sends_the_words_as_before_and_says_why()
    {
        var holding = new DrivenInbox(_ => { });
        holding.Attach(interrupt: null);
        var sent = new List<string>();
        var stopped = 0;
        var sending = new DrivenInbox(_ => { });
        sending.Attach(
            () => { stopped++; return Task.CompletedTask; },
            message => { sent.Add(message.Text); return new TaskCompletionSource<string>().Task; });
        sending.Flow();

        Assert.Equal(new SentNow("turn-end", false, SendNowWhy.NoInterrupt), await holding.HoldNowAsync(Said("one")));
        Assert.Equal(new SentNow("next-step", false, SendNowWhy.NoInterrupt), await sending.HoldNowAsync(Said("two")));

        Assert.Equal(["one"], holding.State.Queued.Select(m => m.Text));
        Assert.Equal(["two"], sent);
        Assert.Equal(0, stopped);
        sending.Close();
        Assert.Null(await sending.HoldNowAsync(Said("too late")));
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
