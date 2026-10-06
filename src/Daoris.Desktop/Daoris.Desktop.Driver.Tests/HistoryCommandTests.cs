using System.Text.Json;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.HistoryStandIn;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HIST1d (D153 point 7; the history-clearing design §6.2, D50): the terminal's history verbs, so a machine with no screen clears
/// as the window does. <c>history</c> reads what this machine keeps of finished work, per workspace; <c>history clear
/// --workspace</c>, <c>quest clear [--failed]</c> and <c>ask --clear</c> list what a clear would take and keep, and clear it only
/// with <c>--yes</c>, through the driver library's <see cref="HistoryClearing"/> at the door <c>terminal</c>. Each kept unit is
/// said in its keep's own sentence. Exit codes are the family's: 0 done, listed or nothing to do · 1 a unit named was kept ·
/// 2 could not.
/// </summary>
/// <remarks>A scratch home and the in-process stand-in HIST1c's tests read (<see cref="HistoryStandIn"/>): the suite's fast half.</remarks>
public sealed class HistoryCommandTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-history-verbs-" + Guid.NewGuid().ToString("N")[..8]);

    public HistoryCommandTests() => Directory.CreateDirectory(Path.Combine(_home, "sessions"));

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private string Sessions => Path.Combine(_home, "sessions");

    private HistoryWorld World(HistoryStandIn service, MachineLog? log = null)
    {
        service.Home = _home;
        return new HistoryWorld(service.Client(), _home, Path.Combine(_home, "driver.json"), new SessionProcesses(Sessions)) { Log = log };
    }

    private async Task<(int Exit, string Said)> RunAsync(HistoryStandIn service, HistoryAsk? ask, MachineLog? log = null)
    {
        Assert.NotNull(ask);
        var output = new StringWriter();
        var exit = await HistoryCommand.RunAsync(ask, World(service, log), output);
        return (exit, output.ToString().ReplaceLineEndings("\n"));
    }

    private Task<(int Exit, string Said)> HistoryAsync(HistoryStandIn service, params string[] args) =>
        RunAsync(service, HistoryCommand.Read(args, out _));

    private Task<(int Exit, string Said)> QuestAsync(HistoryStandIn service, params string[] args) =>
        RunAsync(service, HistoryCommand.Read(WorkScope.Quest, args, out _));

    private Task<(int Exit, string Said)> AskAsync(HistoryStandIn service, params string[] args) =>
        RunAsync(service, HistoryCommand.Read(WorkScope.Ask, args, out _));

    /// <summary>A session's conversation, transcript and files, each holding words.</summary>
    private void Kept(string id)
    {
        File.WriteAllText(Path.Combine(Sessions, $"{id}.events.jsonl"), "{\"seq\":1,\"kind\":\"user\",\"text\":\"a secret plan\"}\n");
        File.WriteAllText(Path.Combine(Sessions, $"{id}.log"), "the transcript\n");
        Directory.CreateDirectory(Path.Combine(Sessions, id, "files"));
        File.WriteAllText(Path.Combine(Sessions, id, "files", "shot.png"), "png");
    }

    private bool Holds(string id) => File.Exists(Path.Combine(Sessions, $"{id}.log"));

    /// <summary>A tree still here for a session: the clear never touches one, and it keeps the unit.</summary>
    private string Tree(string name, string workspace = "default")
    {
        var tree = Path.Combine(_home, "trees", workspace, "engine", name);
        Directory.CreateDirectory(tree);
        File.WriteAllText(Path.Combine(tree, "work.txt"), "work");
        return tree;
    }

    /// <summary>One closed quest's work, <c>q1</c>, with two sessions of this machine's.</summary>
    private HistoryStandIn ClosedQuest(string workspace = "default", string[]? forgotten = null)
    {
        Kept("s1");
        Kept("s2");
        var service = new HistoryStandIn
        {
            Records = [Record("s1", "q1", workspace: workspace), Record("s2", "q1", state: "failed", workspace: workspace)],
            Quests = [Quest("q1", workspace: workspace)],
        };
        service.Listings["quest=q1"] = [Unit("quest", "q1", workspace, quests: ["q1"], sessions: ["s1", "s2"], forgotten: forgotten)];
        return service;
    }

    // ——— The words it takes, and the usage.

    /// <summary>The usage, as the host prints it under a word it does not take, and the lines the host's own usage carries.</summary>
    [Fact]
    public void The_usage_names_each_verb_and_the_hosts_usage_carries_them()
    {
        Assert.Equal(
            "usage: daoris-driver history [--workspace <name>] [--json]\n"
            + "       daoris-driver history clear --workspace <name> [--yes]\n"
            + "       daoris-driver quest clear <id> [--failed] [--yes]  ·  daoris-driver ask --clear <id> [--yes]",
            HistoryCommand.Usage.ReplaceLineEndings("\n"));

        var usage = DriverCommand.Usage.ReplaceLineEndings("\n");
        Assert.Contains("\n  history [--workspace <name>] [--json]\n", usage);
        Assert.Contains("\n  history clear --workspace <name> [--yes]\n", usage);
        Assert.Contains("\n  quest clear <id> [--failed] [--yes]  ·  ask --clear <id> [--yes]\n", usage);
    }

    /// <summary>What each verb's words ask: a reading of one workspace or every one, a workspace's clear, a quest's, its failed sessions', an ask's.</summary>
    [Fact]
    public void The_words_ask_a_reading_or_a_clear_and_its_press()
    {
        Assert.Equal(new HistoryAsk(HistoryScope.Workspace, null), HistoryCommand.Read([], out var problem));
        Assert.Null(problem);
        Assert.Equal(new HistoryAsk(HistoryScope.Workspace, "aurora") { Json = true }, HistoryCommand.Read(["--workspace", "aurora", "--json"], out _));
        Assert.Equal(new HistoryAsk(HistoryScope.Workspace, null) { Json = true }, HistoryCommand.Read(["--json"], out _));
        Assert.Equal(new HistoryAsk(HistoryScope.Workspace, "aurora") { Clear = true }, HistoryCommand.Read(["clear", "--workspace", "aurora"], out _));
        Assert.Equal(new HistoryAsk(HistoryScope.Workspace, "aurora") { Clear = true, Yes = true },
            HistoryCommand.Read(["clear", "--yes", "--workspace", "aurora"], out _));

        Assert.Equal(new HistoryAsk(HistoryScope.Quest, "q1") { Clear = true }, HistoryCommand.Read(WorkScope.Quest, ["clear", "#q1"], out _));
        Assert.Equal(new HistoryAsk(HistoryScope.Failed, "q1") { Clear = true, Yes = true },
            HistoryCommand.Read(WorkScope.Quest, ["clear", "q1", "--failed", "--yes"], out _));
        Assert.Equal(new HistoryAsk(HistoryScope.Ask, "a1") { Clear = true, Yes = true }, HistoryCommand.Read(WorkScope.Ask, ["--clear", "a1", "--yes"], out _));

        Assert.True(HistoryCommand.Asks(WorkScope.Quest, ["clear", "q1"]));
        Assert.True(HistoryCommand.Asks(WorkScope.Ask, ["--clear", "a1"]));
        Assert.False(HistoryCommand.Asks(WorkScope.Quest, ["delete", "q1"]));
        Assert.False(HistoryCommand.Asks(WorkScope.Ask, ["--delete", "a1"]));
        Assert.False(HistoryCommand.Asks(WorkScope.Ask, ["clear", "a1"]));
    }

    public static TheoryData<string, string[]> Problems => new()
    {
        { "history", ["clear"] },
        { "history", ["clear", "--yes"] },
        { "history", ["clear", "--workspace"] },
        { "history", ["clear", "--workspace", "aurora", "--json"] },
        { "history", ["--workspace"] },
        { "history", ["--workspace", "a", "--workspace", "b"] },
        { "history", ["--yes"] },
        { "history", ["frobnicate"] },
        { "quest", ["clear"] },
        { "quest", ["clear", "--failed"] },
        { "quest", ["clear", "q1", "q2"] },
        { "quest", ["clear", "q1", "--yes", "--yes"] },
        { "ask", ["--clear"] },
        { "ask", ["--clear", "a1", "--failed"] },
        { "ask", ["--clear", "a1", "a2"] },
    };

    /// <summary>Words a verb does not take are a problem, said before any service is asked; the host prints it with the usage, exit 2.</summary>
    [Theory]
    [MemberData(nameof(Problems))]
    public void Words_it_does_not_take_are_a_problem(string verb, string[] args)
    {
        var ask = verb switch
        {
            "quest" => HistoryCommand.Read(WorkScope.Quest, args, out var problem) is null ? problem : null,
            "ask" => HistoryCommand.Read(WorkScope.Ask, args, out var problem) is null ? problem : null,
            _ => HistoryCommand.Read(args, out var problem) is null ? problem : null,
        };

        Assert.False(string.IsNullOrWhiteSpace(ask), $"`{verb} {string.Join(' ', args)}` was read as an ask");
    }

    // ——— One quest's work.

    /// <summary>
    /// Without <c>--yes</c> a clear is the first press: it says what it would take, what that holds on the disk and the line that
    /// clears it, and changes nothing. Exit 0.
    /// </summary>
    [Fact]
    public async Task A_quests_clear_lists_first_and_changes_nothing()
    {
        var service = ClosedQuest();

        var (exit, said) = await QuestAsync(service, "clear", "q1");

        Assert.Equal(0, exit);
        Assert.StartsWith("daoris-driver: clearing #q1 would take 1 quest and 2 sessions, with what this machine kept of them (", said);
        Assert.Contains("their words, transcripts and files. Nothing brings it back; `daoris-driver quest clear q1 --yes` clears it.", said);
        Assert.Empty(service.Pressed);
        Assert.True(Holds("s1") && Holds("s2"));
        Assert.DoesNotContain(_home, said, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("a title nobody reads", said);
    }

    /// <summary>
    /// With <c>--yes</c> it clears, through the library at the terminal's door: the service's records, then every file the home kept
    /// of each session, and one log line that names the terminal. Exit 0.
    /// </summary>
    [Fact]
    public async Task A_quests_clear_with_yes_clears_it_and_the_log_names_the_terminal()
    {
        var service = ClosedQuest();
        var log = new MachineLog(_home, "driver");

        var (exit, said) = await RunAsync(service, HistoryCommand.Read(WorkScope.Quest, ["clear", "q1", "--yes"], out _), log);
        log.Dispose();

        Assert.Equal(0, exit);
        Assert.StartsWith("daoris-driver: cleared #q1 from this machine: 1 quest and 2 sessions, ", said);
        Assert.Equal(["quest:q1"], service.Pressed);
        Assert.False(Holds("s1") || Holds("s2"));
        var line = Assert.Single(
            Directory.GetFiles(Path.Combine(_home, MachineLog.Folder)).SelectMany(StubFile.Lines),
            each => each.Contains("\"history.cleared\"", StringComparison.Ordinal));
        Assert.Contains("\"door\":\"terminal\"", line);
        Assert.Contains("\"scope\":\"quest\"", line);
    }

    /// <summary>A quest a remote numbered is forgotten here: the list and the press both say the team keeps its copy.</summary>
    [Fact]
    public async Task A_forgotten_quest_says_the_remote_keeps_the_teams_copy()
    {
        var service = ClosedQuest(workspace: "aurora", forgotten: ["q1"]);

        var (_, listed) = await QuestAsync(service, "clear", "q1");
        var (_, cleared) = await QuestAsync(service, "clear", "q1", "--yes");

        const string Remote = "  the remote for aurora keeps the team's copy; this machine will not fetch it again.";
        Assert.Contains(Remote, listed.Split('\n'));
        Assert.Contains(Remote, cleared.Split('\n'));
    }

    /// <summary>
    /// A unit kept is a refusal, exit 1, said in its keep's own sentence (here this machine's: a tree still here), on the list and
    /// on the press alike; the press sends nothing to the service and no file goes.
    /// </summary>
    [Fact]
    public async Task A_kept_quest_is_refused_in_its_keeps_own_sentence_and_nothing_is_sent()
    {
        var tree = Tree("s-1a2b3c4d");
        Kept("s1");
        var service = new HistoryStandIn { Records = [Record("s1", "q1", tree: tree)], Quests = [Quest("q1")] };
        service.Listings["quest=q1"] = [Unit("quest", "q1", quests: ["q1"], sessions: ["s1"])];

        var listed = await QuestAsync(service, "clear", "q1");
        var pressed = await QuestAsync(service, "clear", "q1", "--yes");

        const string Said = "daoris-driver: #q1 stays on this machine: Session `s1`'s tree is still here; clean it up, or discard it, first.\n";
        Assert.Equal((1, Said), listed);
        Assert.Equal((1, Said), pressed);
        Assert.Empty(service.Pressed);
        Assert.True(Holds("s1") && File.Exists(Path.Combine(tree, "work.txt")));
    }

    /// <summary>The service's word keeps a unit too, said in the service's sentence verbatim.</summary>
    [Fact]
    public async Task A_quest_the_service_keeps_is_said_in_the_services_sentence()
    {
        var service = new HistoryStandIn { Quests = [Quest("q1", status: "Taken")] };
        service.Listings["quest=q1"] =
            [Unit("quest", "q1", quests: ["q1"], refusal: Refusal("open", quest: "q1", error: "Quest `#q1` is taken: its record is work in progress."))];

        var (exit, said) = await QuestAsync(service, "clear", "q1", "--yes");

        Assert.Equal(1, exit);
        Assert.Equal("daoris-driver: #q1 stays on this machine: Quest `#q1` is taken: its record is work in progress.\n", said);
        Assert.Empty(service.Pressed);
    }

    /// <summary>A quest this machine does not hold is refused in the service's own sentence, exit 1.</summary>
    [Fact]
    public async Task A_quest_this_machine_does_not_hold_is_refused_in_the_services_sentence()
    {
        var service = new HistoryStandIn();
        service.Listings["quest=q9"] = [Unit("quest", "q9", refusal: Refusal("unknown", quest: "q9", error: "No quest `#q9` on this machine."))];

        Assert.Equal((1, "daoris-driver: No quest `#q9` on this machine.\n"), await QuestAsync(service, "clear", "q9"));
    }

    /// <summary>
    /// The second press judges again: a unit the service keeps at the press stays, said as changed since the list in its
    /// sentence, exit 1, and no file goes.
    /// </summary>
    [Fact]
    public async Task A_quest_that_changed_since_the_list_is_kept_and_said_so()
    {
        var service = ClosedQuest();
        service.Refusing["quest:q1"] = Unit("quest", "q1", quests: ["q1"], sessions: ["s1", "s2"],
            refusal: Refusal("unpushed", quest: "q1", workspace: "aurora", error: "Its last moves have not reached the remote for `aurora`; sync, then clear it."));

        var (exit, said) = await QuestAsync(service, "clear", "q1", "--yes");

        Assert.Equal(1, exit);
        Assert.Equal(
            "daoris-driver: kept #q1, which changed since the list: Its last moves have not reached the remote for `aurora`; sync, then clear it.\n",
            said);
        Assert.True(Holds("s1") && Holds("s2"));
    }

    /// <summary>A host older than the door says so as the library's sentence, which the host prints as a tool error, exit 2.</summary>
    [Fact]
    public async Task A_service_with_no_history_door_is_the_libraries_sentence()
    {
        var service = new HistoryStandIn { NoDoor = true };

        var refused = await Assert.ThrowsAsync<DriverException>(() => QuestAsync(service, "clear", "q1"));

        Assert.Contains("no history door", refused.Message);
    }

    // ——— A quest's failed sessions, and an ask's work.

    /// <summary>
    /// A closed quest's failed sessions: this machine's go and the quest stays; a teammate's is listed and kept in its sentence.
    /// With no failed session of this machine's, there is nothing to clear: information, exit 0, and nothing is sent.
    /// </summary>
    [Fact]
    public async Task A_quests_failed_sessions_clear_and_nothing_to_clear_is_information()
    {
        Kept("f1");
        var notOurs = Refusal("not-ours", session: "laptop/f2", origin: "laptop", error: "Session `laptop/f2` ran on `laptop`; its record is theirs.");
        var service = new HistoryStandIn { Records = [Record("f1", "q1", state: "failed")], Quests = [Quest("q1")] };
        service.Listings["quest=q1&failed=true"] = [Unit("failed", "q1", sessions: ["f1"], kept: [notOurs.DeepClone()])];

        var (listedExit, listed) = await QuestAsync(service, "clear", "q1", "--failed");

        Assert.Equal(0, listedExit);
        Assert.StartsWith("daoris-driver: clearing #q1's failed sessions would take 1 session, with what this machine kept of them (", listed);
        Assert.Contains("#q1 and its other sessions stay. Nothing brings it back; `daoris-driver quest clear q1 --failed --yes` clears it.", listed);
        Assert.Contains("  keeps laptop/f2: Session `laptop/f2` ran on `laptop`; its record is theirs.", listed.Split('\n'));

        var (exit, said) = await QuestAsync(service, "clear", "q1", "--failed", "--yes");

        Assert.Equal(0, exit);
        Assert.StartsWith("daoris-driver: cleared #q1's failed sessions from this machine: 1 session, ", said);
        Assert.Contains("  keeps laptop/f2: Session `laptop/f2` ran on `laptop`; its record is theirs.", said.Split('\n'));
        Assert.False(Holds("f1"));

        service.Listings["quest=q1&failed=true"] = [Unit("failed", "q1", kept: [notOurs.DeepClone()])];
        var (nothingExit, nothing) = await QuestAsync(service, "clear", "q1", "--failed", "--yes");

        Assert.Equal(0, nothingExit);
        Assert.StartsWith("daoris-driver: #q1 has no failed session of this machine's to clear.\n", nothing);
        Assert.Equal(["failed:q1"], service.Pressed);
    }

    /// <summary>An ask's work goes whole: the ask, its quests and their sessions, its intake among them.</summary>
    [Fact]
    public async Task An_asks_clear_takes_its_work_whole()
    {
        Kept("i1");
        Kept("s1");
        var service = new HistoryStandIn
        {
            Records = [Record("i1", ask: "a1", kind: "chat"), Record("s1", "q1")],
            Quests = [Quest("q1")],
            Asks = [Ask("a1")],
        };
        service.Listings["ask=a1"] = [Unit("ask", "a1", quests: ["q1"], asks: ["a1"], sessions: ["i1", "s1"])];

        var (listedExit, listed) = await AskAsync(service, "--clear", "a1");
        var (exit, said) = await AskAsync(service, "--clear", "#a1", "--yes");

        Assert.Equal(0, listedExit);
        Assert.StartsWith("daoris-driver: clearing ask #a1 would take 1 ask, 1 quest and 2 sessions, with what this machine kept of them (", listed);
        Assert.Contains("`daoris-driver ask --clear a1 --yes` clears it.", listed);
        Assert.Equal(0, exit);
        Assert.StartsWith("daoris-driver: cleared ask #a1 from this machine: 1 ask, 1 quest and 2 sessions, ", said);
        Assert.Equal(["ask:a1"], service.Pressed);
        Assert.False(Holds("i1") || Holds("s1"));
    }

    // ——— A workspace.

    /// <summary>A workspace of two closed quests, one of which a tree here keeps; and its intake's room, which no ask keeps.</summary>
    private HistoryStandIn Aurora()
    {
        Kept("s1");
        Kept("s2");
        var tree = Tree("s-2b2b2b2b", "aurora");
        var room = IntakeRoom.PathOf(_home, "aurora");
        Directory.CreateDirectory(room);
        File.WriteAllText(Path.Combine(room, "AGENTS.md"), "the room");
        var service = new HistoryStandIn
        {
            Records =
            [
                Record("s1", "q1", workspace: "aurora"), Record("s2", "q2", tree: tree, workspace: "aurora"),
                Record("c1", kind: "chat", workspace: "aurora"), Record("d1", "q9", workspace: "default"),
            ],
            Quests = [Quest("q1", workspace: "aurora"), Quest("q2", workspace: "aurora"), Quest("q9", workspace: "default")],
        };
        service.Listings["workspace=aurora"] =
        [
            Unit("quest", "q1", "aurora", quests: ["q1"], sessions: ["s1"], forgotten: ["q1"]),
            Unit("quest", "q2", "aurora", quests: ["q2"], sessions: ["s2"]),
        ];
        service.Listings["workspace=default"] = [Unit("quest", "q9", quests: ["q9"], sessions: ["d1"])];
        return service;
    }

    /// <summary>
    /// A workspace's first press lists each unit it would take and each it keeps in its keep's sentence, the intake's room, and
    /// the team's copy a remote keeps; it changes nothing, exit 0.
    /// </summary>
    [Fact]
    public async Task A_workspaces_clear_lists_what_it_takes_and_keeps()
    {
        var service = Aurora();

        var (exit, said) = await HistoryAsync(service, "clear", "--workspace", "aurora");

        Assert.Equal(0, exit);
        var lines = said.TrimEnd('\n').Split('\n');
        Assert.StartsWith("daoris-driver: clearing aurora's finished history would take 1 quest, 1 session and the intake's room, ", lines[0]);
        Assert.EndsWith(", and keep 1; `daoris-driver history clear --workspace aurora --yes` clears what this list holds. Nothing brings it back.", lines[0]);
        Assert.StartsWith("  takes #q1: 1 quest and 1 session, ", lines[1]);
        Assert.Contains(lines, line => line.StartsWith("  takes the intake's room, ", StringComparison.Ordinal));
        Assert.Contains("  keeps #q2: Session `s2`'s tree is still here; clean it up, or discard it, first.", lines);
        Assert.Contains("  the remote for aurora keeps the team's copy of 1 of these quests; this machine will not fetch it again.", lines);
        Assert.Empty(service.Pressed);
        Assert.True(Holds("s1"));
    }

    /// <summary>
    /// With <c>--yes</c> a workspace's clear takes what may go and keeps the rest: a unit it keeps is information, not a refusal of
    /// the workspace, exit 0. Its room goes, and the log line names the terminal.
    /// </summary>
    [Fact]
    public async Task A_workspaces_clear_with_yes_takes_what_may_go_and_keeps_the_rest()
    {
        var service = Aurora();
        var log = new MachineLog(_home, "driver");

        var (exit, said) = await RunAsync(service, HistoryCommand.Read(["clear", "--workspace", "aurora", "--yes"], out _), log);
        log.Dispose();

        Assert.Equal(0, exit);
        Assert.StartsWith("daoris-driver: cleared aurora's finished history from this machine: 1 of 1 listed, 1 quest, 1 session and the intake's room, ", said);
        Assert.Equal(["quest:q1"], service.Pressed);
        Assert.False(Holds("s1"));
        Assert.True(Holds("s2"));
        Assert.False(Directory.Exists(IntakeRoom.PathOf(_home, "aurora")));
        Assert.Contains("  1 of the quests was forgotten here; the remote for aurora keeps the team's copy, and this machine will not fetch it again.", said.Split('\n'));
        Assert.Contains("  keeps #q2: Session `s2`'s tree is still here; clean it up, or discard it, first.", said.Split('\n'));
        var line = Assert.Single(
            Directory.GetFiles(Path.Combine(_home, MachineLog.Folder)).SelectMany(StubFile.Lines),
            each => each.Contains("\"history.cleared\"", StringComparison.Ordinal));
        Assert.Contains("\"door\":\"terminal\"", line);
        Assert.Contains("\"scope\":\"workspace\"", line);
    }

    /// <summary>A workspace with nothing that may go says so: information, exit 0, and the press sends nothing.</summary>
    [Fact]
    public async Task A_workspace_with_nothing_to_clear_says_so()
    {
        var service = new HistoryStandIn { Quests = [Quest("q3", status: "Open", workspace: "aurora")] };
        service.Listings["workspace=aurora"] = [];

        var (exit, said) = await HistoryAsync(service, "clear", "--workspace", "aurora", "--yes");

        Assert.Equal((0, "daoris-driver: nothing of aurora's finished history may be cleared now, so nothing changed.\n"), (exit, said));
        Assert.Empty(service.Pressed);
    }

    // ——— The reading.

    /// <summary>
    /// <c>history --workspace</c> reads what the home keeps of a workspace's finished work and what a clear would take, the
    /// units kept by reason with the line that frees each, the conversations only <c>sessions delete</c> takes, and the home's
    /// left-over files and log; counts and bytes, never a path or a title. Exit 0.
    /// </summary>
    [Fact]
    public async Task The_reading_says_what_a_workspace_keeps_and_what_a_clear_would_take()
    {
        var service = Aurora();
        Kept("c1");

        var (exit, said) = await HistoryAsync(service, "--workspace", "aurora");

        Assert.Equal(0, exit);
        var lines = said.TrimEnd('\n').Split('\n');
        Assert.StartsWith("daoris-driver: aurora keeps 2 closed quests on this machine, with 2 sessions: ", lines[0]);
        Assert.Contains(lines, line => line.StartsWith("  on the disk: ", StringComparison.Ordinal) && line.Contains(" of conversations", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("  a clear would take 1 quest, 1 session and the intake's room, ", StringComparison.Ordinal)
            && line.EndsWith(": `daoris-driver history clear --workspace aurora` lists it first.", StringComparison.Ordinal));
        Assert.Contains(
            "  1 kept: a session's tree is still here; `daoris-driver trees clean` removes it once its work has landed, or "
            + "`daoris-driver trees remove <session> --force` discards it.",
            lines);
        Assert.Contains(lines, line => line.StartsWith("  1 conversation that served no quest, ", StringComparison.Ordinal)
            && line.EndsWith(": a clear never takes one; `daoris-driver sessions delete <id>` does.", StringComparison.Ordinal));
        Assert.StartsWith("daoris-driver: the home as a whole: nothing left over from records already gone; the machine log holds ", lines[^1]);
        Assert.Empty(service.Pressed);
        Assert.DoesNotContain(_home, said, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary><c>history</c> with no workspace reads every workspace a record names, each once, and the home's line once.</summary>
    [Fact]
    public async Task The_reading_with_no_workspace_reads_every_workspace_a_record_names()
    {
        var service = Aurora();

        var (exit, said) = await HistoryAsync(service);

        Assert.Equal(0, exit);
        var firsts = said.Split('\n').Where(line => line.StartsWith("daoris-driver: ", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, firsts.Count);
        Assert.StartsWith("daoris-driver: aurora keeps ", firsts[0]);
        Assert.StartsWith("daoris-driver: default keeps 1 closed quest on this machine, with 1 session", firsts[1]);
        Assert.StartsWith("daoris-driver: the home as a whole: ", firsts[2]);
    }

    /// <summary>A machine no record names any workspace on keeps nothing finished, and says so.</summary>
    [Fact]
    public async Task The_reading_of_an_empty_machine_says_it_keeps_nothing()
    {
        Assert.Equal((0, "daoris-driver: nothing finished is kept on this machine: no record names a workspace.\n"), await HistoryAsync(new HistoryStandIn()));
    }

    // ——— --json: HISTORY_PLAN's answer, field for field.

    /// <summary>
    /// <c>history --workspace --json</c> prints <c>HISTORY_PLAN</c>'s answer field for field, from the one projection both doors
    /// serialize (<see cref="HistoryAnswers"/>): the plan's, each unit's, a reason's and the reading's fields, in order, camel-cased
    /// as the page reads them. The modules' twin holds the route's answer to the same lists.
    /// </summary>
    [Fact]
    public async Task History_json_is_the_routes_answer_field_for_field()
    {
        var service = Aurora();

        var (exit, said) = await HistoryAsync(service, "--workspace", "aurora", "--json");

        Assert.Equal(0, exit);
        Assert.DoesNotContain('\r', said);
        using var answer = JsonDocument.Parse(said);
        var plan = answer.RootElement;
        Assert.Equal(HistoryAnswers.PlanFields, plan.EnumerateObject().Select(field => field.Name));
        Assert.Equal(("workspace", "aurora"), (plan.GetProperty("scope").GetString(), plan.GetProperty("id").GetString()));
        var units = plan.GetProperty("units").EnumerateArray().ToList();
        Assert.Equal(HistoryAnswers.UnitFields, units[0].EnumerateObject().Select(field => field.Name));
        var keep = units.Single(unit => unit.GetProperty("id").GetString() == "q2").GetProperty("keep");
        Assert.Equal(HistoryAnswers.ReasonFields, keep.EnumerateObject().Select(field => field.Name));
        Assert.Equal((HistoryCodes.TreeHere, "s2"), (keep.GetProperty("code").GetString(), keep.GetProperty("session").GetString()));
        var reading = plan.GetProperty("reading");
        Assert.Equal(HistoryAnswers.ReadingFields, reading.EnumerateObject().Select(field => field.Name));
        Assert.Equal(1, reading.GetProperty("keptBy").GetProperty(HistoryCodes.TreeHere).GetInt32());
        Assert.DoesNotContain(JsonSerializer.Serialize(_home).Trim('"'), said, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary><c>history --json</c> with no workspace prints each workspace's answer, in the order the reading reads them.</summary>
    [Fact]
    public async Task History_json_with_no_workspace_prints_each_workspaces_answer()
    {
        var service = Aurora();

        var (exit, said) = await HistoryAsync(service, "--json");

        Assert.Equal(0, exit);
        using var answer = JsonDocument.Parse(said);
        var workspaces = answer.RootElement.GetProperty("workspaces").EnumerateArray().ToList();
        Assert.Equal(["aurora", "default"], workspaces.Select(plan => plan.GetProperty("id").GetString()));
        Assert.All(workspaces, plan => Assert.Equal(HistoryAnswers.PlanFields, plan.EnumerateObject().Select(field => field.Name)));
    }
}
