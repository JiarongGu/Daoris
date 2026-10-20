using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ACCESSSWAP1 (D183 point 2 and its ACCESSSWAP1 note, after HELPMODE1a5): every change of Ask Daoris's access applies at its running
/// conversation's next turn. A mode widened or narrowed, or a housekeeping grant given or taken back, ends the conversation's agent at its
/// turn's end, never mid-turn, and at once where it is idle; its next words resume the same conversation under the access chosen, its
/// history kept (D131). Its console says when, and the conversation counts as pending until its agent has ended. A fake door and the
/// real turn queue, so nothing is spawned.
/// </summary>
public sealed class HelpAccessSwapTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-help-swap-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly SessionOutput _output = new();

    public HelpAccessSwapTests() => Directory.CreateDirectory(_home);

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

    /// <summary>
    /// A conversation running under <paramref name="mode"/>, its start handed <paramref name="housekeeping"/> and
    /// <paramref name="settings"/> (none by default).
    /// </summary>
    private static Chat Running(
        ChatRunner runner, string id, string mode, IReadOnlyList<string>? housekeeping = null, IReadOnlyList<string>? settings = null)
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
        runner.Context.AccessHanded[id] = new ConfiguredAccess(housekeeping ?? []) { Settings = settings ?? [] };
        return chat;
    }

    private static async Task Talking(Chat chat)
    {
        chat.Turns.Say(new ChatMessage("go", []));
        await Poll.Until(() => chat.Turns.Running, () => "the turn never started");
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
    /// 🔴 ACCESSSWAP1: a widening no longer waits for a next start. Restricted to full while a turn runs ends the conversation at its
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

    /// <summary>🔴 ACCESSSWAP1: a housekeeping grant given while a turn runs takes its next turn the same way, naming what it grants.</summary>
    [Fact]
    public async Task A_grant_given_ends_a_running_conversation_at_its_turn_s_end()
    {
        var runner = Runner();
        var chat = Running(runner, "h1", HelpModes.Restricted);
        await Talking(chat);

        ConfiguredAccess.Choose(_home, ConfiguredAccess.HousekeepingGrant, ConfiguredAccess.Inspect, chosen: true);
        runner.HelpAccessChosen(HelpModes.Restricted);

        Assert.Equal(0, chat.Finished);
        Assert.Contains("its housekeeping grants `inspect` now", Console("h1"));
        Assert.DoesNotContain("its mode is now", Console("h1"));

        chat.Turn.SetResult();
        await Poll.Until(() => chat.Finished == 1, () => "the turn's end never ended it");
    }

    /// <summary>🔴 ACCESSSWAP1: a grant taken back ends an idle conversation at once (D183's HELPMODE1c2a note's "a revoke ends no conversation" overturned).</summary>
    [Fact]
    public async Task A_grant_taken_back_ends_an_idle_conversation_at_once()
    {
        var runner = Runner();
        var chat = Running(runner, "h1", HelpModes.Restricted, [ConfiguredAccess.Inspect]);

        runner.HelpAccessChosen(HelpModes.Restricted);

        await Poll.Until(() => chat.Finished == 1, () => "the idle conversation was never ended");
        Assert.Contains("its housekeeping grants nothing now", Console("h1"));
        Assert.Contains("ends now", Console("h1"));
    }

    /// <summary>
    /// 🔴 HELPMODE1c3: a chosen setting given while a turn runs takes its next turn the same way, naming the settings it now has, and
    /// one taken back ends an idle conversation at once.
    /// </summary>
    [Fact]
    public async Task A_chosen_setting_given_or_taken_back_takes_the_next_turn()
    {
        var runner = Runner();
        var talking = Running(runner, "h1", HelpModes.Restricted);
        var idle = Running(runner, "h2", HelpModes.Restricted, settings: [ConfiguredAccess.Notify]);
        await Talking(talking);

        ConfiguredAccess.Choose(_home, ConfiguredAccess.SettingsGrant, ConfiguredAccess.Cap, chosen: true);
        runner.HelpAccessChosen(HelpModes.Restricted);

        Assert.Equal(0, talking.Finished);
        Assert.Contains("its chosen settings are `cap` now", Console("h1"));
        Assert.DoesNotContain("housekeeping", Console("h1"));
        await Poll.Until(() => idle.Finished == 1, () => "the idle conversation was never ended");
        Assert.Contains("its chosen settings are `cap` now", Console("h2"));

        talking.Turn.SetResult();
        await Poll.Until(() => talking.Finished == 1, () => "the turn's end never ended it");
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

    /// <summary>ACCESSSWAP1: a grant taken back again before the turn ends withdraws the end, but a mode still changed keeps it.</summary>
    [Fact]
    public async Task A_grant_taken_back_again_withdraws_the_end_only_where_nothing_else_changed()
    {
        var runner = Runner();
        var same = Running(runner, "h1", HelpModes.Restricted);
        var widened = Running(runner, "h2", HelpModes.Full);
        await Talking(same);
        await Talking(widened);

        ConfiguredAccess.Choose(_home, ConfiguredAccess.HousekeepingGrant, ConfiguredAccess.Inspect, chosen: true);
        runner.HelpAccessChosen(HelpModes.Restricted);
        ConfiguredAccess.Choose(_home, ConfiguredAccess.HousekeepingGrant, ConfiguredAccess.Inspect, chosen: false);
        runner.HelpAccessChosen(HelpModes.Restricted);
        same.Turn.SetResult();
        widened.Turn.SetResult();

        await Poll.Until(() => widened.Finished == 1, () => "the conversation whose mode still changed was never ended");
        await Task.Delay(100);
        Assert.Equal(0, same.Finished);
    }

    /// <summary>ACCESSSWAP1: a conversation already under the access chosen is told nothing and goes on.</summary>
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
    /// ACCESSSWAP1: what the page's pending marks read. A conversation running under another access than the one chosen is pending, and
    /// a grant is pending while any running conversation was handed another choice of it, each grant on its own; neither is, once each
    /// runs under the access chosen.
    /// </summary>
    [Fact]
    public void The_conversations_under_another_access_are_pending()
    {
        var runner = Runner();
        Running(runner, "h1", HelpModes.Restricted);
        Running(runner, "h2", HelpModes.Restricted, [ConfiguredAccess.Inspect]);
        Running(runner, "h3", HelpModes.Full);

        Assert.Equal(["h2", "h3"], runner.HelpSwapping(HelpModes.Restricted));
        Assert.True(runner.GrantPending(ConfiguredAccess.HousekeepingGrant, ConfiguredAccess.None));
        Assert.False(runner.GrantPending(ConfiguredAccess.SettingsGrant, ConfiguredAccess.None));
        Assert.Equal(["h1", "h2"], runner.HelpSwapping(HelpModes.Full));

        runner.Context.AccessHanded["h2"] = ConfiguredAccess.None;
        Assert.False(runner.GrantPending(ConfiguredAccess.HousekeepingGrant, ConfiguredAccess.None));
        Assert.True(runner.GrantPending(ConfiguredAccess.HousekeepingGrant, new ConfiguredAccess([ConfiguredAccess.Inspect])));
        Assert.True(runner.GrantPending(ConfiguredAccess.SettingsGrant, ConfiguredAccess.None with { Settings = [ConfiguredAccess.Cap] }));
    }
}
