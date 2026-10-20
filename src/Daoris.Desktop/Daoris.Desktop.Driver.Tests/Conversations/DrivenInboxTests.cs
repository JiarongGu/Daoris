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
    /// COWORK1b (the message-urgency design §3.1, §5): Daoris's urgent word on a door that takes words only at its turn's end is
    /// held for it, never refused as a claim is, and told with its reach. It is never the person's queue: the page lists none of
    /// it, and no edit of the person's reaches it. The run still sees it held.
    /// </summary>
    [Fact]
    public void Daoris_word_on_a_turn_end_door_is_held_for_it_and_never_listed_as_the_persons()
    {
        var told = new List<(string? Id, DrivenReach Reach)>();
        var inbox = new DrivenInbox(_ => { });
        inbox.OnSaid((message, reach) => told.Add((message.Id, reach)));
        inbox.Attach(interrupt: null);

        Assert.True(inbox.Hold(new ChatMessage("Daoris: the person reopened it.", []) { Daoris = new DaorisWord("reopen/q1/desk/44") }));

        Assert.Equal([("said-1", DrivenReach.TurnEnd)], told);
        Assert.Empty(inbox.State.Queued);
        Assert.True(inbox.Holds);
        Assert.Equal(QueueEdit.Taken, inbox.Edit("said-1", text: null));
        Assert.Equal("Daoris: the person reopened it.", inbox.NextOrClose()?.Held?.Text);
    }

    /// <summary>COWORK1b: Daoris's word that goes at the next step is told delivered the moment it is sent, once.</summary>
    [Fact]
    public void Daoris_word_sent_at_the_next_step_is_told_delivered_once()
    {
        var inbox = new DrivenInbox(_ => { });
        var sent = new List<string>();
        inbox.Attach(interrupt: null, deliver: message =>
        {
            sent.Add(message.Text);
            return Task.FromResult("end_turn");
        });
        inbox.Flow();
        var delivered = 0;

        Assert.True(inbox.Hold(new ChatMessage("Daoris: reopened.", []) { Daoris = new DaorisWord("reopen/q1/desk/44") { Delivered = () => delivered++ } }));

        Assert.Equal(["Daoris: reopened."], sent);
        Assert.Equal(1, delivered);
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

        inbox.Hold(Said("one"));
        inbox.Hold(Said("two"));
        await inbox.SendNowAsync();
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

        inbox.Hold(Said("start with the tests"));
        var pressing = inbox.SendNowAsync();
        var waiting = inbox.NextOrClose();

        Assert.Null(waiting?.Held);
        Assert.False(waiting?.Waits?.IsCompleted ?? true);
        gate.SetResult();
        await pressing.WaitAsync(Desktop.Driver.Tests.Poll.Patience);
        await waiting!.Waits!.WaitAsync(Desktop.Driver.Tests.Poll.Patience);
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
    /// 🔴 COWORK1a (D188 point 3, the message-urgency design trap 1): on a door that takes words only at the turn's end, *now*
    /// holds the words for that end and stops nothing, said <c>no-interrupt</c>; the turn runs on. The stop that keeps the session
    /// is the person's own press (<see cref="DrivenInbox.SendNowAsync"/>), never a word's.
    /// </summary>
    [Fact]
    public async Task Now_on_a_turn_end_door_holds_the_words_stops_nothing_and_says_so()
    {
        var stopped = 0;
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(() => { stopped++; return Task.CompletedTask; });

        var now = await inbox.HoldNowAsync(Said("stop: the tests are in /spec") with { Level = MessageLevel.Now });

        Assert.Equal(new SentNow("turn-end", false, SendNowWhy.NoInterrupt), now);
        Assert.Equal(0, stopped);
        Assert.Equal(["stop: the tests are in /spec"], inbox.State.Queued.Select(m => m.Text));
        Assert.True(inbox.KeepingStop);

        // The keeping stop, pressed: the turn stops and what waits goes next.
        Assert.True((await inbox.SendNowAsync()).Cancelled);
        Assert.Equal(1, stopped);
        Assert.Equal("stop: the tests are in /spec", inbox.TakeOrClose()?.Text);
    }

    /// <summary>
    /// 🔴 COWORK1a (the design §3.0): an ordinary word on a door that takes words at its next step waits for the turn's end, told
    /// so; an urgent word said after it goes at the next step past it, since its sender chose sooner; and at the turn's end the
    /// ordinary one is the next prompt, after every word sent is answered.
    /// </summary>
    [Fact]
    public async Task An_ordinary_word_waits_for_the_turns_end_and_an_urgent_one_goes_at_the_next_step_past_it()
    {
        var sent = new List<string>();
        var answer = new TaskCompletionSource<string>();
        var told = new List<(string Text, DrivenReach Reach)>();
        var inbox = new DrivenInbox(_ => { });
        inbox.OnSaid((message, reach) => told.Add((message.Text, reach)));
        inbox.Attach(() => Task.CompletedTask, message => { sent.Add(message.Text); return answer.Task; });
        inbox.Flow();

        Assert.True(inbox.Hold(Said("when you are done, tidy the readme") with { Level = MessageLevel.Ordinary }));
        Assert.True(inbox.Hold(Said("the budget is 64 KiB")));

        Assert.Equal(["the budget is 64 KiB"], sent);
        Assert.Equal(["when you are done, tidy the readme"], inbox.State.Queued.Select(m => m.Text));
        Assert.Equal([("when you are done, tidy the readme", DrivenReach.TurnEnd), ("the budget is 64 KiB", DrivenReach.NextStep)], told);

        var first = inbox.NextOrClose();
        Assert.Null(first?.Held);
        answer.SetResult("end_turn");
        Assert.Equal("when you are done, tidy the readme", inbox.NextOrClose()?.Held?.Text);
        Assert.Equal(["the budget is 64 KiB"], sent);
    }

    /// <summary>
    /// 🔴 COWORK1a (the design §14's trap): the first prompt on the wire sends the urgent words held while the session opened, and
    /// keeps the ordinary ones for the turn's end; before, every held word went with it.
    /// </summary>
    [Fact]
    public void The_first_prompt_on_the_wire_sends_the_urgent_words_held_and_keeps_the_ordinary_ones()
    {
        var sent = new List<string>();
        var inbox = new DrivenInbox(_ => { });
        inbox.Hold(Said("tidy the readme after") with { Level = MessageLevel.Ordinary });
        inbox.Hold(Said("the tests are in /spec"));
        inbox.Attach(() => Task.CompletedTask, message => { sent.Add(message.Text); return new TaskCompletionSource<string>().Task; });

        inbox.Flow();

        Assert.Equal(["the tests are in /spec"], sent);
        Assert.Equal(["tidy the readme after"], inbox.State.Queued.Select(m => m.Text));
    }

    /// <summary>
    /// COWORK1a (the design §8.2): a held word's level is changed while it waits, under its id. Made urgent on a door that takes
    /// words at its next step it goes there at once, told again under the same id with its new reach; made ordinary it stays held,
    /// told again. A word no longer held is the session's, and nothing changes it.
    /// </summary>
    [Fact]
    public void A_held_words_level_is_changed_under_its_id_and_made_urgent_it_goes_at_the_next_step()
    {
        var sent = new List<string>();
        var told = new List<(string? Id, DrivenReach Reach, MessageLevel Level)>();
        var inbox = new DrivenInbox(_ => { });
        inbox.OnSaid((message, reach) => told.Add((message.Id, reach, message.Level)));
        inbox.Attach(() => Task.CompletedTask, message => { sent.Add(message.Text); return new TaskCompletionSource<string>().Task; });
        inbox.Flow();
        inbox.Hold(Said("tidy the readme") with { Level = MessageLevel.Ordinary });

        Assert.Equal(QueueEdit.Done, inbox.Edit("said-1", text: null, MessageLevel.Urgent));

        Assert.Equal(["tidy the readme"], sent);
        Assert.Empty(inbox.State.Queued);
        Assert.Equal(("said-1", DrivenReach.NextStep, MessageLevel.Urgent), told.Last());
        Assert.Equal(QueueEdit.Taken, inbox.Edit("said-1", text: null, MessageLevel.Ordinary));

        var holding = new DrivenInbox(_ => { });
        var heard = new List<(string? Id, DrivenReach Reach, MessageLevel Level)>();
        holding.OnSaid((message, reach) => heard.Add((message.Id, reach, message.Level)));
        holding.Attach(() => Task.CompletedTask);
        holding.Hold(Said("the budget is 64 KiB"));

        Assert.Equal(QueueEdit.Done, holding.Edit("said-1", text: null, MessageLevel.Ordinary));
        Assert.Equal(MessageLevel.Ordinary, Assert.Single(holding.State.Queued).Level);
        Assert.Equal(("said-1", DrivenReach.TurnEnd, MessageLevel.Ordinary), heard.Last());
    }

    /// <summary>
    /// COWORK1a (the design §7, §9): the record keeps a level beside the person's own words, urgent where none was said, and none
    /// beside another session's claim, whose level is its own row's (COWORK1d), so nothing reads a claim's default as stated. The
    /// person's channel message is theirs, and keeps one.
    /// </summary>
    [Fact]
    public void The_level_the_record_keeps_is_said_beside_the_persons_own_words_only()
    {
        var claim = new ChannelItemView(ChannelItemView.Message, DateTimeOffset.UtcNow) { Id = "m1", Person = false };
        var persons = claim with { Person = true };

        Assert.Equal("urgent", Said("the budget is 64 KiB").LevelSaid);
        Assert.Equal("ordinary", (Said("tidy the readme after") with { Level = MessageLevel.Ordinary }).LevelSaid);
        Assert.Equal("now", (Said("stop") with { Level = MessageLevel.Now, Channel = persons }).LevelSaid);
        Assert.Null((Said("another agent's claim") with { Channel = claim }).LevelSaid);
    }

    /// <summary>
    /// 🔴 COWORK1a (the design trap 1, a census of the interrupt's callers): no delivery path decides a stop. In the driven inbox only
    /// the keeping stop does; in a conversation's turns only the keeping stop and the withdrawing stop, each the person's own press.
    /// </summary>
    [Fact]
    public void Only_a_stop_press_decides_a_stop_and_no_word_does()
    {
        var root = Daoris.Desktop.Driver.Tests.EveryChildIsHandedTheToolsTests.SourceRoot();

        Assert.Equal(
            ["SendNowAsync"],
            Callers(File.ReadAllText(Path.Combine(root, "Daoris.Desktop.Driver", "Conversations", "DrivenInbox.cs")), "StopThisTurn()"));
        Assert.Equal(
            ["StopKeepingAsync"],
            Callers(File.ReadAllText(Path.Combine(root, "Daoris.Desktop.Driver", "Conversations", "Chat", "ChatTurns.cs")), "StopKeeping()"));
    }

    /// <summary>
    /// The members whose code calls <paramref name="call"/>, each by the last member signature above the call; comments and the
    /// called member's own declaration left out.
    /// </summary>
    internal static List<string> Callers(string source, string call)
    {
        var signature = new System.Text.RegularExpressions.Regex(@"^    (?:public|private|internal)\b[^=;]*?\b(\w+)\s*\(");
        var name = call[..call.IndexOf('(')];
        var callers = new List<string>();
        string? member = null;
        foreach (var line in source.Split('\n'))
        {
            if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
            if (signature.Match(line) is { Success: true } declared) member = declared.Groups[1].Value;
            if (member is null || member == name || !line.Contains(call, StringComparison.Ordinal)) continue;
            if (!callers.Contains(member)) callers.Add(member);
        }

        return callers;
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

    /// <summary>
    /// COWORK1c (the message-urgency design §6, D188 point 6): a word said to a running session is kept on its record first, with its
    /// level and key, and only then held and told, under the record's own id. The run takes it at the turn's end, and it leaves the
    /// record by that id once handed to the wire; the inbox closes only once the take-off has landed, so a record never ends holding
    /// a word its session already took.
    /// </summary>
    [Fact]
    public async Task A_word_kept_for_the_run_waits_on_the_record_under_its_id_and_leaves_it_once_taken()
    {
        var record = new StandInSaid();
        var told = new List<(string? Id, DrivenReach Reach)>();
        var inbox = new DrivenInbox(_ => { });
        inbox.OnSaid((message, reach) => told.Add((message.Id, reach)));
        inbox.Attach(interrupt: null);
        var landing = new TaskCompletionSource();
        record.TakingOff = _ => landing.Task;

        var kept = await inbox.KeepAsync(record, Said("the budget is in level.json") with { Level = MessageLevel.Ordinary, Key = "press/1" }, default);

        Assert.Equal((InboxKeep.Held, DrivenReach.TurnEnd), (kept.Outcome, kept.Reach));
        var word = Assert.Single(record.Said);
        Assert.Equal(("w1", MessageLevel.Ordinary, "press/1"), (word.Id, word.Level, word.Key));
        Assert.Equal([("w1", DrivenReach.TurnEnd)], told);
        Assert.Equal(["w1"], inbox.State.Queued.Select(message => message.Id));

        var held = inbox.NextOrClose()!.Held!;
        Assert.Equal("w1", held.Id);
        // Its door writes it: attempted first, and off the record once written.
        await held.Attempting!();
        held.Went!();
        var waits = inbox.NextOrClose();
        Assert.NotNull(waits?.Waits);
        Assert.False(waits!.Waits!.IsCompleted);
        landing.SetResult();
        await waits.Waits;

        Assert.Null(inbox.NextOrClose());
        Assert.Equal(["w1"], record.TakenOff);
        Assert.Empty(record.Said);
    }

    /// <summary>
    /// COWORK1c (the design §6): a kept word leaves the inbox for its door to be attempted and taken off, never taken off as it is
    /// handed out: the door marks its attempt on the record before it writes it, and once written it leaves the record. One the door
    /// never wrote, nor attempted, stays there.
    /// </summary>
    [Fact]
    public async Task A_kept_word_is_attempted_by_its_door_before_the_wire_and_leaves_the_record_once_written()
    {
        var record = new StandInSaid();
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(interrupt: null);
        await inbox.KeepAsync(record, Said("first"), default);
        await inbox.KeepAsync(record, Said("second"), default);

        var first = inbox.NextOrClose()!.Held!;
        await inbox.TakingOff;
        Assert.Empty(record.Attempted);
        Assert.Empty(record.TakenOff);

        await first.Attempting!();
        Assert.Equal(["w1"], record.Attempted);
        Assert.NotNull(record.Said.Single(word => word.Id == "w1").Attempt);
        first.Went!();
        await inbox.TakingOff;

        Assert.Equal(["w1"], record.TakenOff);
        Assert.Equal(["w2"], record.Ids);
        Assert.Null(record.Said.Single().Attempt);
    }

    /// <summary>
    /// COWORK1c (the design §14's trap 20): two words kept at once reach the session in the record's order, through its one
    /// dispatcher. The first word's write lands first and answers last; had each caller held its own word once its write returned,
    /// the second would have overtaken it.
    /// </summary>
    [Fact]
    public async Task Two_words_kept_at_once_reach_the_session_in_the_records_order()
    {
        var record = new StandInSaid();
        var first = new TaskCompletionSource();
        record.Answering = word => word.Text == "first" ? first.Task : Task.CompletedTask;
        var sent = new List<string?>();
        var inbox = new DrivenInbox(_ => { });
        // The door as the protocol door is: the attempt marked before the write, the word off the record once written.
        inbox.Attach(interrupt: null, deliver: async message =>
        {
            await message.Attempting!();
            lock (sent) sent.Add(message.Id);
            message.Went!();
            return "end_turn";
        });
        inbox.Flow();

        var keepingFirst = inbox.KeepAsync(record, Said("first"), default);
        var keepingSecond = inbox.KeepAsync(record, Said("second"), default);
        await Task.Delay(50);
        first.SetResult();
        await Task.WhenAll(keepingFirst, keepingSecond);
        while (inbox.NextOrClose() is { } next) await (next.Waits ?? next.Answer ?? Task.CompletedTask);

        Assert.Equal(["w1", "w2"], sent);
        Assert.Equal(["w1", "w2"], record.Attempted);
        Assert.Equal(["w1", "w2"], record.TakenOff);
        Assert.Empty(record.Said);
    }

    /// <summary>
    /// COWORK1c: a keep on its way holds the inbox open, so a word said as the turn ends is taken by this run rather than left on a
    /// record that concludes under it; one said once the inbox closed keeps nothing, and its caller keeps it on the record as before.
    /// </summary>
    [Fact]
    public async Task A_keep_on_its_way_holds_the_inbox_open_and_a_closed_inbox_keeps_nothing()
    {
        var record = new StandInSaid();
        var answer = new TaskCompletionSource();
        record.Answering = _ => answer.Task;
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(interrupt: null);

        var keeping = inbox.KeepAsync(record, Said("and the readme"), default);
        var next = inbox.NextOrClose();
        Assert.NotNull(next?.Waits);
        answer.SetResult();
        await next!.Waits!;
        Assert.Equal(InboxKeep.Held, (await keeping).Outcome);
        var held = inbox.NextOrClose()!.Held!;
        Assert.Equal("and the readme", held.Text);
        held.Went!();

        while (inbox.NextOrClose() is { } more) await (more.Waits ?? Task.CompletedTask);
        var late = await inbox.KeepAsync(record, Said("too late"), default);

        Assert.Equal(InboxKeep.Closed, late.Outcome);
        Assert.Equal(["w1"], record.TakenOff);
        Assert.Empty(record.Said);
    }

    /// <summary>
    /// COWORK1c (the design §6): a word whose keep was on its way as the inbox closed under the run never stays on the record, which
    /// is ending: it is taken off again, and its caller holds it as words said while a session winds up are held.
    /// </summary>
    [Fact]
    public async Task A_word_kept_as_the_inbox_closed_is_taken_off_the_ending_record()
    {
        var record = new StandInSaid();
        var answer = new TaskCompletionSource();
        record.Answering = _ => answer.Task;
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(interrupt: null);

        var keeping = inbox.KeepAsync(record, Said("as it ends"), default);
        await Task.Delay(20);
        Assert.Empty(inbox.Close());
        answer.SetResult();

        Assert.Equal(InboxKeep.Closed, (await keeping).Outcome);
        await inbox.TakingOff;
        Assert.Equal(["w1"], record.TakenOff);
        Assert.Empty(record.Said);
    }

    /// <summary>
    /// COWORK1c (the design §6: nothing undelivered vanishes): closed by anything but the person's own stop of the session (its door
    /// failed, the shell shut down), the words kept for the run stay on the record, told as left there, for the session's go-on; only
    /// the words held in this process alone come back, for the run to say. Closed by the person's stop, the kept words leave the
    /// record and come back with the rest, never reached, as before.
    /// </summary>
    [Fact]
    public async Task Closed_under_the_run_the_kept_words_stay_on_the_record_and_the_persons_stop_takes_them_off()
    {
        var record = new StandInSaid();
        var stopped = false;
        var left = new List<string?>();
        var inbox = new DrivenInbox(_ => { });
        inbox.OnClosing(() => { }, personStopped: () => stopped, left: words => left.AddRange(words.Select(word => word.Id)));
        inbox.Attach(interrupt: null);
        await inbox.KeepAsync(record, Said("kept"), default);
        Assert.True(inbox.Hold(Said("held here alone")));

        Assert.Equal(["held here alone"], inbox.Close().Select(word => word.Text));
        Assert.Equal(["w1"], left);
        Assert.Equal(["w1"], record.Ids);

        stopped = true;
        var other = new DrivenInbox(_ => { });
        other.OnClosing(() => { }, personStopped: () => stopped);
        other.Attach(interrupt: null);
        await other.KeepAsync(record, Said("kept, then stopped"), default);

        Assert.Equal(["kept, then stopped"], other.Close().Select(word => word.Text));
        await other.TakingOff;
        Assert.Equal(["w2"], record.TakenOff);
        Assert.Equal(["w1"], record.Ids);
    }

    /// <summary>
    /// COWORK1c: a kept word the person takes back leaves the record too, and one reworded is kept anew in its place: the new words
    /// on the record under their own id, the old taken off, so a restart never brings back what the person changed.
    /// </summary>
    [Fact]
    public async Task A_kept_word_taken_back_or_reworded_changes_the_record_too()
    {
        var record = new StandInSaid();
        var withdrawn = new List<string?>();
        var inbox = new DrivenInbox(_ => { });
        inbox.OnWithdrawn(message => withdrawn.Add(message.Id));
        inbox.Attach(interrupt: null);
        await inbox.KeepAsync(record, Said("first"), default);
        await inbox.KeepAsync(record, Said("second"), default);

        Assert.Equal(QueueEdit.Done, await inbox.EditAsync("w1", text: null));
        Assert.Equal(QueueEdit.Done, await inbox.EditAsync("w2", "second, reworded"));
        await inbox.TakingOff;

        Assert.Equal(["w1", "w2"], withdrawn);
        Assert.Equal(["w3"], record.Ids);
        Assert.Equal(["second, reworded"], record.Said.Select(word => word.Text));
        Assert.Equal([("w3", "second, reworded")], inbox.State.Queued.Select(message => (message.Id, message.Text)));
        Assert.Equal(QueueEdit.Taken, await inbox.EditAsync("w1", text: null));
    }

    /// <summary>
    /// COWORK1c (the design §6, trap 19): a producer's retry under the key a waiting word holds is answered with that word, and the
    /// inbox, which holds it already, holds nothing more; a word the record refused is never held.
    /// </summary>
    [Fact]
    public async Task A_retry_under_a_held_key_holds_nothing_more_and_a_refused_word_is_never_held()
    {
        var record = new StandInSaid();
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(interrupt: null);
        var daoris = new ChatMessage("Daoris: reopened.", []) { Daoris = new DaorisWord("reopen/q1/desk/44") };

        Assert.Equal(InboxKeep.Held, (await inbox.KeepAsync(record, daoris, default)).Outcome);
        Assert.Equal(InboxKeep.Held, (await inbox.KeepAsync(record, daoris, default)).Outcome);
        Assert.True(inbox.Holds);
        Assert.Equal("w1", inbox.NextOrClose()?.Held?.Id);
        Assert.Null(inbox.NextOrClose()?.Held);

        record.Refusing = "running";
        var other = new DrivenInbox(_ => { });
        other.Attach(interrupt: null);
        var refused = await other.KeepAsync(record, Said("hello"), default);
        Assert.Equal((InboxKeep.Refused, "running"), (refused.Outcome, refused.Said?.Refusal));
        Assert.False(other.Holds);
    }

    // ——— COWORK1d: another agent's claim (the message-urgency design §2.3, §4.2).

    private static ChatMessage Claim(string id, MessageLevel level) =>
        new($"Another session says: {id}", []) { Level = level, Channel = new ChannelItemView(ChannelItemView.Message, DateTimeOffset.UnixEpoch) { Id = id, Text = id } };

    /// <summary>
    /// 🔴 Trap 3 (§2.3's race, narrowed): once the answer to the inbox's last open prompt is read, the turn is over, so a claim is
    /// refused rather than written as a prompt that would start a turn of its own; the person's word is still held, and a prompt sent
    /// again opens the inbox to claims.
    /// </summary>
    [Fact]
    public void A_claim_after_the_last_prompts_answer_is_read_is_refused_while_a_persons_word_is_held()
    {
        var sent = new List<ChatMessage>();
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(interrupt: null, deliver: message =>
        {
            sent.Add(message);
            return Task.FromResult("end_turn");
        });
        inbox.Flow();
        Assert.True(inbox.TakesClaims);

        inbox.LastAnswered();

        Assert.False(inbox.TakesClaims);
        Assert.False(inbox.HoldClaim(Claim("m1", MessageLevel.Urgent)));
        Assert.True(inbox.Hold(Said("Use the older bridge.")));
        Assert.True(inbox.TakesClaims);
        Assert.True(inbox.HoldClaim(Claim("m2", MessageLevel.Urgent)));
        Assert.Equal(["Use the older bridge.", "Another session says: m2"], sent.Select(message => message.Text));
    }

    /// <summary>
    /// 🔴 Trap 3: claims alone at a turn's end make no turn. The run ends, and each is let go, never handed back as the person's words
    /// never reached; beside a word that makes a turn, each goes in the order said.
    /// </summary>
    [Fact]
    public void Claims_alone_at_a_turns_end_make_no_turn_and_are_let_go()
    {
        var released = new List<string>();
        var inbox = new DrivenInbox(_ => { });
        inbox.Attach(interrupt: null);
        Assert.True(inbox.HoldClaim(Claim("m1", MessageLevel.Urgent) with { Acknowledge = state => released.Add($"m1 {state}") }));

        Assert.False(inbox.Holds);
        Assert.Empty(inbox.State.Queued);
        Assert.Null(inbox.NextOrClose());
        Assert.Equal([$"m1 {ChannelAcks.Released}"], released);

        var other = new DrivenInbox(_ => { });
        other.Attach(interrupt: null);
        Assert.True(other.HoldClaim(Claim("m2", MessageLevel.Ordinary) with { Acknowledge = state => released.Add($"m2 {state}") }));
        Assert.Empty(other.Close());
        Assert.Contains($"m2 {ChannelAcks.Released}", released);
    }
}
