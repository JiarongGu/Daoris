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
        // Nobody has hands on Daoris's browser (BRW8).
        Assert.Empty(state.GetProperty("drivingBrowser").EnumerateArray());
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
    /// The files a person may `@` (CONV4d) are found through the session's record, as its review is —
    /// so on a cold start the answer is the same sentence, never an empty list, which would read as a
    /// tree with nothing in it.
    /// </summary>
    [Fact]
    public async Task Asking_what_a_session_tree_holds_before_the_driver_is_up_is_a_sentence()
    {
        var refusal = await RefusalAsync(Module(), "SESSION_FILES", new { id = "s1a2b3c4" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
        Assert.Contains("still coming up", refusal);
        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(Module(), "SESSION_FILES", new { }));
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
    /// A repository's line (WSR2): the screen's half of `daoris driver line`, over the same file. The
    /// names ride as rows, not as an object's keys, so no key policy on the bridge can respell one.
    /// </summary>
    [Fact]
    public async Task Setting_a_line_writes_the_same_file_the_terminal_edits()
    {
        var module = Module();
        await AnswerAsync(module, "SET_LINE", new { workspace = "aurora", branch = "develop" });
        var state = await AnswerAsync(module, "SET_LINE", new { repository = "Engine", branch = "release/2026.09" });

        var config = DriverConfig.Load(DriverConfigPath);
        Assert.Equal("release/2026.09", config.Lines["Engine"]);
        Assert.Equal("develop", config.WorkspaceLines["aurora"]);
        Assert.Equal("Engine", state.GetProperty("lines")[0].GetProperty("repository").GetString());
        Assert.Equal("aurora", state.GetProperty("workspaceLines")[0].GetProperty("workspace").GetString());

        var cleared = await AnswerAsync(module, "SET_LINE", new { repository = "engine" });
        Assert.Equal(0, cleared.GetProperty("lines").GetArrayLength());
    }

    [Fact]
    public async Task A_line_git_would_not_take_or_one_with_no_owner_is_refused_in_a_sentence()
    {
        var bad = await RefusalAsync(Module(), "SET_LINE", new { repository = "engine", branch = "a..b" });
        Assert.Contains("not a branch name git would take", bad);

        var neither = await RefusalAsync(Module(), "SET_LINE", new { branch = "develop" });
        Assert.Contains("a `repository` or a `workspace`", neither);
        Assert.False(File.Exists(DriverConfigPath) && DriverConfig.Load(DriverConfigPath).Lines.Count > 0);
    }

    /// <summary>How work lands (WSR1, D87): the screen's half of `daoris driver landing`, over the same file.</summary>
    [Fact]
    public async Task Setting_a_landing_rule_writes_the_same_file_the_terminal_edits()
    {
        var module = Module();
        await AnswerAsync(module, "SET_LANDING", new { workspace = "aurora", form = "branch", pattern = "feature/{quest}-{slug}" });
        var state = await AnswerAsync(module, "SET_LANDING", new { repository = "engine", form = "merge" });

        var config = DriverConfig.Load(DriverConfigPath);
        Assert.Equal("feature/{quest}-{slug}", config.WorkspaceLandings["aurora"].Pattern);
        Assert.Equal(LandingRule.Merge, config.Landings["engine"]);
        Assert.Equal("branch", state.GetProperty("workspaceLandings")[0].GetProperty("form").GetString());
        Assert.Equal("engine", state.GetProperty("landings")[0].GetProperty("repository").GetString());

        var cleared = await AnswerAsync(module, "SET_LANDING", new { workspace = "aurora" });
        Assert.Equal(0, cleared.GetProperty("workspaceLandings").GetArrayLength());
    }

    [Fact]
    public async Task A_landing_rule_that_could_not_land_work_is_refused_in_the_drivers_words()
    {
        var fixedName = await RefusalAsync(Module(), "SET_LANDING", new { repository = "engine", form = "branch", pattern = "feature/fixed" });
        Assert.Contains("`{quest}` or `{session}`", fixedName);

        var push = await RefusalAsync(Module(), "SET_LANDING", new { repository = "engine", form = "push" });
        Assert.Contains("a plugin's to do", push);
    }

    /// <summary>
    /// WSR4 (D100): a branch rule may name the plugin that pushes it and opens the pull request — one
    /// installed here, switched on, and speaking on `work/land`. Refused in a sentence otherwise, as the
    /// terminal refuses it, and nothing is written.
    /// </summary>
    [Fact]
    public async Task A_branch_rule_names_its_plugin_only_where_one_here_lands_work()
    {
        var missing = await RefusalAsync(Module(), "SET_LANDING",
            new { workspace = "aurora", form = "branch", pattern = "feature/{quest}-{slug}", plugin = "example.github-pull-request" });
        Assert.Contains("not installed", missing);
        Assert.False(File.Exists(DriverConfigPath) && DriverConfig.Load(DriverConfigPath).WorkspaceLandings.Count > 0);

        var folder = Path.Combine(Home, "plugins", "example.github-pull-request");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"),
            """{ "id": "example.github-pull-request", "hooks": { "command": ["node", "${plugin}/land.mjs"], "points": ["work/land"] } }""");
        PluginState.Disable(Home, "example.github-pull-request");
        var off = await RefusalAsync(Module(), "SET_LANDING",
            new { workspace = "aurora", form = "branch", pattern = "feature/{quest}-{slug}", plugin = "example.github-pull-request" });
        Assert.Contains("daoris plugin enable example.github-pull-request", off);

        PluginState.Enable(Home, "example.github-pull-request");
        var state = await AnswerAsync(Module(), "SET_LANDING",
            new { workspace = "aurora", form = "branch", pattern = "feature/{quest}-{slug}", plugin = "example.github-pull-request" });
        Assert.Equal("example.github-pull-request", DriverConfig.Load(DriverConfigPath).WorkspaceLandings["aurora"].Plugin);
        Assert.Equal("example.github-pull-request", state.GetProperty("workspaceLandings")[0].GetProperty("plugin").GetString());

        var merge = await RefusalAsync(Module(), "SET_LANDING", new { repository = "engine", form = "merge", plugin = "example.github-pull-request" });
        Assert.Contains("only a branch rule", merge);
    }

    /// <summary>The tidy rides the rule (D88), and is written only when on.</summary>
    [Fact]
    public async Task A_tidy_landing_rule_writes_the_same_file()
    {
        var state = await AnswerAsync(Module(), "SET_LANDING", new { repository = "engine", form = "merge", tidy = true });

        Assert.True(DriverConfig.Load(DriverConfigPath).Landings["engine"].Tidy);
        Assert.True(state.GetProperty("landings")[0].GetProperty("tidy").GetBoolean());
    }

    /// <summary>The clean-up reads the registry's checkouts and the sessions in use, so before the driver is up it is the cold-start sentence.</summary>
    [Fact]
    public async Task The_clean_up_before_the_driver_is_up_is_a_sentence()
    {
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "SWEEP_PLAN"));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "SWEEP", new { only = new[] { "engine:daoris/s-x" } }));
    }

    /// <summary>A plan or a press reads the session's record, so before the driver is up each is the cold-start sentence.</summary>
    [Fact]
    public async Task Landing_before_the_driver_is_up_is_a_sentence()
    {
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "LANDING", new { id = "s1a2b3c4" }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "LAND_SESSION_TREE", new { id = "s1a2b3c4" }));
    }

    /// <summary>What each repository's line is needs the registry's checkouts, so before the driver is up it is the cold-start sentence.</summary>
    [Fact]
    public async Task Asking_the_lines_before_the_driver_is_up_is_a_sentence()
    {
        var refusal = await RefusalAsync(Module(), "LINES");

        Assert.Contains(Refusals.DriverNotReady, refusal);
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

    /// <summary>Ask Daoris's own agent (HELP1, D89): the screen's half of `daoris driver helper`, apart from the intake's.</summary>
    [Fact]
    public async Task Ask_daoris_runs_on_an_agent_of_its_own_off_until_named()
    {
        var module = Module();
        Assert.Equal("", (await AnswerAsync(module, "STATE")).GetProperty("helperAdapter").GetString());

        await AnswerAsync(module, "SET_INTAKE", new { adapter = "claude-code-acp" });
        var on = await AnswerAsync(module, "SET_HELPER", new { adapter = "codex-acp" });
        Assert.Equal("codex-acp", on.GetProperty("helperAdapter").GetString());
        Assert.Equal("claude-code-acp", on.GetProperty("intakeAdapter").GetString());

        var off = await AnswerAsync(module, "SET_HELPER", new { adapter = (string?)null });
        Assert.Equal("", off.GetProperty("helperAdapter").GetString());
        Assert.Null(DriverConfig.Load(DriverConfigPath).HelperAdapter);
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
    /// 🔴 A session that takes no input — an intake, one turn, whose stdin on the protocol door
    /// carries the driver's own frames (INT4h) — is REFUSED in the driver's words, never answered
    /// false. False means "it ended while you were typing", and a page that read it so would tell the
    /// person something untrue about a session that is running fine. Finishing it is refused too:
    /// closing that stream would end the protocol's turn, not a conversation.
    /// </summary>
    [Theory]
    [InlineData("SESSION_INPUT")]
    [InlineData("END_CHAT")]
    [InlineData("CANCEL_TURN")]
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

    /// <summary>
    /// BRW8: the state names the running sessions handed Daoris's browser, which the strip's chip and
    /// Settings → Browser read to say whose hands are on the page.
    /// </summary>
    [Fact]
    public async Task The_state_names_the_sessions_driving_Daoris_browser()
    {
        var loop = Loop();
        using var driving = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "node",
            ArgumentList = { "-e", "setTimeout(() => {}, 60000)" },
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        using var other = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "node",
            ArgumentList = { "-e", "setTimeout(() => {}, 60000)" },
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        using var handed = loop.Processes.Track("d1", driving, drivesBrowser: true);
        using var notHanded = loop.Processes.Track("d2", other);

        try
        {
            var state = await AnswerAsync(new DriverModule(Bus, loop), "STATE");

            Assert.Equal(["d1"], state.GetProperty("drivingBrowser").EnumerateArray().Select(id => id.GetString()));
            Assert.Equal(2, state.GetProperty("running").GetArrayLength());
        }
        finally
        {
            loop.Processes.Stop("d1");
            loop.Processes.Stop("d2");
        }
    }

    /// <summary>
    /// SESS3: a driven session on the protocol door hears what the person adds — its inbox holds the
    /// words ahead of INT4i's refusal, the queue says it is listening and what waits, and the stop sends
    /// what is held now, withdrawing nothing. A finish is still refused: its stream is the driver's.
    /// </summary>
    [Fact]
    public async Task A_driven_session_that_listens_holds_what_the_person_adds_and_sends_it_on_a_stop()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "node",
            ArgumentList = { "-e", "setTimeout(() => {}, 60000)" },
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        using var tracked = loop.Processes.Track("s1", process, refusesInput: "the session on quest #q1 takes no line in its stream.");
        var inbox = loop.Processes.OpenInbox("s1");
        var stops = 0;
        inbox.Attach(() => { stops++; return Task.CompletedTask; });

        try
        {
            var sent = await AnswerAsync(module, "SESSION_INPUT", new { id = "s1", text = "the level file moved" });
            Assert.True(sent.GetProperty("sent").GetBoolean());

            var queue = await AnswerAsync(module, "SESSION_QUEUE", new { id = "s1" });
            Assert.True(queue.GetProperty("listening").GetBoolean());
            Assert.True(queue.GetProperty("taking").GetBoolean());
            Assert.Equal("the level file moved", Assert.Single(queue.GetProperty("queued").EnumerateArray()).GetProperty("text").GetString());

            var stop = await AnswerAsync(module, "CANCEL_TURN", new { id = "s1" });
            Assert.True(stop.GetProperty("cancelled").GetBoolean());
            Assert.Empty(stop.GetProperty("withdrawn").EnumerateArray());
            Assert.Equal(1, stops);
            Assert.Equal("the level file moved", inbox.TakeOrClose()?.Text);

            Assert.Contains(Refusals.DriverRefused, await RefusalAsync(module, "END_CHAT", new { id = "s1" }));

            // Closed, it hears nothing more — and the page is told it is not listening.
            inbox.TakeOrClose();
            var closed = await AnswerAsync(module, "SESSION_QUEUE", new { id = "s1" });
            Assert.False(closed.GetProperty("listening").GetBoolean());
        }
        finally
        {
            loop.Processes.Stop("s1");
        }
    }

    [Fact]
    public async Task Sending_to_a_session_that_is_not_listening_answers_false()
    {
        var state = await AnswerAsync(Module(), "SESSION_INPUT", new { id = "nothing-here", text = "hello" });

        Assert.False(state.GetProperty("sent").GetBoolean());
    }

    /// <summary>CONV4c: the payload's files read back as the names and bytes the page sent; none is none.</summary>
    [Fact]
    public void A_messages_files_are_read_from_the_payload_as_names_and_bytes()
    {
        using var payload = JsonDocument.Parse("""
            {"id":"s1","text":"look","files":[{"name":"run.log","content":"ZXhpdCAz"},{"name":"shot.png","content":""}]}
            """);
        using var bare = JsonDocument.Parse("""{"id":"s1","text":"look"}""");

        var files = DriverModule.FilesOf(payload.RootElement);

        Assert.Equal(["run.log", "shot.png"], files.Select(file => file.Name));
        Assert.Equal("exit 3", System.Text.Encoding.UTF8.GetString(files[0].Content));
        Assert.Empty(files[1].Content);
        Assert.Empty(DriverModule.FilesOf(bare.RootElement));
    }

    /// <summary>
    /// CONV4c: a message's files arrive as bytes the way a quest's do, base64 in the payload — and bytes
    /// that are not base64 are refused in a sentence, never kept as something else.
    /// </summary>
    [Fact]
    public async Task A_file_that_is_not_base64_is_refused_in_a_sentence()
    {
        var refusal = await RefusalAsync(Module(), "SESSION_INPUT", new
        {
            id = "nothing-here", text = "look", files = new[] { new { name = "a.png", content = "not base64 at all!" } },
        });

        Assert.Contains(Refusals.DriverRefused, refusal);
        Assert.Contains("`a.png` did not arrive as a file's bytes", refusal);
    }

    /// <summary>
    /// CONV4a: stopping a turn nothing here holds stops nothing and withdraws nothing — an answer, as
    /// the page asking a moment late deserves, never an error.
    /// </summary>
    [Fact]
    public async Task Stopping_a_turn_nothing_here_holds_answers_that_nothing_ran()
    {
        var stop = await AnswerAsync(Module(), "CANCEL_TURN", new { id = "nothing-here" });

        Assert.False(stop.GetProperty("cancelled").GetBoolean());
        Assert.Empty(stop.GetProperty("withdrawn").EnumerateArray());
    }

    /// <summary>
    /// CONV4a: what a conversation has waiting is asked for by a page that just opened it, and takes
    /// every change after that live — nothing waiting is an empty list.
    /// </summary>
    [Fact]
    public async Task A_conversation_with_nothing_waiting_answers_an_empty_queue()
    {
        var queue = await AnswerAsync(Module(), "SESSION_QUEUE", new { id = "nothing-here" });

        Assert.Equal("nothing-here", queue.GetProperty("session").GetString());
        Assert.Empty(queue.GetProperty("queued").EnumerateArray());
        Assert.False(queue.GetProperty("taking").GetBoolean());
        // HELP4: nothing is opening either, so nothing waits on a door.
        Assert.False(queue.GetProperty("opening").GetBoolean());
        // RAIL2: no turn ended here, so no last move is claimed — the page keeps the record's.
        Assert.Equal(JsonValueKind.Null, queue.GetProperty("lastTurn").ValueKind);
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
    /// CONSOLE2c: what a session runs beside itself is listed over the bridge, each with the key its
    /// console is tailed by, and a stream opening or ending is news the page hears, naming only the
    /// session, so a missed one costs a list the page asks for anyway.
    /// </summary>
    [Fact]
    public async Task A_sessions_streams_are_listed_and_a_stream_opening_or_ending_is_news()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        loop.Output.Open("s1", new SessionStream("task/t1", SessionStreamKind.Task, "dev server"));
        loop.Output.End("s1", "task/t1", "completed");
        loop.Output.Open("s1", new SessionStream("subagent/a", SessionStreamKind.Subagent, "reader"));

        var listed = await AnswerAsync(module, "SESSION_STREAMS", new { id = "s1" });

        Assert.Equal("s1", listed.GetProperty("session").GetString());
        var streams = listed.GetProperty("streams").EnumerateArray().ToList();
        Assert.Equal(["s1/task/t1", "s1/subagent/a"], streams.Select(s => s.GetProperty("key").GetString()));
        Assert.Equal(("task", "dev server", false, "completed"), (
            streams[0].GetProperty("kind").GetString(), streams[0].GetProperty("name").GetString(),
            streams[0].GetProperty("live").GetBoolean(), streams[0].GetProperty("state").GetString()));
        Assert.True(streams[1].GetProperty("live").GetBoolean());
        Assert.Equal(JsonValueKind.Null, streams[1].GetProperty("state").ValueKind);

        await UntilAsync(() => Raised.Count(m => m.Type == "SESSION_STREAMS") == 3);
        Assert.All(Raised.Where(m => m.Type == "SESSION_STREAMS"),
            m => Assert.Equal("""{"Session":"s1"}""", JsonSerializer.Serialize(m.Payload)));

        var none = await AnswerAsync(module, "SESSION_STREAMS", new { id = "nothing-here" });
        Assert.Empty(none.GetProperty("streams").EnumerateArray());
    }

    /// <summary>
    /// CONSOLE3a: a running task its harness said can be stopped is listed as one, and its tab's stop
    /// reaches the session that runs it, by the task's own id. Only a task of that session, and a
    /// session nothing here runs stops nothing, as an answer.
    /// </summary>
    [Fact]
    public async Task A_stoppable_task_is_listed_so_and_its_stop_reaches_its_session()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        loop.Output.Open("s1", new SessionStream("task/t1", SessionStreamKind.Task, "dev server", CanStop: true));
        loop.Output.Open("s1", new SessionStream("task/t2", SessionStreamKind.Task, "a probe"));
        loop.Output.Open("s1", new SessionStream("subagent/a", SessionStreamKind.Subagent, "reader"));

        var listed = (await AnswerAsync(module, "SESSION_STREAMS", new { id = "s1" })).GetProperty("streams").EnumerateArray().ToList();
        Assert.Equal([true, false, false], listed.Select(s => s.GetProperty("canStop").GetBoolean()));

        Assert.False((await AnswerAsync(module, "STOP_TASK", new { id = "s1", key = "s1/task/t1" })).GetProperty("stopped").GetBoolean());

        var asked = new List<string>();
        using var held = loop.Processes.OpenTaskStops("s1", (task, _) =>
        {
            asked.Add(task);
            return Task.FromResult(true);
        });
        Assert.True((await AnswerAsync(module, "STOP_TASK", new { id = "s1", key = "s1/task/t1" })).GetProperty("stopped").GetBoolean());
        Assert.Equal(["t1"], asked);

        Assert.Contains(Refusals.DriverRefused, await RefusalAsync(module, "STOP_TASK", new { id = "s1", key = "s1/subagent/a" }));
        Assert.Contains(Refusals.DriverRefused, await RefusalAsync(module, "STOP_TASK", new { id = "s1", key = "s2/task/t1" }));
        Assert.Equal(["t1"], asked);
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

    /// <summary>
    /// Ask Daoris (HELP1a, D89) is off until its agent is named, and says where to name one — asked
    /// before the loop is, since no service answer changes it.
    /// </summary>
    [Fact]
    public async Task Ask_Daoris_with_no_agent_named_says_where_to_name_one()
    {
        var refusal = await RefusalAsync(Module(), "START_HELP");

        Assert.Contains(Refusals.DriverRefused, refusal);
        Assert.Contains("Settings → Daoris's own AI", refusal);
        Assert.Contains("daoris driver helper <agent>", refusal);
    }

    /// <summary>
    /// HELP1c: the person's Not now settles a proposal of Ask Daoris's, and a settled one takes no second
    /// press. Listing and applying judge against the registry, so they wait for the loop like the review.
    /// </summary>
    [Fact]
    public async Task Not_now_settles_an_Ask_Daoris_proposal_once_and_listing_waits_for_the_loop()
    {
        var folder = HelpProposals.FolderOf(Path.GetDirectoryName(DriverConfigPath)!);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "p1a2b3c4.json"), """
            { "id": "p1a2b3c4", "proposed": "2026-09-29T10:00:00Z", "by": { "session": "h1" }, "kind": "setting",
              "door": "drive", "target": "engine", "workspace": null, "value": null, "sentence": null,
              "why": "the person asked", "state": "proposed", "note": null }
            """);
        var module = Module();

        var dismissed = await AnswerAsync(module, "HELP_DISMISS", new { id = "p1a2b3c4" });

        Assert.Contains("did not apply `#p1a2b3c4`", dismissed.GetProperty("message").GetString());
        Assert.Equal("dismissed", HelpProposals.Find(Path.GetDirectoryName(DriverConfigPath)!, "p1a2b3c4")!.State);
        Assert.Contains("already dismissed", await RefusalAsync(module, "HELP_DISMISS", new { id = "p1a2b3c4" }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(module, "HELP_PROPOSALS", new { session = "h1" }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(module, "HELP_APPLY", new { id = "p1a2b3c4" }));
    }

    [Fact]
    public async Task Ask_Daoris_asked_for_before_the_loop_is_up_says_so()
    {
        var module = Module();
        await AnswerAsync(module, "SET_HELPER", new { adapter = "claude-code-acp" });

        var refusal = await RefusalAsync(module, "START_HELP");

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
    /// A plugin whose folder something on the machine still holds is not half-removed (REV3 modules
    /// F5). On Windows the folder of a running hook process — its working directory — cannot go, and
    /// the recursive delete took every file it could first, leaving a folder with no manifest: a
    /// plugin stranded, neither there nor gone. Removal moves the folder aside whole, or refuses whole.
    /// </summary>
    [Fact]
    public async Task A_plugin_whose_folder_is_held_is_refused_whole_and_left_intact()
    {
        var folder = Path.Combine(Home, "plugins", "acme.held");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), """{ "id": "acme.held", "harnesses": [] }""");
        File.WriteAllText(Path.Combine(folder, "hooks.mjs"), "// a hook the plugin ships");
        var module = Module();

        string refusal;
        using (File.Open(Path.Combine(folder, "hooks.mjs"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            refusal = await RefusalAsync(module, "PLUGIN_ACTION", new { id = "acme.held", action = "remove" });
        }

        Assert.Contains("PLUGIN_BUSY", refusal);
        Assert.True(File.Exists(Path.Combine(folder, "plugin.json")), "the removal took the manifest and stranded the plugin");
        Assert.True(File.Exists(Path.Combine(folder, "hooks.mjs")));
        Assert.Empty(Directory.GetDirectories(Path.Combine(Home, "plugins"), ".removing-*"));
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

    /// <summary>
    /// 🔴 REV3: one at a time. A second *Sign in* while the first still waited on a browser took the
    /// first's place in the running map — the first could no longer be answered or stopped, and its end
    /// removed the second's entry. The second is refused naming what runs; the first is still answerable;
    /// and once it ends, the slot is free again.
    /// </summary>
    [Fact]
    public async Task A_second_harness_action_while_one_runs_is_refused_and_the_first_stays_answerable()
    {
        var fake = Path.Combine(Home, "fake-claude.mjs");
        File.WriteAllText(fake, """
            const args = process.argv.slice(2).join(' ');
            if (args === 'auth status') { console.log(JSON.stringify({ loggedIn: true })); process.exit(0); }
            if (args === 'auth login') {
              process.stdout.write('Paste code here if prompted >');
              process.stdin.once('data', () => process.exit(0));
              setTimeout(() => process.exit(3), 20000);
            } else { console.log('claude 9.9.9'); }
            """);
        File.WriteAllText(DriverConfigPath, $$"""
            { "drivable": [], "holds": [], "commands": { "claude-code": ["node", {{JsonSerializer.Serialize(fake)}}] } }
            """);
        var module = Module();

        await AnswerAsync(module, "HARNESS_ACTION", new { harness = "claude-code", action = "login", profile = "work" });
        var second = await RefusalAsync(
            module, "HARNESS_ACTION", new { harness = "claude-code", action = "login", profile = "home" });
        Assert.Contains(Refusals.HarnessActionBusy, second);
        Assert.Contains("claude-code:login", second);

        await AnswerAsync(module, "HARNESS_INPUT", new { harness = "claude-code", action = "login", text = "abc-123" });
        await UntilAsync(() => Raised.Any(m => m.Type == "HARNESS_ENDED"));

        // Free again: the next one starts.
        var again = await AnswerAsync(module, "HARNESS_ACTION", new { harness = "claude-code", action = "login", profile = "home" });
        Assert.True(again.GetProperty("started").GetBoolean());
        await AnswerAsync(module, "HARNESS_CANCEL", new { harness = "claude-code", action = "login" });
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
