using System.Text.Json;
using Daoris.Desktop.Modules;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// **Credential profiles, from a screen** (DEPLOY3).
/// </summary>
/// <remarks>
/// <para>🔴 <b>Written from the owner looking at the application and saying so</b>: *"there is no
/// credential management location"*. Literally true — the Machine view could LIST a harness's
/// profiles and log into one, and there was no way to make, un-point or choose one. Those three
/// verbs existed only as `daoris harness profile add|remove|default`.</para>
///
/// <para><b>That is D50 violated in the direction nothing checks.</b> The rule is written "whatever
/// a screen can set, a terminal can", and the converse had no test anywhere — so a capability
/// stranded on a terminal was invisible to every gate.</para>
///
/// <para><b>Daoris manages directories and names, never secrets.</b> Adding a profile makes a
/// DIRECTORY; what lands inside it is the harness's own login flow's, and nothing here reads it.</para>
/// </remarks>
public sealed class HarnessProfileTests : Bridge
{
    private DriverLoop Loop() => new(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0");

    private DriverModule Module() => new(Bus, Loop());

    private string ProfileAt(string harness, string profile) =>
        Path.Combine(Home, "harnesses", harness, profile);

    [Fact]
    public async Task Adding_a_profile_makes_a_directory_and_nothing_else()
    {
        await AnswerAsync(Module(), "HARNESS_ACTION",
            new { harness = "claude-code", action = "profile-add", profile = "work" });

        Assert.True(Directory.Exists(ProfileAt("claude-code", "work")));
        // 🔴 And it is the SAME path the CLI verb makes. The two doors share no code — the driver
        // links against no CLI and the CLI has no .NET — so the only thing keeping them one feature
        // is that both compute this, and a test on each side saying so.
        //
        // The first version of this asserted the directory was EMPTY, which is not Daoris's to
        // claim: refreshing the roster asks the harness its login state under that home, and the
        // harness writes its own files there. Asserting somebody else's program leaves no trace is
        // a test that fails for being right.
        Assert.Equal(
            HarnessSettings.ProfileHome(Home, "claude-code", "work"),
            ProfileAt("claude-code", "work"));
    }

    /// <summary>Asking for one that exists is an answer, not a failure — the CLI verb's own rule.</summary>
    [Fact]
    public async Task Adding_the_same_profile_twice_is_an_answer()
    {
        var module = Module();
        await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "claude-code", action = "profile-add", profile = "work" });
        await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "claude-code", action = "profile-add", profile = "work" });

        Assert.True(Directory.Exists(ProfileAt("claude-code", "work")));
    }

    [Fact]
    public async Task Choosing_a_profile_records_it_as_this_machine_s()
    {
        var module = Module();
        await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "claude-code", action = "profile-add", profile = "work" });
        await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "claude-code", action = "profile-default", profile = "work" });

        Assert.Equal("work", HarnessSettings.Load(HarnessSettingsPath).Defaults["claude-code"]);
    }

    /// <summary>
    /// A harness for the remove tests: real spawn, real answers, no account. Signed in exactly when
    /// the profile holds a `credentials.json` — which is what a real login leaves behind — and the
    /// stub adapter's own toolchain asks it with <c>--login-state</c>. Machine-independent: the
    /// first version of these tests asked the machine's own `claude`, whose answer is the machine's.
    /// </summary>
    private DriverModule ModuleWithStubHarness()
    {
        var script = Path.Combine(Home, "harness.mjs");
        File.WriteAllText(script, """
            import { existsSync } from 'node:fs';
            if (process.argv[2] === '--version') { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv[2] === '--login-state') {
              const home = process.env.DAORIS_STUB_CONFIG_DIR;
              console.log(home && existsSync(home + '/credentials.json') ? 'logged-in' : 'logged-out');
              process.exit(0);
            }
            """);
        File.WriteAllText(DriverConfigPath, $$"""
            { "drivable": [], "holds": [], "cap": 1, "adapter": "stub",
              "commands": { "stub": ["node", {{JsonSerializer.Serialize(script)}}] } }
            """);
        return Module();
    }

    /// <summary>
    /// 🔴 <b>Remove un-points and never deletes a credential.</b> A signed-in account stays, every
    /// file with it, and the console says so and where: a button that quietly destroyed a credential
    /// would be the irreversible act this family never does silently.
    /// </summary>
    [Fact]
    public async Task Removing_a_signed_in_profile_stops_pointing_at_it_and_keeps_every_file()
    {
        var module = ModuleWithStubHarness();
        await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "stub", action = "profile-add", profile = "work" });
        File.WriteAllText(Path.Combine(ProfileAt("stub", "work"), "credentials.json"), "{}");
        await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "stub", action = "profile-default", profile = "work" });

        await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "stub", action = "profile-remove", profile = "work" });

        Assert.False(HarnessSettings.Load(HarnessSettingsPath).Defaults.ContainsKey("stub"));
        Assert.True(File.Exists(Path.Combine(ProfileAt("stub", "work"), "credentials.json")));
        Assert.Contains(Raised.Select(Line), line => line.Contains("kept") && line.Contains("signed in"));
    }

    /// <summary>
    /// 🔴 "Deletes nothing" made Forget on a fresh account do nothing anyone could see: the directory
    /// is the account, the roster lists directories, and the harness scaffolds a fresh home the first
    /// time it is asked about it — so a forgotten account stayed on the list, unpointed, forever
    /// (deployed application, 2026-09-23). The harness's own word that the account is signed OUT is
    /// the evidence there is nothing signed-in to destroy, and the directory goes with the forget.
    /// </summary>
    [Fact]
    public async Task Forgetting_a_signed_out_profile_removes_its_directory_scaffolding_and_all()
    {
        var module = ModuleWithStubHarness();
        await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "stub", action = "profile-add", profile = "stale" });
        // What a harness leaves in a home it was merely asked about — settings, never a credential.
        File.WriteAllText(Path.Combine(ProfileAt("stub", "stale"), "settings.json"), "{}");

        await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "stub", action = "profile-remove", profile = "stale" });

        Assert.False(Directory.Exists(ProfileAt("stub", "stale")));
        Assert.Contains(Raised.Select(Line), line => line.Contains("removed") && line.Contains("signed out"));
    }

    /// <summary>An empty directory is the same answer with nothing to ask: it goes.</summary>
    [Fact]
    public async Task Forgetting_a_profile_nobody_ever_wrote_into_removes_the_empty_directory()
    {
        var module = ModuleWithStubHarness();
        await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "stub", action = "profile-add", profile = "fresh" });
        Assert.True(Directory.Exists(ProfileAt("stub", "fresh")));

        await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "stub", action = "profile-remove", profile = "fresh" });

        Assert.False(Directory.Exists(ProfileAt("stub", "fresh")));
    }

    /// <summary>The text of one relayed console line, or empty for any other event.</summary>
    private static string Line(Shenora.Core.Events.EventMessage message)
    {
        if (message.Type != "SESSION_OUTPUT") return string.Empty;
        var json = JsonSerializer.SerializeToElement(message.Payload);
        return string.Join("\n", json.GetProperty("Lines").EnumerateArray().Select(l => l.GetProperty("Text").GetString()));
    }

    /// <summary>A profile verb with no profile is refused rather than guessing one.</summary>
    [Fact]
    public async Task A_profile_verb_without_a_name_is_refused()
    {
        var refusal = await RefusalAsync(
            Module(), "HARNESS_ACTION", new { harness = "claude-code", action = "profile-add" });

        Assert.Contains(Refusals.HarnessActionUnknown, refusal);
    }

    /// <summary>
    /// The refusal for an unknown action names every verb there is — including the three that just
    /// arrived, because a list that goes stale is how a person learns the wrong set.
    /// </summary>
    [Fact]
    public async Task The_unknown_action_refusal_names_the_profile_verbs_too()
    {
        var refusal = await RefusalAsync(
            Module(), "HARNESS_ACTION", new { harness = "claude-code", action = "nope" });

        Assert.Contains("profile-add", refusal);
        Assert.Contains("profile-default", refusal);
    }
}
