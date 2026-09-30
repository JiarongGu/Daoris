using System.Text.Json;
using Daoris.Desktop.Modules;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// **An account's own model and effort, from a screen** (AGT6, D98) — the bridge's half of
/// <c>daoris agent settings</c>, over the same file (D50).
/// </summary>
/// <remarks>
/// <para>The roster answers, per account, what the tool's own settings file says, and per tool which
/// models and efforts the tool itself offers; <c>SET_AGENT_SETTINGS</c> changes those keys and no other.
/// A tool whose settings Daoris does not know is offered nothing, and says so.</para>
///
/// <para>🔴 <b>No real agent runs here.</b> The roster probes every door, so every built door's command
/// is a stand-in script: a probe of the machine's own <c>claude</c> would be a real tool asked real
/// questions by a test.</para>
/// </remarks>
public sealed class AgentSettingsModuleTests : Bridge
{
    private DriverModule Module() => new(Bus, new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0"));

    private string Account(string owner, string name, string? settings = null)
    {
        var home = HarnessSettings.ProfileHome(Home, owner, name);
        Directory.CreateDirectory(home);
        if (settings is not null) File.WriteAllText(Path.Combine(home, AgentSettings.FileName), settings);
        return Path.Combine(home, AgentSettings.FileName);
    }

    /// <summary>Every built door runs a stand-in that answers a version and says nothing else.</summary>
    private void StandIns()
    {
        var script = Path.Combine(Home, "stand-in.mjs");
        File.WriteAllText(script, "console.log('stand-in 1.0.0');\n");
        var command = JsonSerializer.Serialize(new[] { "node", script });
        File.WriteAllText(DriverConfigPath, $$"""
            { "drivable": [], "holds": [], "cap": 1, "adapter": "claude-code",
              "commands": { "claude-code": {{command}}, "claude-code-acp": {{command}}, "codex-acp": {{command}}, "dsh": {{command}} } }
            """);
    }

    [Fact]
    public async Task The_roster_offers_each_accounts_model_and_effort_where_the_tool_declares_its_settings()
    {
        StandIns();
        Account("claude-code", "work", """{"model":"opus","effortLevel":"high","modelSettings":{"claude-opus-5":{"effortLevel":"xhigh"}}}""");
        Account("claude-code", "play");

        var rows = (await AnswerAsync(Module(), "HARNESSES")).GetProperty("harnesses").EnumerateArray()
            .ToDictionary(row => row.GetProperty("harness").GetString()!);

        var claude = rows["claude-code"];
        var choices = claude.GetProperty("settingsChoices");
        Assert.Equal(AgentSettings.Models, choices.GetProperty("models").EnumerateArray().Select(m => m.GetString()!));
        Assert.Equal(AgentSettings.Efforts, choices.GetProperty("efforts").EnumerateArray().Select(e => e.GetString()!));

        var profiles = claude.GetProperty("profiles").EnumerateArray().ToDictionary(p => p.GetProperty("name").GetString()!);
        var work = profiles["work"].GetProperty("settings");
        Assert.Equal("opus", work.GetProperty("model").GetString());
        Assert.Equal("high", work.GetProperty("effort").GetString());
        var perModel = Assert.Single(work.GetProperty("perModel").EnumerateArray().ToList());
        Assert.Equal("claude-opus-5", perModel.GetProperty("model").GetString());
        Assert.Equal("xhigh", perModel.GetProperty("effort").GetString());
        // No file is the tool's own defaults — nothing claimed, and nothing made by asking.
        var play = profiles["play"].GetProperty("settings");
        Assert.Equal(JsonValueKind.Null, play.GetProperty("model").ValueKind);
        Assert.Equal(JsonValueKind.Null, play.GetProperty("problem").ValueKind);
        Assert.False(File.Exists(Path.Combine(HarnessSettings.ProfileHome(Home, "claude-code", "play"), AgentSettings.FileName)));

        // The protocol door runs as Claude Code's accounts, so it offers the same choices (AGT7).
        Assert.Equal(JsonValueKind.Object, rows["claude-code-acp"].GetProperty("settingsChoices").ValueKind);
        // A tool whose settings Daoris does not know is offered nothing.
        Assert.Equal(JsonValueKind.Null, rows["dsh"].GetProperty("settingsChoices").ValueKind);
        Assert.Equal(JsonValueKind.Null, rows["codex-acp"].GetProperty("settingsChoices").ValueKind);
    }

    /// <summary>A file the tool keeps in a shape this build cannot read is a sentence on its account, never a blank roster.</summary>
    [Fact]
    public async Task An_unreadable_settings_file_is_a_sentence_on_its_account()
    {
        StandIns();
        Account("claude-code", "work", "not json at all");

        var claude = (await AnswerAsync(Module(), "HARNESSES")).GetProperty("harnesses").EnumerateArray()
            .Single(row => row.GetProperty("harness").GetString() == "claude-code");
        var settings = claude.GetProperty("profiles").EnumerateArray().Single().GetProperty("settings");

        Assert.Contains("could not be read", settings.GetProperty("problem").GetString());
    }

    [Fact]
    public async Task An_accounts_model_and_effort_are_set_over_the_bridge_and_every_other_key_kept()
    {
        var file = Account("claude-code", "work", """{"theme":"dark","model":"opus"}""");

        var answer = await AnswerAsync(Module(), "SET_AGENT_SETTINGS",
            new { harness = "claude-code", profile = "work", model = "sonnet", effort = "xhigh" });

        Assert.Equal("sonnet", answer.GetProperty("model").GetString());
        Assert.Equal("xhigh", answer.GetProperty("effort").GetString());
        Assert.Equal("""{"theme":"dark","model":"sonnet","effortLevel":"xhigh"}""",
            JsonSerializer.Serialize(JsonDocument.Parse(File.ReadAllText(file)).RootElement));
    }

    /// <summary>A key sent as null is cleared, one left out is untouched, and a model's own effort is set by name.</summary>
    [Fact]
    public async Task Null_clears_absent_leaves_alone_and_an_effort_per_model_is_set_by_name()
    {
        var file = Account("claude-code", "work", """{"model":"opus","effortLevel":"high"}""");

        var answer = await AnswerAsync(Module(), "SET_AGENT_SETTINGS", new
        {
            harness = "claude-code", profile = "work", model = (string?)null,
            perModel = new Dictionary<string, string?> { ["claude-opus-5"] = "low" },
        });

        Assert.Equal(JsonValueKind.Null, answer.GetProperty("model").ValueKind);
        Assert.Equal("high", answer.GetProperty("effort").GetString());
        Assert.Equal("""{"effortLevel":"high","modelSettings":{"claude-opus-5":{"effortLevel":"low"}}}""",
            JsonSerializer.Serialize(JsonDocument.Parse(File.ReadAllText(file)).RootElement));
    }

    /// <summary>A door's accounts are its owner's (AGT7): the protocol door's settings are Claude Code's account's file.</summary>
    [Fact]
    public async Task A_door_sets_its_owners_account()
    {
        var file = Account("claude-code", "work");

        await AnswerAsync(Module(), "SET_AGENT_SETTINGS", new { harness = "claude-code-acp", profile = "work", effort = "medium" });

        Assert.Contains("\"effortLevel\": \"medium\"", File.ReadAllText(file));
    }

    [Fact]
    public async Task What_the_tool_would_not_read_and_where_Daoris_does_not_write_are_refused_in_the_drivers_words()
    {
        var file = Account("claude-code", "work", "{\"model\":\"opus\"}\n");
        var module = Module();

        var max = await RefusalAsync(module, "SET_AGENT_SETTINGS", new { harness = "claude-code", profile = "work", effort = "max" });
        Assert.Contains(Refusals.DriverRefused, max);
        Assert.Contains("one session", max);
        Assert.Equal("{\"model\":\"opus\"}\n", File.ReadAllText(file));

        // Never the tool's own configuration home: that home is the tool's.
        Assert.Contains("never touches", await RefusalAsync(module, "SET_AGENT_SETTINGS", new { harness = "claude-code", model = "opus" }));
        // Never an account made by a setting.
        Assert.Contains("no account `play`", await RefusalAsync(module, "SET_AGENT_SETTINGS", new { harness = "claude-code", profile = "play", model = "opus" }));
        Assert.False(Directory.Exists(HarnessSettings.ProfileHome(Home, "claude-code", "play")));
        // A tool whose settings Daoris does not know is offered nothing to set.
        Assert.Contains("does not know", await RefusalAsync(module, "SET_AGENT_SETTINGS", new { harness = "dsh", profile = "work", model = "x" }));
    }
}
