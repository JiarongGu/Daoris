using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The second check hooked to live conversations: an Ask Daoris turn's end, heard as its log's <c>turn</c> event, is selected on the
/// watch's own task while the append returns at once, a slow or failing selection holding no turn; another session's turn is never
/// selected; and at a start the conversations' turns not yet seen are selected once from each seen mark, ahead of any turn's end.
/// </summary>
public sealed class AskDaorisWatchTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "ask-daoris-watch", Guid.NewGuid().ToString("N")[..8]);

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

    private SessionEvents Events() => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    private void AskDaoris(string session) => new HelpWired(_home).Keep(session, new HelpReceipt { At = At, Door = "claude-code", Folder = "room" });

    private static void Turn(SessionEvents events, string session, string words, DateTimeOffset at, string kind = "edit")
    {
        events.Append(session, new SessionEvent { Kind = SessionEventKind.User, Id = "w-" + words, Text = words, Origin = "person", At = at });
        events.Append(session, new SessionEvent { Kind = SessionEventKind.Tool, Id = "c-" + words, ToolKind = kind, At = at.AddSeconds(1) });
        events.Append(session, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn", At = at.AddSeconds(2) });
    }

    [Fact]
    public async Task An_Ask_Daoris_turn_s_end_is_selected_off_the_append_and_a_slow_or_failing_selection_holds_no_turn()
    {
        AskDaoris("s1");
        AskDaoris("s2");
        var events = Events();
        using var gate = new SemaphoreSlim(0);
        var selected = new List<string>();
        var failed = new List<string>();
        using var watch = new AskDaorisWatch(_home, events, _ => Task.CompletedTask, async (session, ct) =>
        {
            await gate.WaitAsync(ct);
            if (session == "s1") throw new InvalidDataException("a log that does not read");
            lock (selected) selected.Add(session);
        }, (session, _) => { lock (failed) failed.Add(session); });

        var appending = Stopwatch.StartNew();
        Turn(events, "s1", "slow", At);
        Turn(events, "w1", "driven", At);
        Turn(events, "s2", "next", At);

        Assert.True(appending.Elapsed < TimeSpan.FromSeconds(1));
        Assert.Empty(selected);
        gate.Release(2);
        await Until(() => { lock (selected) return selected.Count == 1; });
        Assert.Equal(["s2"], selected);
        Assert.Equal(["s1"], failed);
    }

    [Fact]
    public async Task A_live_turn_that_acted_is_written_into_its_conversation_s_range_and_one_that_only_read_into_none()
    {
        AskDaoris("s1");
        var events = Events();
        using var watch = AskDaorisWatch.Of(_home, events, _ => [], _ => new HashSet<string>(), () => At.AddMinutes(-1));

        Turn(events, "s1", "read", At, kind: "read");
        Turn(events, "s1", "tidy", At.AddSeconds(10));

        await Until(() => new AskDaorisReports(_home).Of("s1").Count == 1);
        var report = Assert.Single(new AskDaorisReports(_home).Of("s1"));
        Assert.Equal(AskDaorisStates.Disabled, report.State);
        Assert.Equal(["w-tidy"], report.Turns.Select(turn => turn.Id));
    }

    [Fact]
    public async Task At_a_start_the_turns_not_yet_seen_are_selected_once_from_each_seen_mark()
    {
        AskDaoris("s1");
        AskDaoris("s2");
        var events = Events();
        Turn(events, "s1", "seen", At);
        new AskDaorisReports(_home).MarkSeen("s1", events.After("s1", 0).Events.Max(each => each.Seq));
        Turn(events, "s1", "missed", At.AddSeconds(10));
        Turn(events, "s2", "older", At);

        using (var first = AskDaorisWatch.Of(_home, events, _ => [], _ => new HashSet<string>(), () => At.AddHours(1)))
        {
            await first.CaughtUp;
        }

        var report = Assert.Single(new AskDaorisReports(_home).Of("s1"));
        Assert.Equal(["w-missed"], report.Turns.Select(turn => turn.Id));
        Assert.Empty(new AskDaorisReports(_home).Of("s2"));

        using (var again = AskDaorisWatch.Of(_home, events, _ => [], _ => new HashSet<string>(), () => At.AddHours(2)))
        {
            await again.CaughtUp;
        }

        Assert.Single(new AskDaorisReports(_home).Of("s1"));
    }
}
