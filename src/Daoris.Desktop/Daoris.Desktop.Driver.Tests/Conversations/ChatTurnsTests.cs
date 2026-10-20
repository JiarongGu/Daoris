using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A conversation's turns (CONV4a) on a door that takes words at its next step (MSG1c, D137 §2.1): a word said while a turn
/// is on the wire is handed to that turn at once and never queued, the turn lasting until every word handed to it is
/// answered; anywhere else it waits for the turn's end, as it always did. And the words a conversation goes on with are
/// never listed as queued, since its record already shows them waiting. A door scripted in memory, so the fast half.
/// </summary>
public sealed class ChatTurnsTests
{
    /// <summary>A door whose turns end when the test says, and which records what reached it.</summary>
    private sealed class Door
    {
        private readonly object _gate = new();
        private readonly List<TaskCompletionSource> _turns = [];

        public List<string> Took { get; } = [];

        public List<string> Steered { get; } = [];

        /// <summary>The ids of the words handed to the running turn at its next step, as the door was handed them (COWORK1c).</summary>
        public List<string?> SteeredIds { get; } = [];

        /// <summary>The ids of the turns that reached the door, as the door was handed them (COWORK1c).</summary>
        public List<string?> TookIds { get; } = [];

        /// <summary>The words *Send now* steered into the running turn (STEER2a).</summary>
        public List<string> SteeredNow { get; } = [];

        public List<ChatQueue> Told { get; } = [];

        /// <summary>How many times the door was asked to stop the turn in flight.</summary>
        public int Interrupts;

        /// <summary>Holds each stop on its way to the door until set: a slow wire (STEER2a).</summary>
        public TaskCompletionSource? InterruptGate { get; set; }

        /// <summary>How many turns had reached the door when each stop landed: the turn it cut.</summary>
        public List<int> InterruptedAt { get; } = [];

        /// <summary>Whether a word said during a turn is handed to it: the door's own word, as the agent's promptQueueing is.</summary>
        public bool TakesWordsMidTurn { get; set; } = true;

        /// <summary>Whether *Send now* steers into the running turn: the agent's own word, as its steering extension is.</summary>
        public bool SteersNow { get; set; } = true;

        public TaskCompletionSource Step { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>The steer's answer, which the turn waits for as it waits for a word at its next step.</summary>
        public TaskCompletionSource Now { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Told each turn as it reaches the door, before it is on the wire (COWORK1c).</summary>
        public Action<ChatMessage>? OnTake { get; set; }

        public ChatTurns Turns(bool steers = true) => new(
            ready: () => Task.FromResult(true),
            take: (message, sent) =>
            {
                var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                OnTake?.Invoke(message);
                lock (_gate)
                {
                    Took.Add(message.Text);
                    TookIds.Add(message.Id);
                    _turns.Add(ended);
                }

                sent();
                return ended.Task;
            },
            interrupt: async () =>
            {
                if (InterruptGate is { } gate) await gate.Task;
                lock (_gate) InterruptedAt.Add(Took.Count);
                Interlocked.Increment(ref Interrupts);
            },
            changed: queue =>
            {
                lock (_gate) Told.Add(queue);
            },
            steer: steers
                ? message =>
                {
                    if (!TakesWordsMidTurn) return null;
                    lock (_gate)
                    {
                        Steered.Add(message.Text);
                        SteeredIds.Add(message.Id);
                    }

                    return Written(message, Step.Task);
                }
                : null,
            steerNow: steers
                ? message =>
                {
                    if (!SteersNow) return null;
                    lock (_gate) SteeredNow.Add(message.Text);
                    return Written(message, Now.Task);
                }
                : null,
            nextStep: steers ? () => TakesWordsMidTurn : null);

        /// <summary>
        /// A word handed to the turn on the wire, as the protocol door handles one (COWORK1c): its attempt marked before the write, and
        /// off the record once written; then the agent's answer.
        /// </summary>
        private static async Task Written(ChatMessage message, Task answered)
        {
            if (message.Attempting is { } attempting) await attempting();
            message.Went?.Invoke();
            await answered;
        }

        public void EndTurn(int at)
        {
            TaskCompletionSource turn;
            lock (_gate) turn = _turns[at];
            turn.TrySetResult();
        }
    }

    [Fact]
    public async Task A_word_said_while_a_turn_is_on_the_wire_is_handed_to_it_and_the_turn_lasts_until_it_is_answered()
    {
        var door = new Door();
        var turns = door.Turns();

        Assert.Equal(TurnReach.Now, turns.Take(new ChatMessage("read the five files", [])));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");

        Assert.Equal(TurnReach.NextStep, turns.Take(new ChatMessage("put PINEAPPLE after the words", [])));

        Assert.Equal(["put PINEAPPLE after the words"], door.Steered);
        Assert.Empty(turns.State.Queued);
        Assert.Single(door.Took);

        // The turn before it is answered: the turn goes on, since the word handed to it is not.
        door.EndTurn(0);
        await Task.Delay(100);
        Assert.True(turns.Running);

        door.Step.SetResult();
        await Poll.Until(() => !turns.Running, () => "the turn never ended once its word was answered");
        Assert.Single(door.Took);
    }

    [Fact]
    public async Task A_word_said_where_the_door_takes_none_mid_turn_waits_for_the_turn_to_end()
    {
        var door = new Door { TakesWordsMidTurn = false };
        var turns = door.Turns();

        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");

        Assert.Equal(TurnReach.TurnEnd, turns.Take(new ChatMessage("the budget is in level.json", [])));
        Assert.Equal(["the budget is in level.json"], turns.State.Queued.Select(message => message.Text));

        door.EndTurn(0);
        await Poll.Until(() => door.Took.Count == 2, () => "the waiting word never went");
        Assert.Equal(["read the five files", "the budget is in level.json"], door.Took);
    }

    /// <summary>A door that hands no word to a running turn (the native door, a silent agent) keeps CONV4a's queue.</summary>
    [Fact]
    public async Task A_door_with_no_next_step_queues_as_it_always_did()
    {
        var door = new Door();
        var turns = door.Turns(steers: false);

        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");

        Assert.Equal(TurnReach.TurnEnd, turns.Take(new ChatMessage("one more thing", [])));
        Assert.Empty(door.Steered);
    }

    /// <summary>
    /// After the person stopped the turn, a word waits for it to end instead of joining a turn that is winding up: handed
    /// to it, the agent would answer it <c>cancelled</c> and its answer would reach nobody (STEER1 §3).
    /// </summary>
    [Fact]
    public async Task A_word_said_after_a_stop_waits_for_the_stopped_turn_to_end()
    {
        var door = new Door();
        var turns = door.Turns();

        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");
        await turns.StopAsync();

        Assert.Equal(TurnReach.TurnEnd, turns.Take(new ChatMessage("start again", [])));
        Assert.Empty(door.Steered);
    }

    /// <summary>
    /// 🔴 STEER2a (D136's 2026-10-16 note): *Send now* into a turn on the wire, where the agent steers, hands the words to it at
    /// once by the steer and never by a prompt at its next step; nothing is stopped or queued, and the turn lasts until the steer
    /// is answered.
    /// </summary>
    [Fact]
    public async Task Sending_now_into_a_turn_on_the_wire_steers_the_words_and_the_turn_lasts_until_answered()
    {
        var door = new Door();
        var turns = door.Turns();
        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");

        var now = await turns.TakeNowAsync(new ChatMessage("put MANGO after the words", []));

        Assert.Equal(new SentNow("now", true, null), now);
        Assert.Equal(["put MANGO after the words"], door.SteeredNow);
        Assert.Empty(door.Steered);
        Assert.Empty(turns.State.Queued);
        Assert.Equal(0, door.Interrupts);

        door.EndTurn(0);
        await Task.Delay(100);
        Assert.True(turns.Running);
        door.Now.SetResult();
        await Poll.Until(() => !turns.Running, () => "the turn never ended once the steer was answered");
        Assert.Single(door.Took);
    }

    /// <summary>
    /// STEER2d: the steer found no turn running (<c>promptRequired</c>), so the door hands the words back: they are the next turn,
    /// ahead of what waits, and the first turn lasts until the steer is answered so nothing closes before they go.
    /// </summary>
    [Fact]
    public async Task Words_handed_back_after_a_steer_found_no_turn_are_the_next_turn()
    {
        var door = new Door();
        var turns = door.Turns();
        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");
        await turns.TakeNowAsync(new ChatMessage("put MANGO after the words", []));

        door.EndTurn(0);
        Assert.True(turns.HandBack(new ChatMessage("put MANGO after the words", []) { Id = "said-c1" }));
        door.Now.SetResult();

        await Poll.Until(() => door.Took.Count == 2, () => "the handed-back words never became the next turn");
        Assert.Equal(["read the five files", "put MANGO after the words"], door.Took);
        Assert.Empty(turns.State.Queued);
        door.EndTurn(1);
        await Poll.Until(() => !turns.Running, () => "the turns never ended");
    }

    /// <summary>STEER2d: handed-back words are recorded only as taken under their own id, never said or told again.</summary>
    [Fact]
    public void Handed_back_words_are_recorded_only_as_taken_under_their_id()
    {
        var back = new ChatMessage("put MANGO after the words", []) { Id = "said-c1", Preface = "on the Agents page", Said = true };

        var recorded = Assert.Single(ChatRunner.Recorded(back));

        Assert.Equal("said-c1", recorded.Id);
        Assert.Equal(SessionEventKind.User, recorded.Kind);
        Assert.True(back.OnRecord);
    }

    /// <summary>
    /// 🔴 COWORK1a (D188 point 3, the message-urgency design trap 1): where the door steers nothing and takes no word mid-turn,
    /// *now* queues the words for the turn's end and stops nothing, said <c>no-interrupt</c>; the turn runs on, and the words are
    /// the next turn once it ends. With no turn running they are that turn at once.
    /// </summary>
    [Fact]
    public async Task Now_where_the_door_steers_nothing_holds_the_words_for_the_turns_end_and_stops_nothing()
    {
        var door = new Door { TakesWordsMidTurn = false, SteersNow = false };
        var turns = door.Turns();

        Assert.Equal(new SentNow(null, true, null), await turns.TakeNowAsync(new ChatMessage("read the five files", [])));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");

        var now = await turns.TakeNowAsync(new ChatMessage("start with the tests", []) { Level = MessageLevel.Now });

        Assert.Equal(new SentNow("turn-end", false, SendNowWhy.NoInterrupt), now);
        Assert.Equal(0, door.Interrupts);
        Assert.Equal(["start with the tests"], turns.State.Queued.Select(message => message.Text));
        Assert.Equal(MessageLevel.Now, turns.State.Queued.Single().Level);
        Assert.Equal(new SentNow("turn-end", false, SendNowWhy.NoInterrupt), await turns.TakeNowAsync(new ChatMessage("and the readme", [])));
        Assert.Equal(0, door.Interrupts);
        door.EndTurn(0);
        await Poll.Until(() => door.Took.Count == 2, () => "the words never went after the turn");
        Assert.Equal(["read the five files", "start with the tests"], door.Took);
    }

    /// <summary>
    /// 🔴 STEER2a (a second maker's review): a stop decided for turn A is on the wire, or dropped, before the kept words' own
    /// turn B goes. A slow stop that landed after B had gone would cut B, whose words have left the queue and would be lost.
    /// The keeping stop is the person's own press (COWORK1a), so it is pressed here beside the words, never by them.
    /// </summary>
    [Fact]
    public async Task The_kept_words_turn_waits_for_the_stop_decided_for_the_turn_before_and_is_never_cut_by_it()
    {
        var door = new Door { TakesWordsMidTurn = false, SteersNow = false, InterruptGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var turns = door.Turns();
        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");

        turns.Take(new ChatMessage("start with the tests", []));
        var pressing = turns.StopKeepingAsync();
        door.EndTurn(0);
        await Task.Delay(150);

        Assert.Single(door.Took);
        door.InterruptGate.SetResult();
        Assert.True((await pressing.WaitAsync(Poll.Patience)).Cancelled);
        await Poll.Until(() => door.Took.Count == 2, () => "the kept words never went");
        Assert.Equal([1], door.InterruptedAt);
    }

    /// <summary>
    /// 🔴 COWORK1a (the design §2.1, trap 13): on a door that takes words at its next step an ordinary word is never handed to the
    /// turn in flight; it waits as a turn of its own. An urgent word said after it goes at the next step past it, since its sender
    /// chose sooner; when the turn ends the ordinary one goes, in the order said.
    /// </summary>
    [Fact]
    public async Task An_ordinary_word_waits_as_its_own_turn_and_an_urgent_one_goes_at_the_next_step_past_it()
    {
        var door = new Door();
        var turns = door.Turns();
        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");

        Assert.Equal(TurnReach.TurnEnd, turns.Take(new ChatMessage("tidy the readme after", []) { Level = MessageLevel.Ordinary }));
        Assert.Equal(TurnReach.NextStep, turns.Take(new ChatMessage("the budget is 64 KiB", [])));

        Assert.Equal(["the budget is 64 KiB"], door.Steered);
        var waiting = Assert.Single(turns.State.Queued);
        Assert.Equal(("tidy the readme after", MessageLevel.Ordinary), (waiting.Text, waiting.Level));

        door.EndTurn(0);
        door.Step.SetResult();
        await Poll.Until(() => door.Took.Count == 2, () => "the ordinary word never went as its own turn");
        Assert.Equal(["read the five files", "tidy the readme after"], door.Took);
    }

    /// <summary>
    /// COWORK1a (the design §8.2): a waiting word's level is changed while it waits, under its id. Made urgent, it goes at the turn's
    /// next step where the door takes words then; made <i>now</i> where the agent steers, it is steered. A word a turn took already is
    /// answered taken.
    /// </summary>
    [Fact]
    public async Task A_waiting_words_level_is_changed_under_its_id_and_raised_it_goes_into_the_turn()
    {
        var door = new Door();
        var turns = door.Turns();
        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");
        turns.Take(new ChatMessage("tidy the readme after", []) { Level = MessageLevel.Ordinary });
        turns.Take(new ChatMessage("and the changelog", []) { Level = MessageLevel.Ordinary });
        var ids = turns.State.Queued.Select(message => message.Id!).ToList();

        Assert.Equal(QueueEdit.Done, turns.EditQueued(ids[0], text: null, MessageLevel.Urgent));
        Assert.Equal(["tidy the readme after"], door.Steered);
        Assert.Equal(QueueEdit.Done, turns.EditQueued(ids[1], text: null, MessageLevel.Now));
        Assert.Equal(["and the changelog"], door.SteeredNow);
        Assert.Empty(turns.State.Queued);
        Assert.Equal(QueueEdit.Taken, turns.EditQueued(ids[1], text: null, MessageLevel.Ordinary));

        var held = new Door { TakesWordsMidTurn = false, SteersNow = false };
        var holding = held.Turns();
        holding.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => held.Took.Count == 1, () => "the first turn never reached the door");
        holding.Take(new ChatMessage("tidy the readme after", []));
        var id = holding.State.Queued.Single().Id!;

        Assert.Equal(QueueEdit.Done, holding.EditQueued(id, text: null, MessageLevel.Ordinary));
        Assert.Equal((id, MessageLevel.Ordinary), (holding.State.Queued.Single().Id, holding.State.Queued.Single().Level));
        Assert.Equal(0, held.Interrupts);
    }

    /// <summary>
    /// 🔴 STEER2a (the owner's case): where the agent steers, *Send now* steers the words into the running turn even past a word
    /// on its way and a helper's answer waiting as its own turn. The steer cuts the step in flight, not the prompt the word is
    /// queued as: the word is still taken at its next step, the helper's answer is still its own turn after, and nothing is
    /// stopped (D136 point 4 forbids the stop, not the steer).
    /// </summary>
    [Fact]
    public async Task Sending_now_where_the_agent_steers_goes_past_words_on_their_way_and_waiting_and_stops_nothing()
    {
        var door = new Door();
        var turns = door.Turns();
        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");
        Assert.Equal(TurnReach.NextStep, turns.Take(new ChatMessage("put PINEAPPLE after the words", [])));
        var helper = new SaidWordView("h1w", "Another agent says five.", DateTimeOffset.UtcNow, [], true) { Helper = "h1" };
        Assert.Equal(TurnReach.TurnEnd, turns.Take(new ChatMessage(helper.Text, []) { Helped = helper }));

        var now = await turns.TakeNowAsync(new ChatMessage("stop: the tests are in /spec", []));

        Assert.Equal(new SentNow("now", true, null), now);
        Assert.Equal(["stop: the tests are in /spec"], door.SteeredNow);
        Assert.Equal(["put PINEAPPLE after the words"], door.Steered);
        Assert.Equal(0, door.Interrupts);

        // The turn lasts until both the word at its next step and the steer are answered; the helper's answer is its own turn after.
        door.EndTurn(0);
        door.Now.SetResult();
        await Task.Delay(100);
        Assert.Single(door.Took);
        door.Step.SetResult();
        await Poll.Until(() => door.Took.Count == 2, () => "the helper's answer never went as its own turn");
        Assert.Equal(["read the five files", "Another agent says five."], door.Took);
    }

    /// <summary>
    /// 🔴 COWORK1e (D188 point 4; the message-urgency design §2.3, trap 11), a conversation's turn pump: the turns judge each helper's
    /// answer as its turn comes, once every turn before it was recorded, so two answers queued at once under separate asks never both
    /// pass as the second. The one the bound holds never reaches the door and leaves the queue; the person's word after it still goes.
    /// </summary>
    [Fact]
    public async Task Two_answers_queued_at_once_never_both_pass_as_the_second()
    {
        var door = new Door();
        var turns = door.Turns();
        var line = new List<SessionEvent>();
        // The record as the door keeps it: each turn's prompt as it reaches the door, the agent acting on it, and its end where the test ends it.
        door.OnTake = message =>
        {
            lock (line) line.AddRange([.. ChatRunner.Recorded(message), new SessionEvent { Kind = SessionEventKind.Message, Text = "On it." }]);
        };
        var judged = new List<string>();
        turns.Bounds(message =>
        {
            lock (line)
            {
                judged.Add(message.Id!);
                return Task.FromResult(AnswersAlone.Holds([message.Helped!], [.. line]));
            }
        });
        void Ends(int turn)
        {
            lock (line) line.Add(new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
            door.EndTurn(turn);
        }

        ChatMessage Answer(string id) =>
            new($"Answer {id}.", []) { Helped = new SaidWordView(id, $"Answer {id}.", DateTimeOffset.UtcNow, [], true) { Helper = $"h-{id}" }, Id = id };

        turns.Take(Answer("a1"));
        await Poll.Until(() => door.Took.Count == 1, () => "the first answer never reached the door");
        Assert.Equal(TurnReach.TurnEnd, turns.Take(Answer("a2")));
        Assert.Equal(TurnReach.TurnEnd, turns.Take(Answer("a3")));

        Ends(0);
        await Poll.Until(() => door.Took.Count == 2, () => "the second answer never went as its own turn");
        Ends(1);
        await Poll.Until(() => { lock (line) return judged.Count == 3; }, () => "the third answer was never judged");
        turns.Take(new ChatMessage("Go on with what you have.", []));
        await Poll.Until(() => door.Took.Count == 3, () => "the person's word never went");

        Assert.Equal(["Answer a1.", "Answer a2.", "Go on with what you have."], door.Took);
        Assert.Equal(["a1", "a2", "a3"], judged);
        Assert.Empty(turns.State.Queued);
    }

    /// <summary>
    /// 🔴 STEER2a (D136 point 4, evidence §3): on a next-step door whose agent cannot steer, the driver never stops a turn while a
    /// word is on its way. The press goes at the next step, behind the word on its way, and past a helper's answer waiting as its
    /// own turn, which is ordinary (COWORK1a, the design §2.1: urgency is when, not order). Either way <c>now: false</c>,
    /// <c>behind</c>.
    /// </summary>
    [Fact]
    public async Task Sending_now_behind_words_on_their_way_where_the_agent_cannot_steer_stops_nothing_and_says_so()
    {
        var door = new Door { SteersNow = false };
        var turns = door.Turns();
        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");
        Assert.Equal(TurnReach.NextStep, turns.Take(new ChatMessage("put PINEAPPLE after the words", [])));
        var helper = new SaidWordView("h1w", "Another agent says five.", DateTimeOffset.UtcNow, [], true) { Helper = "h1" };
        Assert.Equal(TurnReach.TurnEnd, turns.Take(new ChatMessage(helper.Text, []) { Helped = helper }));

        var now = await turns.TakeNowAsync(new ChatMessage("start with the tests", []));

        Assert.Equal(new SentNow("next-step", false, SendNowWhy.Behind), now);
        Assert.Equal(0, door.Interrupts);
        Assert.Empty(door.SteeredNow);
        Assert.Equal(["put PINEAPPLE after the words", "start with the tests"], door.Steered);
        Assert.Empty(turns.State.Queued);

        var next = new Door { SteersNow = false };
        var stepping = next.Turns();
        stepping.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => next.Took.Count == 1, () => "the first turn never reached the door");
        stepping.Take(new ChatMessage("put PINEAPPLE after the words", []));

        Assert.Equal(new SentNow("next-step", false, SendNowWhy.Behind), await stepping.TakeNowAsync(new ChatMessage("and MANGO", [])));
        Assert.Equal(["put PINEAPPLE after the words", "and MANGO"], next.Steered);
        Assert.Empty(next.SteeredNow);
        Assert.Equal(0, next.Interrupts);
    }

    /// <summary>
    /// STEER2a: a door that hands words to the turn at its next step but never said it steers sends them there, as a word said
    /// then goes, and says why it was not now; nothing is stopped, since a stop would answer the word on its way cancelled.
    /// </summary>
    [Fact]
    public async Task Sending_now_where_the_door_takes_words_only_at_its_next_step_sends_them_there_and_says_why()
    {
        var door = new Door { SteersNow = false };
        var turns = door.Turns();
        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");

        var now = await turns.TakeNowAsync(new ChatMessage("one more thing", []));

        Assert.Equal(new SentNow("next-step", false, SendNowWhy.NoInterrupt), now);
        Assert.Equal(["one more thing"], door.Steered);
        Assert.Equal(0, door.Interrupts);
    }

    /// <summary>
    /// STEER2a (CANCEL_TURN with <c>keep</c>): a stop that keeps the waiting words stops the turn in flight and withdraws
    /// nothing, and the words are the next turn; with nothing running it stops nothing.
    /// </summary>
    [Fact]
    public async Task A_stop_that_keeps_the_waiting_words_stops_the_turn_and_they_go_next()
    {
        var door = new Door { TakesWordsMidTurn = false };
        var turns = door.Turns();
        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");
        turns.Take(new ChatMessage("and the readme", []));

        var stop = await turns.StopKeepingAsync();

        Assert.True(stop.Cancelled);
        Assert.Empty(stop.Withdrawn);
        Assert.Equal(1, door.Interrupts);
        Assert.Equal(["and the readme"], turns.State.Queued.Select(message => message.Text));
        door.EndTurn(0);
        await Poll.Until(() => door.Took.Count == 2, () => "the kept words never went");
        door.EndTurn(1);
        await Poll.Until(() => !turns.Running, () => "the kept words' turn never ended");
        Assert.Equal(TurnStop.Nothing, await turns.StopKeepingAsync());
    }

    /// <summary>
    /// The words a conversation goes on with (MSG1c, D137 §3.1) are its first turn, and the record already shows them
    /// waiting: the queue the page is told never lists them, while it still says a turn is opening.
    /// </summary>
    [Fact]
    public async Task The_words_a_conversation_goes_on_with_are_never_listed_as_queued()
    {
        var open = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var told = new List<ChatQueue>();
        var turns = new ChatTurns(
            ready: () => open.Task,
            take: (_, sent) =>
            {
                sent();
                return Task.CompletedTask;
            },
            interrupt: () => Task.CompletedTask,
            changed: queue =>
            {
                lock (told) told.Add(queue);
            });

        turns.Take(new ChatMessage("Port 8080.", []) { GoesOn = new ResumeAsk("conv-1", [], "— your words are the next turn of its own conversation.") });

        Assert.True(turns.State.Opening);
        Assert.Empty(turns.State.Queued);
        open.SetResult(true);
        await Poll.Until(() => !turns.Running, () => "the first turn never ended");
        lock (told) Assert.All(told, queue => Assert.Empty(queue.Queued));
    }

    /// <summary>
    /// MIXAGENT2a-iii: a helper's answer said to a conversation that is talking waits for the turn's end, as a turn of its own:
    /// never handed to the turn on the wire as the person's word is, never listed as queued (its record shows it waiting), and
    /// told delivered once its turn is on the wire, which is when it comes off the record.
    /// </summary>
    [Fact]
    public async Task A_helpers_answer_waits_for_the_turn_to_end_unlisted_and_is_told_delivered_once_sent()
    {
        var door = new Door();
        var turns = door.Turns();
        var delivered = 0;

        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");

        var helper = new SaidWordView("h1w", "Another agent says five.", DateTimeOffset.UtcNow, [], true) { Helper = "h1" };
        Assert.Equal(TurnReach.TurnEnd, turns.Take(new ChatMessage(helper.Text, []) { Helped = helper, Delivered = () => delivered++ }));

        Assert.Empty(door.Steered);
        Assert.Empty(turns.State.Queued);
        Assert.Equal(0, delivered);

        door.EndTurn(0);
        await Poll.Until(() => door.Took.Count == 2, () => "the helper's answer never went");
        Assert.Equal(["read the five files", "Another agent says five."], door.Took);
        // The door records the words before it calls `sent`, which tells them delivered: wait for that call, not only the take.
        await Poll.Until(() => Volatile.Read(ref delivered) == 1, () => $"the helper's answer was told delivered {delivered} times");
        Assert.Equal(1, delivered);
    }

    /// <summary>
    /// 🔴 COWORK1d (the message-urgency design §5; D163 point 2 as D188 amends it): a helper's answer to an urgent ask goes at the
    /// conversation's next step, where its door takes words then, recorded as Daoris's words around the helper's claim; one with no level
    /// waits for the turn's end as before (above).
    /// </summary>
    [Fact]
    public async Task A_helpers_answer_to_an_urgent_ask_goes_at_the_next_step_as_the_helpers_claim()
    {
        var door = new Door();
        var turns = door.Turns();
        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");

        var helper = new SaidWordView("h1w", "Another agent says five.", DateTimeOffset.UtcNow, [], true) { Helper = "h1", Level = MessageLevel.Urgent };
        var answer = new ChatMessage(helper.Text, []) { Helped = helper };

        Assert.Equal(TurnReach.NextStep, turns.Take(answer));
        Assert.Equal(["Another agent says five."], door.Steered);
        Assert.Equal("target", ChatMessage.Words(answer).Origin);
    }

    /// <summary>
    /// MIXAGENT2a-iii: a helper's answer is recorded as Daoris's words around another agent's claim, under its record id so it
    /// pairs with the event that showed it waiting, and never as the person's: no preface and no browser line, origin `target`.
    /// </summary>
    [Fact]
    public void A_helpers_answer_is_recorded_as_daoriss_words_and_never_the_persons()
    {
        var helper = new SaidWordView("h1w", "Another agent says five.", DateTimeOffset.UtcNow, [], true) { Helper = "h1" };

        var recorded = Assert.Single(ChatRunner.Recorded(new ChatMessage(helper.Text, []) { Helped = helper, Preface = "where you are" }));

        Assert.Equal((SessionEventKind.User, "target", "h1w", "Another agent says five."), (recorded.Kind, recorded.Origin, recorded.Id, recorded.Text));
        Assert.Null(recorded.Reaches);
    }

    /// <summary>
    /// MSG1c3 (D137's MSG1c2 note, D142 point 1): a word handed to the turn at its next step, whose turn the person stopped
    /// while it was on its way and the agent then answered <c>cancelled</c>, may have been read with its answer dropped
    /// (STEER1 §3). The line the conversation keeps of it carries a code the page words in the reader's language, the word's
    /// id so the page settles the word it shows waiting, and the driver's English beside them, unchanged. Any other answer
    /// keeps no line: the agent's answer is in the turn.
    /// </summary>
    [Fact]
    public void A_word_a_stop_cut_off_on_its_way_is_said_lost_by_its_code()
    {
        var lost = ChatTurns.Lost("said-1", stoppedUnder: true, stopReason: "cancelled");

        Assert.NotNull(lost);
        Assert.Equal(
            (SessionEventKind.Note, (string?)SessionEventCodes.Lost, (string?)"— it may have read what you added; its answer was not kept."),
            (lost.Kind, lost.Code, lost.Text));
        Assert.Equal(["said-1"], lost.Words!);
        Assert.Null(lost.Why);
        Assert.Null(ChatTurns.Lost("said-1", stoppedUnder: true, stopReason: "end_turn"));
        Assert.Null(ChatTurns.Lost("said-1", stoppedUnder: false, stopReason: "cancelled"));
    }

    /// <summary>QUEUEEDIT1a: a word the person took back is settled on the record by its id and its code, the page's words.</summary>
    [Fact]
    public void A_word_taken_back_is_said_withdrawn_by_its_code()
    {
        var withdrawn = ChatTurns.Withdrawn("said-1");

        Assert.Equal((SessionEventKind.Note, (string?)SessionEventCodes.Withdrawn), (withdrawn.Kind, withdrawn.Code));
        Assert.Equal(["said-1"], withdrawn.Words!);
        Assert.Equal("withdrawn", SessionEventCodes.Withdrawn);
    }

    /// <summary>QUEUEEDIT1a: each word that waits has an id, and taking one back leaves the rest in the order said (D76 CONV4a).</summary>
    [Fact]
    public async Task A_queued_word_has_an_id_and_is_removed_by_it_leaving_the_rest_in_order()
    {
        var door = new Door { TakesWordsMidTurn = false };
        var turns = door.Turns();
        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");
        turns.Take(new ChatMessage("and the sixth", []));
        turns.Take(new ChatMessage("and the seventh", []));

        var queued = turns.State.Queued;
        Assert.All(queued, message => Assert.False(string.IsNullOrEmpty(message.Id)));
        Assert.NotEqual(queued[0].Id, queued[1].Id);

        Assert.Equal(QueueEdit.Done, turns.EditQueued(queued[0].Id!, text: null));

        Assert.Equal(["and the seventh"], turns.State.Queued.Select(message => message.Text));
        Assert.Equal(["and the seventh"], door.Told[^1].Queued.Select(message => message.Text));
        door.EndTurn(0);
        await Poll.Until(() => door.Took.Count == 2, () => "the word left in the queue never became its turn");
        Assert.Equal("and the seventh", door.Took[1]);
    }

    /// <summary>QUEUEEDIT1a: an edit replaces the words where they wait, keeping the order and the files.</summary>
    [Fact]
    public async Task A_queued_word_is_replaced_in_place_keeping_its_place_and_its_files()
    {
        var door = new Door { TakesWordsMidTurn = false };
        var turns = door.Turns();
        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");
        turns.Take(new ChatMessage("and the sixth", [new KeptFile("six.txt", "kept/six.txt")]));
        turns.Take(new ChatMessage("and the seventh", []));
        var first = turns.State.Queued[0];

        Assert.Equal(QueueEdit.Done, turns.EditQueued(first.Id!, "and the eighth instead"));

        var now = turns.State.Queued;
        Assert.Equal(["and the eighth instead", "and the seventh"], now.Select(message => message.Text));
        Assert.Equal(["six.txt"], now[0].Files.Select(file => file.Name));
        Assert.NotEqual(first.Id, now[0].Id);
        door.EndTurn(0);
        await Poll.Until(() => door.Took.Count == 2, () => "the edited word never became its turn");
        Assert.Equal("and the eighth instead", door.Took[1]);
    }

    /// <summary>QUEUEEDIT1a: a word a turn already took is no longer the queue's: a normal answer, never an error.</summary>
    [Fact]
    public async Task A_word_a_turn_already_took_is_answered_taken()
    {
        var door = new Door { TakesWordsMidTurn = false };
        var turns = door.Turns();
        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");
        turns.Take(new ChatMessage("and the sixth", []));
        var sixth = turns.State.Queued[0].Id!;
        door.EndTurn(0);
        await Poll.Until(() => door.Took.Count == 2, () => "the waiting word never became its own turn");

        Assert.Equal(QueueEdit.Taken, turns.EditQueued(sixth, text: null));
        Assert.Equal(QueueEdit.Taken, turns.EditQueued("said-nothing", "words"));
        Assert.Equal("and the sixth", door.Took[1]);
    }

    /// <summary>
    /// QUEUEEDIT1a: the word taken while the door opens is still the queue's (a stop withdraws it the same way): removed, it is
    /// never sent; edited, the edited words are.
    /// </summary>
    [Fact]
    public async Task A_word_taken_while_the_door_opens_is_removed_or_edited_before_it_is_sent()
    {
        var opening = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var took = new List<string>();
        var turns = new ChatTurns(
            ready: () => opening.Task,
            take: (message, sent) =>
            {
                lock (took) took.Add(message.Text);
                sent();
                return Task.CompletedTask;
            },
            interrupt: () => Task.CompletedTask,
            changed: _ => { });
        turns.Take(new ChatMessage("first", []));
        turns.Take(new ChatMessage("second", []));
        await Poll.Until(() => turns.State.Opening, () => "the door never said it was opening");
        var holding = turns.State.Queued[0];
        Assert.Equal("first", holding.Text);

        Assert.Equal(QueueEdit.Done, turns.EditQueued(holding.Id!, "first, said better"));
        Assert.Equal(["first, said better", "second"], turns.State.Queued.Select(message => message.Text));
        var edited = turns.State.Queued[0].Id!;
        Assert.Equal(QueueEdit.Done, turns.EditQueued(turns.State.Queued[1].Id!, text: null));
        Assert.Equal(["first, said better"], turns.State.Queued.Select(message => message.Text));

        opening.SetResult(true);
        await Poll.Until(() => { lock (took) return took.Count == 1; }, () => "the edited word never reached the door");
        lock (took) Assert.Equal(["first, said better"], took);
        Assert.Equal(QueueEdit.Taken, turns.EditQueued(edited, text: null));
    }

    /// <summary>
    /// QUEUEEDIT1a: the id is the queue's view alone: the word a turn takes carries none, so a conversation's record is
    /// shaped as it was (D76: its queued words are not in the record until taken).
    /// </summary>
    [Fact]
    public async Task The_id_is_not_carried_into_the_turn_the_word_becomes()
    {
        var ids = new List<string?>();
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var turns = new ChatTurns(
            ready: () => Task.FromResult(true),
            take: (message, sent) =>
            {
                lock (ids) ids.Add(message.Id);
                sent();
                return ended.Task;
            },
            interrupt: () => Task.CompletedTask,
            changed: _ => { });
        turns.Take(new ChatMessage("first", []));
        turns.Take(new ChatMessage("second", []));
        ended.SetResult();
        await Poll.Until(() => { lock (ids) return ids.Count == 2; }, () => "the turns never both ran");

        Assert.All(ids, id => Assert.Null(id));
    }

    /// <summary>
    /// 🔴 A move at a turn's end is never mid-turn (SWITCH1c-ii, the switch design §4): the turns are held only where nothing runs
    /// or waits, and while held a word takes nothing, so no turn starts that the move's end would cut. Released, they take words
    /// again.
    /// </summary>
    [Fact]
    public async Task Idle_turns_are_held_for_a_move_and_take_no_word_until_released()
    {
        var door = new Door();
        var turns = door.Turns();

        Assert.True(turns.Hold());

        Assert.Null(turns.Take(new ChatMessage("one more thing", [])));
        Assert.False(turns.Running);
        Assert.Empty(door.Took);

        turns.Release();
        Assert.Equal(TurnReach.Now, turns.Take(new ChatMessage("one more thing", [])));
        await Poll.Until(() => door.Took.Count == 1, () => "the word said once released never reached the door");
    }

    /// <summary>A turn that runs, or a word that waits, is never held: the move waits for that turn's end (SWITCH1c-ii).</summary>
    [Fact]
    public async Task Turns_that_run_or_hold_a_waiting_word_are_not_held()
    {
        var door = new Door { TakesWordsMidTurn = false };
        var turns = door.Turns();
        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");

        Assert.False(turns.Hold());
        Assert.Equal(TurnReach.TurnEnd, turns.Take(new ChatMessage("and the sixth", [])));
        Assert.False(turns.Hold());

        door.EndTurn(0);
        await Poll.Until(() => door.Took.Count == 2, () => "the waiting word never became its own turn");
        Assert.False(turns.Hold());

        door.EndTurn(1);
        await Poll.Until(() => !turns.Running, () => "the second turn never ended");
        Assert.True(turns.Hold());
    }

    // ——— COWORK1c: the conversation's queue is a view of its record's `said`.

    /// <summary>
    /// COWORK1c (the message-urgency design §6, trap 20): words said to a running conversation are kept on its record first and wait
    /// under the record's ids, in its order, through its one dispatcher, though the first word's write answered last. Each leaves the
    /// record by its id once its turn is on the wire, and the turns finish only once those take-offs have landed.
    /// </summary>
    [Fact]
    public async Task Two_words_kept_at_once_wait_in_the_records_order_and_leave_it_once_on_the_wire()
    {
        var door = new Door { TakesWordsMidTurn = false };
        var turns = door.Turns();
        var record = new Daoris.Driver.Tests.StandInSaid();
        var first = new TaskCompletionSource();
        record.Answering = word => word.Text == "first" ? first.Task : Task.CompletedTask;
        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");

        var keepingFirst = turns.KeepAsync(record, new ChatMessage("first", []), default);
        var keepingSecond = turns.KeepAsync(record, new ChatMessage("second", []), default);
        await Task.Delay(50);
        first.SetResult();
        var kept = await Task.WhenAll(keepingFirst, keepingSecond);

        Assert.Equal([("w1", TurnReach.TurnEnd), ("w2", TurnReach.TurnEnd)], kept.Select(each => (each!.Word, each.Reach)));
        Assert.Equal(["w1", "w2"], turns.State.Queued.Select(message => message.Id));
        Assert.Equal(["w1", "w2"], record.Ids);

        door.EndTurn(0);
        await Poll.Until(() => door.Took.Count == 2, () => "the first kept word never became its turn");
        door.EndTurn(1);
        await Poll.Until(() => door.Took.Count == 3, () => "the second kept word never became its turn");
        door.EndTurn(2);
        await turns.FinishAsync();

        Assert.Equal(["read the five files", "first", "second"], door.Took);
        Assert.Equal([null, "w1", "w2"], door.TookIds);
        Assert.Equal(["w1", "w2"], record.TakenOff);
        Assert.Empty(record.Said);
    }

    /// <summary>
    /// COWORK1c: a kept word handed to the turn on the wire at its next step goes under the record's id, never one of the door's own,
    /// and leaves the record once handed.
    /// </summary>
    [Fact]
    public async Task A_kept_word_handed_to_the_turn_at_its_next_step_goes_under_the_records_id_and_leaves_it()
    {
        var door = new Door();
        var turns = door.Turns();
        var record = new Daoris.Driver.Tests.StandInSaid();
        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");

        var kept = await turns.KeepAsync(record, new ChatMessage("put PINEAPPLE after the words", []), default);
        await turns.TakingOff;

        Assert.Equal((TurnReach.NextStep, "w1"), (kept!.Reach, kept.Word));
        Assert.Equal(["w1"], door.SteeredIds);
        Assert.Equal(["w1"], record.Attempted);
        Assert.Equal(["w1"], record.TakenOff);
    }

    /// <summary>
    /// COWORK1c (the design §6): a kept word waiting as a turn of its own is marked attempted on the record before its turn reaches the
    /// door, and leaves the record once its turn is on the wire; a word that never reached the door is never marked.
    /// </summary>
    [Fact]
    public async Task A_kept_word_is_attempted_before_its_turn_reaches_the_door()
    {
        var door = new Door { TakesWordsMidTurn = false };
        var record = new Daoris.Driver.Tests.StandInSaid();
        var attemptedAtTake = new List<bool>();
        door.OnTake = message => attemptedAtTake.Add(message.Id is { } id && record.Attempted.Contains(id));
        var turns = door.Turns();
        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");

        await turns.KeepAsync(record, new ChatMessage("first", []), default);
        await turns.KeepAsync(record, new ChatMessage("second", []), default);
        Assert.Empty(record.Attempted);

        door.EndTurn(0);
        await Poll.Until(() => door.Took.Count == 2, () => "the first kept word never became its turn");
        await turns.TakingOff;

        Assert.Equal([false, true], attemptedAtTake);
        Assert.Equal(["w1"], record.Attempted);
        Assert.Equal(["w1"], record.TakenOff);
        Assert.Equal(["w2"], record.Ids);
    }

    /// <summary>
    /// COWORK1c (the design §6): kept words a stop withdraws leave the record with them, handed back to the person; kept words waiting
    /// when the harness went stay on the record, for the conversation to go on with as the shell comes up again, unless the person
    /// stopped it, which ends them with it. Turns that take no more words keep nothing.
    /// </summary>
    [Fact]
    public async Task Kept_words_a_stop_withdraws_leave_the_record_and_those_left_when_the_harness_went_stay()
    {
        var door = new Door { TakesWordsMidTurn = false };
        var record = new Daoris.Driver.Tests.StandInSaid();
        var turns = door.Turns();
        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 1, () => "the first turn never reached the door");

        await turns.KeepAsync(record, new ChatMessage("first", []), default);
        var stop = await turns.StopAsync();
        await turns.TakingOff;
        Assert.Equal(["w1"], stop.Withdrawn.Select(message => message.Id));
        Assert.Equal(["w1"], record.TakenOff);

        await turns.KeepAsync(record, new ChatMessage("second", []), default);
        turns.Gone();
        await turns.TakingOff;
        Assert.Equal(["w2"], record.Ids);
        Assert.Null(await turns.KeepAsync(record, new ChatMessage("too late", []), default));
        Assert.Equal(["w2"], record.Ids);

        var stopped = door.Turns();
        stopped.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Took.Count == 2, () => "the other conversation's first turn never reached the door");
        await stopped.KeepAsync(record, new ChatMessage("third", []), default);
        stopped.Gone(personStopped: true);
        await stopped.TakingOff;
        Assert.Equal(["w2"], record.Ids);
        Assert.Equal(["w1", "w3"], record.TakenOff);
    }

    /// <summary>
    /// A conversation note's codes are a twin (MSG1c3; <c>.claude/knowledge/twins.md</c>): every code the driver declares is
    /// the page's <c>work.conversation.&lt;code&gt;</c>, worded in both catalogues, so a line the driver codes never reaches a
    /// 中文 window as its English. The page's <c>work/conversation.test.ts</c> parses the declarations from its side.
    /// </summary>
    [Theory]
    [InlineData("en")]
    [InlineData("zh")]
    public void Every_code_a_conversations_note_carries_is_worded_in_both_catalogues(string language)
    {
        var catalogue = NoteCodesTests.Catalogue(language);

        Assert.Equal([SessionEventCodes.Lost, SessionEventCodes.Withdrawn, SessionEventCodes.Dropped], SessionEventCodes.All);
        foreach (var code in SessionEventCodes.All)
        {
            Assert.Matches("^[a-z]+(-[a-z]+)*$", code);
            // A conversation's coded line says no value: the facts it needs (the word's id) ride beside it.
            Assert.Empty(NoteCodesTests.Placeholders(NoteCodesTests.Entry(catalogue, $"work.conversation.{code}", language)));
        }
    }
}
