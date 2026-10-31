using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A chat a terminal runs, seen from the window (CLITURN1): the terminal creates its own runner, so the window holds nothing
/// for it. What the owner told is shown; what it did not tell is unknown, never idle; and a press the window cannot carry out
/// is routed to the owner or refused in words, never answered as if nothing ran.
/// </summary>
public sealed class DriverModuleTerminalTurnTests : DriverModuleBridge
{
    private const string Session = "c1a2b3d4";

    /// <summary>The session's process, marked as a terminal's driver marks the harness it started: this very process stands for it.</summary>
    private void ARunningTerminalChat(DriverLoop loop)
    {
        var markers = DaorisHome.Entry(loop.Home, HomeEntry.SessionPids);
        Directory.CreateDirectory(markers);
        using var self = Process.GetCurrentProcess();
        File.WriteAllText(Path.Combine(markers, Session + ".pid"), $"{self.Id} {self.StartTime.ToUniversalTime().Ticks}");
    }

    private static ChatQueue Turn(bool taking) => new(taking, []);

    [Fact]
    public async Task A_turn_the_terminal_told_is_shown_while_it_runs_and_when_it_ends()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        ARunningTerminalChat(loop);
        var marks = new TurnMarks(loop.Home);

        marks.Write(Session, Turn(taking: true));
        var running = await AnswerAsync(module, "SESSION_QUEUE", new { id = Session });
        Assert.True(running.GetProperty("taking").GetBoolean());

        var ended = new DateTimeOffset(2026, 10, 26, 23, 24, 2, TimeSpan.Zero);
        marks.Write(Session, Turn(taking: false) with { LastTurnEnded = ended });
        var between = await AnswerAsync(module, "SESSION_QUEUE", new { id = Session });
        Assert.False(between.GetProperty("taking").GetBoolean());
        Assert.Equal(ended, between.GetProperty("lastTurn").GetDateTimeOffset());
    }

    [Fact]
    public async Task A_chat_another_process_runs_that_told_nothing_is_not_said_to_be_idle()
    {
        var loop = Loop();
        ARunningTerminalChat(loop);

        var queue = await AnswerAsync(new DriverModule(Bus, loop), "SESSION_QUEUE", new { id = Session });

        // The page reads a `taking` that is not a boolean as no answer, and keeps the record's word.
        var kind = queue.TryGetProperty("taking", out var taking) ? taking.ValueKind : JsonValueKind.Null;
        Assert.Equal(JsonValueKind.Null, kind);
    }

    [Fact]
    public async Task A_mark_with_nothing_running_the_chat_claims_no_turn()
    {
        var loop = Loop();
        new TurnMarks(loop.Home).Write(Session, Turn(taking: true));

        var queue = await AnswerAsync(new DriverModule(Bus, loop), "SESSION_QUEUE", new { id = Session });

        Assert.False(queue.GetProperty("taking").GetBoolean());
    }

    [Fact]
    public async Task The_window_does_not_say_no_turn_ran_for_a_turn_it_cannot_stop()
    {
        var loop = Loop();
        ARunningTerminalChat(loop);
        new TurnMarks(loop.Home).Write(Session, Turn(taking: true));

        var refusal = await RefusalAsync(new DriverModule(Bus, loop), "CANCEL_TURN", new { id = Session });

        Assert.Contains(Refusals.DriverRefused, refusal);
        Assert.Contains("another Daoris process", refusal);
    }

    [Fact]
    public async Task A_stop_of_a_chat_a_terminal_runs_is_taken_by_the_terminal_that_owns_it()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        ARunningTerminalChat(loop);
        var stopped = new List<string>();
        using var leaving = new CancellationTokenSource();
        // The owner: a terminal's request watch, which honours a stop for the one session it runs.
        await using var owner = new SessionRequestWatch(loop.Home, runsHere: id => id == Session, stop: id => { stopped.Add(id); return true; }, service: () => null);
        var honouring = Task.Run(async () =>
        {
            while (!leaving.IsCancellationRequested)
            {
                await owner.HonourAsync();
                await Task.Delay(25);
            }
        });

        var answer = await AnswerAsync(module, "STOP_SESSION", new { id = Session });
        await leaving.CancelAsync();
        await honouring;

        Assert.True(answer.GetProperty("stopped").GetBoolean());
        Assert.False(answer.GetProperty("elsewhere").GetBoolean());
        Assert.Equal([Session], stopped);
    }
}
