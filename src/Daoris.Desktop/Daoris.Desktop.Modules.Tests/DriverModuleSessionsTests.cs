using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A session as the rail acts on it over the bridge (`DriverModule.Sessions.cs`, MOD5): a stop, a parked
/// session's resolution, and the openings and search read off this machine's own record.
/// </summary>
public sealed class DriverModuleSessionsTests : DriverModuleBridge
{
    /// <summary>
    /// RAIL1: what a person first said in each session, and a search of what sessions said, answered from
    /// this machine's own record — no service is asked, because none holds it (D47 §4), so both answer on
    /// a cold start too.
    /// </summary>
    [Fact]
    public async Task Openings_and_a_search_are_answered_from_the_machines_own_record()
    {
        var loop = Loop();
        loop.Events.Append("chat1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Cap the hydration per frame" });
        loop.Events.Append("chat1", new SessionEvent { Kind = SessionEventKind.Message, Text = "Capped in the streamer." });
        var module = new DriverModule(Bus, loop);

        var openings = await AnswerAsync(module, "SESSION_OPENINGS", new { ids = new[] { "chat1", "none1" } });
        Assert.Equal("Cap the hydration per frame", openings.GetProperty("openings").GetProperty("chat1").GetString());
        Assert.False(openings.GetProperty("openings").TryGetProperty("none1", out _));

        var search = await AnswerAsync(module, "SESSION_SEARCH", new { q = "streamer" });
        var hit = Assert.Single(search.GetProperty("hits").EnumerateArray());
        Assert.Equal("chat1", hit.GetProperty("session").GetString());
        Assert.Equal("message", hit.GetProperty("kind").GetString());
        Assert.Contains("streamer", hit.GetProperty("snippet").GetString());
        Assert.False(search.GetProperty("cut").GetBoolean());

        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(module, "SESSION_SEARCH", new { }));
    }

    /// <summary>
    /// LOOK2b: the rail said *in s-2394e5d9* of a session whose landing had tidied that tree away and whose branch was
    /// gone. Where each session's work is now is answered for the whole rail in one ask, from this machine's own files:
    /// whether the tree it opened is still here, and its landing, standing or a trace (D113). No service is asked and no
    /// git is run, so it answers on a cold start, and a folder Daoris did not open is never looked at.
    /// </summary>
    [Fact]
    public async Task Where_each_sessions_work_is_now_is_answered_from_the_machines_own_files()
    {
        var loop = Loop();
        var trees = new SessionTrees(loop.Home);
        var standing = Path.Combine(trees.TreesRoot, "aurora", "engine", "s-2394e5d9");
        var kept = Path.Combine(trees.TreesRoot, "aurora", "engine", "s-5a1f0c2b");
        var carriedOn = Path.Combine(trees.TreesRoot, "aurora", "game", "s-77e0d9a4");
        Directory.CreateDirectory(kept);
        File.WriteAllText(Path.Combine(kept, "README.md"), "kept");
        Directory.CreateDirectory(carriedOn);
        File.WriteAllText(Path.Combine(carriedOn, "README.md"), "carried on");
        var at = DateTimeOffset.Parse("2026-10-01T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        trees.Recorded.Record(new LandedBranch("engine", "aurora", "feature/42-streamer", "main", "abc123", "landed1", "42", null, at));
        trees.Recorded.Record(new LandedBranch("game", "aurora", "feature/7-hud", "main", "def456", "carried1", "7", null, at));
        trees.Recorded.Gone("game", ["feature/7-hud"]);
        trees.Recorded.Record(new LandedBranch("engine", "aurora", "feature/9-cache", "main", "fed789", "gone1", "9", null, at));
        trees.Recorded.Gone("engine", ["feature/9-cache"]);
        var module = new DriverModule(Bus, loop);

        var answer = await AnswerAsync(module, "SESSION_WHERE", new
        {
            sessions = new object[]
            {
                new { id = "landed1", tree = standing },
                new { id = "gone1", tree = Path.Combine(trees.TreesRoot, "aurora", "engine", "s-0b3c4d5e") },
                new { id = "carried1", tree = carriedOn },
                new { id = "working1", tree = kept },
                new { id = "rooted1", tree = Path.Combine(Home, "elsewhere", "engine") },
            },
        });
        var rows = answer.GetProperty("sessions").EnumerateArray().ToDictionary(row => row.GetProperty("session").GetString()!);

        // Its tree tidied and its branch standing: where it landed.
        Assert.True(rows["landed1"].GetProperty("treeGone").GetBoolean());
        Assert.Equal("feature/42-streamer", rows["landed1"].GetProperty("landed").GetProperty("branch").GetString());
        Assert.Equal("standing", rows["landed1"].GetProperty("landed").GetProperty("state").GetString());
        // Its tree tidied and its branch gone since: a trace.
        Assert.Equal("gone", rows["gone1"].GetProperty("landed").GetProperty("state").GetString());
        // A tree still here after its branch went: the session carried on in it, as its review reads (D113 §1).
        Assert.False(rows["carried1"].GetProperty("treeGone").GetBoolean());
        Assert.Equal("gone", rows["carried1"].GetProperty("landed").GetProperty("state").GetString());
        // No landing: its tree, still here.
        Assert.False(rows["working1"].GetProperty("treeGone").GetBoolean());
        Assert.Equal(JsonValueKind.Null, rows["working1"].GetProperty("landed").ValueKind);
        // A folder that is no tree of this home's is never looked at, and nothing is said of it.
        Assert.False(rows.ContainsKey("rooted1"));
        // Never a machine path back: the page sent the trees it was answered, and has no use for them again.
        Assert.DoesNotContain(JsonSerializer.Serialize(Home).Trim('"'), answer.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Stopping a session that has already finished is FALSE, not an error: the record says how it
    /// ended, and a page that showed a failure would be reporting the race rather than the outcome.
    /// </summary>
    [Fact]
    public async Task Stopping_a_session_that_is_not_running_answers_false()
    {
        var state = await AnswerAsync(Module(), "STOP_SESSION", new { id = "nothing-here" });

        Assert.False(state.GetProperty("stopped").GetBoolean());
        // Nor an orphan: with no service up there is no record to have ended.
        Assert.False(state.GetProperty("orphan").GetBoolean());
        // Nor run by another process here: nothing marked it (REV3 chat F8).
        Assert.False(state.GetProperty("elsewhere").GetBoolean());
    }

    /// <summary>
    /// The person's three moves on a parked session (design §4) — and the fourth the ledger allows
    /// is NOT one of them. Narrowed on this side because it is a surface rule: `awaiting-person` →
    /// `working` is the driver's observation of a session that carried on, which a person causes by
    /// answering it, not by pressing anything.
    /// </summary>
    [Theory]
    [InlineData("working")]
    [InlineData("failed")]
    [InlineData("queued")]
    public async Task A_move_that_is_not_the_persons_is_refused_before_the_service_is_asked(string state)
    {
        var refusal = await RefusalAsync(Module(), "RESOLVE_SESSION", new { id = "s1a2b3c4", state });

        Assert.Contains(Refusals.SessionMoveNotYours, refusal);
        // The state is carried as a PARAMETER, so the sentence the person reads can name it.
        Assert.Contains($"state={state}", refusal);
    }

    /// <summary>
    /// The same rule the quest door holds, for the same reason: the note is the part whoever reads
    /// the record can act on. Refused before the service is asked, so a reasonless decline never
    /// half-happens.
    /// </summary>
    [Fact]
    public async Task Declining_a_parked_session_needs_a_reason()
    {
        var refusal = await RefusalAsync(
            Module(), "RESOLVE_SESSION", new { id = "s1a2b3c4", state = "declined" });

        Assert.Contains(Refusals.SessionDeclineNeedsReason, refusal);
    }

    /// <summary>
    /// A move the person MAY make still needs somewhere to record it. On a cold start that is the
    /// same sentence every other service-needing control gives, rather than a crash.
    /// </summary>
    [Theory]
    [InlineData("completed")]
    [InlineData("stopped")]
    public async Task A_persons_move_before_the_service_answers_says_so(string state)
    {
        var refusal = await RefusalAsync(
            Module(), "RESOLVE_SESSION", new { id = "s1a2b3c4", state, note = "looked at it; it is right." });

        Assert.Contains(Refusals.DriverNotReady, refusal);
    }

    /// <summary>
    /// SESSUX1a: where each session is listed reads the service's records and quests, so before the driver's service is up
    /// it is the cold-start sentence, and so is an archive, which is judged by the same reading.
    /// </summary>
    [Fact]
    public async Task The_groups_and_an_archive_before_the_service_answers_say_so()
    {
        var module = Module();

        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(module, "SESSION_GROUPS"));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(module, "SESSION_ARCHIVE", new { ids = new[] { "done1" }, archived = true }));
    }

    /// <summary>
    /// SESSUX1a (D126 §2.4): each session's group and the word its row shows, from the records, the quests and the
    /// planner's verdict, which with no look yet is a plan over a fresh snapshot: a quest its three failed sessions park
    /// shows its last session parked, waiting on you. Nothing machine-local comes back.
    /// </summary>
    [Fact]
    public async Task Each_session_is_answered_in_its_group_and_a_quest_its_strikes_park_shows_its_last_session_parked()
    {
        using var ledger = Ledger();
        var loop = await UpAsync(ledger);
        var module = new DriverModule(Bus, loop);

        var answer = await AnswerAsync(module, "SESSION_GROUPS");
        var rows = answer.GetProperty("sessions").EnumerateArray().ToList();
        var bySession = rows.ToDictionary(row => row.GetProperty("session").GetString()!);

        // In the order the person acts on them: what waits on you first, the oldest wait first.
        Assert.Equal(["failed3", "waiting1", "running1", "done1", "failed2", "failed1"], rows.Select(row => row.GetProperty("session").GetString()));
        Assert.Equal("you", bySession["failed3"].GetProperty("group").GetString());
        Assert.Equal("parked", bySession["failed3"].GetProperty("shown").GetString());
        Assert.Equal(3, bySession["failed3"].GetProperty("strikes").GetInt32());
        Assert.Equal("you", bySession["waiting1"].GetProperty("group").GetString());
        Assert.Equal("awaiting-person", bySession["waiting1"].GetProperty("shown").GetString());
        Assert.Equal("working", bySession["running1"].GetProperty("group").GetString());
        Assert.Equal("ended", bySession["done1"].GetProperty("group").GetString());
        Assert.Equal("completed", bySession["done1"].GetProperty("shown").GetString());
        Assert.False(bySession["done1"].GetProperty("archived").GetBoolean());
        Assert.Equal("ended", bySession["failed1"].GetProperty("group").GetString());
        Assert.DoesNotContain(JsonSerializer.Serialize(Home).Trim('"'), answer.GetRawText(), StringComparison.OrdinalIgnoreCase);

        // Asked for some, it answers those.
        var some = await AnswerAsync(module, "SESSION_GROUPS", new { ids = new[] { "done1", "nobody" } });
        Assert.Equal(["done1"], some.GetProperty("sessions").EnumerateArray().Select(row => row.GetProperty("session").GetString()));
    }

    /// <summary>Where the loop has looked, its verdicts are the ones read: the list says what the loop decided, never a second plan.</summary>
    [Fact]
    public async Task The_loops_last_look_is_the_verdict_read()
    {
        using var ledger = Ledger();
        var loop = await UpAsync(ledger);
        loop.Look.Record([new Consideration(
            new QuestView("q4", "game", "engine", "Done long ago", "A body.", "Done"), StartVerdict.Exhausted, "the loop's sentence")]);
        var module = new DriverModule(Bus, loop);

        var answer = await AnswerAsync(module, "SESSION_GROUPS", new { ids = new[] { "done1", "failed3" } });
        var rows = answer.GetProperty("sessions").EnumerateArray().ToDictionary(row => row.GetProperty("session").GetString()!);

        // The loop parked q4 and said nothing of q1, whatever a fresh plan would say of either.
        Assert.Equal("parked", rows["done1"].GetProperty("shown").GetString());
        Assert.Equal("ended", rows["failed3"].GetProperty("group").GetString());
        // SESSUX1b: whether a row's stop holds its quest rides each row; no stop holds one here.
        Assert.False(rows["failed3"].GetProperty("holdsQuest").GetBoolean());
    }

    /// <summary>
    /// SESSUX1a (D126 §5.2): an ended session is archived on this machine, its mark in the home's
    /// <c>sessions/archived.json</c>, and listed as archived; unarchived, it is back in its group, and one that was not
    /// archived is said, never refused (D48 §6).
    /// </summary>
    [Fact]
    public async Task An_ended_session_is_archived_and_unarchived_and_the_marks_come_back_as_they_stand()
    {
        using var ledger = Ledger();
        var loop = await UpAsync(ledger);
        var module = new DriverModule(Bus, loop);

        var archived = await AnswerAsync(module, "SESSION_ARCHIVE", new { ids = new[] { "done1" }, archived = true });

        Assert.Equal(["done1"], archived.GetProperty("archived").EnumerateArray().Select(mark => mark.GetProperty("session").GetString()));
        Assert.Empty(archived.GetProperty("kept").EnumerateArray());
        Assert.True(File.Exists(Path.Combine(Home, "sessions", "archived.json")));
        var row = (await AnswerAsync(module, "SESSION_GROUPS", new { ids = new[] { "done1" } })).GetProperty("sessions")[0];
        Assert.Equal("archived", row.GetProperty("group").GetString());
        Assert.True(row.GetProperty("archived").GetBoolean());

        var back = await AnswerAsync(module, "SESSION_ARCHIVE", new { ids = new[] { "done1", "failed1" }, archived = false });

        Assert.Empty(back.GetProperty("archived").EnumerateArray());
        Assert.Equal(["failed1"], back.GetProperty("notArchived").EnumerateArray().Select(id => id.GetString()));
        Assert.Equal("ended", (await AnswerAsync(module, "SESSION_GROUPS", new { ids = new[] { "done1" } }))
            .GetProperty("sessions")[0].GetProperty("group").GetString());
    }

    /// <summary>
    /// Archive refuses a session still running, one that waits on you (a parked session, or a quest's parked last one),
    /// and an id no record has, each a code the page says in its own language, naming the session; nothing is written.
    /// </summary>
    [Theory]
    [InlineData("running1", Refusals.SessionLive, "")]
    [InlineData("waiting1", Refusals.SessionNeedsYou, "group=you")]
    [InlineData("failed3", Refusals.SessionNeedsYou, "group=you")]
    [InlineData("nobody", Refusals.SessionUnknown, "")]
    public async Task An_archive_that_would_hide_what_needs_you_or_stop_nothing_is_refused(string session, string code, string group)
    {
        using var ledger = Ledger();
        var loop = await UpAsync(ledger);

        var refusal = await RefusalAsync(new DriverModule(Bus, loop), "SESSION_ARCHIVE", new { ids = new[] { session }, archived = true });

        Assert.Contains(code, refusal);
        Assert.Contains($"session={session}", refusal);
        Assert.Contains(group, refusal);
        Assert.False(File.Exists(Path.Combine(Home, "sessions", "archived.json")));
    }

    /// <summary>The second press of *Archive what ended* judges each again: what may go is archived, and what may not is kept with its code.</summary>
    [Fact]
    public async Task Several_at_once_archive_what_may_go_and_say_what_was_kept()
    {
        using var ledger = Ledger();
        var loop = await UpAsync(ledger);

        var answer = await AnswerAsync(new DriverModule(Bus, loop), "SESSION_ARCHIVE",
            new { ids = new[] { "done1", "failed1", "running1", "waiting1" }, archived = true });

        Assert.Equal(["done1", "failed1"], answer.GetProperty("archived").EnumerateArray()
            .Select(mark => mark.GetProperty("session").GetString()).Order(StringComparer.Ordinal));
        var kept = answer.GetProperty("kept").EnumerateArray().ToDictionary(row => row.GetProperty("session").GetString()!);
        Assert.Equal(Refusals.SessionLive, kept["running1"].GetProperty("code").GetString());
        Assert.Equal(Refusals.SessionNeedsYou, kept["waiting1"].GetProperty("code").GetString());
        Assert.Equal("you", kept["waiting1"].GetProperty("group").GetString());
    }

    /// <summary>
    /// SESSUX1d (D126 §3.5): *Open folder* opens the folder a session worked in, its own tree or its repository's checkout,
    /// through the window kit's launcher, as the log's folder opens. The module names the folder from the session's record,
    /// never the page, and nothing machine-local comes back. A tree a tidy took is refused in the catalogue's words, so is
    /// a folder this home did not open and that is no checkout, a teammate's record, and an id no record has.
    /// </summary>
    [Fact]
    public async Task A_sessions_folder_opens_its_own_tree_or_its_checkout_and_one_that_is_gone_is_refused()
    {
        var trees = new SessionTrees(Home);
        var own = Path.Combine(trees.TreesRoot, "aurora", "engine", "s-2394e5d9");
        Directory.CreateDirectory(own);
        var gone = Path.Combine(trees.TreesRoot, "aurora", "engine", "s-0b3c4d5e");
        var root = Path.Combine(Home, "checkouts", "engine");
        Directory.CreateDirectory(root);
        var elsewhere = Path.Combine(Home, "elsewhere", "engine");
        Directory.CreateDirectory(elsewhere);
        using var ledger = FolderLedger(own, gone, root, elsewhere);
        var loop = await UpAsync(ledger);
        var opened = new List<string>();
        var module = new DriverModule(Bus, loop, opened.Add);

        var tree = await AnswerAsync(module, "SESSION_OPEN_FOLDER", new { id = "own1" });
        Assert.True(tree.GetProperty("opened").GetBoolean());
        await AnswerAsync(module, "SESSION_OPEN_FOLDER", new { id = "root1" });
        Assert.Equal([Path.GetFullPath(own), Path.GetFullPath(root)], opened.Select(Path.GetFullPath));
        // Never a machine path back: the module named the folder, and the page has no use for it.
        Assert.DoesNotContain(JsonSerializer.Serialize(Home).Trim('"'), tree.GetRawText(), StringComparison.OrdinalIgnoreCase);

        foreach (var refused in new[] { "gone1", "elsewhere1", "machine-b/own1" })
        {
            var refusal = await RefusalAsync(module, "SESSION_OPEN_FOLDER", new { id = refused });
            Assert.Contains(Refusals.SessionFolderGone, refusal);
            Assert.Contains($"session={refused}", refusal);
        }

        Assert.Contains(Refusals.SessionUnknown, await RefusalAsync(module, "SESSION_OPEN_FOLDER", new { id = "nobody" }));
        Assert.Equal(2, opened.Count);

        var failing = new DriverModule(Bus, loop, _ => throw new System.ComponentModel.Win32Exception("no file manager answers"));
        var failed = await RefusalAsync(failing, "SESSION_OPEN_FOLDER", new { id = "own1" });
        Assert.Contains(Refusals.SessionFolderNotOpened, failed);
        Assert.Contains("no file manager answers", failed);

        // A host with no launcher opens nothing, and says so.
        Assert.False((await AnswerAsync(new DriverModule(Bus, loop), "SESSION_OPEN_FOLDER", new { id = "own1" })).GetProperty("opened").GetBoolean());
    }

    /// <summary>Before the driver's service is up, a folder is the cold-start sentence: the record names it.</summary>
    [Fact]
    public async Task A_sessions_folder_before_the_service_answers_says_so()
    {
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "SESSION_OPEN_FOLDER", new { id = "own1" }));
    }

    /// <summary>
    /// SESSUX1f (D126 §5.4): a conversation that served no quest is listed deletable, and its delete removes its record
    /// through the ledger and what this machine kept of it, its words included; the answer says what went and names no
    /// path. A row the ledger keeps is not deletable.
    /// </summary>
    [Fact]
    public async Task A_conversation_that_served_no_quest_is_listed_deletable_and_deleted_with_what_this_machine_kept()
    {
        using var ledger = DeleteLedger(new Dictionary<string, string> { ["chat1"] = """{"deletable":true}""" });
        var loop = await UpAsync(ledger);
        loop.Events.Append("chat1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "a plan of mine" });
        File.WriteAllText(Path.Combine(Home, "sessions", "chat1.log"), "the transcript");
        var module = new DriverModule(Bus, loop);

        var rows = (await AnswerAsync(module, "SESSION_GROUPS", null)).GetProperty("sessions").EnumerateArray()
            .ToDictionary(row => row.GetProperty("session").GetString()!);
        Assert.True(rows["chat1"].GetProperty("deletable").GetBoolean());
        Assert.False(rows["done1"].GetProperty("deletable").GetBoolean());

        var deleted = await AnswerAsync(module, "SESSION_DELETE", new { id = "chat1" });

        Assert.Equal("chat1", deleted.GetProperty("deleted").GetString());
        Assert.Equal(["record", "conversation", "transcript"], deleted.GetProperty("removed").EnumerateArray().Select(each => each.GetString()));
        Assert.False(File.Exists(Path.Combine(Home, "sessions", "chat1.events.jsonl")));
        Assert.False(File.Exists(Path.Combine(Home, "sessions", "chat1.log")));
        Assert.Empty(loop.Events.Openings(["chat1"]));
        Assert.DoesNotContain(JsonSerializer.Serialize(Home).Trim('"'), deleted.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    public static TheoryData<string, string, string> DeleteRefusals => new()
    {
        { """{"deletable":false,"error":"it worked on #q4","refusal":"served-quest","quest":"q4"}""", Refusals.SessionServedQuest, "quest=q4" },
        { """{"deletable":false,"error":"the remote holds it","refusal":"on-remote","workspace":"aurora"}""", Refusals.SessionOnRemote, "workspace=aurora" },
        { """{"deletable":false,"error":"ask #a1 names it","refusal":"named","ask":"a1"}""", Refusals.SessionNamed, "context=ask" },
        { """{"deletable":false,"error":"#q9 was published by it","refusal":"named","quest":"q9"}""", Refusals.SessionNamed, "context=quest" },
        { """{"deletable":false,"error":"still running","refusal":"live"}""", Refusals.SessionLive, "context=delete" },
    };

    /// <summary>
    /// Each refusal the ledger gives is the catalogue's code with the facts its sentence names, read by its word and never
    /// its sentence; nothing is removed.
    /// </summary>
    [Theory]
    [MemberData(nameof(DeleteRefusals))]
    public async Task A_delete_the_ledger_refuses_is_said_in_the_catalogues_words(string judged, string code, string fact)
    {
        using var ledger = DeleteLedger(new Dictionary<string, string> { ["chat1"] = judged });
        var loop = await UpAsync(ledger);
        loop.Events.Append("chat1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "kept" });

        var refusal = await RefusalAsync(new DriverModule(Bus, loop), "SESSION_DELETE", new { id = "chat1" });

        Assert.Contains(code, refusal);
        Assert.Contains("session=chat1", refusal);
        Assert.Contains(fact, refusal);
        Assert.True(File.Exists(Path.Combine(Home, "sessions", "chat1.events.jsonl")));
    }

    /// <summary>
    /// This machine's half, said in the catalogue's words: a tree of its own still here, a landing that names it, a
    /// teammate's record, and an id no record has.
    /// </summary>
    [Fact]
    public async Task A_delete_this_machine_keeps_is_said_in_the_catalogues_words()
    {
        var tree = Path.Combine(new SessionTrees(Home).TreesRoot, "aurora", "engine", "s-1a2b3c4d");
        Directory.CreateDirectory(tree);
        File.WriteAllText(Path.Combine(tree, "work.txt"), "work");
        new SessionTrees(Home).Recorded.Record(new LandedBranch(
            "engine", "aurora", "daoris/s-landed00", "main", "abc123", "landed1", null, "work", DateTimeOffset.UtcNow));
        using var ledger = DeleteLedger(
            new Dictionary<string, string> { ["treed1"] = """{"deletable":true}""", ["landed1"] = """{"deletable":true}""" }, tree);
        var module = new DriverModule(Bus, await UpAsync(ledger));

        var treed = await RefusalAsync(module, "SESSION_DELETE", new { id = "treed1" });
        Assert.Contains(Refusals.SessionTreeHere, treed);
        Assert.DoesNotContain(JsonSerializer.Serialize(Home).Trim('"'), treed, StringComparison.OrdinalIgnoreCase);
        var landed = await RefusalAsync(module, "SESSION_DELETE", new { id = "landed1" });
        Assert.Contains(Refusals.SessionNamed, landed);
        Assert.Contains("context=landing", landed);
        var theirs = await RefusalAsync(module, "SESSION_DELETE", new { id = "laptop/chat9" });
        Assert.Contains(Refusals.SessionNotOurs, theirs);
        Assert.Contains("machine=laptop", theirs);
        var nobody = await RefusalAsync(module, "SESSION_DELETE", new { id = "nobody" });
        Assert.Contains(Refusals.SessionUnknown, nobody);
        Assert.Contains("context=delete", nobody);
    }

    /// <summary>
    /// SESSUX1g (D126 §7.1): <c>daoris-driver sessions --json</c> prints this route's answer, field for field, so the screen
    /// and the terminal cannot disagree about a session's place.
    /// </summary>
    [Fact]
    public async Task The_groups_answer_has_the_terminals_fields_in_its_order()
    {
        using var ledger = Ledger();
        var module = new DriverModule(Bus, await UpAsync(ledger));

        var row = (await AnswerAsync(module, "SESSION_GROUPS", new { ids = new[] { "done1" } })).GetProperty("sessions")[0];

        Assert.Equal(SessionsCommand.JsonFields, row.EnumerateObject().Select(field => field.Name));
    }

    /// <summary>SESSUX1g (D126 §7.4): an archive from the screen is counted in the machine log as the screen's, with no session named.</summary>
    [Fact]
    public async Task An_archive_from_the_screen_is_counted_in_the_log_as_the_screens()
    {
        using var ledger = Ledger();
        (DriverConfig.Empty with { Drivable = ["engine"] }).Save(DriverConfigPath);
        var log = new MachineLog(Home, "desktop");
        var loop = new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0", log: log);
        await loop.ComeUpAsync(new ServiceClient(ledger.Address, null));

        await AnswerAsync(new DriverModule(Bus, loop), "SESSION_ARCHIVE", new { ids = new[] { "done1", "failed1", "running1" }, archived = true });

        log.Dispose();
        var line = Assert.Single(Directory.GetFiles(Path.Combine(Home, MachineLog.Folder)).SelectMany(File.ReadAllLines),
            each => each.Contains("\"sessions.archived\"", StringComparison.Ordinal));
        Assert.Contains("\"count\":2", line);
        Assert.Contains("\"door\":\"screen\"", line);
        Assert.DoesNotContain("done1", line);
    }

    /// <summary>
    /// SESSUX1g (D126 §7.1): every loop on the home watches the requests, the desktop's as the headless host's does, with the
    /// registry its conversations and driven sessions share and the service it comes up with.
    /// </summary>
    [Fact]
    public void The_shells_loop_watches_the_requests_with_its_own_registry()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "daoris.json"))) root = root.Parent;
        var loop = File.ReadAllText(Path.Combine(root!.FullName, "src", "Daoris.Desktop", "Daoris.Desktop.Modules", "DriverLoop.cs"));

        Assert.Contains("new SessionRequestWatch(homeDirectory, Processes, () => Service)", loop);
        Assert.True(
            loop.IndexOf("new SessionRequestWatch(", StringComparison.Ordinal) < loop.IndexOf("HoldHomeAsync(ct)", StringComparison.Ordinal),
            "the watch starts before the loop waits for the home's lock, so a conversation is reached meanwhile");
    }

    /// <summary>
    /// The records a delete reads, and the ledger's judgement of each it is asked about (its delete answers yes, since this
    /// stand-in answers a path whatever the verb): a conversation <c>chat1</c>, one with its tree here, one a landing names,
    /// a teammate's, and the main ledger's done session.
    /// </summary>
    private LoopbackHost DeleteLedger(IReadOnlyDictionary<string, string> judged, string? tree = null)
    {
        var ledger = Ledger();
        string Chat(string id, string? at = null) =>
            $$"""{"id":"{{id}}","repository":"engine","adapter":"claude-code","state":"completed","kind":"chat","deletable":true,{{(at is null ? "" : $"\"tree\":\"{at.Replace('\\', '/')}\",")}}"created":"2026-10-02T10:00:00Z","updated":"2026-10-02T10:05:00Z"}""";
        var records = $"[{Chat("chat1")},{Chat("treed1", tree)},{Chat("landed1")},"
                      + $$"""{"id":"laptop/chat9","repository":"engine","adapter":"claude-code","state":"completed","kind":"chat"},"""
                      + """{"id":"done1","quest":"q4","repository":"engine","adapter":"claude-code","state":"completed","kind":"driven","created":"2026-10-02T09:50:00Z","updated":"2026-10-02T09:51:00Z"}]""";
        ledger.Serve("/api/sessions?includeClosed=true", System.Text.Encoding.UTF8.GetBytes(records));
        foreach (var (id, answer) in judged)
        {
            ledger.Serve($"/api/sessions/{id}/deletable", System.Text.Encoding.UTF8.GetBytes(answer));
            ledger.Serve($"/api/sessions/{id}", System.Text.Encoding.UTF8.GetBytes(
                answer.Contains("\"refusal\"", StringComparison.Ordinal) ? answer : $$"""{"id":"{{id}}","message":"Deleted session `{{id}}`."}"""));
        }

        return ledger;
    }

    /// <summary>
    /// The records a folder is named from: a session in its own tree, one whose tree is gone, one in its repository's
    /// checkout, one that names a folder this home did not open, and a teammate's.
    /// </summary>
    private LoopbackHost FolderLedger(string own, string gone, string root, string elsewhere)
    {
        var ledger = new LoopbackHost();
        static string Slashed(string path) => path.Replace('\\', '/');
        string Session(string id, string tree) =>
            $$"""{"id":"{{id}}","quest":"q1","repository":"engine","adapter":"claude-code","state":"completed","kind":"driven","tree":"{{Slashed(tree)}}","created":"2026-10-02T09:00:00Z","updated":"2026-10-02T09:10:00Z"}""";
        var records = $"[{Session("own1", own)},{Session("gone1", gone)},{Session("root1", root)},{Session("elsewhere1", elsewhere)},{Session("machine-b/own1", own)}]";
        ledger.Serve("/api/sessions?includeClosed=true", System.Text.Encoding.UTF8.GetBytes(records));
        ledger.Serve("/api/sessions", System.Text.Encoding.UTF8.GetBytes("[]"));
        ledger.Serve("/api/quests?includeClosed=true", System.Text.Encoding.UTF8.GetBytes("[]"));
        ledger.Serve("/api/quests", System.Text.Encoding.UTF8.GetBytes("[]"));
        ledger.Serve("/api/registry", System.Text.Encoding.UTF8.GetBytes(
            $$"""[{"repository":"engine","adopted":true,"registered":true,"root":"{{Slashed(root)}}","workspace":"aurora"}]"""));
        return ledger;
    }

    /// <summary>The loop with its service up over a ledger on this machine, and `engine` drivable, so its strikes can park a quest.</summary>
    private async Task<DriverLoop> UpAsync(LoopbackHost ledger)
    {
        (DriverConfig.Empty with { Drivable = ["engine"] }).Save(DriverConfigPath);
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient(ledger.Address, null));
        return loop;
    }

    /// <summary>
    /// The service's doors a look at the sessions reads, standing in over a real socket on this machine: `engine` registered
    /// and adopted; `q1` open with three failed sessions, which park it; a parked session, a running one, and a quest done.
    /// </summary>
    private LoopbackHost Ledger()
    {
        var ledger = new LoopbackHost();
        var root = Path.Combine(Home, "checkouts", "engine").Replace('\\', '/');
        string Session(string id, string state, string quest, int minute) =>
            $$"""{"id":"{{id}}","quest":"{{quest}}","repository":"engine","adapter":"claude-code","state":"{{state}}","kind":"driven","created":"2026-10-02T09:{{minute:00}}:00Z","updated":"2026-10-02T09:{{minute + 1:00}}:00Z"}""";
        var records = $"[{Session("failed1", "failed", "q1", 0)},{Session("failed2", "failed", "q1", 10)},{Session("failed3", "failed", "q1", 20)},"
                      + $"{Session("waiting1", "awaiting-person", "q2", 30)},{Session("running1", "working", "q3", 40)},{Session("done1", "completed", "q4", 50)}]";
        string Quest(string id, string status) =>
            $$"""{"id":"{{id}}","from":"game","to":"engine","title":"The work of #{{id}}","body":"A body.","status":"{{status}}"}""";
        var live = $"[{Quest("q1", "Open")},{Quest("q2", "Taken")},{Quest("q3", "Taken")}]";
        var every = $"[{Quest("q1", "Open")},{Quest("q2", "Taken")},{Quest("q3", "Taken")},{Quest("q4", "Done")}]";
        var active = $"[{Session("waiting1", "awaiting-person", "q2", 30)},{Session("running1", "working", "q3", 40)}]";
        ledger.Serve("/api/sessions?includeClosed=true", System.Text.Encoding.UTF8.GetBytes(records));
        ledger.Serve("/api/sessions", System.Text.Encoding.UTF8.GetBytes(active));
        ledger.Serve("/api/quests?includeClosed=true", System.Text.Encoding.UTF8.GetBytes(every));
        ledger.Serve("/api/quests", System.Text.Encoding.UTF8.GetBytes(live));
        ledger.Serve("/api/registry", System.Text.Encoding.UTF8.GetBytes(
            $$"""[{"repository":"engine","adopted":true,"registered":true,"root":"{{root}}","workspace":"aurora"}]"""));
        return ledger;
    }
}
