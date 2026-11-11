using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The person's *Send now* on a door that takes no word into a running turn but stops one keeping the conversation: the words go
/// first among what waits, the turn is stopped once, and they are the next turn's first words, recorded as sent now. Against a door
/// of lambdas: no process, so the fast half.
/// </summary>
public sealed class TurnCutInTests
{
    private sealed class Door
    {
        private readonly object _gate = new();
        private readonly List<TaskCompletionSource> _turns = [];

        public List<ChatMessage> Took { get; } = [];

        public int Interrupts;

        public ChatTurns Turns(bool cutsIn, bool nextStep = false) => new(
            ready: () => Task.FromResult(true),
            take: (message, sent) =>
            {
                var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (_gate)
                {
                    Took.Add(message);
                    _turns.Add(ended);
                }

                sent();
                return ended.Task;
            },
            interrupt: () =>
            {
                Interlocked.Increment(ref Interrupts);
                return Task.CompletedTask;
            },
            changed: _ => { },
            steer: nextStep ? message => Task.CompletedTask : null,
            nextStep: nextStep ? () => true : null,
            cutsIn: cutsIn);

        public int Count
        {
            get
            {
                lock (_gate) return Took.Count;
            }
        }

        public void EndTurn(int turn)
        {
            lock (_gate) _turns[turn].TrySetResult();
        }
    }

    [Fact]
    public async Task Send_now_on_a_door_that_stops_a_turn_keeping_puts_the_words_first_stops_the_turn_and_makes_them_the_next_turn()
    {
        var door = new Door();
        var turns = door.Turns(cutsIn: true);
        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Count == 1, () => "the first turn never reached the door");
        turns.Take(new ChatMessage("and then the readme", []) { Level = MessageLevel.Ordinary });

        var now = await turns.TakeNowAsync(new ChatMessage("stop: start with the tests", []));

        Assert.Equal(new SentNow(SendNowWhy.NextTurn, Now: true, Why: null), now);
        Assert.Equal(1, door.Interrupts);
        Assert.Equal(["stop: start with the tests", "and then the readme"], turns.State.Queued.Select(message => message.Text));
        door.EndTurn(0);
        await Poll.Until(() => door.Count == 2, () => "the words never went as the next turn");
        var next = door.Took[1];
        Assert.Equal("stop: start with the tests", next.Text);
        Assert.True(next.StoppedFor);
        var asked = Assert.Single(ChatRunner.Recorded(next), recorded => recorded.Kind == SessionEventKind.User);
        Assert.Equal("now", asked.Level);
        turns.Gone();
    }

    [Fact]
    public async Task Send_now_where_the_door_cannot_stop_a_turn_keeping_holds_the_words_for_the_turns_end()
    {
        var door = new Door();
        var turns = door.Turns(cutsIn: false);
        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Count == 1, () => "the first turn never reached the door");

        var now = await turns.TakeNowAsync(new ChatMessage("start with the tests", []));

        Assert.Equal(new SentNow("turn-end", Now: false, SendNowWhy.NoInterrupt), now);
        Assert.Equal(0, door.Interrupts);
        Assert.False(Assert.Single(turns.State.Queued).StoppedFor);
        turns.Gone();
    }

    [Fact]
    public async Task Send_now_on_a_door_that_takes_words_at_the_next_step_never_stops_the_turn()
    {
        var door = new Door();
        var turns = door.Turns(cutsIn: true, nextStep: true);
        turns.Take(new ChatMessage("read the five files", []));
        await Poll.Until(() => door.Count == 1, () => "the first turn never reached the door");

        var now = await turns.TakeNowAsync(new ChatMessage("start with the tests", []));

        Assert.Equal("next-step", now!.Reaches);
        Assert.Equal(0, door.Interrupts);
        turns.Gone();
    }
}
