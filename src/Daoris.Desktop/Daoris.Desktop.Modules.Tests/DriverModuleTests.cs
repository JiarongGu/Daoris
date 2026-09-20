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

        var roster = await AnswerAsync(Module(), "HARNESSES");
        var stub = roster.GetProperty("harnesses").EnumerateArray().Single(h => h.GetProperty("harness").GetString() == "stub");

        var profile = Assert.Single(stub.GetProperty("profiles").EnumerateArray().ToList());
        Assert.Equal("work", profile.GetProperty("name").GetString());
        Assert.Equal(home, profile.GetProperty("home").GetString());
        // Nothing ran, so nothing can be claimed about the login — unknown, never a guess.
        Assert.Equal("unknown", profile.GetProperty("login").GetString());
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
