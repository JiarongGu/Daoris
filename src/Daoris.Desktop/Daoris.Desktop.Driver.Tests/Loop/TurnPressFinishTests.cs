using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A conversation finishing while the person's press holds it.</summary>
public sealed class TurnPressFinishTests
{
    /// <summary>
    /// 🔴 A word waiting behind the person's press starts no turn once the conversation is finishing: its input is closed when the press lets go.
    /// </summary>
    [Fact]
    public async Task A_word_waiting_behind_a_press_starts_no_turn_once_the_conversation_is_finishing()
    {
        var took = new List<string>();
        var turns = new ChatTurns(
            ready: () => Task.FromResult(true),
            take: (message, sent) =>
            {
                lock (took) took.Add(message.Text);
                sent();
                return Task.CompletedTask;
            },
            interrupt: () => Task.CompletedTask,
            changed: _ => { });

        Assert.Equal(PressHold.Held, await turns.PressAsync());
        Assert.Equal(TurnReach.TurnEnd, turns.Take(new ChatMessage("one more thing", [])));
        await turns.FinishAsync();

        turns.Pressed();
        await Task.Delay(200);

        lock (took) Assert.Empty(took);
    }
}
