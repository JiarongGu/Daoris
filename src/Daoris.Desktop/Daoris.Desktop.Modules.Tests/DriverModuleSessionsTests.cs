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
