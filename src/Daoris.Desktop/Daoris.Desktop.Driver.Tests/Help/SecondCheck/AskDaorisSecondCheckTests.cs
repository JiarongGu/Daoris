using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The second check's selection off the turn's path: a turn's end is queued and returns at once while a selection is held slow or fails,
/// and over a conversation's events log and lineage file a turn that pressed through the connector is written into a queued range, or a
/// disabled one while the check is off.
/// </summary>
public sealed class AskDaorisSecondCheckTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "ask-daoris-second-check", Guid.NewGuid().ToString("N")[..8]);

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

    [Fact]
    public async Task A_selection_held_slow_or_failing_never_holds_the_turn_s_reader_and_the_next_still_runs()
    {
        using var gate = new SemaphoreSlim(0);
        var selected = new List<string>();
        var failed = new List<string>();
        using var ends = new AskDaorisTurnEnds(async (session, ct) =>
        {
            await gate.WaitAsync(ct);
            if (session == "bad") throw new InvalidDataException("a log that does not read");
            lock (selected) selected.Add(session);
        }, (session, _) => { lock (failed) failed.Add(session); });

        var watch = Stopwatch.StartNew();
        ends.Ended("s1");
        ends.Ended("bad");
        ends.Ended("s2");

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1));
        Assert.Empty(selected);
        gate.Release(3);
        await Until(() => { lock (selected) return selected.Count == 2; });
        Assert.Equal(["s1", "s2"], selected);
        Assert.Equal(["bad"], failed);
    }

    [Fact]
    public async Task A_turn_that_pressed_through_the_connector_is_queued_while_on_and_disabled_while_off()
    {
        var at = new DateTimeOffset(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.User, Id = "w1", Text = "tidy up", Origin = "person", At = at });
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Tool, Id = "toolu_1", ToolKind = "other", Title = "housekeeping_clean", At = at.AddSeconds(1) });
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn", At = at.AddSeconds(2) });
        ChildLineage.Append(_home, "s1", """{"at":"2026-11-11T09:00:01.000Z","by":"call","tool":"housekeeping_clean","door":"claude","toolUse":"toolu_1","act":"press"}""");
        var check = new AskDaorisSecondCheck(_home, events, _ => [], _ => new HashSet<string>(), () => at.AddMinutes(-1));

        await check.EndedAsync("s1", CancellationToken.None);

        var report = Assert.Single(new AskDaorisReports(_home).Of("s1"));
        Assert.Equal(AskDaorisStates.Disabled, report.State);
        Assert.Equal([("toolu_1", SelectedWhy.Press, "housekeeping_clean")], report.Turns.Single().Calls.Select(call => (call.Id, call.Why, call.Tool)));

        var seeded = RoleCatalog.Seed.Single(role => role.Id == SecondCheck.Role);
        RoleCatalog.Write(DesktopStore.LocationIn(_home), RoleChange.Set(seeded with { Hold = seeded.Hold! with { Holder = RoleHolders.Agent } }), () => StoreSettled.Yes);
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.User, Id = "w2", Text = "again", Origin = "person", At = at.AddSeconds(3) });
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Tool, Id = "c2", ToolKind = "edit", At = at.AddSeconds(4) });
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn", At = at.AddSeconds(5) });

        await check.EndedAsync("s1", CancellationToken.None);

        Assert.Equal([AskDaorisStates.Disabled, AskDaorisStates.Queued], new AskDaorisReports(_home).Of("s1").Select(each => each.State).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_delegated_child_s_calls_kept_beside_its_session_are_selected_and_one_not_kept_is_said_delegated_not_read()
    {
        var at = new DateTimeOffset(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.User, Id = "w1", Text = "split it", Origin = "person", At = at });
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Tool, Id = "agent-1", ToolKind = "think", Title = "Agent", Stream = "sub-1", At = at.AddSeconds(1) });
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Tool, Id = "agent-2", ToolKind = "think", Title = "Agent", Stream = "sub-2", At = at.AddSeconds(1) });
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn", At = at.AddSeconds(2) });
        ChildCalls.Keep(_home, "s1", "sub-1", new SessionEvent { Kind = SessionEventKind.Tool, Id = "c1", ToolKind = "read", At = at.AddSeconds(1) });
        ChildCalls.Keep(_home, "s1", "sub-1", new SessionEvent { Kind = SessionEventKind.Tool, Id = "c2", ToolKind = "edit", Title = "Edit a.cs", At = at.AddSeconds(1) });
        var check = new AskDaorisSecondCheck(_home, events, _ => [], _ => new HashSet<string>(), () => at.AddMinutes(-1));

        await check.EndedAsync("s1", CancellationToken.None);

        var turn = Assert.Single(Assert.Single(new AskDaorisReports(_home).Of("s1")).Turns);
        Assert.Equal(["agent-1", "agent-1/c2", "agent-2"], turn.Calls.Select(call => call.Id));
        Assert.Equal([new ReadLimitAt(ReadLimit.Delegated, "agent-2")], turn.Limits);
    }
}
