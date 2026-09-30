using System.Text.Json;
using Daoris.Desktop.Modules;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// **Credential profiles, from a screen** (DEPLOY3).
/// </summary>
/// <remarks>
/// <para>🔴 <b>There was nowhere to manage credentials.</b> The Machine view could LIST a harness's
/// profiles and log into one, and there was no way to make, un-point or choose one. Those three
/// verbs existed only as `daoris agent profile add|remove|default` (then `daoris harness`).</para>
///
/// <para><b>That is D50 violated in the direction nothing checks.</b> The rule is written "whatever
/// a screen can set, a terminal can", and the converse had no test anywhere — so a capability
/// stranded on a terminal was invisible to every gate.</para>
///
/// <para><b>A sign-in stays the tool's</b> (the one secret Daoris keeps is an API key, D67 §1, whose
/// tests are below). Adding a profile makes a
/// DIRECTORY; what lands inside it is the harness's own login flow's, and nothing here reads it.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
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
    /// LOOK2c: a default's edit answers what sessions there run as now, the fact the terminal's verb prints. The tool's
    /// own row's *use for a workspace*, with a machine default set, runs that workspace as the default, not in the tool's
    /// own home, and the answer says so for the screen to say.
    /// </summary>
    [Fact]
    public async Task A_defaults_edit_answers_what_sessions_there_run_as_now()
    {
        var module = Module();
        await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "claude-code", action = "profile-add", profile = "work" });
        var machine = (await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "claude-code", action = "profile-default", profile = "work" })).GetProperty("default");
        var cleared = (await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "claude-code", action = "profile-default", workspace = "aurora" })).GetProperty("default");

        Assert.Equal(JsonValueKind.Null, machine.GetProperty("workspace").ValueKind);
        Assert.Equal("work", machine.GetProperty("account").GetString());
        Assert.Equal("aurora", cleared.GetProperty("workspace").GetString());
        Assert.Equal("work", cleared.GetProperty("account").GetString());
        Assert.Equal("machine", cleared.GetProperty("from").GetString());
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
    /// 🔴 <b>An account is made by signing in</b> (D66 §3). One press opens a fresh account, runs the
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

    /// <summary>
    /// 🔴 <b>An account that is an API key</b> (AGT3, D67 §1). The key
    /// crosses the bridge once, inward: the answer, the roster and every event name it only by its
    /// last four characters.
    /// </summary>
    [Fact]
    public async Task Adding_an_API_key_makes_an_account_and_nothing_ever_answers_the_key()
    {
        const string key = "sk-ant-api03-module-test-wxyz";
        var module = Module();

        var answer = await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "claude-code", action = "key-add", key });

        Assert.Equal("account-1", answer.GetProperty("profile").GetString());
        Assert.Equal("…wxyz", answer.GetProperty("key").GetString());
        Assert.True(Directory.Exists(ProfileAt("claude-code", "account-1")));
        Assert.Equal(key, HarnessKeys.Of(Home, "claude-code", "account-1"));

        var roster = await AnswerAsync(module, "HARNESSES");
        var claude = roster.GetProperty("harnesses").EnumerateArray()
            .Single(h => h.GetProperty("harness").GetString() == "claude-code");
        Assert.True(claude.GetProperty("takesKey").GetBoolean());
        var row = claude.GetProperty("profiles").EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "account-1");
        Assert.Equal("…wxyz", row.GetProperty("key").GetString());

        Assert.DoesNotContain(key, answer.GetRawText());
        Assert.DoesNotContain(key, roster.GetRawText());
        Assert.DoesNotContain(Raised, m => JsonSerializer.Serialize(m.Payload).Contains(key));
    }

    /// <summary>Removing a key account removes its key with its directory (D66 §3).</summary>
    [Fact]
    public async Task Removing_a_key_account_removes_its_key()
    {
        var module = Module();
        await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "claude-code", action = "key-add", key = "sk-ant-api03-remove-1234" });

        await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "claude-code", action = "profile-remove", profile = "account-1" });

        Assert.Null(HarnessKeys.Of(Home, "claude-code", "account-1"));
        Assert.False(Directory.Exists(ProfileAt("claude-code", "account-1")));
    }

    /// <summary>
    /// 🔴 A door's accounts are its owner's (AGT7): an account action on `claude-code-acp` lands on
    /// `claude-code`'s accounts — its defaults, its directories, its keys — and makes nothing under
    /// the door's own name.
    /// </summary>
    [Fact]
    public async Task An_account_action_on_a_door_lands_on_the_owner_s_accounts()
    {
        var module = Module();
        await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "claude-code-acp", action = "profile-add", profile = "work" });
        await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "claude-code-acp", action = "profile-default", profile = "work" });
        var keyed = await AnswerAsync(module, "HARNESS_ACTION",
            new { harness = "claude-code-acp", action = "key-add", key = "sk-ant-api03-door-5678" });

        Assert.True(Directory.Exists(ProfileAt("claude-code", "work")));
        Assert.Equal("work", HarnessSettings.Load(HarnessSettingsPath).Defaults["claude-code"]);
        Assert.False(HarnessSettings.Load(HarnessSettingsPath).Defaults.ContainsKey("claude-code-acp"));
        Assert.Equal("sk-ant-api03-door-5678",
            HarnessKeys.Of(Home, "claude-code", keyed.GetProperty("profile").GetString()!));
        Assert.False(Directory.Exists(Path.Combine(Home, "harnesses", "claude-code-acp")));
    }

    /// <summary>An agent whose toolchain declares no key variable takes no key, and makes nothing trying.</summary>
    [Fact]
    public async Task An_agent_that_takes_no_key_refuses_one_and_makes_nothing()
    {
        var refusal = await RefusalAsync(Module(), "HARNESS_ACTION",
            new { harness = "dsh", action = "key-add", key = "sk-something-9999" });

        Assert.Contains("takes no API key", refusal);
        Assert.Empty(HarnessSettings.Profiles(Home, "dsh"));
    }

    /// <summary>
    /// 🔴 <b>Update does what it says</b> (USE1a). The roster says which Update each door has, so the
    /// page offers the button only where pressing it does something: a pinned door with a package or a
    /// channel moves its pin, an unpinned door with its own updater runs it, and the rest have none.
    /// </summary>
    [Fact]
    public async Task The_roster_says_which_update_each_door_has()
    {
        new HarnessSettings().WithVersion("claude-code-acp", "0.79.0").Save(HarnessSettingsPath);

        var roster = await AnswerAsync(Module(), "HARNESSES");

        string? UpdateOf(string name)
        {
            var updates = roster.GetProperty("harnesses").EnumerateArray()
                .Single(h => h.GetProperty("harness").GetString() == name)
                .GetProperty("updates");
            return updates.ValueKind == JsonValueKind.Null ? null : updates.GetString();
        }

        Assert.Equal("pin", UpdateOf("claude-code-acp"));
        Assert.Equal("tool", UpdateOf("claude-code"));
        Assert.Null(UpdateOf("codex-acp"));
        Assert.Null(UpdateOf("dsh"));
    }

    /// <summary>
    /// A door with no Update is refused at the bridge as well as offered nothing on the page — the
    /// same sentence it always was, for a caller that sends the action anyway.
    /// </summary>
    [Fact]
    public async Task Update_on_a_door_with_neither_is_refused_as_before()
    {
        var refusal = await RefusalAsync(Module(), "HARNESS_ACTION", new { harness = "dsh", action = "update" });

        Assert.Contains("declares no updater", refusal);
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

    /// <summary>
    /// A profile verb with no profile is refused rather than guessing one — under its own code, so the
    /// page says the name is missing rather than that no such action exists (REV3 modules F9).
    /// </summary>
    [Fact]
    public async Task A_profile_verb_without_a_name_is_refused()
    {
        var refusal = await RefusalAsync(
            Module(), "HARNESS_ACTION", new { harness = "claude-code", action = "profile-add" });

        Assert.Contains(Refusals.HarnessProfileNeeded, refusal);
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
