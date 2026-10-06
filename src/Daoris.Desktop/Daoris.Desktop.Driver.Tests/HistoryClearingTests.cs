using System.Diagnostics;
using System.Net;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HIST1c (D153; the history-clearing design §2, §4–§5, §6.3, §6.5): this machine's half of clearing finished history. The
/// service judges the records (HIST1b) and its word is said first; then this machine's: a process still running, an
/// automatic landing still trying, a tree still here, a landing's branch still standing. After the service's yes, every file
/// the home kept of each cleared session goes through the helper D126's delete calls too, and what names it is tidied. The
/// machine log keeps counts, never a word or an id.
/// </summary>
/// <remarks>A scratch home and an in-process stand-in for the service only: the suite's fast half.</remarks>
public sealed class HistoryClearingTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-history-" + Guid.NewGuid().ToString("N")[..8]);

    public HistoryClearingTests() => Directory.CreateDirectory(Path.Combine(_home, "sessions"));

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private string Sessions => Path.Combine(_home, "sessions");

    private string ConfigPath => Path.Combine(_home, "driver.json");

    private HistoryWorld World(StandIn service, MachineLog? log = null, Func<DateTimeOffset>? clock = null)
    {
        service.Home = _home;
        return new HistoryWorld(service.Client(), _home, ConfigPath, new SessionProcesses(Sessions))
        {
            Log = log,
            Clock = clock ?? (() => DateTimeOffset.UtcNow),
        };
    }

    // ——— The records, as the service answers them.

    private static JsonObject Record(
        string id, string? quest = null, string state = "completed", string? tree = null, string? ask = null, string workspace = "default",
        string kind = "driven") => new()
        {
            ["id"] = id, ["quest"] = quest, ["repository"] = "engine", ["state"] = state, ["kind"] = kind, ["tree"] = tree?.Replace('\\', '/'),
            ["ask"] = ask, ["workspace"] = workspace, ["created"] = "2026-10-03T09:00:00Z", ["updated"] = "2026-10-03T09:05:00Z",
        };

    private static JsonObject Quest(string id, string status = "Done", string workspace = "default", bool held = false, string? awaits = null) => new()
    {
        ["id"] = id, ["from"] = "game", ["to"] = "engine", ["title"] = "a title nobody reads", ["body"] = "", ["status"] = status,
        ["workspace"] = workspace, ["held"] = held, ["awaits"] = awaits,
    };

    private static JsonObject Ask(string id, string state = "Done", string workspace = "default") => new()
    {
        ["id"] = id, ["workspace"] = workspace, ["sentence"] = "a sentence of the person's", ["state"] = state, ["tier"] = "quest",
    };

    private static JsonObject Unit(
        string kind, string id, string workspace = "default", string[]? quests = null, string[]? asks = null, string[]? sessions = null,
        string[]? teammates = null, string[]? forgotten = null, JsonObject? refusal = null, JsonArray? kept = null)
    {
        static JsonArray Of(string[]? ids) => new([.. (ids ?? []).Select(id => (JsonNode)id)]);
        var unit = new JsonObject
        {
            ["kind"] = kind, ["id"] = id, ["workspace"] = workspace, ["clearable"] = refusal is null,
            ["quests"] = Of(quests), ["forgotten"] = Of(forgotten), ["asks"] = Of(asks), ["sessions"] = Of(sessions),
            ["teammates"] = Of(teammates), ["kept"] = kept ?? [],
        };
        if (refusal is not null) unit["refusal"] = refusal;
        return unit;
    }

    private static JsonObject Refusal(string word, string? quest = null, string? ask = null, string? session = null, string? origin = null,
        string? workspace = null) => new()
        {
            ["refusal"] = word, ["error"] = $"the desk's {word} sentence", ["quest"] = quest, ["ask"] = ask, ["session"] = session,
            ["origin"] = origin, ["workspace"] = workspace,
        };

    // ——— What the home keeps.

    /// <summary>Every file the design's §2.2 lists for a session, each holding words.</summary>
    private void Kept(string id)
    {
        File.WriteAllText(Path.Combine(Sessions, $"{id}.events.jsonl"), "{\"seq\":1,\"kind\":\"user\",\"text\":\"a secret plan\"}\n");
        File.WriteAllText(Path.Combine(Sessions, $"{id}.log"), "the transcript\n");
        Directory.CreateDirectory(Path.Combine(Sessions, id, "files"));
        File.WriteAllText(Path.Combine(Sessions, id, "files", "shot.png"), "png");
        File.WriteAllText(Path.Combine(Sessions, id + HarnessConversations.Suffix), "{}");
        File.WriteAllText(Path.Combine(Sessions, id + ".pid"), "1 1");
        File.WriteAllText(Path.Combine(Sessions, id + GoOnMarks.Suffix), "{}");
        File.WriteAllText(Path.Combine(Sessions, id + NewSessionChoices.Suffix), "{}");
        Directory.CreateDirectory(Path.Combine(_home, SpawnServers.Folder));
        File.WriteAllText(Path.Combine(_home, SpawnServers.Folder, $"{id}.mcp.json"), "{}");
        File.WriteAllText(Path.Combine(_home, SpawnServers.Folder, $"{id}.settings.json"), "{}");
    }

    private string[] KeptOf(string id) =>
    [
        .. new[] { ".events.jsonl", ".log", HarnessConversations.Suffix, ".pid", GoOnMarks.Suffix, NewSessionChoices.Suffix }
            .Select(suffix => Path.Combine(Sessions, id + suffix))
            .Where(File.Exists),
        .. new[] { ".mcp.json", ".settings.json" }.Select(suffix => Path.Combine(_home, SpawnServers.Folder, id + suffix)).Where(File.Exists),
        .. Directory.Exists(Path.Combine(Sessions, id)) ? [Path.Combine(Sessions, id)] : Array.Empty<string>(),
    ];

    private void KeptFiles(string folder, string id)
    {
        Directory.CreateDirectory(Path.Combine(_home, folder, id, "attachments"));
        File.WriteAllText(Path.Combine(_home, folder, id, "attachments", "abc123-spec.md"), "a spec");
    }

    private static readonly DateTimeOffset At = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    // ——— A closed quest's work.

    /// <summary>
    /// §5's order for one closed quest's work: the service clears the records, then every file the home kept of each of its
    /// sessions goes, its landing's trace, its abandon's entry, its archive mark and held words, and <c>driver.json</c>'s marks for
    /// the quest (§4). Another quest's are untouched, and the log line counts without a word or an id.
    /// </summary>
    [Fact]
    public async Task A_closed_quests_work_goes_with_every_file_the_home_kept_of_it_and_a_line_with_counts_only()
    {
        Kept("s1");
        Kept("s2");
        Kept("o1");
        KeptFiles("quests", "q1");
        KeptFiles("quests", "q2");
        new LandedBranches(_home).Record(new LandedBranch("engine", "default", "feature/q1-fix", "main", "abc123", "s1", "q1", null, At));
        new LandedBranches(_home).Gone("engine", ["feature/q1-fix"]);
        new LandedBranches(_home).Record(new LandedBranch("engine", "default", "feature/q2-fix", "main", "def456", "o1", "q2", null, At));
        new LandedBranches(_home).Gone("engine", ["feature/q2-fix"]);
        var landings = new AutoLandings(_home);
        landings.Due(new AutoLanding("s1", "q1", "engine", "default", "a tree", At));
        landings.Tried("s1", new AutoTry(At, AutoLandingCode.Landed), close: true);
        new SessionArchive(_home).Archive(["s2", "o1"], [new SessionGrouping("s2", SessionGroup.Ended, "failed"), new SessionGrouping("o1", SessionGroup.Ended, "completed")], ["s1", "s2", "o1"], At);
        File.WriteAllText(HeldWordsFile.PathOf(_home), """{ "held": [ { "session": "s1", "text": "words", "files": [], "door": "screen" } ] }""");
        new AbandonRecord(_home).Write(new AbandonEntry("quest", "q1", At, "screen", "not needed"));
        new AbandonRecord(_home).Write(new AbandonEntry("quest", "q2", At, "screen", "kept"));
        (DriverConfig.Empty
            .WithForgiven("q1", 3).WithForgiven("q2", 1)
            .WithReleased("q1", "s2")
            .WithPausedQuest("q1", new WorkPause(At, new Dictionary<string, string>()))).Save(ConfigPath);

        var service = new StandIn
        {
            Records = [Record("s1", "q1"), Record("s2", "q1", state: "failed"), Record("o1", "q2")],
            Quests = [Quest("q1"), Quest("q2")],
        };
        service.Listings["quest=q1"] = [Unit("quest", "q1", quests: ["q1"], sessions: ["s1", "s2"])];
        var log = new LogLines(_home);

        var plan = await HistoryClearing.PlanAsync(World(service, log.Log), HistoryScope.Quest, "#q1");
        var unit = Assert.Single(plan.Units);
        Assert.True(unit.Clearable);
        Assert.True(unit.Bytes.Total > 0);
        Assert.True(unit.Bytes.Kept > 0);
        Assert.Empty(service.Pressed);

        var outcome = await HistoryClearing.ClearAsync(World(service, log.Log), HistoryScope.Quest, "q1", [unit.Name], PluginEvents.Screen);

        Assert.Equal(["quest:q1"], service.Pressed);
        Assert.Equal((1, 2, 0, 0), (outcome.Quests, outcome.Sessions, outcome.Changed.Count, outcome.Failed));
        Assert.Equal(unit.Bytes.Total, outcome.Bytes);
        Assert.Empty(KeptOf("s1"));
        Assert.Empty(KeptOf("s2"));
        Assert.NotEmpty(KeptOf("o1"));
        Assert.True(Directory.Exists(Path.Combine(_home, "quests", "q2")));
        Assert.Null(new AutoLandings(_home).Of("s1"));
        Assert.False(File.Exists(HeldWordsFile.PathOf(_home)));
        Assert.Equal(["o1"], new SessionArchive(_home).Marks().Keys);
        Assert.Null(new LandedBranches(_home).Landing("s1"));
        Assert.NotNull(new LandedBranches(_home).Landing("o1"));
        Assert.Equal(["q2"], new AbandonRecord(_home).Entries().Select(entry => entry.Id));
        var config = DriverConfig.Load(ConfigPath);
        Assert.Equal(["q2"], config.Forgiven.Keys);
        Assert.Empty(config.Released);
        Assert.Empty(config.PausedQuests);

        var line = Assert.Single(log.Lines(), each => each.Contains("\"history.cleared\"", StringComparison.Ordinal));
        Assert.Contains("\"scope\":\"quest\"", line);
        Assert.Contains("\"quests\":1", line);
        Assert.Contains("\"sessions\":2", line);
        Assert.Contains("\"forgotten\":0", line);
        Assert.Contains("\"kept\":0", line);
        Assert.Contains("\"door\":\"screen\"", line);
        Assert.Contains($"\"bytes\":{outcome.Bytes}", line);
        foreach (var word in new[] { "q1", "s1", "s2", "secret", "title" }) Assert.DoesNotContain(word, line);
    }

    /// <summary>
    /// A teammate's record is forgotten here with the quest it served (D153 point 3): its copy goes, and so does this machine's
    /// archive mark of it, though it has no file here. A quest a remote numbered is counted as forgotten.
    /// </summary>
    [Fact]
    public async Task A_teammates_copy_goes_with_its_quest_and_takes_its_archive_mark()
    {
        new SessionArchive(_home).Archive(["laptop/t1"], [new SessionGrouping("laptop/t1", SessionGroup.Ended, "completed")], ["laptop/t1"], At);
        var service = new StandIn { Records = [Record("laptop/t1", "q1")], Quests = [Quest("q1")] };
        service.Listings["quest=q1"] = [Unit("quest", "q1", quests: ["q1"], teammates: ["laptop/t1"], forgotten: ["q1"])];

        var outcome = await HistoryClearing.ClearAsync(World(service), HistoryScope.Quest, "q1", [new HistoryUnitName("quest", "q1")], PluginEvents.Terminal);

        Assert.Equal((1, 1, 1), (outcome.Quests, outcome.Teammates, outcome.Forgotten));
        Assert.Empty(new SessionArchive(_home).Marks());
    }

    // ——— The service's word, said first.

    /// <summary>A unit's kind, the service's word and what it names (quest, ask, session, machine), and the sentence meant.</summary>
    public static TheoryData<string, string, string?, string?, string?, string?, string?> ServiceWords => new()
    {
        { "quest", "unknown", "q1", null, null, null, HistoryContexts.Quest },
        { "ask", "unknown", null, "a1", null, null, null },
        { "quest", "open", "q1", null, null, null, null },
        { "quest", "open", "qt", null, null, null, HistoryContexts.Taken },
        { "failed", "open", "q1", null, null, null, HistoryContexts.Failed },
        { "quest", "asked", "q1", "a1", null, null, null },
        { "quest", "live", null, null, "s1", null, null },
        { "quest", "live", null, null, "laptop/t1", "laptop", HistoryContexts.Teammate },
        { "quest", "needs-you", null, null, "parked1", null, null },
        { "quest", "needs-you", null, null, "s1", null, HistoryContexts.Proposal },
        { "quest", "needs-you", "qh", null, null, null, HistoryContexts.Held },
        { "quest", "needs-you", "q1", null, null, null, HistoryContexts.Conflict },
        { "ask", "needs-you", null, "aopen", null, null, HistoryContexts.Ask },
        { "ask", "needs-you", null, "a1", null, null, HistoryContexts.ProposalAsk },
        { "quest", "awaited", "qw", null, "s1", null, HistoryContexts.Published },
        { "quest", "awaited", "qa", null, null, null, null },
        { "quest", "awaited", "qt", null, null, null, HistoryContexts.Chain },
        { "quest", "unpushed", "q1", null, null, null, null },
    };

    /// <summary>
    /// The service's refusal keeps the unit whole, with its word and what it names (§1.2); which of the word's sentences is meant
    /// is read from the records the word names, never from the sentence. Pressed, nothing is sent and nothing is removed.
    /// </summary>
    [Theory]
    [MemberData(nameof(ServiceWords))]
    public async Task The_services_word_keeps_the_unit_whole_said_by_the_records_it_names(
        string kind, string word, string? quest, string? ask, string? session, string? origin, string? context)
    {
        var refusal = Refusal(word, quest, ask, session, origin);
        Kept("s1");
        var service = new StandIn
        {
            Records = [Record("s1", "q1"), Record("parked1", "q1", state: "awaiting-person")],
            Quests = [Quest("q1"), Quest("qt", status: "Taken"), Quest("qh", held: true), Quest("qa", status: "Taken", awaits: "q1"), Quest("qw", status: "Open")],
            Asks = [Ask("a1"), Ask("aopen", state: "Open")],
        };
        var id = kind == "ask" ? "a1" : "q1";
        var scope = kind switch { "ask" => HistoryScope.Ask, "failed" => HistoryScope.Failed, _ => HistoryScope.Quest };
        var query = kind switch { "ask" => "ask=a1", "failed" => "quest=q1&failed=true", _ => "quest=q1" };
        service.Listings[query] = [Unit(kind, id, quests: kind == "failed" ? [] : ["q1"], sessions: ["s1"], refusal: refusal)];

        var keep = Assert.Single((await HistoryClearing.PlanAsync(World(service), scope, id)).Units).Keep;
        var outcome = await HistoryClearing.ClearAsync(World(service), scope, id, [new HistoryUnitName(kind, id)], PluginEvents.Screen);

        Assert.NotNull(keep);
        Assert.Equal((word, context), (keep.Word, keep.Context));
        Assert.Equal($"the desk's {word} sentence", keep.Message);
        Assert.Equal((quest, ask, session, origin), (keep.Quest, keep.Ask, keep.Session, keep.Machine));
        Assert.Empty(service.Pressed);
        Assert.Equal((0, 1), (outcome.Cleared.Count, outcome.Changed.Count));
        Assert.NotEmpty(KeptOf("s1"));
    }

    // ——— This machine's half.

    /// <summary>A tree still here keeps the unit (§1.2), and the service is never asked to clear it: the clear never touches a tree.</summary>
    [Fact]
    public async Task A_tree_still_here_keeps_the_unit_and_the_service_is_not_asked()
    {
        var tree = Path.Combine(_home, "trees", "default", "engine", "s-1a2b3c4d");
        Directory.CreateDirectory(tree);
        File.WriteAllText(Path.Combine(tree, "work.txt"), "work");
        Kept("s1");
        var service = new StandIn { Records = [Record("s1", "q1", tree: tree)], Quests = [Quest("q1")] };
        service.Listings["quest=q1"] = [Unit("quest", "q1", quests: ["q1"], sessions: ["s1"])];

        var outcome = await HistoryClearing.ClearAsync(World(service), HistoryScope.Quest, "q1", [new HistoryUnitName("quest", "q1")], PluginEvents.Screen);

        var keep = Assert.Single(outcome.Changed).Keep!;
        Assert.Equal((HistoryWords.TreeHere, "s1"), (keep.Word, keep.Session));
        Assert.DoesNotContain(_home, keep.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(service.Pressed);
        Assert.True(File.Exists(Path.Combine(tree, "work.txt")));
        Assert.NotEmpty(KeptOf("s1"));
    }

    /// <summary>
    /// A landing's branch that still stands keeps the unit, named by its branch and repository, whether the session made it or
    /// a later done moved it on (LAND2c); a trace does not, and goes with its session.
    /// </summary>
    [Fact]
    public async Task A_landings_standing_branch_keeps_the_unit_and_a_trace_does_not()
    {
        var landings = new LandedBranches(_home);
        landings.Record(new LandedBranch("engine", "default", "feature/q0-fix", "main", "abc123", "s0", "q0", null, At));
        landings.Advanced("engine", "feature/q0-fix", new LandedAdvance("abc123", "abc124", At, "s1"));
        var service = new StandIn { Records = [Record("s1", "q1")], Quests = [Quest("q1")] };
        service.Listings["quest=q1"] = [Unit("quest", "q1", quests: ["q1"], sessions: ["s1"])];

        var keep = Assert.Single((await HistoryClearing.PlanAsync(World(service), HistoryScope.Quest, "q1")).Units).Keep!;

        Assert.Equal((HistoryWords.LandingStands, "feature/q0-fix", "engine", "s1"), (keep.Word, keep.Branch, keep.Repository, keep.Session));

        landings.Gone("engine", ["feature/q0-fix"]);
        var outcome = await HistoryClearing.ClearAsync(World(service), HistoryScope.Quest, "q1", [new HistoryUnitName("quest", "q1")], PluginEvents.Screen);

        Assert.Single(outcome.Cleared);
        // The trace names s0 too, whose record is not here either, so nothing it names is held any more.
        Assert.Null(landings.Landing("s1"));
    }

    /// <summary>An automatic landing still trying keeps the unit as a live one (§2.2): its work is still being landed.</summary>
    [Fact]
    public async Task An_automatic_landing_still_trying_keeps_the_unit()
    {
        new AutoLandings(_home).Due(new AutoLanding("s1", "q1", "engine", "default", "a tree", At));
        var service = new StandIn { Records = [Record("s1", "q1")], Quests = [Quest("q1")] };
        service.Listings["quest=q1"] = [Unit("quest", "q1", quests: ["q1"], sessions: ["s1"])];

        var keep = Assert.Single((await HistoryClearing.PlanAsync(World(service), HistoryScope.Quest, "q1")).Units).Keep!;

        Assert.Equal((HistoryWords.Live, HistoryContexts.Landing, "s1"), (keep.Word, keep.Context, keep.Session));
    }

    /// <summary>A process still running for a session, marked by a driver sharing the home, keeps the unit: stop it first.</summary>
    [Fact]
    public async Task A_process_still_running_here_keeps_the_unit()
    {
        using var self = Process.GetCurrentProcess();
        File.WriteAllText(Path.Combine(Sessions, "s1.pid"), $"{self.Id} {self.StartTime.ToUniversalTime().Ticks}");
        var service = new StandIn { Records = [Record("s1", "q1")], Quests = [Quest("q1")] };
        service.Listings["quest=q1"] = [Unit("quest", "q1", quests: ["q1"], sessions: ["s1"])];

        var keep = Assert.Single((await HistoryClearing.PlanAsync(World(service), HistoryScope.Quest, "q1")).Units).Keep!;

        Assert.Equal((HistoryWords.Live, null, "s1"), (keep.Word, keep.Context, keep.Session));
        Assert.True(File.Exists(Path.Combine(Sessions, "s1.pid")));
    }

    // ——— The other scopes.

    /// <summary>
    /// A closed quest's failed sessions (§1.1, §4): this machine's go with their files; the quest, its other sessions and its
    /// marks in <c>driver.json</c> stay, and a teammate's failed session is listed and kept.
    /// </summary>
    [Fact]
    public async Task A_closed_quests_failed_sessions_go_and_its_quest_its_other_sessions_and_its_marks_stay()
    {
        Kept("f1");
        Kept("s1");
        DriverConfig.Empty.WithForgiven("q1", 3).Save(ConfigPath);
        var service = new StandIn
        {
            Records = [Record("f1", "q1", state: "failed"), Record("s1", "q1"), Record("laptop/f2", "q1", state: "failed")],
            Quests = [Quest("q1")],
        };
        service.Listings["quest=q1&failed=true"] =
            [Unit("failed", "q1", sessions: ["f1"], kept: [Refusal("not-ours", session: "laptop/f2", origin: "laptop")])];

        var unit = Assert.Single((await HistoryClearing.PlanAsync(World(service), HistoryScope.Failed, "q1")).Units);
        var outcome = await HistoryClearing.ClearAsync(World(service), HistoryScope.Failed, "q1", [unit.Name], PluginEvents.Screen);

        Assert.Equal((HistoryWords.NotOurs, "laptop"), (Assert.Single(unit.Kept).Word, unit.Kept[0].Machine));
        Assert.Equal(["failed:q1"], service.Pressed);
        Assert.Equal((0, 1), (outcome.Quests, outcome.Sessions));
        Assert.Empty(KeptOf("f1"));
        Assert.NotEmpty(KeptOf("s1"));
        Assert.Equal(3, DriverConfig.Load(ConfigPath).ForgivenAt("q1"));
    }

    /// <summary>
    /// An ask's work (§1.1): its intake goes with its quests' sessions, and its pause with it; the intake's room stays, since only
    /// a workspace's clear that leaves it no ask takes the room.
    /// </summary>
    [Fact]
    public async Task An_asks_work_takes_its_intake_and_its_pause_and_keeps_the_room()
    {
        Kept("i1");
        Kept("s1");
        var room = IntakeRoom.PathOf(_home, "default");
        Directory.CreateDirectory(room);
        File.WriteAllText(Path.Combine(room, "AGENTS.md"), "the room");
        DriverConfig.Empty.WithPausedAsk("a1", new WorkPause(At, new Dictionary<string, string>())).Save(ConfigPath);
        var service = new StandIn
        {
            Records = [Record("i1", ask: "a1", kind: "chat"), Record("s1", "q1")],
            Quests = [Quest("q1")],
            Asks = [Ask("a1")],
        };
        service.Listings["ask=a1"] = [Unit("ask", "a1", quests: ["q1"], asks: ["a1"], sessions: ["i1", "s1"])];

        var outcome = await HistoryClearing.ClearAsync(World(service), HistoryScope.Ask, "#a1", [new HistoryUnitName("ask", "a1")], PluginEvents.Screen);

        Assert.Equal((1, 1, 2), (outcome.Asks, outcome.Quests, outcome.Sessions));
        Assert.Empty(KeptOf("i1"));
        Assert.Empty(DriverConfig.Load(ConfigPath).PausedAsks);
        Assert.True(File.Exists(Path.Combine(room, "AGENTS.md")));
        Assert.False(outcome.Intake);
    }

    // ——— A workspace.

    /// <summary>
    /// A workspace's reading (§2.4) and its clear (§5): what may go goes and what is kept stays with its word; the home's
    /// left-over files of records no store has (§2.3), untouched for an hour, go with it, and so do <c>driver.json</c>'s marks for
    /// quests no store has, which the hand purge of 2026-10-07 left; the intake's room goes once the workspace keeps no ask. A
    /// file Daoris did not write, a terminal's requests and a file touched within the hour are never left over.
    /// </summary>
    [Fact]
    public async Task A_workspaces_clear_takes_what_may_go_its_left_over_files_and_its_room()
    {
        var now = DateTimeOffset.UtcNow + TimeSpan.FromHours(2);
        Kept("s1");
        Kept("s2");
        Kept("c1");
        KeptFiles("quests", "q1");
        var tree = Path.Combine(_home, "trees", "aurora", "engine", "s-2b2b2b2b");
        Directory.CreateDirectory(tree);
        File.WriteAllText(Path.Combine(tree, "work.txt"), "work");
        // Left over: the files of records no store has, and kept files of a quest and an ask no store has.
        File.WriteAllText(Path.Combine(Sessions, "gone1.log"), "an old transcript");
        Directory.CreateDirectory(Path.Combine(Sessions, "gone1", "files"));
        File.WriteAllText(Path.Combine(Sessions, "gone1", "files", "x.png"), "png");
        Directory.CreateDirectory(Path.Combine(_home, SpawnServers.Folder));
        File.WriteAllText(Path.Combine(_home, SpawnServers.Folder, "gone2.settings.json"), "{}");
        KeptFiles("quests", "qgone");
        KeptFiles("asks", "agone");
        // Never left over: a file touched within the hour, a file Daoris did not write, and a terminal's requests.
        File.WriteAllText(Path.Combine(Sessions, "young1.log"), "being written");
        File.SetLastWriteTimeUtc(Path.Combine(Sessions, "young1.log"), now.UtcDateTime);
        File.WriteAllText(Path.Combine(Sessions, "notes.txt"), "the person's own");
        Directory.CreateDirectory(Path.Combine(Sessions, "requests"));
        File.WriteAllText(Path.Combine(Sessions, "requests", "r1.json"), "{}");
        var room = IntakeRoom.PathOf(_home, "aurora");
        Directory.CreateDirectory(room);
        File.WriteAllText(Path.Combine(room, "AGENTS.md"), "the room");
        DriverConfig.Empty.WithForgiven("qgone", 4).WithForgiven("q2", 1).Save(ConfigPath);

        var service = new StandIn
        {
            Records =
            [
                Record("s1", "q1", workspace: "aurora"), Record("s2", "q2", tree: tree, workspace: "aurora"),
                Record("c1", kind: "chat", workspace: "aurora"), Record("laptop/t1", "q1", workspace: "aurora"),
            ],
            Quests = [Quest("q1", workspace: "aurora"), Quest("q2", workspace: "aurora"), Quest("q3", status: "Open", workspace: "aurora")],
            Asks = [Ask("a9", workspace: "elsewhere")],
        };
        service.Listings["workspace=aurora"] =
        [
            Unit("quest", "q1", "aurora", quests: ["q1"], sessions: ["s1"], teammates: ["laptop/t1"]),
            Unit("quest", "q2", "aurora", quests: ["q2"], sessions: ["s2"]),
        ];
        var world = World(service, clock: () => now);

        var plan = await HistoryClearing.PlanAsync(world, HistoryScope.Workspace, "aurora");

        var reading = plan.Reading!;
        Assert.Equal((2, 0, 2, 1), (reading.Quests, reading.Asks, reading.Sessions, reading.Teammates));
        Assert.Equal((1, 0, 1, 1), (reading.Takes.Quests, reading.Takes.Asks, reading.Takes.Sessions, reading.Takes.Teammates));
        Assert.Equal(1, reading.KeptBy[HistoryWords.TreeHere]);
        Assert.Equal((1, 5), (reading.Conversations, reading.LeftOver));
        Assert.True(reading.ConversationBytes > 0 && reading.LeftOverBytes > 0 && reading.Intake > 0);
        Assert.True(reading.Bytes.Conversations > 0 && reading.Bytes.Transcripts > 0 && reading.Bytes.Files > 0 && reading.Bytes.Kept > 0);
        Assert.Equal(plan.Units.Where(unit => unit.Clearable).Sum(unit => unit.Bytes.Total) + reading.Intake + reading.LeftOverBytes, reading.Takes.Bytes);

        var outcome = await HistoryClearing.ClearAsync(
            world, HistoryScope.Workspace, "aurora", [.. plan.Units.Where(unit => unit.Clearable).Select(unit => unit.Name)], PluginEvents.Screen);

        Assert.Equal(["quest:q1"], service.Pressed);
        Assert.Equal((1, 5, true), (outcome.Cleared.Count, outcome.LeftOver, outcome.Intake));
        Assert.Equal(reading.Takes.Bytes, outcome.Bytes);
        Assert.Empty(KeptOf("s1"));
        Assert.NotEmpty(KeptOf("s2"));
        Assert.NotEmpty(KeptOf("c1"));
        Assert.True(File.Exists(Path.Combine(tree, "work.txt")));
        Assert.False(File.Exists(Path.Combine(Sessions, "gone1.log")));
        Assert.False(Directory.Exists(Path.Combine(Sessions, "gone1")));
        Assert.False(File.Exists(Path.Combine(_home, SpawnServers.Folder, "gone2.settings.json")));
        Assert.False(Directory.Exists(Path.Combine(_home, "quests", "qgone")));
        Assert.False(Directory.Exists(Path.Combine(_home, "asks", "agone")));
        Assert.True(File.Exists(Path.Combine(Sessions, "young1.log")));
        Assert.True(File.Exists(Path.Combine(Sessions, "notes.txt")));
        Assert.True(File.Exists(Path.Combine(Sessions, "requests", "r1.json")));
        Assert.False(Directory.Exists(room));
        Assert.Equal(["q2"], DriverConfig.Load(ConfigPath).Forgiven.Keys);
    }

    /// <summary>A workspace that keeps an ask keeps its intake's room (§2.2).</summary>
    [Fact]
    public async Task A_workspace_that_keeps_an_ask_keeps_its_room()
    {
        var room = IntakeRoom.PathOf(_home, "aurora");
        Directory.CreateDirectory(room);
        File.WriteAllText(Path.Combine(room, "AGENTS.md"), "the room");
        var service = new StandIn { Asks = [Ask("a1", state: "Published", workspace: "aurora")] };
        service.Listings["workspace=aurora"] = [];

        var outcome = await HistoryClearing.ClearAsync(World(service), HistoryScope.Workspace, "aurora", [], PluginEvents.Screen);

        Assert.False(outcome.Intake);
        Assert.True(Directory.Exists(room));
    }

    // ——— Listed first, then pressed.

    /// <summary>
    /// A unit the list said may go and that changed since (§5) is judged again before anything is sent: it is kept and counted,
    /// and a press that took nothing writes no line.
    /// </summary>
    [Fact]
    public async Task A_unit_that_changed_since_the_list_is_kept_and_counted_and_nothing_is_written()
    {
        Kept("s1");
        var service = new StandIn { Records = [Record("s1", "q1")], Quests = [Quest("q1", status: "Taken")] };
        service.Listings["quest=q1"] = [Unit("quest", "q1", quests: ["q1"], sessions: ["s1"], refusal: Refusal("open", quest: "q1"))];
        var log = new LogLines(_home);

        var outcome = await HistoryClearing.ClearAsync(World(service, log.Log), HistoryScope.Quest, "q1", [new HistoryUnitName("quest", "q1")], PluginEvents.Screen);

        Assert.Equal((1, 0, 1), (outcome.Listed, outcome.Cleared.Count, outcome.Changed.Count));
        Assert.False(outcome.Took);
        Assert.Empty(service.Pressed);
        Assert.DoesNotContain(log.Lines(), line => line.Contains("history.cleared", StringComparison.Ordinal));
    }

    /// <summary>The service judges again inside its transaction: a unit it refuses at the press stays with its word, and no file goes.</summary>
    [Fact]
    public async Task A_unit_the_service_refuses_at_the_press_keeps_every_file()
    {
        Kept("s1");
        var service = new StandIn { Records = [Record("s1", "q1")], Quests = [Quest("q1")] };
        service.Listings["quest=q1"] = [Unit("quest", "q1", quests: ["q1"], sessions: ["s1"])];
        service.Refusing["quest:q1"] = Unit("quest", "q1", quests: ["q1"], sessions: ["s1"], refusal: Refusal("unpushed", quest: "q1", workspace: "aurora"));

        var outcome = await HistoryClearing.ClearAsync(World(service), HistoryScope.Quest, "q1", [new HistoryUnitName("quest", "q1")], PluginEvents.Screen);

        var keep = Assert.Single(outcome.Changed).Keep!;
        Assert.Equal((HistoryWords.Unpushed, "aurora"), (keep.Word, keep.Workspace));
        Assert.Empty(outcome.Cleared);
        Assert.NotEmpty(KeptOf("s1"));
    }

    /// <summary>
    /// A press on one quest's page clears that quest's unit and no other, whatever else it sends; a unit the service no longer
    /// knows is kept as unknown.
    /// </summary>
    [Fact]
    public async Task A_quests_press_clears_only_its_own_unit()
    {
        var service = new StandIn { Quests = [Quest("q1"), Quest("q2")] };
        service.Listings["quest=q1"] = [Unit("quest", "q1", quests: ["q1"])];
        service.Listings["quest=q2"] = [Unit("quest", "q2", quests: ["q2"])];

        var outcome = await HistoryClearing.ClearAsync(
            World(service), HistoryScope.Quest, "q1", [new HistoryUnitName("quest", "q2"), new HistoryUnitName("quest", "q1")], PluginEvents.Screen);
        var gone = await HistoryClearing.ClearAsync(World(service), HistoryScope.Quest, "q9", [new HistoryUnitName("quest", "q9")], PluginEvents.Screen);

        Assert.Equal(["quest:q1"], service.Pressed);
        Assert.Equal(1, outcome.Listed);
        Assert.Equal((HistoryWords.Unknown, HistoryContexts.Quest), (gone.Changed.Single().Keep!.Word, gone.Changed.Single().Keep!.Context));
    }

    /// <summary>
    /// An empty folder a cleared session's tree left under the trees home goes with each empty parent it leaves (§2.2); a tree
    /// beside it with anything in it, and the trees home itself, stay.
    /// </summary>
    [Fact]
    public async Task An_empty_folder_a_cleared_sessions_tree_left_goes_with_its_empty_parents()
    {
        var trees = Path.Combine(_home, "trees");
        var empty = Path.Combine(trees, "aurora", "engine", "s-1a1a1a1a");
        var other = Path.Combine(trees, "default", "game", "s-3c3c3c3c");
        Directory.CreateDirectory(empty);
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, "work.txt"), "work");
        var service = new StandIn { Records = [Record("s1", "q1", tree: empty)], Quests = [Quest("q1")] };
        service.Listings["quest=q1"] = [Unit("quest", "q1", quests: ["q1"], sessions: ["s1"])];

        await HistoryClearing.ClearAsync(World(service), HistoryScope.Quest, "q1", [new HistoryUnitName("quest", "q1")], PluginEvents.Screen);

        Assert.False(Directory.Exists(Path.Combine(trees, "aurora")));
        Assert.True(File.Exists(Path.Combine(other, "work.txt")));
        Assert.True(Directory.Exists(trees));
    }

    /// <summary>A host older than the door says so, before anything is judged or removed.</summary>
    [Fact]
    public async Task A_service_with_no_history_door_says_so()
    {
        Kept("s1");
        var service = new StandIn { Records = [Record("s1", "q1")], NoDoor = true };

        var refused = await Assert.ThrowsAsync<DriverException>(() => HistoryClearing.PlanAsync(World(service), HistoryScope.Quest, "q1"));

        Assert.Contains("no history door", refused.Message);
        Assert.NotEmpty(KeptOf("s1"));
    }

    /// <summary>The machine log's lines this test's home holds, as written.</summary>
    private sealed class LogLines(string home)
    {
        public MachineLog Log { get; } = new(home, "desktop");

        public IReadOnlyList<string> Lines()
        {
            Log.Dispose();
            var folder = Path.Combine(home, MachineLog.Folder);
            return Directory.Exists(folder) ? [.. Directory.GetFiles(folder).SelectMany(StubFile.Lines)] : [];
        }
    }

    /// <summary>
    /// The service's doors a clear reads and presses, in-process: the records, the quests and the asks; each scope's listing as
    /// set; and a press that clears what it names, taking its records and its kept files as the service does, unless told to
    /// refuse it.
    /// </summary>
    private sealed class StandIn : HttpMessageHandler
    {
        public List<JsonObject> Records { get; init; } = [];

        public List<JsonObject> Quests { get; init; } = [];

        public List<JsonObject> Asks { get; init; } = [];

        /// <summary>Each listing by its query, as the door answers it.</summary>
        public Dictionary<string, JsonArray> Listings { get; } = new(StringComparer.Ordinal);

        /// <summary>A unit the press refuses, by <c>kind:id</c>, with the unit as the service then judges it.</summary>
        public Dictionary<string, JsonObject> Refusing { get; } = new(StringComparer.Ordinal);

        /// <summary>Each unit a press sent, <c>kind:id</c>.</summary>
        public List<string> Pressed { get; } = [];

        /// <summary>A host from before the door: a bare 404.</summary>
        public bool NoDoor { get; init; }

        /// <summary>The home whose kept files a press takes after the records, as the service's desk does.</summary>
        public string? Home { get; set; }

        public ServiceClient Client() => new("http://stand-in.test", null, new HttpClient(this, disposeHandler: false));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var query = request.RequestUri.Query.TrimStart('?');
            if (request.Method == HttpMethod.Get)
            {
                return path switch
                {
                    "/api/sessions" => Answer(HttpStatusCode.OK, new JsonArray([.. Records.Select(record => record.DeepClone())])),
                    "/api/quests" => Answer(HttpStatusCode.OK, new JsonArray([.. Quests.Select(quest => quest.DeepClone())])),
                    "/api/asks" => Answer(HttpStatusCode.OK, new JsonArray([.. Asks.Select(ask => ask.DeepClone())])),
                    "/api/history" when NoDoor => new HttpResponseMessage(HttpStatusCode.NotFound),
                    "/api/history" => Answer(HttpStatusCode.OK, new JsonObject
                    {
                        ["units"] = Listings.TryGetValue(Uri.UnescapeDataString(query), out var units) ? units.DeepClone() : new JsonArray(),
                    }),
                    _ => new HttpResponseMessage(HttpStatusCode.NotFound),
                };
            }

            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!;
            var answers = new JsonArray();
            foreach (var named in body["units"]!.AsArray())
            {
                var key = $"{(string?)named!["kind"]}:{(string?)named["id"]}";
                Pressed.Add(key);
                if (Refusing.TryGetValue(key, out var refused))
                {
                    answers.Add(new JsonObject { ["unit"] = refused.DeepClone(), ["cleared"] = false, ["message"] = "kept" });
                    continue;
                }

                var unit = Listings.Values.SelectMany(units => units).OfType<JsonObject>()
                    .First(each => $"{(string?)each["kind"]}:{(string?)each["id"]}" == key);
                bool In(string field, JsonObject row) => unit[field]!.AsArray().Any(id => (string?)id == (string?)row["id"]);
                Records.RemoveAll(record => In("sessions", record) || In("teammates", record));
                Quests.RemoveAll(quest => In("quests", quest));
                Asks.RemoveAll(ask => In("asks", ask));
                foreach (var (field, folder) in new[] { ("quests", "quests"), ("asks", "asks") })
                {
                    foreach (var id in unit[field]!.AsArray().Select(id => (string?)id).OfType<string>())
                    {
                        var kept = Path.Combine(Home ?? "", folder, id);
                        if (Home is not null && Directory.Exists(kept)) Directory.Delete(kept, recursive: true);
                    }
                }
                answers.Add(new JsonObject { ["unit"] = unit.DeepClone(), ["cleared"] = true, ["message"] = $"Cleared `#{(string?)unit["id"]}`." });
            }

            return Answer(HttpStatusCode.OK, new JsonObject { ["units"] = answers });
        }

        private static HttpResponseMessage Answer(HttpStatusCode status, JsonNode body) => new(status)
        {
            Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        };
    }
}
