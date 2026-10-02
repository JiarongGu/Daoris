using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TOOL6b (D130 §16.3, §16.4, §2, §3, §7) through <see cref="HarnessRoster.SelectAsync"/>: a start reads one scope; with
/// <c>use: goal</c>, the default, every listed account is used toward the goal, by what Daoris knows without the agent's
/// word: its own sessions running (the last look's records and the starts chosen since), the account it started on least
/// recently, and a weekly reset a limit told. With <c>use: order</c> it walks D125's order. Each start with a choice says
/// which step chose its account; the wait names the accounts the scope does not list and the door that adds one.
/// </summary>
/// <remarks>
/// Nothing here starts a process: the agent is present by a file look on a command this test writes, and is asked nobody's
/// sign-in, so each account is ready unless it is cooling or refused. The real-process tick, cap K over N stub accounts,
/// is <c>AccountGoalTickTests</c>, in the <c>Process</c> half.
/// </remarks>
public sealed class AccountRotationGoalTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Seen = new(2026, 10, 1, 14, 0, 0, TimeSpan.FromMinutes(345));

    private static readonly DateTimeOffset Until = new(2026, 10, 3, 16, 2, 0, TimeSpan.FromMinutes(345));

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-goal-" + Guid.NewGuid().ToString("N")[..8]);

    private DateTimeOffset _now = Seen;

    public AccountRotationGoalTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Settings => Path.Combine(_home, "harnesses.json");

    private string Command => Path.Combine(_home, "agent-here");

    private sealed class Adapter(string name, HarnessToolchain? toolchain) : ISessionAdapter
    {
        public string Name => name;

        public SessionWire Wire => SessionWire.Pipe;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    private HarnessToolchain Toolchain(bool weekFixed = true, string? accountOf = null) => new(
        Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true,
        AccountOf: accountOf, WeekFixed: weekFixed);

    private HarnessRoster Roster() =>
        new(new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["fake"] = new Adapter("fake", Toolchain()),
            // A door onto the fake agent's accounts (AGT7): its sessions are the fake agent's.
            ["fake-door"] = new Adapter("fake-door", Toolchain(accountOf: "fake")),
            // An agent whose maker was not read fixing its weekly reset (Codex's standing, §0.3).
            ["loose"] = new Adapter("loose", Toolchain(weekFixed: false)),
        }), Settings)
        {
            Clock = () => _now,
            Zone = Zone,
        };

    private static DriverConfig Config => DriverConfig.Empty with { Adapter = "fake" };

    private void AccountsOf(string agent, params string[] names)
    {
        foreach (var name in names) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, agent, name));
    }

    private void Accounts(params string[] names) => AccountsOf("fake", names);

    private CoolingEntry Cool(string account, string agent = "fake")
    {
        var entry = new CoolingEntry(agent, account, Until, true, "weekly", Seen, "s1");
        AccountCooling.Cool(_home, entry, _now);
        return entry;
    }

    private void Wire(Func<HarnessSettings, HarnessSettings> edit) => edit(new HarnessSettings()).Save(Settings);

    private static string[] Ran(IEnumerable<HarnessSelection> selections) => [.. selections.Select(selection => selection.Profile!)];

    // ——— Fewest running (§4.2, §7, §16.2): starts in one look are counted as they are chosen, and none is held.

    [Fact]
    public async Task Starts_chosen_at_once_spread_across_the_list_none_running_more_than_its_share()
    {
        Accounts("account-1", "account-2", "account-3");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2", "account-3"]));
        var roster = Present();

        var chosen = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => roster.SelectAsync("fake", Config, null, null)));

        Assert.All(chosen, selection => Assert.True(selection.Allowed, selection.Refusal));
        Assert.Equal(["account-1", "account-1", "account-2", "account-3"], Ran(chosen).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task No_start_is_held_while_an_account_it_may_use_is_ready_and_nothing_counts_accounts()
    {
        foreach (var n in new[] { 1, 2, 6 })
        {
            var names = Enumerable.Range(1, n).Select(i => $"account-{i}").ToArray();
            Accounts(names);
            Wire(s => s.WithRotation("fake", names));
            var roster = Present();

            var chosen = await Task.WhenAll(Enumerable.Range(0, 7).Select(_ => roster.SelectAsync("fake", Config, null, null)));

            Assert.All(chosen, selection => Assert.True(selection.Allowed, selection.Refusal));
            var share = (7 + n - 1) / n;
            Assert.All(Ran(chosen).GroupBy(name => name), each => Assert.InRange(each.Count(), 7 / n, share));
            Assert.Equal(n, Ran(chosen).Distinct().Count());
        }
    }

    [Fact]
    public async Task The_last_look_s_live_sessions_count_by_their_owner_and_the_next_start_takes_the_fewest()
    {
        Accounts("account-1", "account-2", "account-3");
        AccountsOf("loose", "account-3");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2", "account-3"]));
        var roster = Present();

        roster.Look(
        [
            new SessionStarted("fake", "account-1", Seen.AddMinutes(-5), Running: true),
            // A door's session runs on its owner's account (AGT7).
            new SessionStarted("fake-door", "account-2", Seen.AddMinutes(-4), Running: true),
            // Another agent's account of the same name, and a session that ended, run nothing here.
            new SessionStarted("loose", "account-3", Seen.AddMinutes(-3), Running: true),
            new SessionStarted("fake", "account-3", Seen.AddMinutes(-2), Running: false),
        ], roster.Mark());

        var selection = await roster.SelectAsync("fake", Config, null, null);

        Assert.Equal("account-3", selection.Profile);
        Assert.Equal(new AccountChoice(WalkStep.Fewest, "it runs the fewest of Daoris's sessions"), selection.Choice);
        Assert.Equal(("account-1", WalkStep.Fewest), (selection.Rotated!.From, selection.Rotated.Step));
    }

    [Fact]
    public async Task Starts_chosen_since_a_look_count_until_the_next_look_replaces_them()
    {
        Accounts("account-1", "account-2", "account-3");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2", "account-3"]));
        var roster = Present();

        Assert.Equal("account-1", (await roster.SelectAsync("fake", Config, null, null)).Profile);
        var mark = roster.Mark();
        Assert.Equal("account-2", (await roster.SelectAsync("fake", Config, null, null)).Profile);

        // The look began after the first start was chosen, whose record it then holds; the second came after it began.
        roster.Look([new SessionStarted("fake", "account-1", Seen, Running: true)], mark);
        Assert.Equal("account-3", (await roster.SelectAsync("fake", Config, null, null)).Profile);

        // Everything ended: the next look holds nothing running, and the starts chosen before it are its records.
        roster.Look(
        [
            new SessionStarted("fake", "account-1", Seen.AddMinutes(1), Running: false),
            new SessionStarted("fake", "account-2", Seen.AddMinutes(2), Running: false),
            new SessionStarted("fake", "account-3", Seen.AddMinutes(3), Running: false),
        ], roster.Mark());
        var next = await roster.SelectAsync("fake", Config, null, null);
        Assert.Equal(("account-1", WalkStep.LeastRecent), (next.Profile, next.Choice!.Step));
    }

    // ——— Where the count comes from: this machine's records, read at each look (§4.2: counted, not guessed).

    [Fact]
    public void The_records_say_each_session_s_adapter_account_opening_and_whether_it_runs_and_a_teammate_s_say_nothing()
    {
        var started = ServiceClient.ReadStarted("""
            [{ "id": "s1", "adapter": "claude-code-acp", "profile": "account-1", "created": "2026-10-01T10:00:00Z", "state": "working" },
             { "id": "s2", "adapter": "claude-code", "profile": "account-2", "created": "2026-10-01T11:00:00+05:45", "state": "completed" },
             { "id": "s3", "adapter": "claude-code", "created": "not a time", "state": "working" },
             { "id": "s4", "profile": "account-1", "created": "2026-10-01T10:00:00Z", "state": "working" },
             { "id": "other-machine/s1", "adapter": "claude-code", "profile": "account-1", "created": "2026-10-01T10:00:00Z", "state": "working" }]
            """, [new SessionView("s1", "engine", "working"), new SessionView("s3", "engine", "working")]);

        Assert.Equal(
            [
                new SessionStarted("claude-code-acp", "account-1", new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero), Running: true),
                new SessionStarted("claude-code", "account-2", new DateTimeOffset(2026, 10, 1, 11, 0, 0, TimeSpan.FromMinutes(345)), Running: false),
                new SessionStarted("claude-code", null, null, Running: true),
            ],
            started);
    }

    [Fact]
    public async Task Each_look_hands_the_roster_this_machine_s_records_so_the_next_start_counts_them()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]));
        var roster = Present();
        using var ledger = new StandInLedger();
        using var service = ledger.Client();
        ledger.Publish("q0", "engine");
        var (id, _) = await service.OpenSessionAsync("q0", "fake", null, "account-1", _home, null);
        await service.AdvanceAsync(id!, "working");
        ledger.Move("q0", "Done");
        var driver = new Daoris.Driver.Driver(service, Config, roster.Adapters, _home, harnesses: roster);

        Assert.Equal("account-1", (await roster.WiringAsync("fake", Config, null)).Profile);
        await driver.TickAsync();

        var selection = await roster.SelectAsync("fake", Config, null, null);
        Assert.Equal(("account-2", WalkStep.Fewest), (selection.Profile, selection.Choice!.Step));
    }

    // ——— Least recently started (§16.2): spread over time with one session at a time.

    [Fact]
    public async Task One_session_at_a_time_goes_to_the_account_started_on_least_recently()
    {
        Accounts("account-1", "account-2", "account-3");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2", "account-3"]));
        var roster = Present();
        roster.Look(
        [
            new SessionStarted("fake", "account-1", Seen.AddHours(-1), Running: false),
            new SessionStarted("fake", "account-2", Seen.AddHours(-3), Running: false),
        ], roster.Mark());

        var never = await roster.SelectAsync("fake", Config, null, null);
        var oldest = await roster.SelectAsync("fake", Config, null, null);

        Assert.Equal(("account-3", "Daoris has not started on it yet"), (never.Profile, never.Choice!.Clause));
        // The first runs on account-3; of the two that run nothing, the one Daoris started on longest ago.
        Assert.Equal(("account-2", "Daoris started on it least recently"), (oldest.Profile, oldest.Choice!.Clause));
    }

    // ——— Its week lapsing (§16.3 step 4): a weekly reset a limit told, carried a week where the maker fixes it.

    [Fact]
    public async Task A_weekly_reset_a_limit_told_puts_its_account_first_in_its_last_day()
    {
        Accounts("account-1", "account-2", "account-3");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2", "account-3"]));
        AccountWindows.Told(_home, "fake", "account-3", Seen.AddHours(20), Seen.AddDays(-6), "s0");
        AccountWindows.Told(_home, "fake", "account-2", Seen.AddHours(30), Seen.AddDays(-6), "s0");

        var selection = await Present().SelectAsync("fake", Config, null, null);

        Assert.Equal("account-3", selection.Profile);
        Assert.Equal(
            new AccountChoice(WalkStep.Lapsing, $"its week resets first, at Oct 2, 10:00 ({Zone.Id})"),
            selection.Choice);
    }

    [Fact]
    public async Task A_passed_weekly_reset_is_carried_a_week_where_the_maker_fixes_it_and_dropped_where_not()
    {
        Accounts("account-1", "account-2");
        AccountsOf("loose", "account-1", "account-2");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]).WithRotation("loose", ["account-1", "account-2"]));
        // Told a week ago, less four hours: passed, and next week's falls within the day.
        AccountWindows.Told(_home, "fake", "account-2", Seen.AddDays(-7).AddHours(4), Seen.AddDays(-8), "s0");
        AccountWindows.Told(_home, "loose", "account-2", Seen.AddDays(-7).AddHours(4), Seen.AddDays(-8), "s0");
        var roster = Present();

        var carried = await roster.SelectAsync("fake", Config, null, null);
        var dropped = await roster.SelectAsync("loose", Config with { Adapter = "loose" }, null, null);

        Assert.Equal(("account-2", WalkStep.Lapsing), (carried.Profile, carried.Choice!.Step));
        Assert.Equal(("account-1", WalkStep.List), (dropped.Profile, dropped.Choice!.Step));
    }

    // ——— `use: order` (D125's walk, exactly).

    [Fact]
    public async Task Order_keeps_D125_s_walk_whatever_runs_and_says_only_a_rotation()
    {
        Accounts("account-1", "account-2", "account-3");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2", "account-3"]).WithUse("fake", new UseChange(Use: "order")));
        var roster = Present();
        roster.Look([new SessionStarted("fake", "account-1", Seen, Running: true)], roster.Mark());

        var first = await roster.SelectAsync("fake", Config, null, null);
        var second = await roster.SelectAsync("fake", Config, null, null);
        Cool("account-1");
        var rotated = await roster.SelectAsync("fake", Config, null, null);

        Assert.Equal(("account-1", (AccountChoice?)null, (RotatedStart?)null), (first.Profile, first.Choice, first.Rotated));
        Assert.Equal("account-1", second.Profile);
        Assert.Equal(("account-2", (AccountChoice?)null), (rotated.Profile, rotated.Choice));
        Assert.Equal(("account-1", WalkStep.Cooling), (rotated.Rotated!.From, rotated.Rotated.Step));
    }

    // ——— Keep (§4.6, §16.3 step 1).

    [Fact]
    public async Task Driven_work_never_starts_on_the_kept_account_and_a_conversation_takes_it_last()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]).WithUse("fake", new UseChange(Keep: "account-2")));
        var roster = Present();

        var driven = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => roster.SelectAsync("fake", Config, null, null)));
        var talk = await roster.SelectAsync("fake", Config, null, null, StartKind.Conversation);
        Cool("account-1");
        var held = await roster.SelectAsync("fake", Config, null, null);
        var talkOnKept = await roster.SelectAsync("fake", Config, null, null, StartKind.Conversation);

        Assert.Equal(["account-1", "account-1", "account-1"], Ran(driven));
        Assert.All(driven, selection => Assert.Null(selection.Choice));
        Assert.Equal("account-1", talk.Profile);
        Assert.False(held.Allowed);
        Assert.EndsWith(" `account-2` is kept for conversations.", held.Refusal);
        Assert.Equal(("account-2", WalkStep.Cooling), (talkOnKept.Profile, talkOnKept.Choice!.Step));
    }

    // ——— One scope (§2 rule 1, §3.1).

    [Fact]
    public async Task A_scope_with_a_list_begins_within_it_never_on_the_machine_s_default_or_the_tool_s_own_sign_in()
    {
        Accounts("account-1", "account-2", "account-3");
        Wire(s => s.WithDefault("fake", "account-1").WithRotation("fake", ["account-2", "account-3"], "work")
            .WithRotation("loose", ["account-1"]));
        AccountsOf("loose", "account-1");
        var roster = Present();

        var work = await roster.SelectAsync("fake", Config, "work", null);
        var loose = await roster.SelectAsync("loose", Config with { Adapter = "loose" }, null, null);

        Assert.Equal(("account-2", (RotatedStart?)null), (work.Profile, work.Rotated));
        Assert.Equal("account-1", loose.Profile);
    }

    [Fact]
    public async Task A_workspace_that_names_a_default_and_no_list_rotates_nowhere_and_its_wait_asks()
    {
        Accounts("account-1", "account-2", "account-3");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2", "account-3"]).WithWorkspaceDefault("work", "fake", "account-2"));
        var cooling = Cool("account-2");

        var selection = await Present().SelectAsync("fake", Config, "work", null);

        Assert.False(selection.Allowed);
        Assert.Equal(cooling, selection.Cooling);
        Assert.Equal(
            CoolingWords.Hold(cooling, Zone) + " Not cooling, and not among the accounts `work` may use: `account-1`, `account-3` — "
            + "Daoris starts nothing on them unless a list names them; `daoris agent profile order fake account-2 account-1 "
            + "--workspace work` adds `account-1`.",
            selection.Refusal);
    }

    [Fact]
    public async Task The_wait_names_only_accounts_outside_the_list_that_are_not_cooling_or_refused_and_takes_none()
    {
        Accounts("account-1", "account-2", "account-3", "account-4", "account-5");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]));
        Cool("account-1");
        Cool("account-2");
        Cool("account-4");
        var roster = Present();
        roster.Refuse("fake", "account-5", "refused (401).");

        var selection = await roster.SelectAsync("fake", Config, null, null);

        Assert.False(selection.Allowed);
        Assert.EndsWith(
            "Daoris starts nothing on them until then. Not cooling, and not among the accounts this machine's starts may use: "
            + "`account-3` — Daoris starts nothing on them unless a list names them; `daoris agent profile order fake account-1 "
            + "account-2 account-3` adds `account-3`.",
            selection.Refusal);
    }

    [Fact]
    public async Task A_list_of_one_has_no_choice_to_say()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithRotation("fake", ["account-1"]));

        var selection = await Present().SelectAsync("fake", Config, null, null);

        Assert.Equal(("account-1", (AccountChoice?)null, (RotatedStart?)null), (selection.Profile, selection.Choice, selection.Rotated));
    }

    [Fact]
    public async Task A_scope_that_names_no_account_runs_on_the_tool_s_own_sign_in_as_before()
    {
        Accounts("account-1");

        var selection = await Present().SelectAsync("fake", Config, null, null);

        Assert.True(selection.Allowed);
        Assert.Equal(((string?)null, (AccountChoice?)null), (selection.Profile, selection.Choice));
    }

    [Fact]
    public async Task The_wiring_panel_chooses_nothing_a_start_would_count()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]));
        var roster = Present();

        for (var look = 0; look < 3; look++) Assert.Equal("account-1", (await roster.WiringAsync("fake", Config, null)).Profile);

        Assert.Equal("account-1", (await roster.SelectAsync("fake", Config, null, null)).Profile);
        Assert.Equal("account-2", (await roster.WiringAsync("fake", Config, null)).Profile);
    }

    // ——— What a start says once its record is open (§16.4, §13 as §16 amends it).

    [Fact]
    public void A_start_the_goal_chose_opens_naming_the_step_and_that_no_account_has_said_what_it_has_left()
    {
        using var ledger = new StandInLedger();
        using var service = ledger.Client();
        var lines = new List<AccountLine>();
        service.AccountLined += lines.Add;
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        var moved = new HarnessSelection(null, "account-3")
        {
            Choice = new AccountChoice(WalkStep.Fewest, "it runs the fewest of Daoris's sessions"),
            Rotated = new RotatedStart("account-1", null, "it runs the fewest of Daoris's sessions") { Step = WalkStep.Fewest, Scope = "work" },
        };
        var stayed = new HarnessSelection(null, "account-1") { Choice = new AccountChoice(WalkStep.List, "it comes first in `work`'s list") };

        RotatedOpening.Say(service, events, "s1", "claude-code-acp", moved, carried: null);
        RotatedOpening.Say(service, events, "s2", "claude-code-acp", stayed, carried: null);
        RotatedOpening.Say(service, events, "s3", "claude-code-acp", moved, new RotatedOpening.Carried("s0", Turn: 2, Used: null));

        Assert.Equal(
            "opened on `account-3`: it runs the fewest of Daoris's sessions. No account has said what it has left yet.",
            Assert.Single(events.After("s1", 0).Events).Text);
        Assert.Equal(
            "opened on `account-1`: it comes first in `work`'s list. No account has said what it has left yet.",
            Assert.Single(events.After("s2", 0).Events).Text);
        Assert.Equal(
            "carried on from session `s0` on `account-3`; it runs the fewest of Daoris's sessions; its turn 2 was refused. "
            + "No account has said what it has left yet.",
            Assert.Single(events.After("s3", 0).Events).Text);

        // Only a start moved off where its scope begins is a rotation in the log, with the step that moved it.
        Assert.Equal(2, lines.Count);
        Assert.Equal(["session", "adapter", "from", "to", "carries", "why", "scope", "said"], lines[0].Data.Select(field => field.Key));
        Assert.Equal(new object?[] { "s1", "claude-code-acp", "account-1", "account-3", null, "fewest", "work", false }, lines[0].Data.Select(field => field.Value));
        Assert.Equal("s0", lines[1].Data.Single(field => field.Key == "carries").Value);
    }

    [Theory]
    [InlineData(WalkStep.Cooling, "cooling")]
    [InlineData(WalkStep.SignedOut, "signedOut")]
    [InlineData(WalkStep.LeastRecent, "leastRecent")]
    [InlineData(WalkStep.List, "list")]
    public void The_log_says_each_step_by_its_word(WalkStep step, string word)
    {
        Assert.Equal(word, AccountLine.Rotated("s2", "claude-code", "account-1", "account-2", null, step, scope: null).Data.Single(f => f.Key == "why").Value);
        Assert.Null(AccountLine.Rotated("s2", "claude-code", "account-1", "account-2", null, step, scope: "not a name").Data.Single(f => f.Key == "scope").Value);
    }

    [Fact]
    public async Task Each_step_s_clause_names_the_scope_and_the_account()
    {
        Accounts("account-1", "account-2", "account-3");
        Wire(s => s.WithDefault("fake", "account-2").WithRotation("fake", ["account-1", "account-2", "account-3"])
            .WithRotation("fake", ["account-1", "account-3"], "work").WithUse("fake", new UseChange(Keep: "account-1"), "work"));
        var roster = Present();

        var machine = await roster.SelectAsync("fake", Config, null, null);
        var work = await roster.SelectAsync("fake", Config, "work", null);
        Cool("account-2");
        var cooled = await roster.SelectAsync("fake", Config, null, null);

        Assert.Equal("it is this machine's default", machine.Choice?.Clause);
        Assert.Equal(("account-3", "`account-1` is kept for conversations"), (work.Profile, work.Rotated?.Why));
        Assert.Equal((string?)null, work.Choice?.Clause);
        Assert.Equal(
            $"the `fake` account `account-2` is cooling until Oct 3, 16:02 ({Zone.Id}), as the agent said; of the rest, it runs the "
            + "fewest of Daoris's sessions",
            cooled.Choice?.Clause);
        Assert.Equal("account-1", cooled.Profile);
    }

    private HarnessRoster Present() => Roster();
}
