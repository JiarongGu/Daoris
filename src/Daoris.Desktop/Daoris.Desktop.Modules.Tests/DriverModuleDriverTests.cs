using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The driver's state and standing choices over the bridge (`DriverModule.Driver.cs`, MOD5): the state,
/// the drivable set, holds, trees, notifications, strikes, the intake and helper agents, trust and *Sync now*.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class DriverModuleDriverTests : DriverModuleBridge
{
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
    /// How the home stands to the account's DAORIS_HOME rides the state as a field (LEFT2), so the page's hint reads
    /// it rather than a sentence: here a home the account names, and one named for this start alone, which D105
    /// respects without a notice.
    /// </summary>
    [Fact]
    public async Task How_the_home_stands_to_the_account_is_a_field_of_the_state()
    {
        async Task<string?> StandingAsync(string? account) =>
            (await AnswerAsync(new DriverModule(Bus, new DriverLoop(
                Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0", account: () => account)), "STATE"))
            .GetProperty("homeAccount").GetString();

        Assert.Equal(HomeAccount.Same, await StandingAsync(Home));
        Assert.Equal(HomeAccount.ThisStart, await StandingAsync(Path.Combine(Home, "another")));
        Assert.Equal(HomeAccount.ThisStart, await StandingAsync(null));
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

    /// <summary>
    /// Every control here is an EDIT to the file the loop re-reads each tick (D46 §6, D50) — so the
    /// file is what gets asserted, not this module's own answer, which would only prove it agrees
    /// with itself.
    /// </summary>
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
        // SESSUX1b: a retry does what the loop's last look says applies, so the look parked the quest first.
        var loop = Loop();
        loop.Look.Record([new Consideration(
            new QuestView("a78553", "game", "engine", "A parked quest", "A body.", "Open"), StartVerdict.Exhausted, "parked")]);
        var module = new DriverModule(Bus, loop);

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
}
