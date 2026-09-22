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
    /// (`daoris harness profile default … --workspace`) and not from the screen — D50 in the
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
