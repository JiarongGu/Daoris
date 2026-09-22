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
    /// 🔴 <b>Remove un-points and DELETES NOTHING.</b> The directory holds a credential the harness
    /// put there, and a button that quietly destroyed one would be the irreversible act this family
    /// never does silently. Both doors mean the same thing by the word.
    /// </summary>
    [Fact]
    public async Task Removing_a_profile_stops_pointing_at_it_and_keeps_every_file()
    {
        var module = Module();
        await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "claude-code", action = "profile-add", profile = "work" });
        File.WriteAllText(Path.Combine(ProfileAt("claude-code", "work"), "whatever.json"), "{}");
        await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "claude-code", action = "profile-default", profile = "work" });

        await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "claude-code", action = "profile-remove", profile = "work" });

        Assert.False(HarnessSettings.Load(HarnessSettingsPath).Defaults.ContainsKey("claude-code"));
        Assert.True(File.Exists(Path.Combine(ProfileAt("claude-code", "work"), "whatever.json")));
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
