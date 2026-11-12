using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every change of Ask Daoris's access applies at its running
/// conversation's next turn. A mode widened or narrowed, or a housekeeping grant given or taken back, ends the conversation's agent at its
/// turn's end, never mid-turn, and at once where it is idle; its next words resume the same conversation under the access chosen, its
/// history kept. Its console says when, and the conversation counts as pending until its agent has ended. A fake door and the
/// real turn queue, so nothing is spawned.
/// </summary>
public sealed class HelpAccessSwapTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-help-swap-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly SessionOutput _output = new();

    public HelpAccessSwapTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A running conversation whose finishing is counted; its turns are the real queue, told to the runner as a start tells it.</summary>
    private sealed class Chat : ITurnedChat
    {
        private int _finished;
        public TaskCompletionSource Turn { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ChatTurns Turns { get; set; } = null!;

        IChatTurns ITurnedChat.Turns => Turns;

        public int Finished => Volatile.Read(ref _finished);

        public Task FinishAsync(Func<Task> close)
        {
            Interlocked.Increment(ref _finished);
            return Task.CompletedTask;
        }
    }

    // The roster's file is this home's, never the one the process's environment names.
    private ChatRunner Runner() => new(
        new ChatLedger().Client(), ProviderRegistry.Built(), _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), _output,
        harnesses: new HarnessRoster(ProviderRegistry.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)));

    /// <summary>
    /// A conversation running under <paramref name="mode"/>, its start handed <paramref name="housekeeping"/> and
    /// <paramref name="settings"/> (none by default).
    /// </summary>
    private static Chat Running(
        ChatRunner runner, string id, string mode, IReadOnlyList<string>? off = null, Func<Task<bool>>? ready = null)
    {
        var chat = new Chat();
        chat.Turns = new ChatTurns(
            ready: ready ?? (() => Task.FromResult(true)),
            take: (_, sent) =>
            {
                sent();
                return chat.Turn.Task;
            },
            interrupt: () => Task.CompletedTask,
            changed: queue => runner.HelpTurnChanged(id, queue));
        runner.Context.Turned[id] = chat;
        runner.Context.HelpModes[id] = mode;
        runner.Context.AccessHanded[id] = new ConfiguredAccess { CheckoutsOff = off ?? [] };
        return chat;
    }

    private static async Task Talking(Chat chat)
    {
        chat.Turns.Say(new ChatMessage("go", []));
        await Poll.Until(() => chat.Turns.Running, () => "the turn never started");
    }

    private string Console(string id) => string.Join('\n', _output.Tail(id).Lines.Select(line => line.Text));

    [Fact]
    public async Task A_focus_waiting_ends_a_busy_conversation_at_its_turn_s_end_and_an_idle_one_now()
    {
        var runner = Runner();
        var busy = Running(runner, "h1", HelpModes.Restricted);
        var idle = Running(runner, "h2", HelpModes.Restricted);
        await Talking(busy);

        runner.HelpFocusChosen("h1", waiting: true);
        runner.HelpFocusChosen("h2", waiting: true);

        await Poll.Until(() => idle.Finished == 1, () => "the idle conversation was never ended");
        Assert.Equal(0, busy.Finished);
        busy.Turn.SetResult();
        await Poll.Until(() => busy.Finished == 1, () => "the turn's end never ended it");
    }

    /// <summary>
    /// The person's first word, said while its agent still opens, is a turn under way: a focus chosen then ends the conversation at that
    /// turn's end, never before its first answer, so the word is answered and kept.
    /// </summary>
    [Fact]
    public async Task A_focus_chosen_before_the_first_answer_keeps_the_word_said_while_the_agent_opens()
    {
        var runner = Runner();
        var opened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var chat = Running(runner, "h1", HelpModes.Restricted, ready: () => opened.Task);
        chat.Turns.Say(new ChatMessage("the ticket", []));

        runner.HelpFocusChosen("h1", waiting: true);
        await Task.Delay(200);
        Assert.Equal(0, chat.Finished);

        opened.SetResult(true);
        await Poll.Until(() => chat.Turns.Running, () => "the first word's turn never started");
        Assert.Equal(0, chat.Finished);
        chat.Turn.SetResult();
        await Poll.Until(() => chat.Finished == 1, () => "the first turn's end never ended it");
    }

    [Fact]
    public async Task The_applied_focus_chosen_again_withdraws_the_end()
    {
        var runner = Runner();
        var busy = Running(runner, "h1", HelpModes.Restricted);
        await Talking(busy);

        runner.HelpFocusChosen("h1", waiting: true);
        runner.HelpFocusChosen("h1", waiting: false);
        busy.Turn.SetResult();

        await Task.Delay(200);
        Assert.Equal(0, busy.Finished);
    }

    /// <summary>
    /// 🔴 HELPMODE1a5: full to restricted while a turn runs says on its console that it ends at its turn's end, and it is not ended
    /// mid-turn; the turn's end ends it, once.
    /// </summary>
    [Fact]
    public async Task A_narrowing_switch_ends_a_running_conversation_at_its_turn_s_end_and_not_mid_turn()
    {
        var runner = Runner();
        var chat = Running(runner, "h1", HelpModes.Full);
        await Talking(chat);

        runner.HelpAccessChosen(HelpModes.Restricted);

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

        runner.HelpAccessChosen(HelpModes.Restricted);

        await Poll.Until(() => chat.Finished == 1, () => "the idle conversation was never ended");
        Assert.Contains("its mode is now restricted", Console("h1"));
        Assert.Contains("ends now", Console("h1"));
    }

    /// <summary>
    /// A widening does not wait for a next start. Restricted to full while a turn runs ends the conversation at its
    /// turn's end, never mid-turn, and its console says its next words resume it under the new access.
    /// </summary>
    [Fact]
    public async Task A_widening_switch_ends_a_running_conversation_at_its_turn_s_end()
    {
        var runner = Runner();
        var chat = Running(runner, "h1", HelpModes.Restricted);
        await Talking(chat);

        runner.HelpAccessChosen(HelpModes.Full);

        Assert.Equal(0, chat.Finished);
        Assert.Contains("its mode is now full access", Console("h1"));
        Assert.Contains("ends at its turn's end, never mid-turn", Console("h1"));
        Assert.Contains("your next words resume this conversation under the new access, its history kept", Console("h1"));
        Assert.DoesNotContain("next start", Console("h1"));

        chat.Turn.SetResult();
        await Poll.Until(() => chat.Finished == 1, () => "the turn's end never ended it");
        await Task.Delay(100);
        Assert.Equal(1, chat.Finished);
    }

    /// <summary>A swap forgets the conversation's delegate key at once; one whose access did not change keeps its own.</summary>
    [Fact]
    public async Task A_swap_forgets_the_conversation_s_delegate_key_and_leaves_the_others()
    {
        var runner = Runner();
        var chat = Running(runner, "h1", HelpModes.Full);
        Running(runner, "h2", HelpModes.Restricted);
        var processes = runner.Context.Processes;
        processes.Delegates.Draw("h1");
        processes.Delegates.Draw("h2");
        await Talking(chat);

        runner.HelpAccessChosen(HelpModes.Restricted);

        Assert.Empty(processes.Delegates.Of("h1"));
        Assert.Single(processes.Delegates.Of("h2"));
        chat.Turn.SetResult();
    }

    /// <summary>A full-access press is said on its own conversation's console; its run's end forgets its keys and its mode together.</summary>
    [Fact]
    public void A_full_access_press_is_said_on_its_conversation_and_its_run_s_end_forgets_its_keys()
    {
        var runner = Runner();
        Running(runner, "h1", HelpModes.Full);
        Running(runner, "h2", HelpModes.Full);
        runner.Context.Processes.Delegates.Draw("h1");

        runner.HelpPressed("h1", "DRIVER.WORK_PAUSE");

        Assert.Contains("DRIVER.WORK_PAUSE", Console("h1"));
        Assert.Contains("as your own press", Console("h1"));
        Assert.DoesNotContain("WORK_PAUSE", Console("h2"));
        Assert.Equal(HelpModes.Full, runner.HelpModeOf("h1"));

        runner.Executors.Of<HelpAccessSwap>().Forget("h1");

        Assert.Empty(runner.Context.Processes.Delegates.Of("h1"));
        Assert.Null(runner.HelpModeOf("h1"));
    }

    /// <summary>A family of acts granted while a turn runs is read at the next press, so it ends no conversation and is said nowhere.</summary>
    [Fact]
    public async Task A_family_granted_ends_no_running_conversation()
    {
        var runner = Runner();
        var chat = Running(runner, "h1", HelpModes.Restricted);
        await Talking(chat);

        ConfiguredAccess.Choose(_home, ConfiguredAccess.ActsGrant, ActFamilies.Settings, chosen: true);
        runner.HelpAccessChosen(HelpModes.Restricted);
        chat.Turn.SetResult();
        await Task.Delay(100);

        Assert.Equal(0, chat.Finished);
        Assert.Equal("", Console("h1"));
    }

    /// <summary>A grant taken back ends an idle conversation at once.</summary>
    [Fact]
    public async Task A_grant_taken_back_ends_an_idle_conversation_at_once()
    {
        var runner = Runner();
        var chat = Running(runner, "h1", HelpModes.Restricted, ["engine"]);

        runner.HelpAccessChosen(HelpModes.Restricted);

        await Poll.Until(() => chat.Finished == 1, () => "the idle conversation was never ended");
        Assert.Contains("it reads every checkout it may read now", Console("h1"));
        Assert.Contains("ends now", Console("h1"));
    }

    /// <summary>
    /// 🔴 HELPMODE1c4: a checkout turned off while a turn runs takes its next turn the same way, naming the checkouts turned off, and
    /// marks the grant pending until then; turned on again before the turn ends, the end is withdrawn.
    /// </summary>
    [Fact]
    public async Task A_checkout_turned_off_takes_the_next_turn_and_is_pending_till_then()
    {
        var runner = Runner();
        var talking = Running(runner, "h1", HelpModes.Restricted);
        await Talking(talking);

        var off = ConfiguredAccess.Choose(_home, ConfiguredAccess.CheckoutsGrant, "engine", chosen: false);
        runner.HelpAccessChosen(HelpModes.Restricted);

        Assert.Equal(0, talking.Finished);
        Assert.Contains("the checkouts turned off for it are `engine` now", Console("h1"));
        Assert.True(runner.GrantPending(ConfiguredAccess.CheckoutsGrant, off));
        Assert.False(runner.GrantPending(ConfiguredAccess.ActsGrant, off));

        ConfiguredAccess.Choose(_home, ConfiguredAccess.CheckoutsGrant, "engine", chosen: true);
        runner.HelpAccessChosen(HelpModes.Restricted);
        talking.Turn.SetResult();
        await Poll.Until(() => !talking.Turns.Running, () => "the turn never ended");
        Assert.Equal(0, talking.Finished);
    }

    /// <summary>HELPMODE1a5: a switch back to the mode it runs under, before the turn ends, withdraws the end.</summary>
    [Fact]
    public async Task Choosing_full_again_before_the_turn_ends_withdraws_the_end()
    {
        var runner = Runner();
        var chat = Running(runner, "h1", HelpModes.Full);
        await Talking(chat);

        runner.HelpAccessChosen(HelpModes.Restricted);
        runner.HelpAccessChosen(HelpModes.Full);
        chat.Turn.SetResult();
        await Poll.Until(() => !chat.Turns.Running, () => "the turn never ended");
        await Task.Delay(100);

        Assert.Equal(0, chat.Finished);
    }

    /// <summary>A grant taken back again before the turn ends withdraws the end, but a mode still changed keeps it.</summary>
    [Fact]
    public async Task A_grant_taken_back_again_withdraws_the_end_only_where_nothing_else_changed()
    {
        var runner = Runner();
        var same = Running(runner, "h1", HelpModes.Restricted);
        var widened = Running(runner, "h2", HelpModes.Full);
        await Talking(same);
        await Talking(widened);

        ConfiguredAccess.Choose(_home, ConfiguredAccess.CheckoutsGrant, "engine", chosen: false);
        runner.HelpAccessChosen(HelpModes.Restricted);
        ConfiguredAccess.Choose(_home, ConfiguredAccess.CheckoutsGrant, "engine", chosen: true);
        runner.HelpAccessChosen(HelpModes.Restricted);
        same.Turn.SetResult();
        widened.Turn.SetResult();

        await Poll.Until(() => widened.Finished == 1, () => "the conversation whose mode still changed was never ended");
        await Task.Delay(100);
        Assert.Equal(0, same.Finished);
    }

    /// <summary>A conversation already under the access chosen is told nothing and goes on.</summary>
    [Fact]
    public async Task A_conversation_under_the_access_chosen_is_told_nothing_and_not_ended()
    {
        var runner = Runner();
        var chat = Running(runner, "h1", HelpModes.Restricted);

        runner.HelpAccessChosen(HelpModes.Restricted);
        await Task.Delay(100);

        Assert.Equal(0, chat.Finished);
        Assert.Equal("", Console("h1"));
    }

    /// <summary>
    /// What the page's pending marks read. A conversation running under another access than the one chosen is pending, and
    /// a grant is pending while any running conversation is handed another choice of it, each grant on its own; neither is, once each
    /// runs under the access chosen.
    /// </summary>
    [Fact]
    public void The_conversations_under_another_access_are_pending()
    {
        var runner = Runner();
        Running(runner, "h1", HelpModes.Restricted);
        Running(runner, "h2", HelpModes.Restricted, ["engine"]);
        Running(runner, "h3", HelpModes.Full);

        Assert.Equal(["h2", "h3"], runner.HelpSwapping(HelpModes.Restricted));
        Assert.True(runner.GrantPending(ConfiguredAccess.CheckoutsGrant, ConfiguredAccess.None));
        Assert.Equal(["h1", "h2"], runner.HelpSwapping(HelpModes.Full));

        runner.Context.AccessHanded["h2"] = ConfiguredAccess.None;
        Assert.False(runner.GrantPending(ConfiguredAccess.CheckoutsGrant, ConfiguredAccess.None));
        Assert.True(runner.GrantPending(ConfiguredAccess.CheckoutsGrant, ConfiguredAccess.None with { CheckoutsOff = ["engine"] }));
    }
}
