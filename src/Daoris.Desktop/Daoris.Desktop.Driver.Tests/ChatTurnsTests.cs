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

        public List<ChatQueue> Told { get; } = [];

        /// <summary>Whether a word said during a turn is handed to it: the door's own word, as the agent's promptQueueing is.</summary>
        public bool TakesWordsMidTurn { get; set; } = true;

        public TaskCompletionSource Step { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ChatTurns Turns(bool steers = true) => new(
            ready: () => Task.FromResult(true),
            take: (message, sent) =>
            {
                var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (_gate)
                {
                    Took.Add(message.Text);
                    _turns.Add(ended);
                }

                sent();
                return ended.Task;
            },
            interrupt: () => Task.CompletedTask,
            changed: queue =>
            {
                lock (_gate) Told.Add(queue);
            },
            steer: steers
                ? message =>
                {
                    if (!TakesWordsMidTurn) return null;
                    lock (_gate) Steered.Add(message.Text);
                    return Step.Task;
                }
                : null);

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
}
