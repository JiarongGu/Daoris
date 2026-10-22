using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// SESSWATCH1a (D200, the session-watch design §2.3, §5.2) over the bridge: a quiet session the watcher brings to the person is a row of
/// *What needs you*, quiet or busy, and its presses are the person's. *Leave it running* stands the watcher down until it is heard again;
/// *End and carry on* is their stop, released in the same press, refused for a busy row, an intake, and a run already gone. Daoris ends
/// nothing quiet by itself.
/// </summary>
public sealed class DriverModuleSessionQuietTests : DriverModuleBridge
{
    private TimeSpan _now = TimeSpan.FromHours(1);

    /// <summary>A session registered with the loop's watcher and left unheard past three times its bound, its job unread.</summary>
    private (SessionWatch Watch, SessionWatch.WatchHandle Run) Quiet(DriverLoop loop, string session, string kind = SessionWatch.Quest, string? quest = "q1")
    {
        var watch = SessionWatch.Of(loop.Events);
        watch.Clock = new QuietClock(() => _now, stopsInSleep: true);
        var run = watch.Watch(session, new WatchedRun(kind, quest, "stub"));
        for (var minute = 0; minute < 31; minute++)
        {
            _now += TimeSpan.FromMinutes(1);
            watch.Look(_ => null, TimeSpan.FromSeconds(15));
        }

        return (watch, run);
    }

    [Fact]
    public async Task Leave_it_running_takes_the_row_away_and_a_session_that_brings_none_is_refused()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        var nothing = await AnswerAsync(module, "SESSION_QUIET_LEAVE", new { id = "s1" });
        Assert.Equal((false, "not-quiet"), (nothing.GetProperty("left").GetBoolean(), nothing.GetProperty("why").GetString()));

        var (watch, run) = Quiet(loop, "s1");
        using var _ = run;
        Assert.NotNull(watch.RowOf("s1"));

        var left = await AnswerAsync(module, "SESSION_QUIET_LEAVE", new { id = "s1" });

        Assert.True(left.GetProperty("left").GetBoolean());
        Assert.Null(watch.RowOf("s1"));
        Assert.Empty(AttentionStateSlice.QuietWaits(watch, loop.Processes));
    }

    [Fact]
    public async Task End_and_carry_on_is_refused_for_a_session_not_quiet_and_for_an_intake()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);

        var none = await AnswerAsync(module, "SESSION_QUIET_CARRY_ON", new { id = "s1" });
        Assert.Equal((false, "not-quiet"), (none.GetProperty("stopped").GetBoolean(), none.GetProperty("why").GetString()));

        var (_, intake) = Quiet(loop, "i1", SessionWatch.Intake, quest: null);
        using (intake)
        {
            var refused = await AnswerAsync(module, "SESSION_QUIET_CARRY_ON", new { id = "i1" });
            Assert.Equal((false, "not-quest"), (refused.GetProperty("stopped").GetBoolean(), refused.GetProperty("why").GetString()));
        }

        Assert.False(File.Exists(DriverConfigPath) && DriverConfig.Load(DriverConfigPath).Released.Count > 0);
    }

    [Fact]
    public async Task End_and_carry_on_stops_the_run_as_the_persons_released_stop_with_its_note()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        using var process = Process.Start(new ProcessStartInfo("node", ["-e", "setInterval(() => {}, 1000)"]) { UseShellExecute = false })!;
        using var tracked = loop.Processes.Track("s1", process);
        var (watch, run) = Quiet(loop, "s1");
        using var _ = run;

        var answer = await AnswerAsync(module, "SESSION_QUIET_CARRY_ON", new { id = "s1" });

        Assert.True(answer.GetProperty("stopped").GetBoolean());
        // Released in the same press: the stop holds no quest, and the look carries it on.
        Assert.Equal("s1", DriverConfig.Load(loop.ConfigPath).ReleasedFor("q1"));
        // The person's stop, its note the press's: concluded `stopped`, no strike.
        var stop = loop.Processes.StopMade("s1")!;
        Assert.Null(stop.Reason);
        Assert.Equal("stopped", stop.Conclusion.State);
        Assert.True(Observation.IsQuietPressed(stop.Conclusion.Parts));
        Assert.Null(watch.RowOf("s1"));
        Assert.True(process.WaitForExit(20_000));
    }

    [Fact]
    public async Task End_and_carry_on_is_refused_for_a_busy_row_offered_no_end()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        var watch = SessionWatch.Of(loop.Events);
        watch.Clock = new QuietClock(() => _now, stopsInSleep: true);
        using var run = watch.Watch("s2", new WatchedRun(SessionWatch.Quest, "q2", "stub"));
        var cpu = TimeSpan.Zero;
        for (var minute = 0; minute < 31; minute++)
        {
            _now += TimeSpan.FromMinutes(1);
            cpu += TimeSpan.FromSeconds(30);
            watch.Look(_ => new JobRead(true, cpu), TimeSpan.FromSeconds(15));
        }

        Assert.True(watch.RowOf("s2")!.Busy);
        var refused = await AnswerAsync(module, "SESSION_QUIET_CARRY_ON", new { id = "s2" });

        Assert.Equal((false, "not-quiet"), (refused.GetProperty("stopped").GetBoolean(), refused.GetProperty("why").GetString()));
    }

    /// <summary>The row as <c>NeedsAPerson</c> judges it from the watcher's memory: its session, quest, repository and watch.</summary>
    [Fact]
    public void A_quiet_session_is_a_row_holding_work_with_its_watch()
    {
        var loop = Loop();
        var (watch, run) = Quiet(loop, "s1");
        using var _ = run;

        var waits = AttentionStateSlice.QuietWaits(watch, loop.Processes)
            .Select(wait => JsonSerializer.SerializeToNode(wait, Shenora.Core.Ipc.IpcJson.Options)!.AsObject())
            .ToList();
        var row = Assert.Single(NeedsAPerson.Rows(new AttentionFacts
        {
            Sessions = [JsonNode.Parse("""{"id":"s1","quest":"q1","repository":"engine","workspace":"default","state":"working"}""")!.AsObject()],
            Quiet = waits,
        }));

        Assert.Equal(("quiet", "s1", "s1", "q1", "engine"), (Text(row, "kind"), Text(row, "id"), Text(row, "session"), Text(row, "quest"), Text(row, "where")));
        Assert.Equal("default", Text(row, "workspace"));
        var quiet = row["quiet"]!.AsObject();
        Assert.Equal(30, quiet["minutes"]!.GetValue<int>());
        Assert.False(quiet["busy"]!.GetValue<bool>());
        Assert.False(quiet["intake"]!.GetValue<bool>());
        Assert.False(quiet["words"]!.GetValue<bool>());
        Assert.NotNull(Text(row, "since"));
        // Pushed whole: its watch is on the allow-list.
        Assert.Equal(quiet.ToJsonString(), AttentionStateSlice.Picked(row)["quiet"]!.ToJsonString());
    }

    private static string? Text(JsonObject row, string field) => row[field]?.GetValue<string>();
}
