using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The work of an ask or a quest over the bridge (PAUSE1b, D132 §7.3): <c>WORK_PLAN</c> answers every piece and what a pause
/// would do with it, <c>WORK_PAUSE</c> stops what of it runs here as the person's stop and keeps everything else, and
/// <c>WORK_RESUME</c> releases what the pause made and names what still holds. One implementation with the terminal's
/// (<c>WorkPausingTests</c>); these hold what the page receives, and that nothing of this machine's paths comes back.
/// </summary>
/// <remarks>The service stands in over a real socket on this machine, as the sessions' tests stand it in; nothing runs here, so
/// the working session is ended as an orphan is, the person's stop.</remarks>
public sealed class DriverModuleWorkTests : DriverModuleBridge
{
    private string TreeOf(string name) => Path.Combine(Home, "trees", "aurora", "engine", name).Replace('\\', '/');

    /// <summary>
    /// Ask <c>a1</c>'s work: <c>q1</c> open; <c>q2</c> taken, worked by a session nothing here runs, after an earlier one
    /// whose tree this home opened; <c>q3</c> taken, its session waiting on you.
    /// </summary>
    private LoopbackHost Ledger()
    {
        var ledger = new LoopbackHost();
        string Session(string id, string state, string quest, int minute, string? tree = null) =>
            $$"""{"id":"{{id}}","quest":"{{quest}}","repository":"engine","adapter":"claude-code","state":"{{state}}","kind":"driven",{{(tree is null ? "" : $"\"tree\":\"{tree}\",")}}"created":"2026-10-03T09:{{minute:00}}:00Z","updated":"2026-10-03T09:{{minute + 1:00}}:00Z"}""";
        string Quest(string id, string status) =>
            $$"""{"id":"{{id}}","from":"ask #a1","to":"engine","title":"The work of #{{id}}","body":"A body.","status":"{{status}}"}""";
        var quests = $"[{Quest("q1", "Open")},{Quest("q2", "Taken")},{Quest("q3", "Taken")}]";
        var working = Session("w0rk1ng0", "working", "q2", 20);
        var parked = Session("p4rk3d00", "awaiting-person", "q3", 30);
        ledger.Serve("/api/quests?includeClosed=true", Bytes(quests));
        ledger.Serve("/api/quests", Bytes(quests));
        ledger.Serve("/api/sessions?includeClosed=true", Bytes($"[{Session("earl1er0", "failed", "q2", 10, TreeOf("s-1a2b3c4d"))},{working},{parked}]"));
        ledger.Serve("/api/sessions", Bytes($"[{working},{parked}]"));
        ledger.Serve("/api/sessions/w0rk1ng0/state", Bytes("""{"session":{"id":"w0rk1ng0","state":"stopped"},"message":"Session is now stopped."}"""));
        ledger.Serve("/api/asks/a1", Bytes("""{"id":"a1","workspace":"aurora","sentence":"Add the note field","state":"Published","tier":"named"}"""));
        ledger.Serve("/api/registry", Bytes($$"""[{"repository":"engine","adopted":true,"registered":true,"root":"{{Home.Replace('\\', '/')}}/engine","workspace":"aurora"}]"""));
        return ledger;
    }

    private static byte[] Bytes(string text) => System.Text.Encoding.UTF8.GetBytes(text);

    private async Task<DriverModule> UpAsync(LoopbackHost ledger, MachineLog? log = null)
    {
        var loop = new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0", log: log);
        await loop.ComeUpAsync(new ServiceClient(ledger.Address, null));
        return new DriverModule(Bus, loop);
    }

    [Fact]
    public async Task The_plan_answers_each_piece_and_what_a_pause_does_with_it_and_never_a_path()
    {
        using var ledger = Ledger();
        var module = await UpAsync(ledger);

        var plan = await AnswerAsync(module, "WORK_PLAN", new { ask = "#a1" });

        Assert.Equal(("ask", "a1", true), (plan.GetProperty("scope").GetString(), plan.GetProperty("id").GetString(), plan.GetProperty("pausable").GetBoolean()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, plan.GetProperty("paused").ValueKind);
        Assert.Equal(
            ["q1:paused:asked", "q2:paused:asked", "q3:paused:asked"],
            plan.GetProperty("quests").EnumerateArray().Select(q => $"{q.GetProperty("quest").GetString()}:{q.GetProperty("pause").GetString()}:{q.GetProperty("joined").GetString()}"));
        Assert.Equal(
            ["earl1er0:ended", "w0rk1ng0:stopped", "p4rk3d00:parked"],
            plan.GetProperty("sessions").EnumerateArray().Select(s => $"{s.GetProperty("session").GetString()}:{s.GetProperty("pause").GetString()}"));
        var tree = Assert.Single(plan.GetProperty("trees").EnumerateArray());
        Assert.Equal(("engine", "daoris/s-1a2b3c4d"), (tree.GetProperty("repository").GetString(), tree.GetProperty("branch").GetString()));
        // A tree by its repository and branch only: the page never learns a path on this machine (the platform language §4).
        Assert.DoesNotContain("trees/aurora", plan.GetRawText());
        Assert.DoesNotContain(Home.Replace('\\', '/'), plan.GetRawText().Replace("\\\\", "/"));
    }

    /// <summary>
    /// A pause stops what runs here as the person's stop and leaves the parked session parked; the file keeps the pause and its
    /// stop, the plan then says it is paused, and the machine log counts it as the screen's.
    /// </summary>
    [Fact]
    public async Task A_pause_stops_what_runs_keeps_the_parked_session_and_the_plan_then_says_paused()
    {
        using var ledger = Ledger();
        var log = new MachineLog(Home, "desktop");
        var module = await UpAsync(ledger, log);

        var paused = await AnswerAsync(module, "WORK_PAUSE", new { ask = "a1" });

        Assert.Equal(("paused", false), (paused.GetProperty("did").GetString(), paused.GetProperty("already").GetBoolean()));
        var stopped = Assert.Single(paused.GetProperty("stopped").EnumerateArray());
        Assert.Equal(("w0rk1ng0", "q2"), (stopped.GetProperty("session").GetString(), stopped.GetProperty("quest").GetString()));
        var kept = Assert.Single(paused.GetProperty("kept").EnumerateArray());
        Assert.Equal(("p4rk3d00", "parked"), (kept.GetProperty("session").GetString(), kept.GetProperty("why").GetString()));
        Assert.Equal("w0rk1ng0", DriverConfig.Load(DriverConfigPath).PausedAsk("a1")!.Stopped["q2"]);

        var plan = await AnswerAsync(module, "WORK_PLAN", new { ask = "a1" });
        var stop = Assert.Single(plan.GetProperty("paused").GetProperty("stopped").EnumerateArray());
        Assert.Equal(("q2", "w0rk1ng0"), (stop.GetProperty("quest").GetString(), stop.GetProperty("session").GetString()));
        Assert.All(plan.GetProperty("quests").EnumerateArray(), q => Assert.Equal("a1", q.GetProperty("pausedBy").GetProperty("id").GetString()));

        log.Dispose();
        var line = Assert.Single(Directory.GetFiles(Path.Combine(Home, MachineLog.Folder)).SelectMany(File.ReadAllLines),
            each => each.Contains("\"work.paused\"", StringComparison.Ordinal));
        Assert.Contains("\"door\":\"screen\"", line);
        Assert.Contains("\"stopped\":1", line);
    }

    /// <summary>Resume releases each stop the pause made, removes the pause, and answers what still holds, with the planner's verdict.</summary>
    [Fact]
    public async Task Resume_releases_each_stop_the_pause_made_and_answers_what_still_holds()
    {
        using var ledger = Ledger();
        DriverConfig.Empty
            .WithPausedAsk("a1", new WorkPause(DateTimeOffset.UtcNow, new Dictionary<string, string> { ["q2"] = "w0rk1ng0" }))
            .Save(DriverConfigPath);
        var module = await UpAsync(ledger);

        var resumed = await AnswerAsync(module, "WORK_RESUME", new { ask = "a1" });

        Assert.Equal("resumed", resumed.GetProperty("did").GetString());
        var released = Assert.Single(resumed.GetProperty("released").EnumerateArray());
        Assert.Equal(("q2", "w0rk1ng0"), (released.GetProperty("quest").GetString(), released.GetProperty("session").GetString()));
        var config = DriverConfig.Load(DriverConfigPath);
        Assert.Null(config.PausedAsk("a1"));
        Assert.Equal("w0rk1ng0", config.ReleasedFor("q2"));
        // `engine` is not driven here, so its open quest is still held by that, named with the planner's verdict.
        var hold = Assert.Single(resumed.GetProperty("holds").EnumerateArray());
        Assert.Equal(("q1", "NotDrivable"), (hold.GetProperty("quest").GetString(), hold.GetProperty("verdict").GetString()));
    }

    /// <summary>A resume of what is not paused is said, never refused (D48 §6), and writes nothing.</summary>
    [Fact]
    public async Task A_resume_of_what_is_not_paused_is_said_and_writes_nothing()
    {
        using var ledger = Ledger();
        var module = await UpAsync(ledger);

        var resumed = await AnswerAsync(module, "WORK_RESUME", new { quest = "q1" });

        Assert.Equal(("quest", "not-paused"), (resumed.GetProperty("scope").GetString(), resumed.GetProperty("did").GetString()));
        Assert.False(File.Exists(DriverConfigPath));
    }

    /// <summary>An ask this machine has no record of, or a quest not here, is <c>WORK_UNKNOWN</c>, a quest's by the catalogue's context.</summary>
    [Theory]
    [InlineData("WORK_PLAN")]
    [InlineData("WORK_PAUSE")]
    [InlineData("WORK_RESUME")]
    public async Task An_unknown_ask_or_quest_is_refused_naming_it(string route)
    {
        using var ledger = Ledger();
        var module = await UpAsync(ledger);

        var ask = await RefusalAsync(module, route, new { ask = "zz" });
        var quest = await RefusalAsync(module, route, new { quest = "q99" });

        Assert.Contains(Refusals.WorkUnknown, ask);
        Assert.Contains("id=zz", ask);
        Assert.DoesNotContain("context=", ask);
        Assert.Contains(Refusals.WorkUnknown, quest);
        Assert.Contains("id=q99", quest);
        Assert.Contains("context=quest", quest);
        Assert.False(File.Exists(DriverConfigPath));
    }

    /// <summary>A work is named by its ask or its quest, one of them: neither, or both, is the driver's refusal, in its words.</summary>
    [Fact]
    public async Task A_work_named_by_neither_or_both_is_refused()
    {
        using var ledger = Ledger();
        var module = await UpAsync(ledger);

        Assert.Contains(Refusals.DriverRefused, await RefusalAsync(module, "WORK_PAUSE", new { }));
        Assert.Contains(Refusals.DriverRefused, await RefusalAsync(module, "WORK_PAUSE", new { ask = "a1", quest = "q1" }));
    }

    /// <summary>Before the loop's service answers, a work route is still coming up, as every route that reads the service says.</summary>
    [Fact]
    public async Task Before_the_service_answers_it_is_still_coming_up()
    {
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "WORK_PAUSE", new { ask = "a1" }));
    }
}
