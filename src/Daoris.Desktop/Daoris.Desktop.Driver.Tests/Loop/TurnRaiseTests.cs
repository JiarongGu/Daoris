using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A waiting word raised into the running turn, as the edit door does it.</summary>
public sealed class TurnRaiseTests
{
    /// <summary>
    /// 🔴 A waiting word of the person's raised into the answering turn marks its reading as theirs, as a word said at that level does.
    /// </summary>
    [Fact]
    public async Task A_persons_waiting_word_raised_into_the_answering_turn_marks_its_reading()
    {
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stepped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var took = 0;
        var steered = new List<string>();
        var turns = new ChatTurns(
            ready: () => Task.FromResult(true),
            take: (_, sent) =>
            {
                Interlocked.Increment(ref took);
                sent();
                return ended.Task;
            },
            interrupt: () => Task.CompletedTask,
            changed: _ => { },
            steer: message =>
            {
                lock (steered) steered.Add(message.Text);
                return Task.Run(async () =>
                {
                    message.Went?.Invoke();
                    await stepped.Task;
                });
            },
            steerNow: null,
            nextStep: () => true);
        var told = new List<bool>();
        var findings = ChatSendBack.Findings(
            new SaidWordView("f1", "Another agent read your work and claims what follows.", DateTimeOffset.UnixEpoch, [], Reopens: true)
            {
                By = "op1", Level = MessageLevel.Ordinary,
            },
            theirs =>
            {
                lock (told) told.Add(theirs);
                return Task.CompletedTask;
            });

        Assert.Equal(SendBack.Reserved, await turns.SendBackAsync(0, () => 0, _ => Task.FromResult<ChatMessage?>(findings)));
        await Poll.Until(() => Volatile.Read(ref took) == 1, () => "the findings' turn never reached the door");
        Assert.Equal(TurnReach.TurnEnd, turns.Take(new ChatMessage("also check the docs", []) { Level = MessageLevel.Ordinary }));
        Assert.Equal(QueueEdit.Done, turns.EditQueued(turns.State.Queued.Single().Id!, text: null, MessageLevel.Urgent));
        Assert.Equal(["also check the docs"], steered);

        stepped.SetResult();
        ended.SetResult();
        await Poll.Until(() => told.Count == 1, () => "the answering turn's completion was never told");
        Assert.True(told[0]);
        turns.Gone();
    }
}
