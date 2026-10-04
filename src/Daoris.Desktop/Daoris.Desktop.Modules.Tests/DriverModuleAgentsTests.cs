using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// This machine's agents over the bridge (`DriverModule.Agents.cs`, MOD5): the roster, a harness's
/// actions and their console, and usage.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class DriverModuleAgentsTests : DriverModuleBridge
{
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
    }

    /// <summary>
    /// UX6e (D150 §5.1): an agent's page has *What it may do* only where Daoris hands its agent the rules file at spawn,
    /// which is the adapter's to say: Claude Code's two doors take it, and an agent the rules mean nothing to does not.
    /// </summary>
    [Fact]
    public async Task The_roster_says_which_doors_are_handed_the_rules()
    {
        var rows = (await AnswerAsync(Module(), "HARNESSES")).GetProperty("harnesses").EnumerateArray()
            .ToDictionary(h => h.GetProperty("harness").GetString()!, h => h.GetProperty("takesRules").GetBoolean());

        Assert.True(rows["claude-code"]);
        Assert.True(rows["claude-code-acp"]);
        Assert.False(rows["dsh"]);
        // A text door answers false — the stub is one, held where it chats (ProtocolChatTests); the
        // roster leaves the test doubles out, and every harness it lists carries structure today.
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
    /// UX6e (D150 §5.3): each account and the tool's own sign-in say when they were last read. Nothing has answered for the
    /// stub's account, so it was never read, and the tool's own sign-in is asked only at a press; one agent's *Read again*
    /// asks its accounts and its own sign-in, and each then says when.
    /// </summary>
    [Fact]
    public async Task An_account_says_when_it_was_read_and_one_agents_read_again_dates_its_accounts()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, "stub", "work"));
        File.WriteAllText(DriverConfigPath, """
            { "drivable": [], "holds": [], "cap": 1, "adapter": "stub",
              "commands": { "stub": ["node", "agent.mjs"] } }
            """);
        var module = Module();

        var stub = (await AnswerAsync(module, "HARNESSES")).GetProperty("harnesses").EnumerateArray()
            .Single(h => h.GetProperty("harness").GetString() == "stub");
        Assert.Equal(JsonValueKind.Null, stub.GetProperty("profiles")[0].GetProperty("read").ValueKind);
        Assert.Equal(JsonValueKind.Null, stub.GetProperty("ownRead").ValueKind);

        stub = (await AnswerAsync(module, "HARNESSES", new { refresh = true, agent = "stub" })).GetProperty("harnesses").EnumerateArray()
            .Single(h => h.GetProperty("harness").GetString() == "stub");
        Assert.Equal(JsonValueKind.String, stub.GetProperty("profiles")[0].GetProperty("read").ValueKind);
        Assert.Equal(JsonValueKind.String, stub.GetProperty("ownRead").ValueKind);
    }

    /// <summary>One agent's *Read again* names an agent this machine has accounts for; another is refused in a sentence.</summary>
    [Fact]
    public async Task Reading_an_agent_this_machine_has_no_door_onto_is_refused()
    {
        var refusal = await RefusalAsync(Module(), "HARNESSES", new { refresh = true, agent = "not-an-agent" });

        Assert.Contains("not-an-agent", refusal);
    }

    /// <summary>
    /// The stub's command as a script that logs each status question it is asked by the account's directory, answering
    /// signed out for <c>out</c> and signed in, by name, for any other.
    /// </summary>
    private string Asking()
    {
        var script = Path.Combine(Home, "asking.mjs");
        File.WriteAllText(script, """
            import fs from 'node:fs';
            import path from 'node:path';
            const home = process.env.DAORIS_STUB_CONFIG_DIR;
            const account = home ? path.basename(home) : '(own)';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) {
              fs.appendFileSync(new URL('./asked.log', import.meta.url), account + '\n');
              console.log(account === 'out' ? 'logged-out' : 'logged-in as ' + account + '@example.invalid');
              process.exit(0);
            }
            process.exit(1);
            """);
        foreach (var account in new[] { "out", "work" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, "stub", account));
        }

        File.WriteAllText(DriverConfigPath, $$"""
            { "drivable": [], "holds": [], "cap": 1, "adapter": "stub",
              "commands": { "stub": ["node", {{JsonSerializer.Serialize(script)}}] } }
            """);
        return Path.Combine(Home, "asked.log");
    }

    private static JsonElement Stub(JsonElement roster) =>
        roster.GetProperty("harnesses").EnumerateArray().Single(h => h.GetProperty("harness").GetString() == "stub");

    private static JsonElement Account(JsonElement stub, string name) =>
        stub.GetProperty("profiles").EnumerateArray().Single(p => p.GetProperty("name").GetString() == name);

    /// <summary>
    /// 🔴 ROSTER1 (D150 §5.3): the roster asks no account when it is read, the first read after a start included (a cold
    /// cache), where it asked every named account; an account's own *Read again* asks that account alone and says when; and a
    /// module started afresh, as after a restart, starts from what was read, asking nothing.
    /// </summary>
    [Fact]
    public async Task Reading_the_roster_asks_no_account_and_an_accounts_read_again_asks_that_one_alone()
    {
        var asked = Asking();
        var module = Module();

        await AnswerAsync(module, "HARNESSES");
        await AnswerAsync(module, "HARNESSES");
        Assert.False(File.Exists(asked), "a read of the roster asked an account");

        var stub = Stub(await AnswerAsync(module, "HARNESSES", new { refresh = true, agent = "stub", profile = "out" }));

        Assert.Equal(["out"], File.ReadAllLines(asked));
        Assert.Equal("out", Account(stub, "out").GetProperty("login").GetString());
        Assert.Equal(JsonValueKind.String, Account(stub, "out").GetProperty("read").ValueKind);
        Assert.Equal(JsonValueKind.Null, Account(stub, "work").GetProperty("read").ValueKind);
        Assert.Equal(JsonValueKind.Null, stub.GetProperty("ownRead").ValueKind);

        var restarted = Stub(await AnswerAsync(Module(), "HARNESSES"));
        Assert.Equal(("out", JsonValueKind.String),
            (Account(restarted, "out").GetProperty("login").GetString(), Account(restarted, "out").GetProperty("read").ValueKind));
        Assert.Equal(["out"], File.ReadAllLines(asked));
    }

    /// <summary>An account's *Read again* names an account the agent has; another is refused in a sentence naming the rest.</summary>
    [Fact]
    public async Task Reading_an_account_the_agent_does_not_have_is_refused_naming_the_ones_it_has()
    {
        var asked = Asking();

        var refusal = await RefusalAsync(Module(), "HARNESSES", new { refresh = true, agent = "stub", profile = "typo" });

        Assert.Contains("`typo`", refusal);
        Assert.Contains("out, work", refusal);
        Assert.False(File.Exists(asked));
    }

    /// <summary>
    /// ROSTER1: an account's edit asks no account — a default set or cleared, an account made — where each asked every
    /// account again; a removed account's reading goes with it, so one made later under its name starts never read.
    /// </summary>
    [Fact]
    public async Task An_accounts_edit_asks_no_account_and_a_removal_forgets_its_reading()
    {
        var asked = Asking();
        var module = Module();
        await AnswerAsync(module, "HARNESSES", new { refresh = true, agent = "stub", profile = "work" });

        await AnswerAsync(module, "HARNESS_ACTION", new { harness = "stub", action = "profile-default", profile = "work" });
        await AnswerAsync(module, "HARNESS_ACTION", new { harness = "stub", action = "profile-default", profile = "work", workspace = "aurora" });
        await AnswerAsync(module, "HARNESS_ACTION", new { harness = "stub", action = "profile-add", profile = "spare" });
        await AnswerAsync(module, "HARNESS_ACTION", new { harness = "stub", action = "profile-remove", profile = "work" });
        await AnswerAsync(module, "HARNESS_ACTION", new { harness = "stub", action = "profile-add", profile = "work" });

        Assert.Equal(["work"], File.ReadAllLines(asked));
        var stub = Stub(await AnswerAsync(module, "HARNESSES"));
        Assert.Equal(("unknown", JsonValueKind.Null),
            (Account(stub, "work").GetProperty("login").GetString(), Account(stub, "work").GetProperty("read").ValueKind));
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
        // A sign-in into an account reaches one that is here, never a new folder (ACCT1).
        Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, "claude-code", "work"));
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
        // A sign-in into an account reaches one that is here, never a new folder (ACCT1).
        Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, "claude-code", "work"));
        Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, "claude-code", "home"));
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
}
