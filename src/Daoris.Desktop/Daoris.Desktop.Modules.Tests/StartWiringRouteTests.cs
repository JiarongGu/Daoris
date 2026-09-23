using System.Text.Json;
using Daoris.Desktop.Modules;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// <b>The wiring panel's route</b> (MAP1b): for each workspace the page names, what a driven start
/// would run on and where each part came from — the driver's <c>WiringAsync</c>, carried as names.
/// </summary>
public sealed class StartWiringRouteTests : Bridge
{
    private DriverModule Module()
    {
        // A harness with real spawns and no account: signed in exactly when its home holds
        // `credentials.json`, or when a key arrives in its variable.
        var script = Path.Combine(Home, "harness.mjs");
        File.WriteAllText(script, """
            import { existsSync } from 'node:fs';
            const home = process.env.DAORIS_STUB_CONFIG_DIR;
            if (process.argv[2] === '--version') { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv[2] === '--login-state') {
              const signed = process.env.DAORIS_STUB_KEY || (home && existsSync(home + '/credentials.json'));
              console.log(signed ? 'logged-in as someone' : 'logged-out');
              process.exit(0);
            }
            """);
        File.WriteAllText(DriverConfigPath, $$"""
            { "drivable": [], "holds": [], "cap": 1, "adapter": "stub",
              "commands": { "stub": ["node", {{JsonSerializer.Serialize(script)}}] } }
            """);
        return new DriverModule(Bus, new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0"));
    }

    private void SignedIn(string profile)
    {
        var home = HarnessSettings.ProfileHome(Home, "stub", profile);
        Directory.CreateDirectory(home);
        File.WriteAllText(Path.Combine(home, "credentials.json"), "someone");
    }

    [Fact]
    public async Task Each_workspace_named_gets_the_start_it_would_take_and_where_each_part_came_from()
    {
        SignedIn("work");
        SignedIn("office");
        new HarnessSettings().WithDefault("stub", "work").WithWorkspaceDefault("aurora", "stub", "office")
            .Save(HarnessSettingsPath);

        var answer = await AnswerAsync(Module(), "STARTS", new { workspaces = new[] { "default", "aurora", "aurora" } });

        Assert.Equal("stub", answer.GetProperty("adapter").GetString());
        var starts = answer.GetProperty("starts").EnumerateArray().ToList();
        // Once per workspace, in order — a repeated name is one circle.
        Assert.Equal(["aurora", "default"], starts.Select(s => s.GetProperty("workspace").GetString()));

        var aurora = starts[0];
        Assert.Equal("work", aurora.GetProperty("job").GetString());
        Assert.Equal("stub", aurora.GetProperty("owner").GetString());
        Assert.Equal("office", aurora.GetProperty("profile").GetString());
        Assert.Equal("workspace", aurora.GetProperty("profileFrom").GetString());
        Assert.True(aurora.GetProperty("commanded").GetBoolean());
        Assert.Equal(JsonValueKind.Null, aurora.GetProperty("refusal").ValueKind);

        Assert.Equal(("work", "machine"),
            (starts[1].GetProperty("profile").GetString(), starts[1].GetProperty("profileFrom").GetString()));
    }

    /// <summary>A start that would be held says so in the driver's own words.</summary>
    [Fact]
    public async Task A_start_that_would_be_held_carries_the_driver_s_sentence()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, "stub", "stale"));
        new HarnessSettings().WithDefault("stub", "stale").Save(HarnessSettingsPath);

        var answer = await AnswerAsync(Module(), "STARTS", new { workspaces = new[] { "default" } });

        var start = answer.GetProperty("starts")[0];
        Assert.Contains("--profile stale", start.GetProperty("refusal").GetString());
    }

    /// <summary>🔴 The route carries names only: never a home, a binary path or a key (AGT3).</summary>
    [Fact]
    public async Task The_answer_carries_no_key_and_no_machine_path()
    {
        var account = HarnessKeys.Add(Home, "stub", "sk-route-secret-5151");
        new HarnessSettings().WithDefault("stub", account).Save(HarnessSettingsPath);

        var answer = await AnswerAsync(Module(), "STARTS", new { workspaces = new[] { "default" } });
        var raw = answer.GetRawText();

        Assert.Equal(account, answer.GetProperty("starts")[0].GetProperty("profile").GetString());
        Assert.DoesNotContain("sk-route-secret", raw);
        Assert.DoesNotContain(JsonSerializer.Serialize(Home).Trim('"'), raw);
    }

    /// <summary>No workspace named is no rows — the page names the circles it shows.</summary>
    [Fact]
    public async Task No_workspace_named_is_no_rows()
    {
        var answer = await AnswerAsync(Module(), "STARTS");

        Assert.Empty(answer.GetProperty("starts").EnumerateArray());
    }
}
