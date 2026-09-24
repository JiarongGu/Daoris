using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The session-control surface's host half (D46 §6, D49) — the largest thing the page talks to, and
/// the one with the most to get wrong.
/// </summary>
/// <remarks>
/// No service is running in these tests, deliberately. Everything here is either an edit to
/// `driver.json`, a read of this machine's harnesses, or a refusal — and the refusals are the point:
/// "the driver is still coming up" is the state a person meets most often on a cold start, and it has
/// to be a sentence rather than a crash.
/// </remarks>
public sealed class DriverModuleTests : Bridge
{
    private DriverLoop Loop() => new(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0");

    private DriverModule Module() => new(Bus, Loop());

    [Fact]
    public async Task A_machine_that_opted_nothing_in_answers_the_empty_shape()
    {
        var state = await AnswerAsync(Module(), "STATE");

        Assert.Empty(state.GetProperty("drivable").EnumerateArray());
        Assert.Empty(state.GetProperty("holds").EnumerateArray());
        Assert.Empty(state.GetProperty("running").EnumerateArray());
        // The path is answered so the page can tell a person which file its checkboxes edit.
        Assert.Equal(DriverConfigPath, state.GetProperty("configPath").GetString());
        Assert.Equal("claude-code", state.GetProperty("adapter").GetString());
        // And the home (D63), so the page can say where this machine's Daoris lives — with nothing
        // to announce on a start that established nothing.
        Assert.Equal(Home, state.GetProperty("home").GetString());
        Assert.Equal(JsonValueKind.Null, state.GetProperty("homeNotice").ValueKind);
        // And what the shell adopted, when it is not its own page — nothing adopted here, so null,
        // and the field is there for the page to read rather than a toast it can miss.
        Assert.Equal(JsonValueKind.Null, state.GetProperty("hostNotice").ValueKind);
    }

    /// <summary>
    /// What establishing the home did rides the STATE, not only the one-time event: the page
    /// subscribes after the host answers, and a sentence raised before that reached nobody.
    /// </summary>
    [Fact]
    public async Task What_establishing_the_home_did_is_in_the_state_for_the_page_to_show()
    {
        var established = new HomeEstablished(
            Home, SetForUser: true, Moved: ["driver.json"], Failed: [],
            Notice: $"Daoris home: {Home} — moved in: driver.json.");
        var loop = new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0", established);

        var state = await AnswerAsync(new DriverModule(Bus, loop), "STATE");

        Assert.Equal(established.Notice, state.GetProperty("homeNotice").GetString());
    }

    /// <summary>
    /// Every control here is an EDIT to the file the loop re-reads each tick (D46 §6, D50) — so the
    /// file is what gets asserted, not this module's own answer, which would only prove it agrees
    /// with itself.
    /// </summary>
    /// <summary>
    /// The review route on a cold start (SURF6). Every other refusal it can raise needs a service to
    /// answer first; this one is the state a person actually meets, and it has to be a sentence.
    /// </summary>
    [Fact]
    public async Task Asking_what_a_session_landed_before_the_driver_is_up_is_a_sentence()
    {
        var refusal = await RefusalAsync(Module(), "SESSION_DIFF", new { id = "s1a2b3c4" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
        // The sentence, not just the code: this is the state a person meets on a cold start.
        Assert.Contains("still coming up", refusal);
    }

    /// <summary>
    /// *Sync now* (SYNC6b) runs the loop's own sync set — so before the loop is up there is no pass to
    /// run, and the answer is the cold-start sentence rather than a pass that quietly did nothing.
    /// </summary>
    [Fact]
    public async Task Syncing_now_before_the_driver_is_up_is_a_sentence()
    {
        var refusal = await RefusalAsync(Module(), "SYNC_NOW", new { workspace = "default" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
        Assert.Contains("still coming up", refusal);
    }

    /// <summary>A review of nothing in particular is a malformed call, not an empty answer.</summary>
    [Fact]
    public async Task Asking_what_a_session_landed_without_naming_one_is_refused()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(Module(), "SESSION_DIFF", new { }));
    }

    [Fact]
    public async Task Opting_a_repository_in_writes_the_file_the_loop_reads()
    {
        var state = await AnswerAsync(
            Module(), "SET_DRIVABLE", new { repository = "engine", drivable = true });

        Assert.Equal("engine", state.GetProperty("drivable")[0].GetString());
        Assert.Contains("engine", DriverConfig.Load(DriverConfigPath).Drivable);
    }

    [Fact]
    public async Task Holding_is_a_pause_on_something_still_opted_in()
    {
        var module = Module();
        await AnswerAsync(module, "SET_DRIVABLE", new { repository = "engine", drivable = true });
        await AnswerAsync(module, "SET_HOLD", new { repository = "engine", held = true });

        var config = DriverConfig.Load(DriverConfigPath);
        // Both, not either: a hold that un-opted-in would lose the person's standing decision.
        Assert.Contains("engine", config.Drivable);
        Assert.Contains("engine", config.Holds);
    }

    /// <summary>
    /// Session trees (D51): the desktop half of the standing opt-in, over the same file the CLI's
    /// `daoris driver trees <repo> on|off` edits — two editors, one truth (D50).
    /// </summary>
    [Fact]
    public async Task Opting_a_repository_into_session_trees_writes_the_same_file()
    {
        var module = Module();
        var state = await AnswerAsync(module, "SET_TREES", new { repository = "engine", ownTree = true });

        Assert.Equal("engine", state.GetProperty("trees")[0].GetString());
        Assert.True(DriverConfig.Load(DriverConfigPath).OpensOwnTree("engine"));

        var off = await AnswerAsync(module, "SET_TREES", new { repository = "engine", ownTree = false });
        Assert.Equal(0, off.GetProperty("trees").GetArrayLength());
    }

    /// <summary>
    /// Notifications (SURF5b): the desktop half of a machine-local switch whose other door is
    /// `daoris driver notify on|off`. One file, two editors (D50) — and it is ON until somebody
    /// says otherwise, because a driver nobody has to watch is the whole point of one.
    /// </summary>
    [Fact]
    public async Task Turning_notifications_off_writes_the_same_file_the_terminal_edits()
    {
        var module = Module();

        Assert.True((await AnswerAsync(module, "STATE")).GetProperty("notify").GetBoolean());

        var off = await AnswerAsync(module, "SET_NOTIFY", new { notify = false });
        Assert.False(off.GetProperty("notify").GetBoolean());
        Assert.False(DriverConfig.Load(DriverConfigPath).Notify);

        var on = await AnswerAsync(module, "SET_NOTIFY", new { notify = true });
        Assert.True(on.GetProperty("notify").GetBoolean());
    }

    /// <summary>
    /// The strike limit over the bridge (DRV6/D58) — the other half of D50's two doors, and the half
    /// that rots: a setting added to the terminal and not reported here compiles perfectly and leaves
    /// the screen unable to show, let alone change, what the machine is actually doing.
    /// </summary>
    [Fact]
    public async Task The_strike_limit_and_a_retry_write_the_same_file_the_terminal_edits()
    {
        var module = Module();

        Assert.Equal(3, (await AnswerAsync(module, "STATE")).GetProperty("strikes").GetInt32());

        var set = await AnswerAsync(module, "SET_STRIKES", new { strikes = 5 });
        Assert.Equal(5, set.GetProperty("strikes").GetInt32());
        Assert.Equal(5, DriverConfig.Load(DriverConfigPath).Strikes);

        // 🔴 Zero is a real answer — "keep trying", the behaviour before this existed — and must not
        // be read as the absence of one.
        var never = await AnswerAsync(module, "SET_STRIKES", new { strikes = 0 });
        Assert.Equal(0, never.GetProperty("strikes").GetInt32());
        Assert.Equal(0, DriverConfig.Load(DriverConfigPath).Strikes);

        await AnswerAsync(module, "SET_STRIKES", new { strikes = 3 });
        var retried = await AnswerAsync(module, "RETRY_QUEST", new { quest = "a78553" });
        Assert.Equal(3, retried.GetProperty("forgiven").GetProperty("a78553").GetInt32());
        Assert.Equal(3, DriverConfig.Load(DriverConfigPath).ForgivenAt("a78553"));
    }

    /// <summary>
    /// The intake's harness (INT4b) over the bridge — the screen's half of `daoris driver intake`. Off
    /// until named, and 🔴 kept by every other edit: the state is what the page shows, and a control
    /// that deleted it would switch the intake off with nobody touching it.
    /// </summary>
    /// <remarks>
    /// 🔴 Off is the empty string on the wire, never null. The bridge leaves a null property out, and
    /// the page tells a shell older than the intake by the field's ABSENCE — so a null "off" read as an
    /// older shell and the screen offered no intake control at all: seen on the window (AGT6), from
    /// exactly the state a person would want to switch it on from. This helper keeps nulls, which is
    /// why it never saw that.
    /// </remarks>
    [Fact]
    public async Task The_intake_harness_is_off_until_named_and_every_other_edit_keeps_it()
    {
        var module = Module();

        Assert.Equal("", (await AnswerAsync(module, "STATE")).GetProperty("intakeAdapter").GetString());

        var on = await AnswerAsync(module, "SET_INTAKE", new { adapter = "claude-code-acp" });
        Assert.Equal("claude-code-acp", on.GetProperty("intakeAdapter").GetString());

        await AnswerAsync(module, "SET_NOTIFY", new { notify = false });
        Assert.Equal("claude-code-acp", DriverConfig.Load(DriverConfigPath).IntakeAdapter);

        var off = await AnswerAsync(module, "SET_INTAKE", new { adapter = (string?)null });
        Assert.Equal("", off.GetProperty("intakeAdapter").GetString());
        Assert.Null(DriverConfig.Load(DriverConfigPath).IntakeAdapter);
    }

    /// <summary>
    /// The screen's half of trusting a folder (D73): the person confirms a hold the driver is showing,
    /// and exactly that grant is written — the folder the driver held, in the file it read.
    /// </summary>
    [Fact]
    public async Task Trusting_a_folder_the_driver_is_holding_writes_the_grant_it_read()
    {
        var loop = Loop();
        var file = Path.Combine(Home, "profile", ".claude.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """{"numStartups":7,"projects":{}}""");
        var folder = Path.Combine(Home, "engine");
        loop.Trust.Record([new TrustHold(folder, file, Quest: "q1")]);

        var answer = await AnswerAsync(new DriverModule(Bus, loop), "TRUST_FOLDER", new { folder, trustFile = file });

        Assert.True(answer.GetProperty("changed").GetBoolean());
        Assert.True(answer.GetProperty("verified").GetBoolean());
        Assert.Contains(folder, answer.GetProperty("message").GetString());
        Assert.True(ClaudeTrust.Accepted(file, folder));
        Assert.Contains("\"numStartups\": 7", File.ReadAllText(file));
    }

    /// <summary>
    /// 🔴 <b>Never wider than the hold.</b> The screen can only confirm what the driver is holding: a
    /// folder it is not holding — or the right folder in some other file — is refused in the driver's
    /// own words, and nothing is written. The terminal is the door that names any folder.
    /// </summary>
    [Fact]
    public async Task Trusting_a_folder_the_driver_is_not_holding_is_refused_and_writes_nothing()
    {
        var loop = Loop();
        var file = Path.Combine(Home, "profile", ".claude.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """{"projects":{}}""");
        var held = Path.Combine(Home, "engine");
        loop.Trust.Record([new TrustHold(held, file, Quest: "q1")]);
        var module = new DriverModule(Bus, loop);

        var elsewhere = await RefusalAsync(module, "TRUST_FOLDER", new { folder = Path.Combine(Home, "other"), trustFile = file });
        var otherFile = await RefusalAsync(module, "TRUST_FOLDER", new { folder = held, trustFile = Path.Combine(Home, "x.json") });

        Assert.Contains(Refusals.DriverRefused, elsewhere);
        Assert.Contains("daoris agent trust", elsewhere);
        Assert.Contains(Refusals.DriverRefused, otherFile);
        Assert.Equal("""{"projects":{}}""", File.ReadAllText(file));
        Assert.False(File.Exists(Path.Combine(Home, "x.json")));
    }

    /// <summary>
    /// What sessions consumed (TOOL3/D57 §4), over the bridge and nowhere else. A machine that has
    /// measured nothing answers empty lists — <b>never a zero</b>, because "nothing was measured" and
    /// "it used nothing" are different claims and only one of them is true.
    /// </summary>
    [Fact]
    public async Task A_machine_that_has_measured_nothing_answers_empty_rather_than_zero()
    {
        var usage = await AnswerAsync(Module(), "USAGE");

        Assert.Empty(usage.GetProperty("sessions").EnumerateArray());
        Assert.Empty(usage.GetProperty("accounts").EnumerateArray());
    }

    [Fact]
    public async Task Usage_reaches_the_page_per_session_and_per_account()
    {
        var store = new SessionUsage(Home);
        store.Record(new UsageEntry(
            "s1", "engine", "claude-code", "work", 48_000, 200_000, DateTimeOffset.UtcNow.AddMinutes(-5)));
        store.Record(new UsageEntry(
            "s2", "tools", "claude-code", "work", 12_000, 200_000, DateTimeOffset.UtcNow));

        var usage = await AnswerAsync(Module(), "USAGE");

        // Newest first: somebody looking at this is asking about recent work.
        Assert.Equal("s2", usage.GetProperty("sessions")[0].GetProperty("session").GetString());
        var account = usage.GetProperty("accounts")[0];
        Assert.Equal("work", account.GetProperty("profile").GetString());
        Assert.Equal(60_000, account.GetProperty("used").GetInt64());
        Assert.Equal(2, account.GetProperty("sessions").GetInt32());
    }

    /// <summary>Every other standing choice survives it — this is an editor, not the file's owner.</summary>
    [Fact]
    public async Task The_notify_switch_leaves_the_rest_of_the_file_standing()
    {
        var module = Module();
        await AnswerAsync(module, "SET_DRIVABLE", new { repository = "engine", drivable = true });
        await AnswerAsync(module, "SET_TREES", new { repository = "engine", ownTree = true });

        await AnswerAsync(module, "SET_NOTIFY", new { notify = false });

        var config = DriverConfig.Load(DriverConfigPath);
        Assert.Contains("engine", config.Drivable);
        Assert.True(config.OpensOwnTree("engine"));
        Assert.False(config.Notify);
    }

    [Fact]
    public async Task Taking_a_repository_back_out_leaves_the_others_standing()
    {
        var module = Module();
        await AnswerAsync(module, "SET_DRIVABLE", new { repository = "engine", drivable = true });
        await AnswerAsync(module, "SET_DRIVABLE", new { repository = "game", drivable = true });

        await AnswerAsync(module, "SET_DRIVABLE", new { repository = "engine", drivable = false });

        var left = DriverConfig.Load(DriverConfigPath).Drivable;
        Assert.DoesNotContain("engine", left);
        Assert.Contains("game", left);
    }

    /// <summary>
    /// The person's controls must not destroy the fields they have no checkbox for — the adapter
    /// command above all, which is what makes the stub adapter run at all.
    /// </summary>
    [Fact]
    public async Task An_edit_keeps_the_settings_the_surface_does_not_own()
    {
        File.WriteAllText(DriverConfigPath, """
            {
              "drivable": [],
              "holds": [],
              "cap": 4,
              "adapter": "stub",
              "timeoutMinutes": 45,
              "pollSeconds": 9,
              "commands": { "stub": ["node", "agent.mjs"] }
            }
            """);

        await AnswerAsync(Module(), "SET_DRIVABLE", new { repository = "engine", drivable = true });

        var config = DriverConfig.Load(DriverConfigPath);
        Assert.Equal(4, config.Cap);
        Assert.Equal("stub", config.Adapter);
        Assert.Equal(45, config.TimeoutMinutes);
        Assert.Equal(["node", "agent.mjs"], config.Commands["stub"]);
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
    /// 🔴 A session that takes no input — an intake, one turn, whose stdin on the protocol door
    /// carries the driver's own frames (INT4h) — is REFUSED in the driver's words, never answered
    /// false. False means "it ended while you were typing", and a page that read it so would tell the
    /// person something untrue about a session that is running fine. Finishing it is refused too:
    /// closing that stream would end the protocol's turn, not a conversation.
    /// </summary>
    [Theory]
    [InlineData("SESSION_INPUT")]
    [InlineData("END_CHAT")]
    public async Task A_session_that_takes_no_input_is_refused_in_the_drivers_words(string type)
    {
        var loop = Loop();
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "node",
            ArgumentList = { "-e", "setTimeout(() => {}, 60000)" },
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        using var tracked = loop.Processes.Track(
            "i1", process, refusesInput: "ask #a1b2c3's intake takes no messages.");

        try
        {
            var refusal = await RefusalAsync(new DriverModule(Bus, loop), type, new { id = "i1", text = "hello" });

            Assert.Contains(Refusals.DriverRefused, refusal);
            Assert.Contains("ask #a1b2c3's intake takes no messages.", refusal);
        }
        finally
        {
            loop.Processes.Stop("i1");
        }
    }

    [Fact]
    public async Task Sending_to_a_session_that_is_not_listening_answers_false()
    {
        var state = await AnswerAsync(Module(), "SESSION_INPUT", new { id = "nothing-here", text = "hello" });

        Assert.False(state.GetProperty("sent").GetBoolean());
    }

    /// <summary>
    /// The console degrades to empty rather than failing — the page treats it as the part of the
    /// drawer that may be missing, and the record above it is what the drawer exists to show.
    /// </summary>
    [Fact]
    public async Task Tailing_a_session_nobody_has_heard_of_answers_an_empty_console()
    {
        var tail = await AnswerAsync(Module(), "TAIL_SESSION", new { id = "nothing-here" });

        Assert.Empty(tail.GetProperty("lines").EnumerateArray());
        Assert.False(tail.GetProperty("live").GetBoolean());
        Assert.Equal(0, tail.GetProperty("dropped").GetInt32());
    }

    /// <summary>
    /// D76 §2 (CONV1): a session's conversation is read back over the bridge a page at a time — the
    /// newest first, then earlier, then only what is newer — from the record under the home, so it
    /// answers after a restart when the console's window is long gone.
    /// </summary>
    [Fact]
    public async Task A_sessions_conversation_is_read_back_a_page_at_a_time()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        for (var i = 1; i <= 5; i++)
        {
            loop.Events.Append("s1", new SessionEvent { Kind = SessionEventKind.Message, Text = $"m{i}" });
        }

        var latest = await AnswerAsync(module, "SESSION_HISTORY", new { id = "s1", limit = 2 });
        Assert.Equal("s1", latest.GetProperty("session").GetString());
        Assert.Equal(["m4", "m5"], latest.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("text").GetString()));
        Assert.True(latest.GetProperty("earlier").GetBoolean());
        Assert.Equal(5, latest.GetProperty("latest").GetInt64());
        Assert.Equal("message", latest.GetProperty("events")[0].GetProperty("kind").GetString());

        var earlier = await AnswerAsync(module, "SESSION_HISTORY", new { id = "s1", before = 4, limit = 2 });
        Assert.Equal(["m2", "m3"], earlier.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("text").GetString()));

        var newer = await AnswerAsync(module, "SESSION_HISTORY", new { id = "s1", after = 3 });
        Assert.Equal(["m4", "m5"], newer.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("text").GetString()));

        var none = await AnswerAsync(module, "SESSION_HISTORY", new { id = "nothing-here" });
        Assert.Empty(none.GetProperty("events").EnumerateArray());
    }

    /// <summary>An id arrives from the page, so one that is not an id is refused in a sentence, never read.</summary>
    [Fact]
    public async Task A_history_asked_for_by_a_path_is_refused()
    {
        var refusal = await RefusalAsync(Module(), "SESSION_HISTORY", new { id = "../escape" });

        Assert.Contains("is not a session id", refusal);
    }

    /// <summary>
    /// A cold start is the state a person meets most often, and it must be a SENTENCE: the loop's
    /// service is not answering yet, so there is nothing to put behind a conversation.
    /// </summary>
    [Fact]
    public async Task A_chat_asked_for_before_the_loop_is_up_says_so_rather_than_crashing()
    {
        var refusal = await RefusalAsync(Module(), "START_CHAT", new { repository = "engine" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
    }

    /// <summary>Detection is free and read-only (D49 §4) — the roster answers with no service at all.</summary>
    /// <summary>
    /// The plugins (D64) as the page reads them: the same catalogue the driver reads each tick, each
    /// with what it declares and speaks on, and the two doors' switch over the same row.
    /// </summary>
    [Fact]
    public async Task The_plugin_catalogue_is_answered_and_the_switch_edits_the_same_row_a_terminal_does()
    {
        var folder = Path.Combine(Home, "plugins", "acme.gate");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), """
            { "id": "acme.gate", "name": "Acme gate", "version": "1.2.0", "description": "Holds quests overnight.",
              "harnesses": [ { "name": "acme-agent", "command": ["${plugin}/agent.mjs"] } ],
              "hooks": { "command": ["node", "${plugin}/hooks.mjs"], "points": ["quest/consider"] } }
            """);
        var broken = Path.Combine(Home, "plugins", "future");
        Directory.CreateDirectory(broken);
        File.WriteAllText(Path.Combine(broken, "plugin.json"), """{ "id": "future", "apiVersion": 99 }""");
        var module = Module();

        var answered = await AnswerAsync(module, "PLUGINS");

        Assert.Equal(Path.Combine(Home, "plugins"), answered.GetProperty("folder").GetString());
        var plugins = answered.GetProperty("plugins").EnumerateArray().ToList();
        Assert.Equal(2, plugins.Count);
        var gate = plugins.Single(p => p.GetProperty("id").GetString() == "acme.gate");
        Assert.Equal("Acme gate", gate.GetProperty("name").GetString());
        Assert.Equal("1.2.0", gate.GetProperty("version").GetString());
        Assert.True(gate.GetProperty("enabled").GetBoolean());
        Assert.Equal(JsonValueKind.Null, gate.GetProperty("problem").ValueKind);
        Assert.Equal(["acme-agent"], gate.GetProperty("harnesses").EnumerateArray().Select(h => h.GetString()!).ToArray());
        Assert.Equal(["quest/consider"], gate.GetProperty("points").EnumerateArray().Select(p => p.GetString()!).ToArray());
        // No loop is running here, so nothing is up — and the page must be told so rather than guess.
        Assert.False(gate.GetProperty("running").GetBoolean());
        Assert.Equal(folder, gate.GetProperty("folder").GetString());
        // A refused plugin is listed WITH its sentence, and contributes nothing.
        var future = plugins.Single(p => p.GetProperty("id").GetString() == "future");
        Assert.Contains("99", future.GetProperty("problem").GetString());
        Assert.Empty(future.GetProperty("harnesses").EnumerateArray());

        // The switch: a row in the same file `daoris plugin disable` writes, never a rename.
        var off = await AnswerAsync(module, "PLUGIN_ACTION", new { id = "acme.gate", action = "disable" });
        Assert.Equal("disable", off.GetProperty("action").GetString());
        Assert.Contains("acme.gate", File.ReadAllText(Path.Combine(Home, "plugins.json")));
        Assert.True(Directory.Exists(folder));
        var again = await AnswerAsync(module, "PLUGINS");
        Assert.False(again.GetProperty("plugins").EnumerateArray()
            .Single(p => p.GetProperty("id").GetString() == "acme.gate").GetProperty("enabled").GetBoolean());

        await AnswerAsync(module, "PLUGIN_ACTION", new { id = "acme.gate", action = "enable" });
        Assert.DoesNotContain("acme.gate", File.ReadAllText(Path.Combine(Home, "plugins.json")));

        // Remove takes the install folder and NAMES the data folder, which stays.
        var data = Path.Combine(Home, "plugins", ".data", "acme.gate");
        Directory.CreateDirectory(data);
        var removed = await AnswerAsync(module, "PLUGIN_ACTION", new { id = "acme.gate", action = "remove" });
        Assert.Equal(data, removed.GetProperty("data").GetString());
        Assert.False(Directory.Exists(folder));
        Assert.True(Directory.Exists(data));

        // An id nobody has, and an action this build lacks, are refusals with the page's own codes.
        Assert.Contains("PLUGIN_UNKNOWN", await RefusalAsync(module, "PLUGIN_ACTION", new { id = "nobody", action = "enable" }));
        Assert.Contains("PLUGIN_ACTION_UNKNOWN", await RefusalAsync(module, "PLUGIN_ACTION", new { id = "future", action = "explode" }));
    }

    /// <summary>
    /// What an agent may do (PERM1, D72) as the page reads it: Daoris's defaults, each with its reason and
    /// whether it is on, and every scope the machine's file holds — the file the terminal's
    /// `daoris agent rules` edits.
    /// </summary>
    [Fact]
    public async Task The_rules_are_answered_with_the_defaults_and_every_scope_the_file_holds()
    {
        var file = PermissionRules.Load(Home);
        file = PermissionRules.Add(file, RuleScope.Machine, null, RuleList.Allow, "Bash(npm run test:*)");
        file = PermissionRules.Add(file, RuleScope.Repository, "engine", RuleList.Deny, "Bash(rm -rf:*)");
        PermissionRules.Save(Home, PermissionRules.SwitchDefault(file, "no-push", on: false));

        var answered = await AnswerAsync(Module(), "RULES");

        Assert.Equal(PermissionRules.PathOf(Home), answered.GetProperty("path").GetString());
        var defaults = answered.GetProperty("defaults").EnumerateArray().ToList();
        Assert.Equal(
            ["connector", "commit", "no-push", "tree-guard"],
            defaults.Select(d => d.GetProperty("id").GetString()!).ToArray());
        Assert.True(defaults[0].GetProperty("on").GetBoolean());
        Assert.False(defaults[2].GetProperty("on").GetBoolean());
        Assert.Equal("deny", defaults[2].GetProperty("list").GetString());
        // The driver's own sentence travels: what the number meant, never the number (POLISH4a).
        Assert.Contains("stays the person's call", defaults[2].GetProperty("why").GetString());
        // The tree guard (PERM3) is a hook: no rule, and the tools it judges named instead. The bridge
        // leaves a null out, so a rule default carries no `hook` the page could misread.
        Assert.Empty(defaults[3].GetProperty("rules").EnumerateArray());
        Assert.Equal(TreeGuard.Matcher, defaults[3].GetProperty("hook").GetString());
        Assert.True(defaults[0].GetProperty("hook").ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);

        var scopes = answered.GetProperty("scopes").EnumerateArray().ToList();
        var machine = scopes.Single(s => s.GetProperty("scope").GetString() == "machine");
        Assert.Equal(["Bash(npm run test:*)"], machine.GetProperty("allow").EnumerateArray().Select(r => r.GetString()!).ToArray());
        var engine = scopes.Single(s => s.GetProperty("scope").GetString() == "repository");
        Assert.Equal("engine", engine.GetProperty("name").GetString());
        Assert.Equal(["Bash(rm -rf:*)"], engine.GetProperty("deny").EnumerateArray().Select(r => r.GetString()!).ToArray());
    }

    /// <summary>
    /// The screen's half of `daoris agent rules` (D50): every change is an edit to the same file, and
    /// the answer is the state after it, like every other control here.
    /// </summary>
    [Fact]
    public async Task A_rule_changed_on_the_screen_lands_in_the_file_a_terminal_edits()
    {
        var module = Module();

        var added = await AnswerAsync(module, "RULE_ACTION",
            new { action = "add", list = "allow", rule = "Bash(make:*)", scope = "repository", name = "engine" });
        Assert.Equal(["Bash(make:*)"], PermissionRules.Load(Home).Repositories["engine"].Allow);
        Assert.Contains(added.GetProperty("scopes").EnumerateArray(), s => s.GetProperty("scope").GetString() == "repository");

        await AnswerAsync(module, "RULE_ACTION",
            new { action = "add", list = "ask", rule = "WebFetch", scope = "workspace", name = "default" });
        Assert.Equal(["WebFetch"], PermissionRules.Load(Home).Workspaces["default"].Ask);

        await AnswerAsync(module, "RULE_ACTION",
            new { action = "remove", rule = "Bash(make:*)", scope = "repository", name = "engine" });
        Assert.False(PermissionRules.Load(Home).Repositories.ContainsKey("engine"));

        var off = await AnswerAsync(module, "RULE_ACTION", new { action = "default", id = "connector", on = false });
        Assert.Equal(["connector"], PermissionRules.Load(Home).DefaultsOff);
        Assert.False(off.GetProperty("defaults").EnumerateArray().First().GetProperty("on").GetBoolean());
    }

    /// <summary>A refusal is the driver's own sentence, verbatim — never a bare code or a crash.</summary>
    [Fact]
    public async Task A_rule_that_is_not_one_and_an_action_this_build_lacks_are_refused_in_the_drivers_words()
    {
        var module = Module();

        var notARule = await RefusalAsync(module, "RULE_ACTION",
            new { action = "add", list = "allow", rule = "rm -rf /", scope = "machine" });
        Assert.Contains(Refusals.DriverRefused, notARule);
        Assert.Contains("not a permission rule", notARule);

        var unknown = await RefusalAsync(module, "RULE_ACTION", new { action = "explode" });
        Assert.Contains("add, remove, default", unknown);
        Assert.False(File.Exists(PermissionRules.PathOf(Home)));
    }

    /// <summary>A proposal exactly as the connector writes it (PERM2) — THE FILE is the contract.</summary>
    private void Propose(
        string id, string state, string action, string? list = null, string? rule = null,
        string scope = "machine", string? name = null, string? session = "s1a2b3c4", string? ask = null,
        string proposed = "2026-09-24T10:00:00.0000000+00:00")
    {
        var folder = Path.Combine(Home, RuleProposals.Folder);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, $"{id}.json"), JsonSerializer.Serialize(new
        {
            id,
            proposed,
            by = new { session, ask, folder = "C:/somewhere/engine" },
            change = new { action, scope, name, list, rule, @default = (string?)null, on = (bool?)null },
            why = "The tests need it.",
            state,
        }));
    }

    /// <summary>
    /// What agents proposed about the rules (PERM2, D74) rides the same answer as the rules: the person
    /// reads a waiting widening beside the rules it would change, and the history of every other.
    /// </summary>
    [Fact]
    public async Task The_rules_carry_every_proposal_newest_first_with_who_made_it()
    {
        Propose("p0000001", "applied", "add", list: "deny", rule: "Bash(rm:*)", session: null);
        Propose("p0000002", "waiting", "add", list: "allow", rule: "WebFetch", scope: "workspace", name: "default",
            session: "i9n8t7k6", ask: "a1b2c3", proposed: "2026-09-24T11:00:00.0000000+00:00");

        var answered = await AnswerAsync(Module(), "RULES");

        var proposals = answered.GetProperty("proposals").EnumerateArray().ToList();
        Assert.Equal(["p0000002", "p0000001"], proposals.Select(p => p.GetProperty("id").GetString()!).ToArray());
        var waiting = proposals[0];
        Assert.Equal("waiting", waiting.GetProperty("state").GetString());
        Assert.Equal("add", waiting.GetProperty("action").GetString());
        Assert.Equal("allow", waiting.GetProperty("list").GetString());
        Assert.Equal("WebFetch", waiting.GetProperty("rule").GetString());
        Assert.Equal("workspace", waiting.GetProperty("scope").GetString());
        Assert.Equal("default", waiting.GetProperty("name").GetString());
        Assert.Equal("i9n8t7k6", waiting.GetProperty("session").GetString());
        Assert.Equal("a1b2c3", waiting.GetProperty("ask").GetString());
        Assert.Equal("The tests need it.", waiting.GetProperty("why").GetString());
        // 🔴 The folder a session ran in is a machine path, and it stays in the file: the page is told
        // who proposed, never where they stood.
        Assert.False(waiting.TryGetProperty("folder", out _));
        // A session the driver did not start carries none, which the bridge leaves out.
        Assert.True(!proposals[1].TryGetProperty("session", out var none) || none.ValueKind is JsonValueKind.Null);
    }

    /// <summary>The screen's half of `daoris agent rules accept|decline` (D50), answered with the rules after it.</summary>
    [Fact]
    public async Task A_proposal_answered_on_the_screen_lands_as_the_terminal_would_leave_it()
    {
        Propose("p0000007", "waiting", "add", list: "allow", rule: "WebFetch");
        Propose("p0000008", "waiting", "add", list: "allow", rule: "Bash(curl:*)");
        var module = Module();

        var accepted = await AnswerAsync(module, "RULE_PROPOSAL", new { id = "p0000007", accept = true });
        Assert.Equal(["WebFetch"], PermissionRules.Load(Home).Machine.Allow);
        var row = accepted.GetProperty("proposals").EnumerateArray().Single(p => p.GetProperty("id").GetString() == "p0000007");
        Assert.Equal("accepted", row.GetProperty("state").GetString());
        Assert.Equal("the person", row.GetProperty("settledBy").GetString());

        var declined = await AnswerAsync(module, "RULE_PROPOSAL", new { id = "p0000008", accept = false, note = "Not from a session." });
        Assert.Equal(["WebFetch"], PermissionRules.Load(Home).Machine.Allow);
        Assert.Equal("Not from a session.", declined.GetProperty("proposals").EnumerateArray()
            .Single(p => p.GetProperty("id").GetString() == "p0000008").GetProperty("note").GetString());

        var again = await RefusalAsync(module, "RULE_PROPOSAL", new { id = "p0000007", accept = false });
        Assert.Contains(Refusals.DriverRefused, again);
        Assert.Contains("already", again);
        Assert.Contains("no proposal", await RefusalAsync(module, "RULE_PROPOSAL", new { id = "nope1234", accept = true }));
    }

    /// <summary>A declared harness is on the roster with the plugin it came from beside it (D64).</summary>
    [Fact]
    public async Task A_declared_harness_is_on_the_roster_naming_the_plugin_it_came_from()
    {
        var folder = Path.Combine(Home, "plugins", "acme.gate");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), """
            { "id": "acme.gate", "harnesses": [ { "name": "acme-agent", "command": ["${plugin}/agent.mjs"], "profileVariable": "ACME_HOME" } ] }
            """);
        File.WriteAllText(Path.Combine(folder, "agent.mjs"), "// never run by a probe\n");

        var roster = await AnswerAsync(Module(), "HARNESSES");

        var rows = roster.GetProperty("harnesses").EnumerateArray().ToList();
        var declared = rows.Single(h => h.GetProperty("harness").GetString() == "acme-agent");
        Assert.Equal("acme.gate", declared.GetProperty("plugin").GetString());
        Assert.Equal("acp", declared.GetProperty("wire").GetString());
        // Probed by presence: the file is there, so it is present, and nothing was started to say so.
        Assert.True(declared.GetProperty("present").GetBoolean());
        // The build's own carry no plugin.
        Assert.Equal(JsonValueKind.Null, rows.Single(h => h.GetProperty("harness").GetString() == "claude-code").GetProperty("plugin").ValueKind);
    }

    /// <summary>
    /// Whether a door carries a conversation's STRUCTURE (D76 §1) is the adapter's to say, and the page's
    /// to read: an empty record on a structured door is nothing said yet, and on a text door it is a
    /// conversation that lives in the console. Guessed from emptiness, the page told a fresh chat on a
    /// structured door that its door carries only text (CONV3b).
    /// </summary>
    [Fact]
    public async Task The_roster_says_which_doors_carry_a_conversations_structure()
    {
        var rows = (await AnswerAsync(Module(), "HARNESSES")).GetProperty("harnesses").EnumerateArray()
            .ToDictionary(h => h.GetProperty("harness").GetString()!, h => h.GetProperty("structured").GetBoolean());

        Assert.True(rows["claude-code"]);      // stream-json, read by its adapter (CONV3a)
        Assert.True(rows["claude-code-acp"]);  // the protocol door
        // A text door answers false — the stub is one, held where it chats (ProtocolChatTests); the
        // roster leaves the test doubles out, and every harness it lists carries structure today.
    }

    [Fact]
    public async Task The_harness_roster_answers_this_machine_s_toolchains()
    {
        var roster = await AnswerAsync(Module(), "HARNESSES");

        Assert.Equal(DriverConfigPath.Replace("driver.json", "harnesses.json"), roster.GetProperty("settingsPath").GetString());
        var harnesses = roster.GetProperty("harnesses").EnumerateArray().ToList();
        Assert.NotEmpty(harnesses);
        // Every row carries what the page renders, whether or not the tool is installed here.
        foreach (var harness in harnesses)
        {
            Assert.False(string.IsNullOrWhiteSpace(harness.GetProperty("harness").GetString()));
            Assert.True(harness.TryGetProperty("present", out _));
            Assert.True(harness.TryGetProperty("profiles", out _));
        }

        // AGT1: what a person calls the tool, and whose it is — `dsh` meant nothing to the owner
        // until it said. Answered by the toolchain's own declaration, not a table on the page.
        var claude = harnesses.Single(h => h.GetProperty("harness").GetString() == "claude-code");
        Assert.Equal("Claude Code", claude.GetProperty("product").GetString());
        Assert.Equal("Anthropic", claude.GetProperty("maker").GetString());
        var dsh = harnesses.Single(h => h.GetProperty("harness").GetString() == "dsh");
        Assert.Equal("DeepSeek", dsh.GetProperty("maker").GetString());
    }

    /// <summary>
    /// A profile's HOME is a machine path, and this bridge is the one surface allowed to carry one
    /// (D47 §4) — the page renders it so a person can find the directory Daoris owns for them.
    /// </summary>
    [Fact]
    public async Task A_profile_is_answered_with_the_directory_it_actually_is()
    {
        var home = HarnessSettings.ProfileHome(Home, "stub", "work");
        Directory.CreateDirectory(home);
        // The stub is a door on this machine only once a command names what it runs — a door with
        // nothing to run is off the roster, which is what kept a fixture off the deployed roster.
        File.WriteAllText(DriverConfigPath, """
            { "drivable": [], "holds": [], "cap": 1, "adapter": "stub",
              "commands": { "stub": ["node", "agent.mjs"] } }
            """);

        var roster = await AnswerAsync(Module(), "HARNESSES");
        var stub = roster.GetProperty("harnesses").EnumerateArray().Single(h => h.GetProperty("harness").GetString() == "stub");

        var profile = Assert.Single(stub.GetProperty("profiles").EnumerateArray().ToList());
        Assert.Equal("work", profile.GetProperty("name").GetString());
        Assert.Equal(home, profile.GetProperty("home").GetString());
        // Nothing ran, so nothing can be claimed about the login — unknown, never a guess.
        Assert.Equal("unknown", profile.GetProperty("login").GetString());
        // The tool's own home is answered the same way, beside the profiles rather than instead of
        // them: the account a person actually has, which the roster used to call "No accounts".
        Assert.Equal("unknown", stub.GetProperty("ownLogin").GetString());
    }

    /// <summary>
    /// 🔴 A work account for the work circle (D49 §4) could be set from a terminal
    /// (`daoris agent profile default … --workspace`) and not from the screen — D50 in the
    /// direction nothing tests. The bridge carries the workspace, the roster answers which circles
    /// use which account, and an action naming no profile CLEARS the default rather than refusing:
    /// "use the tool's own home again" is a choice, not a missing argument.
    /// </summary>
    [Fact]
    public async Task A_workspace_s_default_account_is_set_answered_and_cleared_over_the_bridge()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, "stub", "work"));
        File.WriteAllText(DriverConfigPath, """
            { "drivable": [], "holds": [], "cap": 1, "adapter": "stub",
              "commands": { "stub": ["node", "agent.mjs"] } }
            """);

        await AnswerAsync(Module(), "HARNESS_ACTION",
            new { harness = "stub", action = "profile-default", profile = "work", workspace = "aurora" });
        var stub = (await AnswerAsync(Module(), "HARNESSES")).GetProperty("harnesses").EnumerateArray()
            .Single(h => h.GetProperty("harness").GetString() == "stub");
        var circle = Assert.Single(stub.GetProperty("workspaceDefaults").EnumerateArray().ToList());
        Assert.Equal("aurora", circle.GetProperty("workspace").GetString());
        Assert.Equal("work", circle.GetProperty("profile").GetString());

        // No profile named: the circle goes back to the tool's own home.
        await AnswerAsync(Module(), "HARNESS_ACTION",
            new { harness = "stub", action = "profile-default", workspace = "aurora" });
        stub = (await AnswerAsync(Module(), "HARNESSES")).GetProperty("harnesses").EnumerateArray()
            .Single(h => h.GetProperty("harness").GetString() == "stub");
        Assert.Empty(stub.GetProperty("workspaceDefaults").EnumerateArray());

        // And the machine's own default clears the same way.
        await AnswerAsync(Module(), "HARNESS_ACTION", new { harness = "stub", action = "profile-default", profile = "work" });
        await AnswerAsync(Module(), "HARNESS_ACTION", new { harness = "stub", action = "profile-default" });
        Assert.Empty(HarnessSettings.Load(DriverConfigPath.Replace("driver.json", "harnesses.json")).Defaults);
    }

    /// <summary>
    /// The driver's own refusal, reaching the person intact — it names the adapter asked for AND the
    /// ones that exist (D23), which is the part a generic failure throws away.
    /// </summary>
    [Fact]
    public async Task An_action_on_a_harness_daoris_manages_nothing_for_carries_the_driver_s_own_sentence()
    {
        var refusal = await RefusalAsync(
            Module(), "HARNESS_ACTION", new { harness = "not-a-harness", action = "install" });

        Assert.Contains(Refusals.DriverRefused, refusal);
        Assert.Contains("not-a-harness", refusal);
        // What exists, named — the whole reason this sentence was worth writing.
        Assert.Contains("claude-code", refusal);
    }

    /// <summary>
    /// 🔴 A login outlives its request (2026-09-23). The bridge times a request out at thirty seconds
    /// and a login waits on a person for minutes, so the request that waited with it failed while the
    /// process ran on. It is answered once the process has STARTED; the prompt streams; the answer
    /// reaches the process; the end is news — `HARNESS_ENDED` — and an answer after the end is refused.
    /// </summary>
    [Fact]
    public async Task A_login_is_answered_when_it_has_started_and_its_end_is_news_the_page_hears()
    {
        // A stand-in for the harness, taking the two questions the module asks of it.
        var fake = Path.Combine(Home, "fake-claude.mjs");
        File.WriteAllText(fake, """
            const args = process.argv.slice(2).join(' ');
            if (args === 'auth status') { console.log(JSON.stringify({ loggedIn: true })); process.exit(0); }
            if (args === 'auth login') {
              console.log('Opening browser to sign in…');
              process.stdout.write('Paste code here if prompted >');
              process.stdin.once('data', (d) => { console.log('Logged in with ' + d.toString().trim()); process.exit(0); });
              setTimeout(() => process.exit(3), 20000);
            } else { console.log('claude 9.9.9'); }
            """);
        File.WriteAllText(DriverConfigPath, $$"""
            { "drivable": [], "holds": [], "commands": { "claude-code": ["node", {{JsonSerializer.Serialize(fake)}}] } }
            """);
        var module = Module();

        var answer = await AnswerAsync(
            module, "HARNESS_ACTION", new { harness = "claude-code", action = "login", profile = "work" });
        Assert.True(answer.GetProperty("started").GetBoolean());
        Assert.False(answer.TryGetProperty("exitCode", out _));

        // The prompt with no newline reached the page under the action's own id…
        await UntilAsync(() => Raised.Any(m => m.Type == "SESSION_OUTPUT"
            && JsonSerializer.Serialize(m.Payload).Contains("Paste code")));
        // …and the answer reaches the process, which ends; the end is announced, naming the account.
        await AnswerAsync(module, "HARNESS_INPUT", new { harness = "claude-code", action = "login", text = "abc-123" });
        await UntilAsync(() => Raised.Any(m => m.Type == "HARNESS_ENDED"));

        var ended = JsonSerializer.SerializeToElement(Raised.Single(m => m.Type == "HARNESS_ENDED").Payload);
        Assert.Equal(0, ended.GetProperty("ExitCode").GetInt32());
        Assert.Equal("work", ended.GetProperty("Profile").GetString());
        Assert.Equal("login", ended.GetProperty("Action").GetString());
        Assert.Contains(Raised, m => m.Type == "SESSION_OUTPUT" && JsonSerializer.Serialize(m.Payload).Contains("Logged in with abc-123"));

        var late = await RefusalAsync(module, "HARNESS_INPUT", new { harness = "claude-code", action = "login", text = "late" });
        Assert.Contains(Refusals.HarnessActionIdle, late);
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var patience = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < patience, "the condition never held");
            await Task.Delay(25);
        }
    }

    /// <summary>
    /// An answer or a stop for an action that is not running is refused naming it (2026-09-23): the
    /// code a person pasted after the login already ended must not look delivered.
    /// </summary>
    [Theory]
    [InlineData("HARNESS_INPUT")]
    [InlineData("HARNESS_CANCEL")]
    public async Task An_answer_or_a_stop_for_an_action_that_is_not_running_is_refused_naming_it(string type)
    {
        var refusal = await RefusalAsync(
            Module(), type, new { harness = "claude-code", action = "login", text = "abc-123" });

        Assert.Contains(Refusals.HarnessActionIdle, refusal);
        Assert.Contains("login", refusal);
    }

    [Fact]
    public async Task An_unknown_harness_action_is_refused_naming_it()
    {
        var refusal = await RefusalAsync(
            Module(), "HARNESS_ACTION", new { harness = "claude-code", action = "frobnicate" });

        Assert.Contains(Refusals.HarnessActionUnknown, refusal);
        Assert.Contains("action=frobnicate", refusal);
    }

    [Fact]
    public async Task A_request_missing_what_it_needs_is_refused_rather_than_defaulted()
    {
        // A drivable toggle with no repository would otherwise opt in "" — a row nobody can see and
        // nothing can remove.
        Assert.NotEmpty(await RefusalAsync(Module(), "SET_DRIVABLE", new { drivable = true }));
    }

    [Fact]
    public async Task An_unknown_request_type_is_refused()
    {
        Assert.Contains("NO_ROUTE", await RefusalAsync(Module(), "FROBNICATE"));
    }
}
