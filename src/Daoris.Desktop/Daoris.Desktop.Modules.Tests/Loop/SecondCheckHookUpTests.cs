using System.Diagnostics;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Tests;

/// <summary>
/// The second check hooked up in the window's loop: Ask Daoris's presses, as the press record tells them, and its calls held for the
/// person and answered are kept for the selection beside its reports, so a turn that pressed with no card is selected with its press,
/// and an answered call is the person's yes; a hold of a session that is no Ask Daoris conversation is kept nowhere.
/// </summary>
public sealed class SecondCheckHookUpTests : IDisposable
{
    private static readonly DateTimeOffset At = DateTimeOffset.UtcNow.AddMinutes(1);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "second-check-hook-up", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private static async Task Until(Func<bool> done)
    {
        var watch = Stopwatch.StartNew();
        while (!done() && watch.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(10);
        Assert.True(done());
    }

    private static IpcRequest Press(string route, string payload) => new()
    {
        Module = "DAORIS",
        Type = route,
        Payload = System.Text.Json.JsonDocument.Parse(payload).RootElement.Clone(),
    };

    [Fact]
    public async Task A_press_with_no_card_is_kept_and_selects_its_turn_while_an_answered_hold_is_the_person_s_yes()
    {
        new HelpWired(_home).Keep("s1a2b3c4", new HelpReceipt { At = At, Door = "claude-code", Folder = "room" });
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        var presses = new HelpPressRecord(TimeProvider.System);
        var held = HeldPermissions.Of(events);
        using var hookUp = SecondCheckHookUp.Start(_home, events, presses, held);
        await hookUp.CaughtUp;

        events.Append("s1a2b3c4", new SessionEvent { Kind = SessionEventKind.User, Id = "w1", Text = "pause it", Origin = "person", At = DateTimeOffset.UtcNow });
        var press = presses.Began(Press("WORK_PAUSE", """{"quest":"q1"}"""), "s1a2b3c4");
        presses.Ended(press, succeeded: true);
        var asked = held.For("s1a2b3c4").Hold("r1", "toolu_7", "Bash", [new PermissionChoice("allow", "Allow once", PermissionChoice.AllowOnce)], "execute")!;
        held.Answer("s1a2b3c4", "r1", "allow");
        await asked;
        var other = held.For("d9e8f7a6").Hold("r2", "toolu_8", "Bash", [new PermissionChoice("allow", "Allow once", PermissionChoice.AllowOnce)], "execute")!;
        held.Answer("d9e8f7a6", "r2", "allow");
        await other;
        events.Append("s1a2b3c4", new SessionEvent { Kind = SessionEventKind.Tool, Id = "toolu_7", ToolKind = "execute", Title = "git push", At = DateTimeOffset.UtcNow });
        await Until(() => new AskDaorisKept(_home).Answered("s1a2b3c4").Count == 1 && new AskDaorisKept(_home).Presses("s1a2b3c4") is [{ Outcome: HelpPressRecord.Done }]);
        events.Append("s1a2b3c4", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn", At = DateTimeOffset.UtcNow.AddSeconds(1) });

        await Until(() => new AskDaorisReports(_home).Of("s1a2b3c4").Count == 1);
        var turn = Assert.Single(Assert.Single(new AskDaorisReports(_home).Of("s1a2b3c4")).Turns);
        Assert.Equal([("WORK_PAUSE", HelpPressRecord.Done, false)], turn.Presses.Select(each => (each.Route, each.Outcome, each.Yours)));
        Assert.Equal(["toolu_7"], turn.Yours);
        Assert.Empty(turn.Calls);
        Assert.Empty(new AskDaorisKept(_home).Answered("d9e8f7a6"));
    }

    [Fact]
    public void A_press_s_place_is_kept_in_the_terminal_s_spelling()
    {
        var now = new HelpPressNow("p1", "DAORIS", "SET_HOLD", "s1a2b3c4", "repository", "atlas", At, HelpPressRecord.Running)
        {
            Place = HelpFocus.OfRepository("games", "atlas").Wire(),
        };

        Assert.Equal(new KeptPress("p1", "s1a2b3c4", At, "SET_HOLD", "games/atlas", HelpPressRecord.Running, false), SecondCheckHookUp.KeptOf(now));
        Assert.Equal("machine", SecondCheckHookUp.KeptOf(now with { Place = HelpFocus.Machine.Wire() }).Place);
        Assert.Null(SecondCheckHookUp.KeptOf(now with { Place = null }).Place);
    }
}
