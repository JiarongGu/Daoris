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

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

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
             { "id": "other-machine/s1", "adapter": "claude-code", "profile": "account-1", "created": "2026-10-01T10:00:00Z", "state": "working" },
             { "id": "s5", "adapter": "claude-code-acp", "profile": "account-1", "created": "2026-10-01T12:00:00Z", "state": "failed",
               "noteParts": [{ "code": "ended.choice-not-offered", "values": { "agent": "claude-code-acp", "model": "opus" } }] }]
            """, [new SessionView("s1", "engine", "working"), new SessionView("s3", "engine", "working")]);

        Assert.Equal(
            [
                new SessionStarted("claude-code-acp", "account-1", new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero), Running: true) { Id = "s1" },
                new SessionStarted("claude-code", "account-2", new DateTimeOffset(2026, 10, 1, 11, 0, 0, TimeSpan.FromMinutes(345)), Running: false) { Id = "s2" },
                new SessionStarted("claude-code", null, null, Running: true) { Id = "s3" },
                // MODELROLE1c2 (D205 §3.5(c)): a record its agent's refusal of the choice ended is read so, by its end code.
                new SessionStarted("claude-code-acp", "account-1", new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero), Running: false)
                {
                    Id = "s5",
                    NotOffered = true,
                },
            ],
            started);
    }

    /// <summary>
    /// MODELROLE1c2 (D205 §4.1, round 4, R3-B1): a start's one count is given back by <c>Release</c>, both halves and idempotently: its
    /// number taken back with no look between, and, after a look captured its still-running record, that record made neither running nor
    /// the account's last start, so the refusing account is neither busy nor ranked as just used.
    /// </summary>
    [Fact]
    public async Task Release_gives_back_a_start_s_count_with_no_look_and_after_a_look()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]));
        var roster = Present();

        var chosen = await roster.SelectAsync("fake", Config, null, null);
        Assert.True(chosen.Number > 0);
        Assert.True(roster.Busy("fake", chosen.Profile));
        roster.Release(chosen.Number);
        roster.Release(chosen.Number);
        Assert.False(roster.Busy("fake", chosen.Profile));

        var mark = roster.Mark();
        roster.Look([new SessionStarted("fake", "account-1", DateTimeOffset.UtcNow, Running: true) { Id = "s1" }], mark);
        Assert.True(roster.Busy("fake", "account-1"));
        roster.Release(0, "s1");
        Assert.False(roster.Busy("fake", "account-1"));
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

    // ——— Its week lapsing (§16.3 step 4, as USAGERESET1 weighs it): what would lapse is known only from a reading of the
    // week's use. A weekly reset a limit told, and the same carried a week on where the maker fixes it, say when and not how
    // much: neither earns the account anything (absent is never zero, D57).

    [Fact]
    public async Task A_weekly_reset_a_limit_told_with_no_reading_earns_nothing_in_its_last_day()
    {
        Accounts("account-1", "account-2", "account-3");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2", "account-3"]));
        AccountWindows.Told(_home, "fake", "account-3", Seen.AddHours(20), Seen.AddDays(-6), "s0");
        AccountWindows.Told(_home, "fake", "account-2", Seen.AddHours(30), Seen.AddDays(-6), "s0");

        var selection = await Present().SelectAsync("fake", Config, null, null);

        Assert.Equal(("account-1", WalkStep.List), (selection.Profile, selection.Choice!.Step));
    }

    [Fact]
    public async Task A_weekly_reset_carried_a_week_on_earns_nothing_where_the_maker_fixes_it_or_not()
    {
        Accounts("account-1", "account-2");
        AccountsOf("loose", "account-1", "account-2");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]).WithRotation("loose", ["account-1", "account-2"]));
        // Told a week ago, less four hours: passed, and next week's falls within the day where the maker fixes it.
        AccountWindows.Told(_home, "fake", "account-2", Seen.AddDays(-7).AddHours(4), Seen.AddDays(-8), "s0");
        AccountWindows.Told(_home, "loose", "account-2", Seen.AddDays(-7).AddHours(4), Seen.AddDays(-8), "s0");
        var roster = Present();

        var carried = await roster.SelectAsync("fake", Config, null, null);
        var dropped = await roster.SelectAsync("loose", Config with { Adapter = "loose" }, null, null);

        Assert.Equal(Seen.AddHours(4), AccountWindows.WeekOf(_home, "fake", "account-2", Seen, fixedWeek: true));
        Assert.Equal(("account-1", WalkStep.List), (carried.Profile, carried.Choice!.Step));
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

    // ——— One set (POOL1a2, D197 points 1 and 2; the account-pool design §2.4): an older build's scopes read as one order.

    /// <summary>
    /// Rewritten by POOL1a2: a workspace's own list once began within it, never on the machine's default. Read as the set now, the
    /// machine that named one account and no list is used <c>order</c> with that account first (design §6.1 rule 3), so the
    /// workspace's start begins there too; never on the tool's own sign-in, whatever was named.
    /// </summary>
    [Fact]
    public async Task An_older_build_s_workspace_list_is_read_as_the_set_begun_at_the_machine_s_one_account()
    {
        Accounts("account-1", "account-2", "account-3");
        Wire(s => s.WithDefault("fake", "account-1").WithRotation("fake", ["account-2", "account-3"], "work")
            .WithRotation("loose", ["account-1"]));
        AccountsOf("loose", "account-1");
        var roster = Present();

        var work = await roster.SelectAsync("fake", Config, "work", null);
        var loose = await roster.SelectAsync("loose", Config with { Adapter = "loose" }, null, null);

        Assert.Equal(("account-1", (RotatedStart?)null), (work.Profile, work.Rotated));
        Assert.Equal("account-1", loose.Profile);
    }

    /// <summary>
    /// Rewritten by POOL1a2: a workspace that named a default and no list once ran on that account alone, so its cool-off held the
    /// start and the wait named the accounts outside it. Read as the set now, the start passes the cooling account and runs.
    /// </summary>
    [Fact]
    public async Task A_workspace_that_named_a_default_and_no_list_passes_its_cooling_account_within_the_set()
    {
        Accounts("account-1", "account-2", "account-3");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2", "account-3"]).WithWorkspaceDefault("work", "fake", "account-2"));
        Cool("account-2");

        var selection = await Present().SelectAsync("fake", Config, "work", null);

        Assert.True(selection.Allowed);
        Assert.Equal("account-1", selection.Profile);
    }

    /// <summary>
    /// Rewritten by POOL1a2: an account no list held once carried no start, and the wait named it with the door that would add it.
    /// Every account of Daoris's is in the set now, so it carries the start; a cooling or refused one is passed as ever.
    /// </summary>
    [Fact]
    public async Task An_account_no_list_held_carries_the_start_since_none_is_outside_the_set()
    {
        Accounts("account-1", "account-2", "account-3", "account-4", "account-5");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]));
        Cool("account-1");
        Cool("account-2");
        Cool("account-4");
        var roster = Present();
        roster.Refuse("fake", "account-5", "refused (401).");

        var selection = await roster.SelectAsync("fake", Config, null, null);

        Assert.True(selection.Allowed);
        Assert.Equal("account-3", selection.Profile);
    }

    /// <summary>
    /// ROSTERREFUSE2: the kept-aside clause a wait adds is a line of its own after the refusal's, so its code is kept and the clause
    /// is said by code. Rewritten by POOL1a2: the outside-the-list clauses (<c>held.outside-machine</c>, <c>held.outside-workspace</c>)
    /// are added no more, since no account is outside the set; their codes stay in the catalogues for notes written before.
    /// </summary>
    [Fact]
    public async Task A_wait_s_added_clauses_are_parts_by_code_after_the_refusal_s()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]).WithUse("fake", new UseChange(Keep: "account-2")));
        var cooling = Cool("account-1");

        var held = await Present().SelectAsync("fake", Config, "work", null);

        Assert.Equal(held.Refusal, held.RefusalNote!.Note);
        Assert.Equal(["held.cooling-account", "held.kept-aside"], NoteAssert.Codes(held.RefusalNote.Parts));
        Assert.Equal("account-2", held.RefusalNote.Parts[1].Value("account"));
        Assert.Equal(cooling, held.Cooling);
    }

    /// <summary>Rewritten by POOL1a2: a list of one beside another account is the set of both now, so the case is a set of one.</summary>
    [Fact]
    public async Task A_set_of_one_has_no_choice_to_say()
    {
        Accounts("account-1");
        Wire(s => s.WithRotation("fake", ["account-1"]));

        var selection = await Present().SelectAsync("fake", Config, null, null);

        Assert.Equal(("account-1", (AccountChoice?)null, (RotatedStart?)null), (selection.Profile, selection.Choice, selection.Rotated));
    }

    /// <summary>OWNSPACE1a (D175 point 2): a scope that names no account runs on the agent's one Daoris account, chosen by no step.</summary>
    [Fact]
    public async Task A_scope_that_names_no_account_runs_on_the_agent_s_one_Daoris_account()
    {
        Accounts("account-1");

        var selection = await Present().SelectAsync("fake", Config, null, null);

        Assert.True(selection.Allowed);
        Assert.Equal(("account-1", (AccountChoice?)null), (selection.Profile, selection.Choice));
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
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
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
        Assert.Equal(["session", "adapter", "from", "to", "carries", "why", "scope", "said", "fromSaid", "toSaid"], lines[0].Data.Select(field => field.Key));
        Assert.Equal(new object?[] { "s1", "claude-code-acp", "account-1", "account-3", null, "fewest", "work", false, null, null }, lines[0].Data.Select(field => field.Value));
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

    /// <summary>
    /// Rewritten by POOL1a2: the machine's default was a step of its own, and a workspace's own list and kept account chose
    /// otherwise. The default is the order's first now (design §2.2), so its clause is the list's, and a workspace's start takes the
    /// set's account.
    /// </summary>
    [Fact]
    public async Task Each_step_s_clause_names_the_set_and_the_account()
    {
        Accounts("account-1", "account-2", "account-3");
        Wire(s => s.WithDefault("fake", "account-2").WithRotation("fake", ["account-1", "account-2", "account-3"]));
        var roster = Present();

        var machine = await roster.SelectAsync("fake", Config, null, null);
        var work = await roster.SelectAsync("fake", Config, "work", null);
        Cool("account-2");
        var cooled = await roster.SelectAsync("fake", Config, null, null);

        // POOL1c (D197; the account-pool design §4.3): the order's first, said as that, never as this machine's list.
        Assert.Equal(("account-2", "it comes first in the order of Daoris's accounts"), (machine.Profile, machine.Choice?.Clause));
        // The workspace's start is the set's second, counted beside the first: it spreads as any start in the same look does.
        Assert.Equal(("account-1", "it runs the fewest of Daoris's sessions"), (work.Profile, work.Rotated?.Why));
        Assert.Equal(
            $"the `fake` account `account-2` is cooling until Oct 3, 16:02 ({Zone.Id}), as the agent said; of the rest, it runs the "
            + "fewest of Daoris's sessions",
            cooled.Choice?.Clause);
        Assert.Equal("account-3", cooled.Profile);
    }

    // ——— What the agents said (TOOL6c, D130 §5.2, §6, §16.3 steps 2 and 5, §16.4): read from `windows.json` as each door
    // kept it, a floor as of when it was said, gone at its reset.

    /// <summary>What an account's agent said, kept as a door keeps it: the session window, then the week where given.</summary>
    private void Said(string account, double session, double? weekly = null, double weekHours = 96, string standing = "clear", TimeSpan? ago = null)
    {
        IReadOnlyList<WindowReading> readings = weekly is { } week
            ? [new WindowReading("session", session, _now.AddHours(2), standing), new WindowReading("weekly", week, _now.AddHours(weekHours))]
            : [new WindowReading("session", session, _now.AddHours(2), standing)];
        AccountWindows.Said(_home, "fake", account, readings, _now - (ago ?? TimeSpan.FromMinutes(20)), "s0");
    }

    [Fact]
    public async Task An_account_its_agent_said_is_near_is_passed_and_the_start_says_what_each_account_said()
    {
        Accounts("account-1", "account-2", "account-3");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2", "account-3"]));
        Said("account-1", 0.95, weekly: 0.30);
        using var ledger = new StandInLedger();
        using var service = ledger.Client();
        var lines = new List<AccountLine>();
        service.AccountLined += lines.Add;
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));

        var selection = await Present().SelectAsync("fake", Config, null, null);
        RotatedOpening.Say(service, events, "s1", "fake", selection, carried: null);

        Assert.Equal("account-2", selection.Profile);
        Assert.Equal(
            new AccountChoice(WalkStep.Near, "`account-1` has used 95% of its session limit, over its curve's 90%"),
            selection.Choice);
        Assert.Equal(
            "opened on `account-2`: `account-1` has used 95% of its session limit, over its curve's 90%. What each "
            + "account said: `account-1` 20 min ago, 95% of its session limit and 30% of its weekly limit used, over its session curve's 90%; "
            + "`account-2` nothing yet; `account-3` nothing yet.",
            Assert.Single(events.After("s1", 0).Events).Text);
        var rotated = Assert.Single(lines);
        Assert.Equal(
            new object?[] { "s1", "fake", "account-1", "account-2", null, "near", null, true, "near", null },
            rotated.Data.Select(field => field.Value));
    }

    [Fact]
    public async Task Near_by_credits_and_over_the_person_s_own_curve_and_never_by_the_pace_warning_alone()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]).WithUse("fake", new UseChange(Switch: "curve", Curve: UseCurve.Flat("session", 85))));

        Said("account-1", 0.88);
        Assert.Equal(
            ("account-2", "`account-1` has used 88% of its session limit, over its curve's 85%"),
            await Ran());

        // Its pace warning passes nothing alone (USAGEREAD3a): the start stays where its list begins.
        Said("account-1", 0.10, standing: "warned");
        Assert.Equal("account-1", (await Ran()).Item1);

        AccountWindows.Said(_home, "fake", "account-1", [new WindowReading("session", 0.10, _now.AddHours(2), "clear", Credits: true)], _now, "s0");
        Assert.Equal(("account-2", "`account-1` said it is drawing on usage credits"), await Ran());

        async Task<(string?, string?)> Ran()
        {
            var selection = await Present().SelectAsync("fake", Config, null, null);
            return (selection.Profile, selection.Choice?.Clause);
        }
    }

    [Fact]
    public async Task Near_is_a_pass_never_a_wait_and_the_switch_off_still_passes_an_account_over_the_smart_curve()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]));
        Said("account-1", 0.95);
        Said("account-2", 0.97);

        var both = await Present().SelectAsync("fake", Config, null, null);
        Assert.True(both.Allowed, both.Refusal);
        Assert.Equal("account-1", both.Profile);

        // `off` stops no session switching while it runs; a start still passes an account over the smart curve (the switch
        // design §2–§3, SWITCH1b-ii), where D130's switch off passed nothing.
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]).WithUse("fake", new UseChange(Switch: "off")));
        AccountWindows.Said(_home, "fake", "account-2", [new WindowReading("session", 0.1, _now.AddHours(2), "clear")], _now, "s0");
        var off = await Present().SelectAsync("fake", Config, null, null);
        Assert.Equal(
            ("account-2", "`account-1` has used 95% of its session limit, over its curve's 90%"),
            (off.Profile, off.Choice?.Clause));
    }

    [Fact]
    public async Task Under_order_a_near_default_is_passed_and_says_so_with_what_each_account_said()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithDefault("fake", "account-1").WithRotation("fake", ["account-1", "account-2"]).WithUse("fake", new UseChange(Use: "order")));
        Said("account-1", 0.95);

        var selection = await Present().SelectAsync("fake", Config, null, null);

        Assert.Equal(("account-2", (AccountChoice?)null), (selection.Profile, selection.Choice));
        Assert.Equal(
            ("account-1", WalkStep.Near, "`account-1` has used 95% of its session limit, over its curve's 90%"),
            (selection.Rotated!.From, selection.Rotated.Step, selection.Rotated.Why));
        Assert.Equal("What each account said: `account-1` 20 min ago, 95% of its session limit used, over its session curve's 90%; `account-2` nothing yet.", selection.SaidLine);
    }

    /// <summary>
    /// UX7d-1 (D152's UX7d-1 note): the walk hands the line's steps by code, so the record's first line carries its parts
    /// beside the same English: the account it opened on with the step's reason and values, then what each account said.
    /// </summary>
    [Fact]
    public async Task A_start_s_first_line_carries_the_walk_s_steps_by_code()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithDefault("fake", "account-1").WithRotation("fake", ["account-1", "account-2"]).WithUse("fake", new UseChange(Use: "order")));
        Said("account-1", 0.95);
        using var ledger = new StandInLedger();
        using var service = ledger.Client();
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));

        RotatedOpening.Say(service, events, "s1", "fake", await Present().SelectAsync("fake", Config, null, null), carried: null);

        var opened = Assert.Single(events.After("s1", 0).Events);
        Assert.Equal(
            "opened on `account-2`: `account-1` has used 95% of its session limit, over its curve's 90%. What each "
            + "account said: `account-1` 20 min ago, 95% of its session limit used, over its session curve's 90%; `account-2` nothing yet.",
            opened.Text);
        Assert.Equal(
            ["opening.opened", "opening.said-at", "opening.said-used", "opening.said-over", "opening.said-nothing"],
            NoteAssert.Codes(opened.Parts));
        NoteAssert.Holds(opened.Text, opened.Parts);
        Assert.Equal(
            // Read back from the record, a number is a whole number as JSON keeps it.
            new object?[] { "account-2", OpeningWhy.OverCurve, "account-1", "session", 95L, 90L },
            new[] { "account", "why", "over", "window", "used", "curve" }.Select(name => opened.Parts![0].Value(name)));
    }

    /// <summary>
    /// SWITCH1b-ii (the switch design §3): step 5 is the most room under its curve first, on its tightest window, and an
    /// account with room ranks ahead of one that said nothing.
    /// </summary>
    [Fact]
    public async Task The_account_with_the_most_room_under_its_curve_goes_first()
    {
        Accounts("account-1", "account-2", "account-3");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2", "account-3"]));
        // Four days to each reset: three sevenths of the week are gone, and the smart curve stands at 63% of it.
        Said("account-1", 0.20, weekly: 0.50);
        Said("account-3", 0.20, weekly: 0.10);
        var roster = Present();

        var first = await roster.SelectAsync("fake", Config, null, null);
        var second = await roster.SelectAsync("fake", Config, null, null);

        Assert.Equal("account-3", first.Profile);
        Assert.Equal(
            new AccountChoice(WalkStep.Pace, "it has the most room under its curve, 53 points on its weekly window"),
            first.Choice);
        // The second start: account-3 runs one now; of the two that run none, the one with thirteen points of room goes ahead
        // of the one that said nothing.
        Assert.Equal("account-1", second.Profile);
        Assert.Equal(
            new AccountChoice(WalkStep.Pace, "it has the most room under its curve, 13 points on its weekly window"),
            second.Choice);
    }

    /// <summary>Under <c>switch: off</c> step 5 stands as D130 wrote it (the switch design §3): furthest behind its week's pace first.</summary>
    [Fact]
    public async Task Under_off_the_account_furthest_behind_its_week_s_pace_goes_first_and_one_that_said_nothing_sits_between()
    {
        Accounts("account-1", "account-2", "account-3");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2", "account-3"]).WithUse("fake", new UseChange(Switch: "off")));
        // Four days to each reset: three sevenths of the week are gone. account-1 is ahead of its pace, under the smart curve.
        Said("account-1", 0.20, weekly: 0.50);
        Said("account-3", 0.20, weekly: 0.10);
        var roster = Present();

        var first = await roster.SelectAsync("fake", Config, null, null);
        var second = await roster.SelectAsync("fake", Config, null, null);

        Assert.Equal("account-3", first.Profile);
        Assert.Equal(
            new AccountChoice(WalkStep.Pace, "it is furthest behind its week's pace, 10% of its weekly limit used with 43% of its week gone"),
            first.Choice);
        // The second start: account-3 runs one now; of the two that run none, the one that said nothing ranks ahead of the one
        // ahead of its pace, and says why by that account's word.
        Assert.Equal("account-2", second.Profile);
        Assert.Equal(
            new AccountChoice(WalkStep.Pace, "`account-1` is ahead of its week's pace, 50% of its weekly limit used with 43% of its week gone"),
            second.Choice);
    }

    [Fact]
    public async Task A_week_the_agent_said_resets_within_the_day_goes_first_by_what_would_lapse_read_from_the_reading()
    {
        Accounts("account-1", "account-2", "account-3");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2", "account-3"]));
        // account-3: half its week unused, resetting in 20 hours; account-2: 3 points, resetting in 2. By its time alone
        // account-2's went first and account-3's half lapsed (USAGERESET1).
        Said("account-3", 0.10, weekly: 0.50, weekHours: 20);
        Said("account-2", 0.10, weekly: 0.97, weekHours: 2);

        var selection = await Present().SelectAsync("fake", Config, null, null);

        Assert.Equal("account-3", selection.Profile);
        Assert.Equal(
            new AccountChoice(WalkStep.Lapsing, "its weekly window resets in 20 h with 50% of it unused, read 20 min ago"), selection.Choice);
    }

    [Fact]
    public async Task A_weekly_reading_older_than_a_newer_five_hour_frame_earns_nothing()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]));
        // account-2's week was read three hours ago; a session's frame has said its five-hour window since, so the account
        // ran after the week was read, and what would lapse there is not known.
        AccountWindows.Said(_home, "fake", "account-2", [new WindowReading("weekly", 0.40, _now.AddHours(8))], _now.AddHours(-3), null);
        AccountWindows.Said(_home, "fake", "account-2", [new WindowReading("session", 0.10, _now.AddHours(4), "clear")], _now.AddMinutes(-5), "s0");

        var selection = await Present().SelectAsync("fake", Config, null, null);

        Assert.NotEqual(WalkStep.Lapsing, selection.Choice!.Step);
    }

    [Fact]
    public async Task A_reading_gone_at_its_reset_leaves_the_account_unknown_and_the_start_says_none_has_said()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]));
        Said("account-1", 0.95);

        Assert.Equal("account-2", (await Present().SelectAsync("fake", Config, null, null)).Profile);

        _now = _now.AddHours(2);
        var after = await Present().SelectAsync("fake", Config, null, null);
        Assert.Equal(("account-1", (string?)null), (after.Profile, after.SaidLine));
        Assert.Equal(WalkStep.List, after.Choice!.Step);
    }

    private HarnessRoster Present() => Roster();
}
