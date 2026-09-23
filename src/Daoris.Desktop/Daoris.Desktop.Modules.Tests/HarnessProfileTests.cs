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
/// verbs existed only as `daoris agent profile add|remove|default` (then `daoris harness`).</para>
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
    /// A harness with real spawns, real answers and no account. Signed in exactly when the profile
    /// holds a `credentials.json` — what a real login leaves behind — and it says WHO by that file's
    /// contents, the way `claude auth status` names an email. The stub adapter's own toolchain asks
    /// with <c>--login-state</c> and signs in with <c>--login</c>, whose body is each test's own.
    /// Machine-independent: the first version of these tests asked the machine's own `claude`.
    /// </summary>
    private DriverModule ModuleWithStubHarness(string login = "process.exit(1);")
    {
        var script = Path.Combine(Home, "harness.mjs");
        File.WriteAllText(script, $$"""
            import { existsSync, readFileSync, writeFileSync } from 'node:fs';
            const home = process.env.DAORIS_STUB_CONFIG_DIR;
            if (process.argv[2] === '--version') { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv[2] === '--login-state') {
              const signed = home && existsSync(home + '/credentials.json');
              console.log(signed ? 'logged-in as ' + readFileSync(home + '/credentials.json', 'utf8').trim() : 'logged-out');
              process.exit(0);
            }
            if (process.argv[2] === '--login') { {{login}} }
            """);
        File.WriteAllText(DriverConfigPath, $$"""
            { "drivable": [], "holds": [], "cap": 1, "adapter": "stub",
              "commands": { "stub": ["node", {{JsonSerializer.Serialize(script)}}] } }
            """);
        return Module();
    }

    /// <summary>
    /// 🔴 <b>Removing an account removes it</b> (D66 §3, amending SES3's "deletes nothing"). The
    /// owner's report: Forget did not delete the account. The old rule kept a signed-in directory
    /// and un-pointed it, so the account stayed listed and signed in — the leftover the person
    /// pressed the button to be rid of. Now the directory goes, credentials included, and every
    /// default naming it goes with it.
    /// </summary>
    [Fact]
    public async Task Removing_an_account_deletes_it_signed_in_or_not_and_un_points_it_everywhere()
    {
        var module = ModuleWithStubHarness();
        await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "stub", action = "profile-add", profile = "work" });
        File.WriteAllText(Path.Combine(ProfileAt("stub", "work"), "credentials.json"), "someone@example.invalid");
        await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "stub", action = "profile-default", profile = "work" });
        await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "stub", action = "profile-default", profile = "work", workspace = "aurora" });

        var answer = await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "stub", action = "profile-remove", profile = "work" });

        Assert.Equal(0, answer.GetProperty("exitCode").GetInt32());
        Assert.False(Directory.Exists(ProfileAt("stub", "work")));
        var settings = HarnessSettings.Load(HarnessSettingsPath);
        Assert.False(settings.Defaults.ContainsKey("stub"));
        Assert.Null(settings.Resolve("stub", "aurora", null));
        Assert.Contains(Raised.Select(Line), line => line.Contains("removed") && line.Contains("sign-in"));
    }

    /// <summary>
    /// 🔴 <b>An account is made by signing in</b> (D66 §3). The owner: *"we probably should just
    /// allow to login and create account based on login"*. One press opens a fresh account, runs the
    /// tool's own sign-in into it, and keeps it — under a neutral name, because nobody knows whose it
    /// is until the tool says — and the end names who signed in, by the tool's own answer.
    /// </summary>
    [Fact]
    public async Task Signing_in_to_another_account_keeps_it_and_the_end_names_who()
    {
        var module = ModuleWithStubHarness("""
            writeFileSync(home + '/credentials.json', 'someone@example.invalid');
            console.log('signed in');
            process.exit(0);
            """);
        // Someone already has the first number: the new account takes the next free one.
        Directory.CreateDirectory(ProfileAt("stub", "account-1"));

        var answer = await AnswerAsync(module, "HARNESS_ACTION", new { harness = "stub", action = "login-new" });
        Assert.True(answer.GetProperty("started").GetBoolean());
        await UntilAsync(() => Raised.Any(m => m.Type == "HARNESS_ENDED"));

        var ended = JsonSerializer.SerializeToElement(Raised.Single(m => m.Type == "HARNESS_ENDED").Payload);
        Assert.Equal(0, ended.GetProperty("ExitCode").GetInt32());
        Assert.Equal("login-new", ended.GetProperty("Action").GetString());
        Assert.Equal("account-2", ended.GetProperty("Profile").GetString());
        Assert.Equal("someone@example.invalid", ended.GetProperty("Account").GetString());
        Assert.True(ended.GetProperty("Kept").GetBoolean());
        Assert.True(File.Exists(Path.Combine(ProfileAt("stub", "account-2"), "credentials.json")));
    }

    /// <summary>
    /// A sign-in that does not finish leaves nothing behind (D66 §3) — the tool failed, was
    /// stopped, or ended without signing anyone in. The account existed only for the sign-in, so
    /// the list is exactly what it was before the press.
    /// </summary>
    [Theory]
    [InlineData("process.exit(1);")]
    [InlineData("console.log('closed without signing in'); process.exit(0);")]
    public async Task A_sign_in_that_does_not_finish_leaves_nothing_behind(string login)
    {
        var module = ModuleWithStubHarness(login);

        await AnswerAsync(module, "HARNESS_ACTION", new { harness = "stub", action = "login-new" });
        await UntilAsync(() => Raised.Any(m => m.Type == "HARNESS_ENDED"));

        var ended = JsonSerializer.SerializeToElement(Raised.Single(m => m.Type == "HARNESS_ENDED").Payload);
        Assert.False(ended.GetProperty("Kept").GetBoolean());
        Assert.False(Directory.Exists(ProfileAt("stub", "account-1")));
        Assert.Empty(HarnessSettings.Profiles(Home, "stub"));
        Assert.Contains(Raised.Select(Line), line => line.Contains("nothing was kept"));
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
        Assert.Contains("login-new", refusal);
    }
}
