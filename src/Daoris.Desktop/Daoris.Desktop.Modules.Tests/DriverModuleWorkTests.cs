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
        // Whose take each taken quest is (PAUSE1d): q2 this machine's, with no record that took it; q3 another machine's.
        ledger.Serve("/api/quests/q2/claim", Bytes("""{"quest":"q2","claim":"held"}"""));
        ledger.Serve("/api/quests/q3/claim", Bytes("""{"quest":"q3","claim":"none"}"""));
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

    /// <summary>
    /// CARRY2d: a resume's holds carried the verdict and the driver's English alone, so 中文 showed English for a stop, a pause,
    /// a park and a take elsewhere. Each hold now carries its facts beside the unchanged sentence, by the tick's own names and
    /// shapes (<c>heldBy</c>, <c>pausedBy</c>, <c>strikes</c>, <c>takenBy</c>), so the page says it as it says a sitting quest;
    /// each fact is null for every verdict but its own, and nothing is reshaped.
    /// </summary>
    [Fact]
    public void Each_hold_a_resume_names_carries_the_facts_its_sentence_is_said_from()
    {
        var wire = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
        System.Text.Json.JsonElement Shown(WorkHold hold) => System.Text.Json.JsonSerializer.SerializeToElement(DriverModule.WorkHoldShown(hold), wire);
        const string StopSaid = "you stopped session `b3f0re00`; Try again carries it on — `daoris driver retry q8 --session b3f0re00`.";
        const string Null = nameof(System.Text.Json.JsonValueKind.Null);

        var stopped = Shown(new WorkHold("q8", StartVerdict.Stopped, StopSaid) { HeldBy = "b3f0re00" });
        var paused = Shown(new WorkHold("q9", StartVerdict.Paused, "you paused `#q9`; Resume starts it — `daoris-driver quest resume q9`.")
        {
            PausedBy = new PausedBy(WorkScope.Quest, "q9"),
        });
        var parked = Shown(new WorkHold("q10", StartVerdict.Exhausted, "3 session(s) have failed on `#q10` without landing anything.") { Strikes = 3 });
        var taken = Shown(new WorkHold("q11", StartVerdict.TakenElsewhere, "Quest `#q11` is taken on `laptop`, by session `laptop/s7`.")
        {
            TakenBy = new TakenBy("cut0ff00", "laptop", "laptop/s7"),
        });
        var here = Shown(new WorkHold("q11", StartVerdict.TakenElsewhere, "Quest `#q11` was taken here.") { TakenBy = new TakenBy("cut0ff00", Here: true) });
        var held = Shown(new WorkHold("q1", StartVerdict.Held, "`game` is held by the person."));

        Assert.Equal(("q8", "Stopped", StopSaid),
            (stopped.GetProperty("quest").GetString(), stopped.GetProperty("verdict").GetString(), stopped.GetProperty("reason").GetString()));
        Assert.Equal("b3f0re00", stopped.GetProperty("heldBy").GetString());
        Assert.Equal(("quest", "q9"),
            (paused.GetProperty("pausedBy").GetProperty("scope").GetString(), paused.GetProperty("pausedBy").GetProperty("id").GetString()));
        Assert.Equal(3, parked.GetProperty("strikes").GetInt32());
        var whose = taken.GetProperty("takenBy");
        Assert.Equal(("laptop", "laptop/s7", false, "cut0ff00"),
            (whose.GetProperty("machine").GetString(), whose.GetProperty("session").GetString(), whose.GetProperty("here").GetBoolean(), whose.GetProperty("last").GetString()));
        Assert.True(here.GetProperty("takenBy").GetProperty("here").GetBoolean());
        Assert.Equal(Null, here.GetProperty("takenBy").GetProperty("machine").ValueKind.ToString());
        // A hold whose sentence needs no fact names none, and each fact rides its own verdict alone.
        Assert.Equal([Null, Null, Null, Null],
            new[] { "heldBy", "pausedBy", "strikes", "takenBy" }.Select(fact => held.GetProperty(fact).ValueKind.ToString()));
        Assert.Equal([Null, Null, Null], new[] { "pausedBy", "strikes", "takenBy" }.Select(fact => stopped.GetProperty(fact).ValueKind.ToString()));
        Assert.Equal([Null, Null, Null], new[] { "heldBy", "strikes", "takenBy" }.Select(fact => paused.GetProperty(fact).ValueKind.ToString()));
    }

    /// <summary>
    /// CARRY2d, through the route: a stop the person made before the pause still holds its quest once the work resumes, and the
    /// page is handed whose stop it is by its session, beside the driver's sentence, so 中文 says it from that fact.
    /// </summary>
    [Fact]
    public async Task A_resume_hands_the_page_the_session_whose_stop_still_holds_a_quest()
    {
        using var ledger = new LoopbackHost();
        var quest = """{"id":"q8","from":"ask #a1","to":"engine","title":"The work of #q8","body":"A body.","status":"Taken"}""";
        ledger.Serve("/api/quests?includeClosed=true", Bytes($"[{quest}]"));
        ledger.Serve("/api/quests", Bytes($"[{quest}]"));
        ledger.Serve("/api/sessions?includeClosed=true", Bytes(
            """[{"id":"b3f0re00","quest":"q8","repository":"engine","adapter":"claude-code","state":"stopped","kind":"driven","created":"2026-10-03T09:05:00Z","updated":"2026-10-03T09:06:00Z"}]"""));
        ledger.Serve("/api/sessions", Bytes("[]"));
        ledger.Serve("/api/asks/a1", Bytes("""{"id":"a1","workspace":"aurora","sentence":"Add the note field","state":"Published","tier":"named"}"""));
        ledger.Serve("/api/registry", Bytes($$"""[{"repository":"engine","adopted":true,"registered":true,"root":"{{Home.Replace('\\', '/')}}/engine","workspace":"aurora"}]"""));
        (DriverConfig.Empty with { Drivable = ["engine"] })
            .WithPausedAsk("a1", new WorkPause(DateTimeOffset.UtcNow, new Dictionary<string, string>()))
            .Save(DriverConfigPath);
        var module = await UpAsync(ledger);

        var resumed = await AnswerAsync(module, "WORK_RESUME", new { ask = "a1" });

        var hold = Assert.Single(resumed.GetProperty("holds").EnumerateArray());
        Assert.Equal(("q8", "Stopped", "b3f0re00"),
            (hold.GetProperty("quest").GetString(), hold.GetProperty("verdict").GetString(), hold.GetProperty("heldBy").GetString()));
        Assert.Contains("session `b3f0re00`", hold.GetProperty("reason").GetString());
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

    /// <summary>
    /// The plan answers the abandon's half too (PAUSE1d, design §3.1, §3.2): each piece's key and act, why it keeps what it
    /// keeps, the keys the second press sends back, and a tree by its repository and branch, never a path.
    /// </summary>
    [Fact]
    public async Task The_plan_answers_what_an_abandon_takes_and_keeps_and_never_a_path()
    {
        using var ledger = Ledger();
        var module = await UpAsync(ledger);

        var plan = await AnswerAsync(module, "WORK_PLAN", new { ask = "a1" });

        var abandon = plan.GetProperty("abandon");
        Assert.True(abandon.GetProperty("abandonable").GetBoolean());
        Assert.Equal(
            ["q1:decline:", "q2:keep:taken-outside", "q3:keep:taken-elsewhere"],
            plan.GetProperty("quests").EnumerateArray().Select(q =>
                $"{q.GetProperty("quest").GetString()}:{q.GetProperty("abandon").GetString()}:{q.GetProperty("kept").GetString()}"));
        Assert.Equal(
            ["earl1er0:archive", "w0rk1ng0:stop", "p4rk3d00:end"],
            plan.GetProperty("sessions").EnumerateArray().Select(s => $"{s.GetProperty("session").GetString()}:{s.GetProperty("abandon").GetString()}"));
        // The tree's folder is not on this machine and the registry's checkout is no repository: git cannot say, so it is kept.
        var tree = Assert.Single(plan.GetProperty("trees").EnumerateArray());
        Assert.Equal(("keep", "unknown", "tree:engine:daoris/s-1a2b3c4d"),
            (tree.GetProperty("abandon").GetString(), tree.GetProperty("kept").GetString(), tree.GetProperty("key").GetString()));
        Assert.Contains("quest:q1", abandon.GetProperty("pieces").EnumerateArray().Select(piece => piece.GetString()));
        Assert.Contains("ask:a1", abandon.GetProperty("pieces").EnumerateArray().Select(piece => piece.GetString()));
        Assert.Equal("a1", abandon.GetProperty("closes").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, abandon.GetProperty("abandoned").ValueKind);
        Assert.DoesNotContain("trees/aurora", plan.GetRawText());
        Assert.DoesNotContain(Home.Replace('\\', '/'), plan.GetRawText().Replace("\\\\", "/"));
    }

    /// <summary>An abandon with no reason is <c>WORK_REASON</c>, before anything is asked or written: each declined quest keeps it.</summary>
    [Fact]
    public async Task An_abandon_without_a_reason_is_refused_and_writes_nothing()
    {
        using var ledger = Ledger();
        var module = await UpAsync(ledger);

        var blank = await RefusalAsync(module, "WORK_ABANDON", new { ask = "a1", reason = "  ", pieces = new[] { "quest:q1" } });
        var none = await RefusalAsync(module, "WORK_ABANDON", new { ask = "a1", pieces = new[] { "quest:q1" } });

        Assert.Contains(Refusals.WorkReason, blank);
        Assert.Contains(Refusals.WorkReason, none);
        Assert.False(File.Exists(DriverConfigPath));
        Assert.False(File.Exists(new AbandonRecord(Home).FilePath));
    }

    /// <summary>An abandon of an ask or a quest this machine does not have is <c>WORK_UNKNOWN</c>, as the other work routes say.</summary>
    [Fact]
    public async Task An_abandon_of_an_unknown_ask_or_quest_is_refused_naming_it()
    {
        using var ledger = Ledger();
        var module = await UpAsync(ledger);

        var ask = await RefusalAsync(module, "WORK_ABANDON", new { ask = "zz", reason = "gone wrong", pieces = Array.Empty<string>() });
        var quest = await RefusalAsync(module, "WORK_ABANDON", new { quest = "q99", reason = "gone wrong", pieces = Array.Empty<string>() });

        Assert.Contains(Refusals.WorkUnknown, ask);
        Assert.Contains("id=zz", ask);
        Assert.Contains("context=quest", quest);
    }

    /// <summary>A second press must send the list it held: no <c>pieces</c> is the driver's refusal, in its words.</summary>
    [Fact]
    public async Task An_abandon_that_sends_no_list_is_refused()
    {
        using var ledger = Ledger();
        var module = await UpAsync(ledger);

        Assert.Contains(Refusals.DriverRefused, await RefusalAsync(module, "WORK_ABANDON", new { ask = "a1", reason = "gone wrong" }));
    }

    /// <summary>
    /// The second press sends the list it held, with the reason (design §3.1, §7.3): the open quest declined with the reason,
    /// only while open, the ask closed with it, and what went said from facts; the record is written and the machine log
    /// counts it as the screen's. An empty list abandons nothing, said as information (D48 §6).
    /// </summary>
    [Fact]
    public async Task The_second_press_declines_closes_and_records_what_went()
    {
        using var ledger = new LoopbackHost();
        var quest = """{"id":"q1","from":"ask #a1","to":"engine","title":"The work of #q1","body":"A body.","status":"Open","workspace":"aurora"}""";
        ledger.Serve("/api/quests?includeClosed=true", Bytes($"[{quest}]"));
        ledger.Serve("/api/quests", Bytes($"[{quest}]"));
        ledger.Serve("/api/sessions?includeClosed=true", Bytes("[]"));
        ledger.Serve("/api/sessions", Bytes("[]"));
        ledger.Serve("/api/asks/a1", Bytes("""{"id":"a1","workspace":"aurora","sentence":"Add the note field","state":"Published","tier":"named"}"""));
        ledger.Serve("/api/asks?includeClosed=true", Bytes("""[{"id":"a1","workspace":"aurora","sentence":"Add the note field","state":"Published","tier":"named"}]"""));
        ledger.Serve("/api/registry", Bytes("[]"));
        ledger.Serve("/api/quests/q1/respond", Bytes($$"""{"quest":{{quest}},"message":"Quest `#q1` is now Declined."}"""));
        ledger.Serve("/api/asks/a1/close", Bytes("""{"ask":{"id":"a1","workspace":"aurora","sentence":"Add the note field","state":"Closed","tier":"named"},"message":"Ask `#a1` is closed."}"""));
        var log = new MachineLog(Home, "desktop");
        var module = await UpAsync(ledger, log);

        var listed = await AnswerAsync(module, "WORK_PLAN", new { ask = "a1" });
        var pieces = listed.GetProperty("abandon").GetProperty("pieces").EnumerateArray().Select(piece => piece.GetString()!).ToArray();
        var went = await AnswerAsync(module, "WORK_ABANDON", new { ask = "a1", reason = "It went the wrong way.", pieces });
        var nothing = await AnswerAsync(module, "WORK_ABANDON", new { ask = "a1", reason = "It went the wrong way.", pieces = Array.Empty<string>() });

        Assert.Equal(["quest:q1", "ask:a1"], pieces);
        Assert.Equal(("abandoned", 2, 2), (went.GetProperty("did").GetString(), went.GetProperty("listed").GetInt32(), went.GetProperty("went").GetInt32()));
        Assert.Equal(["q1"], went.GetProperty("declined").EnumerateArray().Select(q => q.GetString()));
        Assert.True(went.GetProperty("closed").GetBoolean());
        Assert.False(went.GetProperty("stillPaused").GetBoolean());
        Assert.Equal("nothing", nothing.GetProperty("did").GetString());
        var entry = Assert.Single(new AbandonRecord(Home).Entries());
        Assert.Equal(("screen", "It went the wrong way.", true), (entry.Door, entry.Reason, entry.Closed));
        Assert.Null(DriverConfig.Load(DriverConfigPath).PausedAsk("a1"));

        log.Dispose();
        var line = Assert.Single(Directory.GetFiles(Path.Combine(Home, MachineLog.Folder)).SelectMany(File.ReadAllLines),
            each => each.Contains("\"work.abandoned\"", StringComparison.Ordinal));
        Assert.Contains("\"door\":\"screen\"", line);
        Assert.Contains("\"declined\":1", line);
    }
}
