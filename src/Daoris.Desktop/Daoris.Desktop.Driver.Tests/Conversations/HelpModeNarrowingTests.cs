using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HELPMODE1a5 (D183 point 2, the HELPMODE1a2 note): a switch that narrows Ask Daoris's mode (full to restricted) holds at once, since
/// full access is never kept past the person's choice. The conversation running full ends at its turn's end, never mid-turn, an idle
/// one ends now; a widening still takes the next start. A fake door and the real turn queue, so nothing is spawned.
/// </summary>
public sealed class HelpModeNarrowingTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-help-narrow-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly SessionOutput _output = new();

    public HelpModeNarrowingTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A running conversation whose finishing is counted; its turns are the real queue, told to the runner as a start tells it.</summary>
    private sealed class Chat : ChatRunner.ITurnedChat
    {
        private int _finished;
        public TaskCompletionSource Turn { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ChatTurns Turns { get; set; } = null!;

        public int Finished => Volatile.Read(ref _finished);

        public Task FinishAsync(Action endInput)
        {
            Interlocked.Increment(ref _finished);
            return Task.CompletedTask;
        }
    }

    private ChatRunner Runner() => new(
        new ChatLedger().Client(), AdapterSet.Built(), _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), _output);

    private static Chat Running(ChatRunner runner, string id, string mode)
    {
        var chat = new Chat();
        chat.Turns = new ChatTurns(
            ready: () => Task.FromResult(true),
            take: (_, sent) =>
            {
                sent();
                return chat.Turn.Task;
            },
            interrupt: () => Task.CompletedTask,
            changed: queue => runner.HelpTurnChanged(id, queue));
        runner.Context.Turned[id] = chat;
        runner.Context.HelpModes[id] = mode;
        return chat;
    }

    private string Console(string id) => string.Join('\n', _output.Tail(id).Lines.Select(line => line.Text));

    /// <summary>
    /// 🔴 HELPMODE1a5: full to restricted while a turn runs says on its console that it ends at its turn's end, and it is not ended
    /// mid-turn; the turn's end ends it, once.
    /// </summary>
    [Fact]
    public async Task A_narrowing_switch_ends_a_running_conversation_at_its_turn_s_end_and_not_mid_turn()
    {
        var runner = Runner();
        var chat = Running(runner, "h1", HelpModes.Full);
        chat.Turns.Say(new ChatMessage("go", []));
        await Poll.Until(() => chat.Turns.Running, () => "the turn never started");

        runner.HelpModeChosen(HelpModes.Restricted);

        Assert.Contains("ends at its turn's end", Console("h1"));
        Assert.DoesNotContain("next start", Console("h1"));
        Assert.Equal(0, chat.Finished);

        chat.Turn.SetResult();
        await Poll.Until(() => chat.Finished == 1, () => "the turn's end never ended it");
        await Task.Delay(100);
        Assert.Equal(1, chat.Finished);
    }

    /// <summary>🔴 HELPMODE1a5: a conversation idle between turns ends at once, and says so.</summary>
    [Fact]
    public async Task A_narrowing_switch_ends_an_idle_conversation_at_once()
    {
        var runner = Runner();
        var chat = Running(runner, "h1", HelpModes.Full);

        runner.HelpModeChosen(HelpModes.Restricted);

        await Poll.Until(() => chat.Finished == 1, () => "the idle conversation was never ended");
        Assert.Contains("restricted", Console("h1"));
    }

    /// <summary>HELPMODE1a5: a widening changes nothing until the next start (HELPMODE1a2): the console says so, nothing ends.</summary>
    [Fact]
    public async Task A_widening_switch_takes_the_next_start_and_ends_nothing()
    {
        var runner = Runner();
        var chat = Running(runner, "h1", HelpModes.Restricted);
        chat.Turns.Say(new ChatMessage("go", []));
        await Poll.Until(() => chat.Turns.Running, () => "the turn never started");

        runner.HelpModeChosen(HelpModes.Full);
        chat.Turn.SetResult();
        await Poll.Until(() => !chat.Turns.Running, () => "the turn never ended");
        await Task.Delay(100);

        Assert.Contains("next start", Console("h1"));
        Assert.Equal(0, chat.Finished);
    }

    /// <summary>HELPMODE1a5: a switch back to the mode it runs under, before the turn ends, withdraws the end.</summary>
    [Fact]
    public async Task Choosing_full_again_before_the_turn_ends_withdraws_the_end()
    {
        var runner = Runner();
        var chat = Running(runner, "h1", HelpModes.Full);
        chat.Turns.Say(new ChatMessage("go", []));
        await Poll.Until(() => chat.Turns.Running, () => "the turn never started");

        runner.HelpModeChosen(HelpModes.Restricted);
        runner.HelpModeChosen(HelpModes.Full);
        chat.Turn.SetResult();
        await Poll.Until(() => !chat.Turns.Running, () => "the turn never ended");
        await Task.Delay(100);

        Assert.Equal(0, chat.Finished);
    }
}
