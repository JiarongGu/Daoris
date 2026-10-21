using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// How an agent's accounts are used, over the bridge (`AccountsRoutes.cs`, TOOL4g; D125 §2.4, §3.7, §6; D130 §3.2,
/// §9, §16.6): each scope's list and settings, each account's cool-off, what its agent last said and its learned week, and
/// the edits the Agents place makes — the list, how it is used, *Try now*, and a workspace back on this machine's accounts.
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

    /// <summary>
    /// USAGEPROBE1b: each window of an account's reading carries its projection to now and how it was made, from the same
    /// <see cref="UsageProjection.Of"/> a start reads: the reading alone where Daoris spent nothing since, the reading plus its
    /// own turns by the account's measured ratio, and the reading alone where a turn since cannot be weighed.
    /// </summary>
    [Fact]
    public async Task Each_window_answers_its_projection_and_how_it_was_made()
    {
        Accounts("claude-code", "account-1", "account-2", "account-3");
        var seen = Now.AddMinutes(-5);
        foreach (var account in new[] { "account-1", "account-2", "account-3" })
        {
            AccountWindows.Said(Home, "claude-code", account, [new WindowReading("session", 0.20, Now.AddHours(4))], seen, "s1");
        }

        // account-2: a week's runs of readings rising 3 points on each 300,000 of work, then two turns since its last reading.
        var binding = new TurnBinding("claude-code", "account-2", "claude-code", UsageHistory.PerTurn);
        var tokens = new TurnTokens(100_000, 20_000, 500_000, 40_000);
        var seq = 0;
        for (var day = 3; day >= 1; day--)
        {
            var start = Now.AddDays(-day);
            var used = 0.0;
            for (var hour = 0; hour < 4; hour++)
            {
                var at = start.AddHours(hour);
                Keep(at, start.AddHours(5), used);
                var ended = at.AddMinutes(30);
                UsageHistory.Turned(Home, binding, new TurnSeen("3f9c2a71", ++seq, at.AddMinutes(5), ended, tokens, null, false), ended);
                used += 0.03;
            }

            Keep(start.AddHours(4), start.AddHours(5), used);
        }

        UsageHistory.Turned(Home, binding, new TurnSeen("3f9c2a72", 1, null, Now.AddMinutes(-2), tokens, null, false), Now);

        // account-3: a turn since its reading whose report's meaning is not known, which nothing weighs.
        UsageHistory.Turned(
            Home, new TurnBinding("claude-code", "account-3", "claude-code", UsageHistory.MeaningUnknown),
            new TurnSeen("3f9c2a73", 1, null, Now.AddMinutes(-2), tokens, null, false), Now);

        var claude = Agent(await AnswerAsync(Module(), "ACCOUNTS"), "claude-code");

        var untouched = Account(claude, "account-1").GetProperty("said").GetProperty("windows")[0];
        Assert.Equal("reading", untouched.GetProperty("projectedBy").GetString());
        Assert.Equal(0.20, untouched.GetProperty("projected").GetDouble(), 6);

        var measured = Account(claude, "account-2").GetProperty("said").GetProperty("windows")[0];
        Assert.Equal("measured", measured.GetProperty("projectedBy").GetString());
        Assert.InRange(measured.GetProperty("projected").GetDouble(), 0.2 + 0.03 * 0.95, 0.2 + 0.03 * 1.5);

        var unweighed = Account(claude, "account-3").GetProperty("said").GetProperty("windows")[0];
        Assert.Equal("unweighed", unweighed.GetProperty("projectedBy").GetString());
        Assert.Equal(0.20, unweighed.GetProperty("projected").GetDouble(), 6);

        void Keep(DateTimeOffset at, DateTimeOffset reset, double used) =>
            UsageHistory.Kept(
                Home, new ReadingFrom("claude-code", "account-2", "claude-code", UsageHistory.SourceFrame, AccountReadings.HistoryReader, at),
                [new WindowSeen("five_hour", "session", null, 300, Math.Round(used, 2), 0.01, reset, UsageHistory.ResetSaid)], at);
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
        // Nothing said of its windows: absent, never zero (D57).
        Assert.Equal(JsonValueKind.Null, claude.GetProperty("own").GetProperty("said").ValueKind);
    }

    /// <summary>
    /// CODEXUSE3: what the tool's own sign-in last said of its windows, read at a person's press and kept under its own key, is
    /// answered beside the accounts as an account's reading is, and is none of them: no account row, and near in no scope.
    /// </summary>
    [Fact]
    public async Task The_own_sign_ins_windows_are_answered_beside_the_accounts_and_near_in_no_scope()
    {
        Accounts("codex", "account-1");
        Wiring("""{ "rotation": { "codex": ["account-1"] } }""");
        AccountWindows.Said(Home, "codex", AccountWindows.Own,
            [new WindowReading("session", 0.95, Now.AddHours(2)), new WindowReading("weekly", 0.15, Now.AddDays(4))], Now, null);

        var codex = Agent(await AnswerAsync(Module(), "ACCOUNTS"), "codex");

        var said = codex.GetProperty("own").GetProperty("said");
        var windows = said.GetProperty("windows").EnumerateArray().ToList();
        Assert.Equal(["session", "weekly"], windows.Select(each => each.GetProperty("window").GetString()));
        Assert.Equal(0.95, windows[0].GetProperty("used").GetDouble(), 3);
        Assert.Equal(0.15, windows[1].GetProperty("used").GetDouble(), 3);
        Assert.Equal(["account-1"], codex.GetProperty("accounts").EnumerateArray().Select(each => each.GetProperty("name").GetString()));
        Assert.Equal(JsonValueKind.Null, Account(codex, "account-1").GetProperty("said").ValueKind);
        // Near reads a list's accounts alone: 95% on the own sign-in is near nothing, since no list holds it.
        Assert.Empty(Scope(codex, null).GetProperty("near").EnumerateArray());
    }

    // ---------------------------------------------------------------- the verdicts .NET judges for the page (PUSH1e)

    /// <summary>
    /// PUSH1e (D182 point 6): each reading's window answers when it began, the curve's value on it now as a whole percent and whether
    /// it is over that, measured by the curve of the scope its row is measured by: this machine's where its list holds the account
    /// or no workspace's own list does, else the first workspace's whose own list holds it. The page words these and computes none.
    /// </summary>
    [Fact]
    public async Task Each_window_answers_its_start_its_curve_now_and_whether_it_is_over_by_its_rows_scope()
    {
        Accounts("claude-code", "account-1", "account-2", "account-3");
        Wiring("""
            {
              "rotation": { "claude-code": ["account-1"] },
              "workspaceRotation": { "own": { "claude-code": ["account-2"] } },
              "workspaceRotationUse": { "own": { "claude-code": { "switch": "curve", "curve": { "session": [[0, 80]] } } } }
            }
            """);
        var week = Now.AddDays(4);
        AccountWindows.Said(Home, "claude-code", "account-1", [new WindowReading("weekly", 0.70, week)], Now, "s1");
        AccountWindows.Said(Home, "claude-code", "account-2", [new WindowReading("session", 0.85, Now.AddHours(2))], Now, "s2");
        AccountWindows.Said(
            Home, "claude-code", "account-3",
            [new WindowReading("session", 0.85, Now.AddHours(2)), new WindowReading("60-minute", 0.99, Now.AddMinutes(30)), new WindowReading("weekly", null, week, "warned")],
            Now, "s3");

        var claude = Agent(await AnswerAsync(Module(), "ACCOUNTS"), "claude-code");

        JsonElement Window(string account, string window) =>
            Account(claude, account).GetProperty("said").GetProperty("windows").EnumerateArray().Single(each => each.GetProperty("window").GetString() == window);

        // Three sevenths of the week gone: the smart week stands at 63%, and 70% is over it.
        var weekly = Window("account-1", "weekly");
        Assert.Equal((63, true), (weekly.GetProperty("curve").GetInt32(), weekly.GetProperty("over").GetBoolean()));
        Assert.Equal(week.AddDays(-7).ToUnixTimeSeconds(), weekly.GetProperty("from").GetDateTimeOffset().ToUnixTimeSeconds());
        // Measured by its workspace's own curve, the only list holding it: 80%, which 85% is over; this machine's would say 90.
        var own = Window("account-2", "session");
        Assert.Equal((80, true), (own.GetProperty("curve").GetInt32(), own.GetProperty("over").GetBoolean()));
        // In no list: this machine's smart curve, 90%, which 85% is under.
        var none = Window("account-3", "session");
        Assert.Equal((90, false), (none.GetProperty("curve").GetInt32(), none.GetProperty("over").GetBoolean()));
        // A window this build has no length for has no start and no curve; one with no number is not measured.
        var odd = Window("account-3", "60-minute");
        Assert.Equal(
            (JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null),
            (odd.GetProperty("from").ValueKind, odd.GetProperty("curve").ValueKind, odd.GetProperty("over").ValueKind));
        var unread = Window("account-3", "weekly");
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null), (unread.GetProperty("curve").ValueKind, unread.GetProperty("over").ValueKind));
        Assert.Equal(JsonValueKind.String, unread.GetProperty("from").ValueKind);
    }

    /// <summary>
    /// PUSH1e: each scope answers the curve in effect on each window it names (the person's points under <c>curve</c> where set,
    /// else Daoris's), each of its list's accounts' windows measured by it, and each account of its list whose tightest window is
    /// over it, with the curve's value there: what the switch's charts draw and the said line's clause says.
    /// </summary>
    [Fact]
    public async Task Each_scope_answers_its_curves_its_accounts_measured_by_them_and_which_are_over()
    {
        Accounts("claude-code", "account-1", "account-2");
        Wiring("""
            {
              "rotation": { "claude-code": ["account-1", "account-2"] },
              "workspaceRotation": { "own": { "claude-code": ["account-1"] } },
              "workspaceRotationUse": { "own": { "claude-code": { "switch": "curve", "curve": { "session": [[0, 50]] } } } }
            }
            """);
        AccountWindows.Said(
            Home, "claude-code", "account-1",
            [new WindowReading("session", 0.6, Now.AddHours(2)), new WindowReading("weekly", 0.1, Now.AddDays(4))], Now, "s1");
        AccountWindows.Said(Home, "claude-code", "account-2", [new WindowReading("session", 0.95, Now.AddHours(2))], Now, "s2");

        var claude = Agent(await AnswerAsync(Module(), "ACCOUNTS"), "claude-code");

        static string Curves(JsonElement scope) => scope.GetProperty("curves").GetRawText();
        static string[] Measured(JsonElement scope) =>
        [
            .. scope.GetProperty("measured").EnumerateArray().Select(each =>
                $"{each.GetProperty("account").GetString()} {each.GetProperty("window").GetString()} {each.GetProperty("curve").GetInt32()} {each.GetProperty("over").GetBoolean()}"),
        ];
        static string[] Over(JsonElement scope) =>
        [
            .. scope.GetProperty("overCurve").EnumerateArray().Select(each =>
                $"{each.GetProperty("account").GetString()} {each.GetProperty("window").GetString()} {each.GetProperty("curve").GetInt32()}"),
        ];

        var machine = Scope(claude, null);
        Assert.Equal("""{"session":[[0,90]],"weekly":[[0,20],[75,95],[100,100]]}""", Curves(machine));
        Assert.Equal(["account-1 session 90 False", "account-1 weekly 63 False", "account-2 session 90 True"], Measured(machine));
        Assert.Equal(["account-2 session 90"], Over(machine));

        var own = Scope(claude, "own");
        Assert.Equal("""{"session":[[0,50]],"weekly":[[0,20],[75,95],[100,100]]}""", Curves(own));
        Assert.Equal(["account-1 session 50 True", "account-1 weekly 63 False"], Measured(own));
        Assert.Equal(["account-1 session 50"], Over(own));
    }

    /// <summary>
    /// SWITCH1e (D165 point 4, the switch design §4): each scope answers the accounts of its list a running session could move to,
    /// as <see cref="SwitchDecision"/> moves: ready, and at least the margin under the scope's curve on every window it said. Over
    /// its curve, within the margin, cooling or saying no number is no room; it is judged under <c>off</c> too, which the page reads.
    /// </summary>
    [Fact]
    public async Task Each_scope_answers_the_accounts_a_running_session_could_move_to()
    {
        Accounts("claude-code", "account-1", "account-2", "account-3", "account-4", "account-5");
        Wiring("""
            {
              "rotation": { "claude-code": ["account-1", "account-2", "account-3", "account-4", "account-5"] },
              "workspaceRotation": { "off": { "claude-code": ["account-3", "account-2"] } },
              "workspaceRotationUse": { "off": { "claude-code": { "switch": "off" } } }
            }
            """);
        // Four days to its reset, the smart week stands at 63%; the smart session at 90%.
        AccountWindows.Said(Home, "claude-code", "account-1", [new WindowReading("session", 0.95, Now.AddHours(2))], Now, "s1");
        AccountWindows.Said(Home, "claude-code", "account-2", [new WindowReading("weekly", 0.10, Now.AddDays(4))], Now, "s2");
        AccountWindows.Said(Home, "claude-code", "account-3", [new WindowReading("session", 0.85, Now.AddHours(2))], Now, "s3");
        AccountWindows.Said(Home, "claude-code", "account-4", [new WindowReading("weekly", 0.10, Now.AddDays(4))], Now, "s4");
        AccountCooling.Cool(Home, new CoolingEntry("claude-code", "account-4", Now.AddHours(3), true, "weekly", Now.AddMinutes(-5), "s4"), Now);

        var claude = Agent(await AnswerAsync(Module(), "ACCOUNTS"), "claude-code");

        Assert.Equal(["account-2"], Strings(Scope(claude, null).GetProperty("room")));
        Assert.Equal(["account-2"], Strings(Scope(claude, "off").GetProperty("room")));
    }

    /// <summary>
    /// The page's verdicts are .NET's on the shared table (PUSH1e): for every <c>tightest</c> row of the CLI's
    /// <c>usage-curves.json</c>, a scope with the row's switch and curve and an account saying the row's windows answers each
    /// measured window's curve as the row's value in whole points, and the account among those over the curve exactly where the
    /// row says its line has the clause, with the curve's value the clause names.
    /// </summary>
    [Fact]
    public void The_scopes_verdicts_are_the_shared_tables_rows()
    {
        var table = JsonNode.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Cli", "test", "fixtures", "usage-curves.json")))!;
        var now = DateTimeOffset.Parse(table["now"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture);
        var loop = Loop();
        var rows = table["tightest"]!.AsArray();
        Assert.True(rows.Count >= 10);
        foreach (var row in rows)
        {
            var named = row![0]!.GetValue<string>();
            var account = $"a{rows.IndexOf(row)}";
            Accounts("claude-code", account);
            var use = new JsonObject { ["switch"] = row[1]!.GetValue<string>() };
            if (row[2] is { } curve) use["curve"] = curve.DeepClone();
            Wiring(new JsonObject
            {
                ["rotation"] = new JsonObject { ["claude-code"] = new JsonArray(account) },
                ["rotationUse"] = new JsonObject { ["claude-code"] = use },
            }.ToJsonString());
            AccountWindows.Said(
                Home, "claude-code", account,
                [.. row[3]!.AsArray().Select(window => new WindowReading(
                    window![0]!.GetValue<string>(), window[1]?.GetValue<double>(),
                    DateTimeOffset.Parse(window[2]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture),
                    window[1] is null ? "clear" : null))],
                now.AddMinutes(-1), "s1");

            var answer = JsonSerializer.SerializeToElement(AccountsRoutes.AccountsAnswer(loop.Harnesses, null, now), KeepNulls);
            var scope = Scope(Agent(answer, "claude-code"), null);
            var over = scope.GetProperty("overCurve").EnumerateArray().Where(each => each.GetProperty("account").GetString() == account).ToList();
            if (row[5] is null)
            {
                Assert.True(over.Count == 0, named);
            }
            else
            {
                var tightest = row[4]!;
                Assert.True(over.Count == 1, named);
                Assert.Equal(tightest["window"]!.GetValue<string>(), over[0].GetProperty("window").GetString());
                Assert.Equal(UsageCurves.Points(tightest["curve"]!.GetValue<double>()), over[0].GetProperty("curve").GetInt32());
                Assert.Contains($"{over[0].GetProperty("curve").GetInt32()}%", row[5]!.GetValue<string>(), StringComparison.Ordinal);
            }

            if (row[4] is { } least)
            {
                var measured = scope.GetProperty("measured").EnumerateArray()
                    .Single(each => each.GetProperty("account").GetString() == account && each.GetProperty("window").GetString() == least["window"]!.GetValue<string>());
                Assert.Equal(UsageCurves.Points(least["curve"]!.GetValue<double>()), measured.GetProperty("curve").GetInt32());
                Assert.Equal(least["room"]!.GetValue<double>() < 0, measured.GetProperty("over").GetBoolean());
            }
            else
            {
                Assert.True(scope.GetProperty("measured").EnumerateArray().All(each => each.GetProperty("account").GetString() != account), named);
            }
        }
    }

    /// <summary>The wire's options with every null written, as the bridge answers the route.</summary>
    private static readonly JsonSerializerOptions KeepNulls = new(IpcJson.Options)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
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
    /// USAGEREAD3a (the account-switch design §1.3): Claude Code's pace warning is kept as <c>warned</c> and passed through on
    /// the account's reading, and never makes it near alone; a <c>near</c> written before is read as <c>warned</c>, since the
    /// warning alone ever wrote it. Near is by a limit reached, credits or the number, so <c>by</c> never names a word.
    /// </summary>
    [Fact]
    public async Task The_agent_s_pace_warning_is_passed_as_warned_and_is_never_near_alone()
    {
        Accounts("claude-code", "account-1", "account-2", "account-3");
        Wiring("""{ "rotation": { "claude-code": ["account-1", "account-2", "account-3"] } }""");
        // Two sevenths of each week gone, its smart curve at 49% (SWITCH1b-ii): at 30%, warned; at 40%, warned as the file held
        // it before (`near`); at 99%, warned and near, over its curve.
        static string Stamp(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
        System.Text.Json.Nodes.JsonObject Week(double used, string standing) => new()
        {
            ["weekly"] = new System.Text.Json.Nodes.JsonObject
            {
                ["reset"] = Stamp(Now.AddDays(5)), ["used"] = used, ["standing"] = standing, ["seen"] = Stamp(Now),
            },
        };
        System.IO.File.WriteAllText(AccountWindows.PathOf(Home), new System.Text.Json.Nodes.JsonObject
        {
            ["claude-code"] = new System.Text.Json.Nodes.JsonObject
            {
                ["account-1"] = Week(0.30, "warned"), ["account-2"] = Week(0.40, "near"), ["account-3"] = Week(0.99, "warned"),
            },
        }.ToJsonString());

        var claude = Agent(await AnswerAsync(Module(), "ACCOUNTS"), "claude-code");

        string? Standing(string account) =>
            Account(claude, account).GetProperty("said").GetProperty("windows").EnumerateArray().Single().GetProperty("standing").GetString();
        Assert.Equal(["warned", "warned", "warned"], new[] { "account-1", "account-2", "account-3" }.Select(Standing));
        var near = Assert.Single(Scope(claude, null).GetProperty("near").EnumerateArray().ToList());
        Assert.Equal(("account-3", "weekly", "number"), (near.GetProperty("account").GetString(), near.GetProperty("window").GetString(), near.GetProperty("by").GetString()));
    }

    /// <summary>
    /// SWITCH1b-ii (D165 point 2, the switch design §3): a scope's near list is the walk's, its accounts over its curve now, so
    /// the page's near pill names the accounts a start passes: the smart curve on a week, a person's on a session, and under
    /// <c>off</c> the smart one, which a start still passes an account by.
    /// </summary>
    [Fact]
    public async Task A_scope_s_near_list_is_its_accounts_over_its_curve_as_the_walk_passes_them()
    {
        Accounts("claude-code", "account-1", "account-2", "account-3");
        Wiring("""
            {
              "rotation": { "claude-code": ["account-1", "account-2", "account-3"] },
              "workspaceRotation": {
                "own": { "claude-code": ["account-2", "account-3"] },
                "off": { "claude-code": ["account-1", "account-3"] }
              },
              "workspaceRotationUse": {
                "own": { "claude-code": { "switch": "curve", "curve": { "session": [[0, 80]] } } },
                "off": { "claude-code": { "switch": "off" } }
              }
            }
            """);
        // Four days to its reset, three sevenths of the week gone: the smart curve stands at 63% of it.
        AccountWindows.Said(Home, "claude-code", "account-1", [new WindowReading("weekly", 0.70, Now.AddDays(4))], Now, "s1");
        AccountWindows.Said(Home, "claude-code", "account-2", [new WindowReading("session", 0.85, Now.AddHours(2))], Now, "s2");
        AccountWindows.Said(Home, "claude-code", "account-3", [new WindowReading("weekly", 0.50, Now.AddDays(4))], Now, "s3");

        var claude = Agent(await AnswerAsync(Module(), "ACCOUNTS"), "claude-code");

        static string[] Near(JsonElement scope) =>
            [.. scope.GetProperty("near").EnumerateArray().Select(each => $"{each.GetProperty("account").GetString()} {each.GetProperty("window").GetString()} {each.GetProperty("by").GetString()}")];
        Assert.Equal(["account-1 weekly number"], Near(Scope(claude, null)));
        Assert.Equal(["account-2 session number"], Near(Scope(claude, "own")));
        Assert.Equal(["account-1 weekly number"], Near(Scope(claude, "off")));
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
        Assert.All(["window", "unused", "seen"], name => Assert.Equal(JsonValueKind.Null, machine.GetProperty(name).ValueKind));
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

    /// <summary>
    /// NEXTLAPSE1 (D165's USAGERESET1 note): a next start ranked by what would lapse carries, beside its reset in <c>when</c>,
    /// the window that won, the whole percent of it unused and when that reading was made, as the start's line says them.
    /// </summary>
    [Fact]
    public async Task A_next_start_ranked_by_what_would_lapse_carries_its_window_its_unused_share_and_its_reading_s_moment()
    {
        Accounts("claude-code", "account-1", "account-2");
        Wiring("""{ "rotation": { "claude-code": ["account-1", "account-2"] } }""");
        // account-1's week resets in four days; account-2's in ten hours, with 60% of it unused, read an hour ago.
        var seen = Now.AddHours(-1);
        AccountWindows.Said(Home, "claude-code", "account-1", [new WindowReading("weekly", 0.30, Now.AddDays(4))], Now, "s1");
        AccountWindows.Said(Home, "claude-code", "account-2", [new WindowReading("weekly", 0.40, Now.AddHours(10))], seen, "s2");

        var next = Scope(Agent(await AnswerAsync(Module(), "ACCOUNTS"), "claude-code"), null).GetProperty("next");

        Assert.Equal("account-2", next.GetProperty("account").GetString());
        Assert.Equal("lapsing", next.GetProperty("reason").GetString());
        Assert.Equal("account-1", next.GetProperty("over").GetString());
        Assert.Equal(Now.AddHours(10).ToUnixTimeSeconds(), next.GetProperty("when").GetDateTimeOffset().ToUnixTimeSeconds());
        Assert.Equal("weekly", next.GetProperty("window").GetString());
        Assert.Equal(60, next.GetProperty("unused").GetInt32());
        Assert.Equal(seen.ToUnixTimeSeconds(), next.GetProperty("seen").GetDateTimeOffset().ToUnixTimeSeconds());
    }

    /// <summary>
    /// OWNSPACE1a (D175 point 2, amending D125 §3.7): a scope that names no account answers the agent's Daoris account, never the
    /// tool's own sign-in, whose cool-off holds nothing; with no Daoris account, the next start waits for a sign-in.
    /// </summary>
    [Fact]
    public async Task A_scope_naming_no_account_answers_the_agent_s_Daoris_account_and_with_none_a_wait()
    {
        AccountCooling.Cool(Home, new CoolingEntry("claude-code", null, Now.AddMinutes(45), false, null, Now, null), Now);
        var none = Scope(Agent(await AnswerAsync(Module(), "ACCOUNTS"), "claude-code"), null).GetProperty("next");

        Assert.Equal(JsonValueKind.Null, none.GetProperty("account").ValueKind);
        Assert.Equal("noAccount", none.GetProperty("reason").GetString());
        Assert.Empty(none.GetProperty("others").EnumerateArray());

        Accounts("claude-code", "account-1");
        var next = Scope(Agent(await AnswerAsync(Module(), "ACCOUNTS"), "claude-code"), null).GetProperty("next");

        Assert.Equal("account-1", next.GetProperty("account").GetString());
        Assert.Equal("named", next.GetProperty("reason").GetString());
        Assert.Empty(next.GetProperty("others").EnumerateArray());
    }

    /// <summary>
    /// OWNSPACE1b-A (D175 settlement 5): a scope naming nothing says <c>inherited</c>, since the agent's Daoris accounts are its
    /// list, with one account or none; a scope that names its own account does not.
    /// </summary>
    [Fact]
    public async Task A_scope_naming_nothing_answers_that_Daoris_accounts_are_inherited()
    {
        var bare = Scope(Agent(await AnswerAsync(Module(), "ACCOUNTS"), "claude-code"), null);
        Assert.True(bare.GetProperty("inherited").GetBoolean());

        Accounts("claude-code", "account-1", "account-2");
        Wiring("""{ "workspaces": { "work": { "claude-code": "account-2" } } }""");
        var claude = Agent(await AnswerAsync(Module(), "ACCOUNTS"), "claude-code");

        Assert.True(Scope(claude, null).GetProperty("inherited").GetBoolean());
        Assert.False(Scope(claude, "work").GetProperty("inherited").GetBoolean());
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

    /// <summary>
    /// SWITCH1d (D165 point 1; the switch design §5): each window answers where its reading came from, <c>read</c> from the
    /// agent's own answer or <c>said</c> by a session's frame, so the screen says which; each scope answers its switch and its
    /// curve as written, the smart switch and no curve where nothing is set, beside the old names an older page reads; and each
    /// agent says whether its accounts' use is read by asking it, which the screen's <i>Read now</i> needs.
    /// </summary>
    [Fact]
    public async Task Each_window_answers_its_source_and_each_scope_its_switch_and_curve()
    {
        Accounts("claude-code", "account-1", "account-2");
        Wiring("""
            {
              "rotation": { "claude-code": ["account-1", "account-2"] },
              "workspaceRotation": { "own": { "claude-code": ["account-2"] } },
              "workspaceRotationUse": {
                "own": { "claude-code": { "switch": "curve", "curve": { "weekly": [[0, 30], [50, 70], [100, 100]] } } }
              }
            }
            """);
        AccountWindows.Said(
            Home, "claude-code", "account-1", [new WindowReading("session", 0.4, Now.AddHours(2))], Now, null, source: AccountWindows.SourceRead);
        AccountWindows.Said(Home, "claude-code", "account-2", [new WindowReading("weekly", 0.2, Now.AddDays(4))], Now, "s2");

        var answer = await AnswerAsync(Module(), "ACCOUNTS");
        var claude = Agent(answer, "claude-code");

        string? Source(string account) =>
            Account(claude, account).GetProperty("said").GetProperty("windows").EnumerateArray().Single().GetProperty("source").GetString();
        Assert.Equal("read", Source("account-1"));
        Assert.Equal("said", Source("account-2"));

        var machine = Scope(claude, null).GetProperty("use");
        Assert.Equal("smart", machine.GetProperty("switch").GetString());
        Assert.Empty(machine.GetProperty("curve").EnumerateObject());
        var own = Scope(claude, "own").GetProperty("use");
        Assert.Equal("curve", own.GetProperty("switch").GetString());
        Assert.Equal("[[0,30],[50,70],[100,100]]", own.GetProperty("curve").GetProperty("weekly").GetRawText());
        Assert.False(own.GetProperty("curve").TryGetProperty("session", out _));
        // The old names, still answered as derived for a page that reads them.
        Assert.True(own.GetProperty("early").GetBoolean());
        Assert.Equal(90, own.GetProperty("near").GetInt32());

        Assert.True(claude.GetProperty("reads").GetBoolean());
        Assert.True(Agent(answer, "codex").GetProperty("reads").GetBoolean());
        Assert.False(Agent(answer, "dsh").GetProperty("reads").GetBoolean());
    }

    // ---------------------------------------------------------------- ACCOUNTS_READ: Read now

    /// <summary>
    /// SWITCH1d (the switch design §1.2): the screen's <i>Read now</i>, and its read as it opens, ask each account's agent its use
    /// only where the reading is older than its question's floor, so an account read within it is asked nothing and keeps its
    /// reading. Answered with the door it was asked through.
    /// </summary>
    [Fact]
    public async Task Read_now_asks_no_account_read_within_the_floor_and_keeps_its_reading()
    {
        Accounts("claude-code", "account-1");
        AccountWindows.Said(
            Home, "claude-code", "account-1", [new WindowReading("session", 0.4, Now.AddHours(2))], DateTimeOffset.UtcNow, null,
            source: AccountWindows.SourceRead);
        var before = File.ReadAllText(AccountWindows.PathOf(Home));

        var answer = await AnswerAsync(Module(), "ACCOUNTS_READ", new { harness = "claude-code" });

        Assert.Equal("claude-code", answer.GetProperty("harness").GetString());
        Assert.Equal(before, File.ReadAllText(AccountWindows.PathOf(Home)));
    }

    /// <summary>An agent whose accounts are asked nothing reads nothing; a name that is no agent of Daoris's is refused.</summary>
    [Fact]
    public async Task Read_now_reads_nothing_for_an_agent_asked_nothing_and_refuses_one_with_no_accounts()
    {
        Accounts("dsh", "account-1");

        var answer = await AnswerAsync(Module(), "ACCOUNTS_READ", new { harness = "dsh" });

        Assert.Equal("dsh", answer.GetProperty("harness").GetString());
        Assert.False(File.Exists(AccountWindows.PathOf(Home)));
        await RefusalAsync(Module(), "ACCOUNTS_READ", new { harness = "not-an-agent" });
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

    /// <summary>
    /// SWITCH1d (D165 point 2; the switch design §5, §6): the screen's switch and curve, as <c>profile use --switch</c> and
    /// <c>--curve</c> write them: the switch as chosen, each window of the curve set keeping the other, and <c>noCurve</c> taking
    /// the curve out with the switch kept, as <c>--no-curve</c> does.
    /// </summary>
    [Fact]
    public async Task The_switch_and_the_curve_are_written_as_chosen_and_the_curve_taken_out_by_no_curve()
    {
        Accounts("claude-code", "account-1");
        Wiring("""{ "rotation": { "claude-code": ["account-1"] } }""");
        static JsonElement Curve(string json) => JsonDocument.Parse(json).RootElement.Clone();

        await AnswerAsync(Module(), "ACCOUNT_USE", new Dictionary<string, object?>
        {
            ["action"] = "use", ["harness"] = "claude-code", ["switch"] = "curve", ["curve"] = Curve("""{ "weekly": [[0, 30], [50, 70], [100, 100]] }"""),
        });
        await AnswerAsync(Module(), "ACCOUNT_USE", new Dictionary<string, object?>
        {
            ["action"] = "use", ["harness"] = "claude-code", ["curve"] = Curve("""{ "session": [[0, 80]] }"""),
        });

        var use = HarnessSettings.Load(HarnessSettingsPath).ResolveScope("claude-code", null).Use;
        Assert.Equal("curve", use.Switch);
        Assert.Equal([new CurvePoint(0, 80)], use.Curve["session"]);
        Assert.Equal([new CurvePoint(0, 30), new CurvePoint(50, 70), new CurvePoint(100, 100)], use.Curve["weekly"]);

        await AnswerAsync(Module(), "ACCOUNT_USE", new { action = "use", harness = "claude-code", @switch = "off", noCurve = true });

        use = HarnessSettings.Load(HarnessSettingsPath).ResolveScope("claude-code", null).Use;
        Assert.Equal("off", use.Switch);
        Assert.True(use.Curve.IsEmpty);
    }

    /// <summary>
    /// SWITCH1d: a curve that breaks a rule is refused in its own code, the rule its <c>context</c> and the window, the point and
    /// the numbers its values, so the page says it in the reader's language; its sentence is the terminal's (the shared table's
    /// <c>refusal</c> rows). Nothing is written.
    /// </summary>
    [Theory]
    [InlineData("""{ "weekly": [[0, 30], [50, 70], [100, 60]] }""", "context=falling window=weekly point=3 previous=2 value=60 before=70")]
    [InlineData("""{ "weekly": [[50, 30], [40, 70]] }""", "context=rising window=weekly point=2 previous=1 value=40 before=50")]
    [InlineData("""{ "monthly": [[0, 50]] }""", "context=window window=monthly")]
    [InlineData("""{ "weekly": "0:30" }""", "context=list window=weekly")]
    [InlineData("""{ "weekly": [] }""", "context=count window=weekly size=0")]
    [InlineData("""{ "session": [[0]] }""", "context=point window=session point=1")]
    [InlineData("""{ "session": [[-5, 90]] }""", "context=elapsed window=session point=1 value=-5")]
    [InlineData("""{ "session": [[0, 101]] }""", "context=used window=session point=1 value=101")]
    [InlineData("""[[0, 30]]""", "context=shape")]
    public async Task A_curve_that_breaks_a_rule_is_refused_in_its_code_naming_the_rule(string curve, string values)
    {
        Accounts("claude-code", "account-1");
        Wiring("""{ "rotation": { "claude-code": ["account-1"] } }""");
        var before = File.ReadAllText(HarnessSettingsPath);
        using var document = JsonDocument.Parse(curve);

        var refused = await RefusalAsync(Module(), "ACCOUNT_USE", new Dictionary<string, object?>
        {
            ["action"] = "use", ["harness"] = "claude-code", ["switch"] = "curve", ["curve"] = document.RootElement,
        });

        Assert.StartsWith("ACCOUNT_USE_CURVE " + UseCurve.Problem(document.RootElement), refused);
        foreach (var value in values.Split(' ')) Assert.Contains(value, refused);
        Assert.Equal(before, File.ReadAllText(HarnessSettingsPath));
    }

    /// <summary>A switch this build does not know is refused as a value, naming the setting, as a way to use accounts is.</summary>
    [Fact]
    public async Task A_switch_this_build_does_not_know_is_refused()
    {
        Accounts("claude-code", "account-1");
        Wiring("""{ "rotation": { "claude-code": ["account-1"] } }""");

        var refused = await RefusalAsync(Module(), "ACCOUNT_USE", new { action = "use", harness = "claude-code", @switch = "sometimes" });

        Assert.StartsWith("ACCOUNT_USE_VALUE", refused);
        Assert.Contains("setting=switch", refused);
        Assert.Contains("value=sometimes", refused);
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

        var problem = Record.Exception(() => AccountsRoutes.DefaultAllowed(settings, "claude-code", profile, workspace));

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

    /// <summary>CAPSET1: the cap of sessions Daoris starts at once, the file <c>daoris driver cap</c> edits.</summary>
    [Fact]
    public async Task The_cap_is_answered_with_the_state_and_set_to_a_whole_number_of_at_least_one()
    {
        Assert.Equal(2, (await AnswerAsync(Module(), "STATE")).GetProperty("cap").GetInt32());

        var state = await AnswerAsync(Module(), "SET_CAP", new { cap = 5 });

        Assert.Equal(5, state.GetProperty("cap").GetInt32());
        Assert.Equal(5, DriverConfig.Load(DriverConfigPath).Cap);
        Assert.StartsWith(Refusals.DriverRefused, await RefusalAsync(Module(), "SET_CAP", new { cap = 0 }));
        Assert.Equal(5, DriverConfig.Load(DriverConfigPath).Cap);
    }
}
