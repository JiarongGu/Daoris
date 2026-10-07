using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// How an agent's accounts are used, over the bridge (`DriverModule.Accounts.cs`, TOOL4g; D125 §2.4, §3.7, §6; D130 §3.2,
/// §9, §16.6): each scope's list and settings, each account's cool-off, what its agent last said and its learned week, and
/// the edits Settings → Agents makes — the list, how it is used, *Try now*, and a workspace back on this machine's accounts.
/// </summary>
/// <remarks>
/// The fast half (MOD8): every answer here is a file read under the test's home, and every edit a write to
/// <c>harnesses.json</c> or <c>cooling.json</c>; nothing is probed and no process starts. No service runs, so no session is
/// counted and each account's running count is null: absent, never zero (D57).
/// </remarks>
public sealed class DriverModuleAccountsTests : DriverModuleBridge
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private void Accounts(string agent, params string[] names)
    {
        foreach (var name in names) Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, agent, name));
    }

    private void Wiring(string json) => File.WriteAllText(HarnessSettingsPath, json);

    private static JsonElement Agent(JsonElement answer, string agent) =>
        answer.GetProperty("agents").EnumerateArray().Single(each => each.GetProperty("agent").GetString() == agent);

    private static JsonElement Account(JsonElement agent, string name) =>
        agent.GetProperty("accounts").EnumerateArray().Single(each => each.GetProperty("name").GetString() == name);

    private static JsonElement Scope(JsonElement agent, string? workspace) =>
        agent.GetProperty("scopes").EnumerateArray().Single(each => workspace is null
            ? each.GetProperty("workspace").ValueKind == JsonValueKind.Null
            : each.GetProperty("workspace").GetString() == workspace);

    private static string[] Strings(JsonElement array) => [.. array.EnumerateArray().Select(each => each.GetString()!)];

    // ---------------------------------------------------------------- ACCOUNTS

    /// <summary>An agent with no accounts named answers its machine scope empty, today's defaults, and nothing cooling.</summary>
    [Fact]
    public async Task An_agent_with_no_accounts_answers_an_empty_scope_with_todays_defaults()
    {
        var claude = Agent(await AnswerAsync(Module(), "ACCOUNTS"), "claude-code");

        Assert.Empty(claude.GetProperty("accounts").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, claude.GetProperty("own").GetProperty("cooling").ValueKind);
        var machine = Scope(claude, null);
        Assert.Equal(JsonValueKind.Null, machine.GetProperty("default").ValueKind);
        Assert.Empty(machine.GetProperty("list").EnumerateArray());
        Assert.Equal("goal", machine.GetProperty("use").GetProperty("use").GetString());
        Assert.True(machine.GetProperty("use").GetProperty("early").GetBoolean());
        Assert.Equal(90, machine.GetProperty("use").GetProperty("near").GetInt32());
        Assert.Equal(JsonValueKind.Null, machine.GetProperty("problem").ValueKind);
        // Only the machine's scope: no workspace names a default or a list of its own.
        Assert.Single(claude.GetProperty("scopes").EnumerateArray());
    }

    /// <summary>
    /// D125 §2.4 and D130 §5.2: each account's cool-off as the driver kept it, what its agent last said per window with when,
    /// and the weekly reset a limit taught it. Names, times and numbers only — never the agent's words.
    /// </summary>
    [Fact]
    public async Task Each_account_answers_its_cool_off_what_its_agent_last_said_and_its_learned_week()
    {
        Accounts("claude-code", "account-1", "account-2", "account-3");
        var until = Now.AddHours(3).AddMinutes(10);
        AccountCooling.Cool(Home, new CoolingEntry("claude-code", "account-1", until, true, "weekly", Now.AddMinutes(-5), "s1a2b3c4"), Now);
        AccountWindows.Said(Home, "claude-code", "account-2",
            [new WindowReading("session", 0.88, Now.AddHours(2), "clear"), new WindowReading("weekly", 0.14, Now.AddDays(5))],
            Now.AddHours(-3), "s2");
        AccountWindows.Told(Home, "claude-code", "account-3", Now.AddDays(2), Now.AddDays(-5), "s3");

        var claude = Agent(await AnswerAsync(Module(), "ACCOUNTS"), "claude-code");

        var cooling = Account(claude, "account-1").GetProperty("cooling");
        Assert.Equal(until.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm"), cooling.GetProperty("until").GetDateTimeOffset().ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm"));
        Assert.True(cooling.GetProperty("stated").GetBoolean());
        Assert.Equal("weekly", cooling.GetProperty("window").GetString());
        Assert.False(cooling.GetProperty("assumedZone").GetBoolean());
        Assert.False(cooling.GetProperty("notBelieved").GetBoolean());
        Assert.Equal(JsonValueKind.Null, Account(claude, "account-1").GetProperty("said").ValueKind);

        var said = Account(claude, "account-2").GetProperty("said");
        var windows = said.GetProperty("windows").EnumerateArray().ToList();
        Assert.Equal(["session", "weekly"], windows.Select(each => each.GetProperty("window").GetString()));
        Assert.Equal(0.88, windows[0].GetProperty("used").GetDouble(), 3);
        Assert.Equal("clear", windows[0].GetProperty("standing").GetString());
        Assert.Equal(JsonValueKind.Null, windows[1].GetProperty("standing").ValueKind);
        Assert.True(said.GetProperty("seen").GetDateTimeOffset() < Now.AddHours(-2));
        Assert.Equal(JsonValueKind.Null, Account(claude, "account-2").GetProperty("cooling").ValueKind);

        // A week a limit told says no use, so it is no reading; it is the account's week all the same.
        Assert.Equal(JsonValueKind.Null, Account(claude, "account-3").GetProperty("said").ValueKind);
        Assert.True(Account(claude, "account-3").GetProperty("week").GetDateTimeOffset() > Now.AddDays(1));
        // No service is answering here, so no session is counted: unknown, never zero.
        Assert.Equal(JsonValueKind.Null, Account(claude, "account-3").GetProperty("running").ValueKind);
        Assert.DoesNotContain("s1a2b3c4", (await AnswerAsync(Module(), "ACCOUNTS")).GetRawText(), StringComparison.Ordinal);
    }

    /// <summary>D125 §3.7: the tool's own sign-in's cool-off is answered beside the accounts, never as one of them.</summary>
    [Fact]
    public async Task The_own_sign_ins_cool_off_is_answered_beside_the_accounts()
    {
        AccountCooling.Cool(Home, new CoolingEntry("claude-code", null, Now.AddHours(1), false, null, Now, null), Now);

        var claude = Agent(await AnswerAsync(Module(), "ACCOUNTS"), "claude-code");

        var own = claude.GetProperty("own").GetProperty("cooling");
        Assert.False(own.GetProperty("stated").GetBoolean());
        Assert.Empty(claude.GetProperty("accounts").EnumerateArray());
    }

    /// <summary>
    /// D130 §3.1, §6: a scope answers its own default, its list, where it begins, how it is used, and which of its accounts
    /// are near their limit by its own <i>near</i>; a workspace answers a scope of its own only where it names a default or
    /// a list of its own.
    /// </summary>
    [Fact]
    public async Task A_scope_answers_its_list_its_settings_and_which_accounts_are_near_by_its_own_near()
    {
        Accounts("claude-code", "account-1", "account-2", "account-3");
        Wiring("""
            {
              "defaults": { "claude-code": "account-2" },
              "workspaces": { "solo": { "claude-code": "account-3" } },
              "rotation": { "claude-code": ["account-1", "account-2", "account-3"] },
              "rotationUse": { "claude-code": { "use": "order", "keep": "account-3", "near": 80 } },
              "workspaceRotation": { "work": { "claude-code": ["account-2", "account-1"] } }
            }
            """);
        AccountWindows.Said(Home, "claude-code", "account-1", [new WindowReading("session", 0.85, Now.AddHours(2))], Now, "s1");

        var claude = Agent(await AnswerAsync(Module(), "ACCOUNTS"), "claude-code");

        var machine = Scope(claude, null);
        Assert.Equal("account-2", machine.GetProperty("default").GetString());
        Assert.Equal(["account-1", "account-2", "account-3"], Strings(machine.GetProperty("list")));
        Assert.Equal("account-2", machine.GetProperty("begins").GetString());
        Assert.Equal("order", machine.GetProperty("use").GetProperty("use").GetString());
        Assert.Equal("account-3", machine.GetProperty("use").GetProperty("keep").GetString());
        var near = Assert.Single(machine.GetProperty("near").EnumerateArray().ToList());
        Assert.Equal("account-1", near.GetProperty("account").GetString());
        Assert.Equal("session", near.GetProperty("window").GetString());
        Assert.Equal("number", near.GetProperty("by").GetString());

        // Its own list, today's settings: 85% is under its near of 90.
        var work = Scope(claude, "work");
        Assert.Equal(["account-2", "account-1"], Strings(work.GetProperty("list")));
        Assert.Equal("account-2", work.GetProperty("begins").GetString());
        Assert.Equal(90, work.GetProperty("use").GetProperty("near").GetInt32());
        Assert.Empty(work.GetProperty("near").EnumerateArray());

        // A default and no list is a scope of its own: that account alone.
        var solo = Scope(claude, "solo");
        Assert.Equal("account-3", solo.GetProperty("default").GetString());
        Assert.Empty(solo.GetProperty("list").EnumerateArray());
    }

    /// <summary>
    /// TOOL6e (D130 §3–§4): each scope answers which account its next start would take, the step that chose it and the
    /// account it was weighed against, and what holds each other account of the agent's — a cool-off until when, an account
    /// the scope does not use — read from the walk's own pieces, with nothing probed.
    /// </summary>
    [Fact]
    public async Task Each_scope_answers_the_account_its_next_start_takes_why_and_what_holds_the_others()
    {
        Accounts("claude-code", "account-1", "account-2", "account-3");
        Wiring("""
            {
              "workspaces": { "work": { "claude-code": "account-3" } },
              "rotation": { "claude-code": ["account-1", "account-2"] },
              "workspaceRotation": { "work": { "claude-code": ["account-3", "account-1"] } }
            }
            """);
        var until = Now.AddHours(2);
        AccountCooling.Cool(Home, new CoolingEntry("claude-code", "account-3", until, true, "weekly", Now.AddMinutes(-5), "s1"), Now);

        var claude = Agent(await AnswerAsync(Module(), "ACCOUNTS"), "claude-code");

        var machine = Scope(claude, null).GetProperty("next");
        Assert.Equal("account-1", machine.GetProperty("account").GetString());
        Assert.Equal("list", machine.GetProperty("reason").GetString());
        Assert.Equal("account-2", machine.GetProperty("over").GetString());
        Assert.Equal(JsonValueKind.Null, machine.GetProperty("when").ValueKind);
        Assert.Equal(
            ["account-2=ready", "account-3=outside"],
            machine.GetProperty("others").EnumerateArray().Select(each => $"{each.GetProperty("account").GetString()}={each.GetProperty("hold").GetString()}"));

        // Its default cooling: the only other account it uses carries the start, and the cool-off says until when.
        var work = Scope(claude, "work").GetProperty("next");
        Assert.Equal("account-1", work.GetProperty("account").GetString());
        Assert.Equal("onlyReady", work.GetProperty("reason").GetString());
        var others = work.GetProperty("others").EnumerateArray().ToList();
        Assert.Equal(["account-3", "account-2"], others.Select(each => each.GetProperty("account").GetString()));
        Assert.Equal(["cooling", "outside"], others.Select(each => each.GetProperty("hold").GetString()));
        Assert.Equal(
            until.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm"),
            others[0].GetProperty("until").GetDateTimeOffset().ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm"));
        Assert.Equal(JsonValueKind.Null, others[1].GetProperty("until").ValueKind);
    }

    /// <summary>D125 §3.7 as TOOL6e says it: a scope that names no account runs on the tool's own sign-in, and waits out its cool-off.</summary>
    [Fact]
    public async Task A_scope_naming_no_account_answers_the_tool_s_own_sign_in_and_its_wait()
    {
        Accounts("claude-code", "account-1");
        AccountCooling.Cool(Home, new CoolingEntry("claude-code", null, Now.AddMinutes(45), false, null, Now, null), Now);

        var next = Scope(Agent(await AnswerAsync(Module(), "ACCOUNTS"), "claude-code"), null).GetProperty("next");

        Assert.Equal(JsonValueKind.Null, next.GetProperty("account").ValueKind);
        Assert.Equal("waits", next.GetProperty("reason").GetString());
        Assert.True(next.GetProperty("when").GetDateTimeOffset() > Now.AddMinutes(44));
        Assert.Equal(
            [JsonValueKind.Null, JsonValueKind.String],
            next.GetProperty("others").EnumerateArray().Select(each => each.GetProperty("account").ValueKind));
    }

    /// <summary>
    /// TOOL6e: an account whose cool-off ended within the day, which the file still holds, answers when it was offered again,
    /// and so does the tool's own sign-in; one still cooling, or never cooled, answers none.
    /// </summary>
    [Fact]
    public async Task An_account_whose_cool_off_ended_within_the_day_answers_when_it_was_offered_again()
    {
        Accounts("claude-code", "account-1", "account-2", "account-3");
        var ended = Now.AddMinutes(-58);
        AccountCooling.Cool(Home, new CoolingEntry("claude-code", "account-1", ended, true, "weekly", Now.AddHours(-30), "s1"), Now.AddHours(-30));
        AccountCooling.Cool(Home, new CoolingEntry("claude-code", null, Now.AddMinutes(-5), false, null, Now.AddHours(-1), null), Now.AddHours(-1));
        AccountCooling.Cool(Home, new CoolingEntry("claude-code", "account-3", Now.AddHours(1), true, "session", Now.AddHours(-1), "s3"), Now.AddHours(-1));

        var claude = Agent(await AnswerAsync(Module(), "ACCOUNTS"), "claude-code");

        var offered = Account(claude, "account-1");
        Assert.Equal(JsonValueKind.Null, offered.GetProperty("cooling").ValueKind);
        Assert.Equal(
            ended.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm"),
            offered.GetProperty("offered").GetDateTimeOffset().ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm"));
        Assert.Equal(JsonValueKind.Null, Account(claude, "account-2").GetProperty("offered").ValueKind);
        Assert.Equal(JsonValueKind.Null, Account(claude, "account-3").GetProperty("offered").ValueKind);
        Assert.True(claude.GetProperty("own").GetProperty("offered").GetDateTimeOffset() < Now);
        Assert.Equal(JsonValueKind.Null, claude.GetProperty("own").GetProperty("cooling").ValueKind);
    }

    /// <summary>A file edited by hand that breaks the rule is read with the list winning, and the scope names the conflict.</summary>
    [Fact]
    public async Task A_scope_whose_default_is_outside_its_list_names_the_conflict()
    {
        Accounts("claude-code", "account-1", "account-2");
        Wiring("""
            { "defaults": { "claude-code": "account-2" }, "rotation": { "claude-code": ["account-1"] } }
            """);

        var machine = Scope(Agent(await AnswerAsync(Module(), "ACCOUNTS"), "claude-code"), null);

        Assert.Equal("account-1", machine.GetProperty("begins").GetString());
        Assert.Equal("default", machine.GetProperty("problem").GetProperty("kind").GetString());
        Assert.Equal("account-2", machine.GetProperty("problem").GetProperty("account").GetString());
    }

    /// <summary>
    /// D130 §6: switching before the limit reads the agent's own word, which only some agents' sessions carry; the screen
    /// offers the switch where they do, and says why not where they do not. Codex's door carries none, and its own server is
    /// asked instead (CODEXUSE1), so it says too; dsh says nothing either way.
    /// </summary>
    [Fact]
    public async Task Each_agent_says_whether_its_sessions_say_how_near_their_limits_are()
    {
        var answer = await AnswerAsync(Module(), "ACCOUNTS");

        Assert.True(Agent(answer, "claude-code").GetProperty("speaks").GetBoolean());
        Assert.True(Agent(answer, "codex").GetProperty("speaks").GetBoolean());
        Assert.False(Agent(answer, "dsh").GetProperty("speaks").GetBoolean());
    }

    // ---------------------------------------------------------------- ACCOUNT_USE: the list

    /// <summary>D125 §3.1, D130 §3.1: a list written whole, the machine's or a workspace's, in the person's order; none clears it.</summary>
    [Fact]
    public async Task A_list_is_written_whole_for_the_machine_or_a_workspace_and_cleared_by_naming_none()
    {
        Accounts("claude-code", "account-1", "account-2");

        await AnswerAsync(Module(), "ACCOUNT_USE", new { action = "order", harness = "claude-code", accounts = new[] { "account-2", "account-1" } });
        await AnswerAsync(Module(), "ACCOUNT_USE",
            new { action = "order", harness = "claude-code-acp", accounts = new[] { "account-1" }, workspace = "work" });

        var settings = HarnessSettings.Load(HarnessSettingsPath);
        Assert.Equal(["account-2", "account-1"], settings.Rotation["claude-code"]);
        // A door's list is its owner's (AGT7).
        Assert.Equal(["account-1"], settings.WorkspaceRotation["work"]["claude-code"]);

        await AnswerAsync(Module(), "ACCOUNT_USE", new { action = "order", harness = "claude-code", accounts = Array.Empty<string>() });
        Assert.False(HarnessSettings.Load(HarnessSettingsPath).Rotation.ContainsKey("claude-code"));
    }

    /// <summary>
    /// The terminal's refusals, in a code the page translates (REV2): a name that is no account here, one named twice, and a
    /// list that would leave out the scope's default or its kept account, or leave driven work none. Nothing is written.
    /// </summary>
    [Theory]
    [InlineData("""{ }""", new[] { "account-9" }, "ACCOUNT_ORDER_UNKNOWN")]
    [InlineData("""{ }""", new[] { "account-1", "account-1" }, "ACCOUNT_ORDER_TWICE")]
    [InlineData("""{ "defaults": { "claude-code": "account-2" } }""", new[] { "account-1" }, "ACCOUNT_SCOPE_DEFAULT")]
    [InlineData("""{ "rotation": { "claude-code": ["account-1", "account-2"] }, "rotationUse": { "claude-code": { "keep": "account-2" } } }""",
        new[] { "account-1" }, "ACCOUNT_SCOPE_KEEP")]
    [InlineData("""{ "rotation": { "claude-code": ["account-1", "account-2"] }, "rotationUse": { "claude-code": { "keep": "account-2" } } }""",
        new[] { "account-2" }, "ACCOUNT_SCOPE_ALONE")]
    public async Task A_list_the_terminal_refuses_is_refused_with_its_code_and_nothing_written(string wiring, string[] order, string code)
    {
        Accounts("claude-code", "account-1", "account-2");
        Wiring(wiring);
        var before = File.ReadAllText(HarnessSettingsPath);

        var refused = await RefusalAsync(Module(), "ACCOUNT_USE", new { action = "order", harness = "claude-code", accounts = order });

        Assert.StartsWith(code, refused);
        Assert.Equal(before, File.ReadAllText(HarnessSettingsPath));
    }

    /// <summary>
    /// D130 §3.1: <i>This machine's accounts</i> — a workspace's default, list and settings cleared at once, so its starts read the
    /// machine's scope again; the machine's own are untouched.
    /// </summary>
    [Fact]
    public async Task A_workspace_returned_to_this_machines_accounts_names_no_default_list_or_settings_of_its_own()
    {
        Accounts("claude-code", "account-1", "account-2");
        Wiring("""
            {
              "defaults": { "claude-code": "account-1" },
              "workspaces": { "work": { "claude-code": "account-2" } },
              "workspaceRotation": { "work": { "claude-code": ["account-2", "account-1"] } },
              "workspaceRotationUse": { "work": { "claude-code": { "use": "order" } } }
            }
            """);

        await AnswerAsync(Module(), "ACCOUNT_USE", new { action = "inherit", harness = "claude-code", workspace = "work" });

        var settings = HarnessSettings.Load(HarnessSettingsPath);
        Assert.False(settings.Workspaces.ContainsKey("work"));
        Assert.False(settings.WorkspaceRotation.ContainsKey("work"));
        Assert.False(settings.WorkspaceUses.ContainsKey("work"));
        Assert.Equal("account-1", settings.Defaults["claude-code"]);
        Assert.Equal(ChoiceFrom.Machine, settings.ResolveScope("claude-code", "work").From);
    }

    // ---------------------------------------------------------------- ACCOUNT_USE: how a list is used

    /// <summary>D130 §16.6: each choice written as made, a default included; a clear returns the scope to today's defaults.</summary>
    [Fact]
    public async Task How_a_list_is_used_is_written_as_chosen_and_cleared_back_to_todays_defaults()
    {
        Accounts("claude-code", "account-1", "account-2");
        Wiring("""{ "workspaceRotation": { "work": { "claude-code": ["account-1", "account-2"] } } }""");

        await AnswerAsync(Module(), "ACCOUNT_USE", new
        {
            action = "use", harness = "claude-code", workspace = "work", use = "order", keep = "account-2", early = false, near = 85,
        });

        var use = HarnessSettings.Load(HarnessSettingsPath).ResolveScope("claude-code", "work").Use;
        Assert.Equal(new RotationUse("order", "account-2", false, 85), use);

        await AnswerAsync(Module(), "ACCOUNT_USE", new { action = "use", harness = "claude-code", workspace = "work", noKeep = true, use = "goal" });
        Assert.Equal(new RotationUse("goal", null, false, 85), HarnessSettings.Load(HarnessSettingsPath).ResolveScope("claude-code", "work").Use);

        await AnswerAsync(Module(), "ACCOUNT_USE", new { action = "use", harness = "claude-code", workspace = "work", clear = true });
        Assert.False(HarnessSettings.Load(HarnessSettingsPath).WorkspaceUses.ContainsKey("work"));
    }

    /// <summary>
    /// <c>profile use</c>'s refusals: a scope with no list of its own has nothing to use; a kept account is one of the list and
    /// leaves driven work another; a value this build does not know is refused rather than written. Nothing is written.
    /// </summary>
    [Theory]
    [InlineData("""{ }""", "use", "goal", "ACCOUNT_USE_NO_LIST")]
    [InlineData("""{ "rotation": { "claude-code": ["account-1", "account-2"] } }""", "keep", "account-9", "ACCOUNT_SCOPE_KEEP")]
    [InlineData("""{ "rotation": { "claude-code": ["account-1"] } }""", "keep", "account-1", "ACCOUNT_SCOPE_ALONE")]
    [InlineData("""{ "rotation": { "claude-code": ["account-1"] } }""", "use", "spread", "ACCOUNT_USE_VALUE")]
    public async Task A_use_the_terminal_refuses_is_refused_with_its_code_and_nothing_written(string wiring, string field, string value, string code)
    {
        Accounts("claude-code", "account-1", "account-2");
        Wiring(wiring);
        var before = File.ReadAllText(HarnessSettingsPath);
        var payload = new Dictionary<string, object?> { ["action"] = "use", ["harness"] = "claude-code", [field] = value };

        var refused = await RefusalAsync(Module(), "ACCOUNT_USE", payload);

        Assert.StartsWith(code, refused);
        Assert.Equal(before, File.ReadAllText(HarnessSettingsPath));
    }

    /// <summary><i>Near</i> is a whole percent from 50 to 99 (§14); a number outside is refused, naming the range.</summary>
    [Theory]
    [InlineData(49)]
    [InlineData(100)]
    public async Task A_near_outside_its_range_is_refused(int near)
    {
        Accounts("claude-code", "account-1");
        Wiring("""{ "rotation": { "claude-code": ["account-1"] } }""");

        var refused = await RefusalAsync(Module(), "ACCOUNT_USE", new { action = "use", harness = "claude-code", near });

        Assert.StartsWith("ACCOUNT_USE_VALUE", refused);
        Assert.Contains("setting=near", refused);
    }

    // ---------------------------------------------------------------- ACCOUNT_USE: Try now

    /// <summary>
    /// D125 §2.3, §6: <i>Try now</i> ends that account's cool-off and no other, and says whether it was cooling; the tool's own
    /// sign-in is named by <c>own</c>, as the terminal's <c>--own</c>.
    /// </summary>
    [Fact]
    public async Task Try_now_ends_that_accounts_cool_off_and_says_whether_it_was_cooling()
    {
        Accounts("claude-code", "account-1", "account-2");
        AccountCooling.Cool(Home, new CoolingEntry("claude-code", "account-1", Now.AddHours(2), true, null, Now, null), Now);
        AccountCooling.Cool(Home, new CoolingEntry("claude-code", "account-2", Now.AddHours(2), true, null, Now, null), Now);
        AccountCooling.Cool(Home, new CoolingEntry("claude-code", null, Now.AddHours(2), true, null, Now, null), Now);

        var ended = await AnswerAsync(Module(), "ACCOUNT_USE", new { action = "ready", harness = "claude-code-acp", profile = "account-1" });
        var again = await AnswerAsync(Module(), "ACCOUNT_USE", new { action = "ready", harness = "claude-code", profile = "account-1" });
        var own = await AnswerAsync(Module(), "ACCOUNT_USE", new { action = "ready", harness = "claude-code", own = true });

        Assert.True(ended.GetProperty("ended").GetBoolean());
        Assert.False(again.GetProperty("ended").GetBoolean());
        Assert.True(own.GetProperty("ended").GetBoolean());
        var cooling = AccountCooling.Read(Home, DateTimeOffset.UtcNow);
        Assert.Equal(["account-2"], cooling.Select(entry => entry.Account));
    }

    [Fact]
    public async Task Try_now_names_an_account_or_the_tools_own_sign_in()
    {
        var refused = await RefusalAsync(Module(), "ACCOUNT_USE", new { action = "ready", harness = "claude-code" });

        Assert.StartsWith(Refusals.HarnessProfileNeeded, refused);
    }

    [Fact]
    public async Task An_account_action_this_build_does_not_have_is_refused_naming_it()
    {
        var refused = await RefusalAsync(Module(), "ACCOUNT_USE", new { action = "rotate", harness = "claude-code" });

        Assert.StartsWith("ACCOUNT_ACTION_UNKNOWN", refused);
        Assert.Contains("action=rotate", refused);
    }

    // ---------------------------------------------------------------- the screen's default (TOOL6a's fix)

    /// <summary>
    /// D130 §3.1, as the terminal refuses it since TOOL6a: a scope's default is where its starts begin within its list, so the
    /// screen's <i>Make default</i> refuses an account its scope's list does not hold, before anything is written. A
    /// workspace with no list of its own takes any account.
    /// </summary>
    [Fact]
    public async Task The_screens_default_outside_its_scopes_list_is_refused_before_anything_is_written()
    {
        Accounts("claude-code", "account-1", "account-2");
        Wiring("""{ "rotation": { "claude-code": ["account-1"] } }""");
        var before = File.ReadAllText(HarnessSettingsPath);

        var refused = await RefusalAsync(Module(), "HARNESS_ACTION",
            new { action = "profile-default", harness = "claude-code", profile = "account-2" });

        Assert.StartsWith("ACCOUNT_DEFAULT_OUTSIDE_LIST", refused);
        Assert.Contains("account=account-2", refused);
        Assert.Contains("list=account-1", refused);
        Assert.Equal(before, File.ReadAllText(HarnessSettingsPath));
    }

    /// <summary>The rule the route and Ask Daoris's door ask, row by row: <c>rotation-use.test.ts</c>'s default rows.</summary>
    [Theory]
    [InlineData(null, "account-2", false)]
    [InlineData("work", "account-2", false)]
    [InlineData(null, "account-1", false)]
    [InlineData(null, null, false)]
    [InlineData("lab", "account-2", true)]
    public void A_default_is_allowed_only_inside_its_scopes_own_list(string? workspace, string? profile, bool refused)
    {
        var settings = new HarnessSettings
        {
            Rotation = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) { ["claude-code"] = ["account-1", "account-2"] },
            WorkspaceRotation = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.OrdinalIgnoreCase)
            {
                ["lab"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) { ["claude-code"] = ["account-1"] },
            },
        };

        var problem = Record.Exception(() => DriverModule.DefaultAllowed(settings, "claude-code", profile, workspace));

        Assert.Equal(refused, problem is not null);
    }

    // ---------------------------------------------------------------- the screen's Remove (TOOL4e's fix)

    /// <summary>
    /// TOOL4e's note: the screen's Remove leaves no default, list or kept account naming the account, as the terminal's
    /// <c>profile remove</c> does through <see cref="HarnessSettings.WithoutAccount"/>: the next account made takes the first
    /// free name, which may be this one's. The rest of each list keeps its place, and a list left naming none goes.
    /// </summary>
    [Fact]
    public async Task Removing_an_account_takes_it_out_of_every_default_list_and_kept_account()
    {
        Accounts("claude-code", "account-1", "account-2");
        Wiring("""
            { "defaults": { "claude-code": "account-1" },
              "rotation": { "claude-code": ["account-1", "account-2"] }, "rotationUse": { "claude-code": { "keep": "account-1" } },
              "workspaceRotation": { "aurora": { "claude-code": ["account-1"] } } }
            """);

        await AnswerAsync(Module(), "HARNESS_ACTION", new { harness = "claude-code-acp", action = "profile-remove", profile = "account-1" });

        var settings = HarnessSettings.Load(HarnessSettingsPath);
        Assert.False(Directory.Exists(HarnessSettings.ProfileHome(Home, "claude-code", "account-1")));
        Assert.False(settings.Defaults.ContainsKey("claude-code"));
        Assert.Equal(["account-2"], settings.Rotation["claude-code"]);
        Assert.False(settings.WorkspaceRotation.ContainsKey("aurora"));
        Assert.Null(settings.ResolveScope("claude-code", null).Use.Keep);
    }

    // ---------------------------------------------------------------- the cool-off setting

    /// <summary>D125 §2.2, §6: how long an account cools when its agent names no time, the file <c>daoris driver cooloff</c> edits.</summary>
    [Fact]
    public async Task The_default_cool_off_is_answered_with_the_state_and_set_to_whole_minutes_of_at_least_one()
    {
        Assert.Equal(60, (await AnswerAsync(Module(), "STATE")).GetProperty("coolOff").GetInt32());

        var state = await AnswerAsync(Module(), "SET_COOLOFF", new { minutes = 90 });

        Assert.Equal(90, state.GetProperty("coolOff").GetInt32());
        Assert.Equal(90, DriverConfig.Load(DriverConfigPath).CoolOffMinutes);
        Assert.StartsWith(Refusals.DriverRefused, await RefusalAsync(Module(), "SET_COOLOFF", new { minutes = 0 }));
        Assert.Equal(90, DriverConfig.Load(DriverConfigPath).CoolOffMinutes);
    }
}
