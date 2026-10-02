using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TOOL4f (D125 §3): the next start runs on the next ready account of the person's order. The walk is
/// <see cref="HarnessRoster.SelectAsync"/>'s: the account the resolution names runs if it is ready; if a default named it
/// and the order lists it, the next ready account after it in the order runs, wrapping; a pick, the tool's own home and an
/// account outside the order never rotate; and when no account is ready the start waits for the first reset.
/// </summary>
/// <remarks>
/// Nothing here starts a process. The harness is present by a file look (<see cref="HarnessToolchain.ProbeByPresence"/>)
/// on a command this test writes, and asks nobody whether an account is signed in, so each account is ready unless it
/// is cooling or refused. Where a hold must come before any probe, the command is one no machine has, so a probe that
/// was reached would say the harness is not installed. Signed out, which only a probe says, is walked past in
/// <c>AccountRotationTickTests</c>, the <c>Process</c> half, and in the words below.
/// </remarks>
public sealed class AccountRotationTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Seen = new(2026, 10, 1, 14, 0, 0, TimeSpan.FromMinutes(345));

    /// <summary>Observation 4's reset, read: 3 October, 16:02 in the test's zone.</summary>
    private static readonly DateTimeOffset Until = new(2026, 10, 3, 16, 2, 0, TimeSpan.FromMinutes(345));

    private const string Nowhere = "daoris-tool4f-no-such-command";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-rotation-" + Guid.NewGuid().ToString("N")[..8]);

    private DateTimeOffset _now = Seen;

    public AccountRotationTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Settings => Path.Combine(_home, "harnesses.json");

    /// <summary>A file that is there, so the harness is present by a look and nothing runs.</summary>
    private string Command => Path.Combine(_home, "agent-here");

    private sealed class Adapter(string name, HarnessToolchain? toolchain, SessionWire wire = SessionWire.Pipe) : ISessionAdapter
    {
        public string Name => name;

        public SessionWire Wire => wire;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    private HarnessRoster Roster(string binary) =>
        new(new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["fake"] = new Adapter("fake", new HarnessToolchain(
                Binary: [binary], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true)),
        }), Settings)
        {
            Clock = () => _now,
            Zone = Zone,
        };

    private HarnessRoster Present() => Roster(Command);

    private static DriverConfig Config => DriverConfig.Empty with { Adapter = "fake" };

    /// <summary>The agent's accounts on this machine: a directory each, as a sign-in leaves them.</summary>
    private void Accounts(params string[] names)
    {
        foreach (var name in names) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", name));
    }

    private CoolingEntry Cool(string? account, DateTimeOffset? until = null, bool stated = true)
    {
        var entry = new CoolingEntry("fake", account, until ?? Until, stated, "weekly", Seen, "s1");
        AccountCooling.Cool(_home, entry, _now);
        return entry;
    }

    private void Wire(Func<HarnessSettings, HarnessSettings> edit) => edit(new HarnessSettings()).Save(Settings);

    // ——— The walk's order (§3.1, §3.3, §3.4): who may be tried, and in which order. Pure.

    public static TheoryData<string?, ChoiceFrom, string[], string[], string?[]> Walks => new()
    {
        // A default the order lists: it, then the rest of the order after it, wrapping.
        { "account-1", ChoiceFrom.Machine, ["account-1", "account-2", "account-3"], ["account-1", "account-2", "account-3"], ["account-1", "account-2", "account-3"] },
        { "account-2", ChoiceFrom.Machine, ["account-1", "account-2", "account-3"], ["account-1", "account-2", "account-3"], ["account-2", "account-3", "account-1"] },
        { "account-3", ChoiceFrom.Workspace, ["account-1", "account-2", "account-3"], ["account-1", "account-2", "account-3"], ["account-3", "account-1", "account-2"] },
        // Names compare as the wiring compares them.
        { "Account-2", ChoiceFrom.Machine, ["account-1", "account-2"], ["account-1", "account-2"], ["Account-2", "account-1"] },
        // A person's pick never rotates.
        { "account-1", ChoiceFrom.Picked, ["account-1", "account-2"], ["account-1", "account-2"], ["account-1"] },
        // The tool's own home is never in an order, and work resolved to it is never moved.
        { null, ChoiceFrom.Unset, ["account-1", "account-2"], ["account-1", "account-2"], [null] },
        // A default the order does not list, or no order: the one account, as today.
        { "personal", ChoiceFrom.Machine, ["account-1", "account-2"], ["account-1", "account-2", "personal"], ["personal"] },
        { "account-1", ChoiceFrom.Machine, [], ["account-1", "account-2"], ["account-1"] },
        // An order naming an account that is not here is never rotated into.
        { "account-1", ChoiceFrom.Machine, ["account-1", "gone", "account-2"], ["account-1", "account-2"], ["account-1", "account-2"] },
    };

    [Theory]
    [MemberData(nameof(Walks))]
    public void The_walk_is_the_resolved_account_then_the_rest_of_its_order(
        string? resolved, ChoiceFrom from, string[] order, string[] accounts, string?[] walk)
    {
        Assert.Equal(walk, AccountRotation.Candidates(resolved, from, order, accounts));
    }

    // ——— Which account (§3.3): the selection.

    [Fact]
    public async Task A_cooling_default_the_order_lists_rotates_to_the_next_ready_account()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithDefault("fake", "account-1").WithRotation("fake", ["account-1", "account-2"]));
        var cooling = Cool("account-1");

        var selection = await Present().SelectAsync("fake", Config, null, null);

        Assert.True(selection.Allowed);
        Assert.Equal("account-2", selection.Profile);
        Assert.Equal(HarnessSettings.ProfileHome(_home, "fake", "account-2"), selection.ProfileHome);
        Assert.Null(selection.Cooling);
        Assert.Equal(
            new RotatedStart("account-1", cooling, $"the `fake` account `account-1` is cooling until Oct 3, 16:02 ({Zone.Id}), as the agent said"),
            selection.Rotated);
    }

    [Fact]
    public async Task Rotation_never_moves_work_off_a_ready_account()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithDefault("fake", "account-1").WithRotation("fake", ["account-1", "account-2"]));
        Cool("account-2");

        var selection = await Present().SelectAsync("fake", Config, null, null);

        Assert.Equal(("account-1", (RotatedStart?)null), (selection.Profile, selection.Rotated));
    }

    [Fact]
    public async Task The_walk_wraps_and_walks_past_a_cooling_account_and_a_refused_one_alike()
    {
        Accounts("account-1", "account-2", "account-3", "account-4");
        Wire(s => s.WithDefault("fake", "account-2").WithRotation("fake", ["account-1", "account-2", "account-3", "account-4"]));
        Cool("account-2");
        Cool("account-3");
        var roster = Present();
        roster.Refuse("fake", "account-4", "an earlier session found that its provider refused the `fake` account `account-4` (401).");

        var selection = await roster.SelectAsync("fake", Config, null, null);

        Assert.Equal("account-1", selection.Profile);
        Assert.Equal("account-2", selection.Rotated!.From);
    }

    [Fact]
    public async Task A_workspace_s_order_applies_to_its_starts_and_the_machine_s_to_the_rest()
    {
        Accounts("account-1", "account-2", "account-3");
        Wire(s => s
            .WithDefault("fake", "account-1").WithRotation("fake", ["account-1", "account-2"])
            .WithWorkspaceDefault("work", "fake", "account-2").WithRotation("fake", ["account-2", "account-3"], "work"));
        Cool("account-1");
        Cool("account-2");
        var roster = Present();

        var work = await roster.SelectAsync("fake", Config, "work", null);
        var home = await roster.SelectAsync("fake", Config, "home", null);

        // A work circle rotates among its own accounts.
        Assert.Equal(("account-3", "account-2"), (work.Profile, work.Rotated!.From));
        // The machine's order holds nothing ready: the start waits, for the first reset.
        Assert.False(home.Allowed);
        Assert.Null(home.Rotated);
    }

    [Fact]
    public async Task Back_when_ready_new_starts_run_on_the_resolved_account_again()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithDefault("fake", "account-1").WithRotation("fake", ["account-1", "account-2"]));
        Cool("account-1");
        var roster = Present();
        Assert.Equal("account-2", (await roster.SelectAsync("fake", Config, null, null)).Profile);

        _now = Until.AddMinutes(1);

        var selection = await roster.SelectAsync("fake", Config, null, null);
        Assert.Equal(("account-1", (RotatedStart?)null), (selection.Profile, selection.Rotated));
    }

    [Fact]
    public async Task A_cool_off_ended_early_takes_the_next_start_back()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithDefault("fake", "account-1").WithRotation("fake", ["account-1", "account-2"]));
        Cool("account-1");
        var roster = Present();

        Assert.True(roster.Ready("fake", "account-1"));

        Assert.Equal("account-1", (await roster.SelectAsync("fake", Config, null, null)).Profile);
    }

    /// <summary>
    /// TOOL4j: the protocol stub runs as the stub's accounts, so its start walks the stub's order and its spawn is handed
    /// the stub account it runs as. Present by a file look on the command this test writes; the stub, whose own command
    /// is not named here, is asked nothing, so every account is ready unless it is cooling.
    /// </summary>
    [Fact]
    public async Task The_protocol_stub_s_start_walks_the_stub_s_order_and_runs_as_the_next_stub_account()
    {
        var adapters = AdapterSet.Built();
        var roster = new HarnessRoster(adapters, Settings) { Clock = () => _now, Zone = Zone };
        foreach (var name in new[] { "account-1", "account-2" }) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "stub", name));
        Wire(s => s.WithDefault("stub", "account-1").WithRotation("stub", ["account-1", "account-2"]));
        var cooling = new CoolingEntry("stub", "account-1", Until, true, "weekly", Seen, "s1");
        AccountCooling.Cool(_home, cooling, _now);
        var config = DriverConfig.Empty with
        {
            Adapter = "acp-stub",
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = [Command] },
        };

        var selection = await roster.SelectAsync("acp-stub", config, null, null);

        Assert.True(selection.Allowed, selection.Refusal);
        Assert.Equal("account-2", selection.Profile);
        Assert.Equal(HarnessSettings.ProfileHome(_home, "stub", "account-2"), selection.ProfileHome);
        Assert.Equal(
            new RotatedStart("account-1", cooling, $"the `stub` account `account-1` is cooling until Oct 3, 16:02 ({Zone.Id}), as the agent said"),
            selection.Rotated);

        // The spawn is handed that account through the stub's own variable, as a pipe-door start on it would be.
        var spawn = new ProcessStartInfo(Command);
        HarnessProbe.Apply(spawn, adapters.Resolve("acp-stub").Toolchain!, selection.ProfileHome);
        Assert.Equal(HarnessSettings.ProfileHome(_home, "stub", "account-2"), spawn.Environment["DAORIS_STUB_CONFIG_DIR"]);
    }

    // ——— What never rotates (§3.4).

    [Fact]
    public async Task A_pick_on_a_cooling_account_is_refused_naming_the_accounts_that_are_ready_and_never_rotated()
    {
        Accounts("account-1", "account-2", "account-3", "account-4");
        Wire(s => s.WithDefault("fake", "account-1").WithRotation("fake", ["account-1", "account-2"]));
        var cooling = Cool("account-1");
        Cool("account-4");
        var roster = Present();
        roster.Refuse("fake", "account-3", "refused (401).");

        var selection = await roster.SelectAsync("fake", Config, null, chosen: "account-1");

        Assert.False(selection.Allowed);
        Assert.Null(selection.Rotated);
        Assert.Equal(cooling, selection.Cooling);
        Assert.Equal($"{CoolingWords.Hold(cooling, Zone)} Ready now: `account-2`.", selection.Refusal);
    }

    [Fact]
    public async Task A_pick_on_a_cooling_account_with_nothing_else_ready_says_so()
    {
        Accounts("account-1");
        var cooling = Cool("account-1");

        var selection = await Present().SelectAsync("fake", Config, null, chosen: "account-1");

        Assert.Equal($"{CoolingWords.Hold(cooling, Zone)} No other `fake` account is ready.", selection.Refusal);
    }

    [Fact]
    public async Task A_pick_that_is_ready_runs_where_the_default_is_cooling()
    {
        Accounts("account-1", "account-2", "account-3");
        Wire(s => s.WithDefault("fake", "account-1").WithRotation("fake", ["account-1", "account-2"]));
        Cool("account-1");

        var selection = await Present().SelectAsync("fake", Config, null, chosen: "account-3");

        Assert.Equal(("account-3", (RotatedStart?)null), (selection.Profile, selection.Rotated));
    }

    [Fact]
    public async Task The_tool_s_own_home_cooling_waits_and_is_never_rotated_into_an_order()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]));
        var own = Cool(account: null);

        var selection = await Present().SelectAsync("fake", Config, null, null);

        Assert.False(selection.Allowed);
        Assert.Null(selection.Rotated);
        Assert.Equal(CoolingWords.Hold(own, Zone), selection.Refusal);
        Assert.Equal(own, selection.Cooling);
    }

    [Fact]
    public async Task A_cooling_default_the_order_does_not_list_waits_as_it_always_did()
    {
        Accounts("account-1", "account-2", "personal");
        Wire(s => s.WithDefault("fake", "personal").WithRotation("fake", ["account-1", "account-2"]));
        var cooling = Cool("personal");

        var selection = await Present().SelectAsync("fake", Config, null, null);

        Assert.Equal((false, CoolingWords.Hold(cooling, Zone), (RotatedStart?)null), (selection.Allowed, selection.Refusal, selection.Rotated));
    }

    [Fact]
    public async Task With_no_order_a_cooling_default_waits_byte_for_byte_as_before()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithDefault("fake", "account-1"));
        var cooling = Cool("account-1");

        var selection = await Present().SelectAsync("fake", Config, null, null);

        Assert.Equal(CoolingWords.Hold(cooling, Zone), selection.Refusal);
        Assert.Equal(cooling, selection.Cooling);
    }

    // ——— When no account is ready (§4): the start waits for the first reset, with nothing probed.

    [Fact]
    public async Task Every_account_cooling_holds_the_start_before_any_probe_until_the_first_reset()
    {
        Accounts("account-1", "account-2", "account-3");
        Wire(s => s.WithDefault("fake", "account-1").WithRotation("fake", ["account-1", "account-2", "account-3"]));
        Cool("account-1", Until.AddHours(5));
        var first = Cool("account-2", Until);
        Cool("account-3", Until.AddDays(1));

        // A probe that was reached would find nothing on this PATH, and say so instead.
        var selection = await Roster(Nowhere).SelectAsync("fake", Config, null, null);

        Assert.False(selection.Allowed);
        Assert.Equal(first, selection.Cooling);
        Assert.Equal(
            $"every `fake` account this start may use is cooling; the first ready, `account-2`, at Oct 3, 16:02 ({Zone.Id}), "
            + "as the agent said. Daoris starts nothing on them until then.",
            selection.Refusal);
    }

    [Fact]
    public async Task A_wait_over_an_order_some_of_it_refused_names_each_account_and_the_first_reset()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithDefault("fake", "account-1").WithRotation("fake", ["account-1", "account-2"]));
        var cooling = Cool("account-1");
        var roster = Roster(Nowhere);
        roster.Refuse("fake", "account-2", "refused (401).");

        var selection = await roster.SelectAsync("fake", Config, null, null);

        Assert.Equal(cooling, selection.Cooling);
        Assert.Equal(
            $"no `fake` account this start may use is ready: `account-1` is cooling until Oct 3, 16:02 ({Zone.Id}), "
            + $"`account-2` was refused by its provider; the first ready, `account-1`, at Oct 3, 16:02 ({Zone.Id}), as the "
            + "agent said. Daoris starts nothing on them until then.",
            selection.Refusal);
    }

    [Fact]
    public async Task An_order_none_of_it_cooling_and_none_of_it_ready_says_the_default_s_own_refusal()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithDefault("fake", "account-1").WithRotation("fake", ["account-1", "account-2"]));
        var roster = Present();
        roster.Refuse("fake", "account-1", "the first was refused.");
        roster.Refuse("fake", "account-2", "the second was refused.");

        var selection = await roster.SelectAsync("fake", Config, null, null);

        Assert.Equal(("the first was refused.", (CoolingEntry?)null), (selection.Refusal, selection.Cooling));
    }

    // ——— The wiring panel (MAP1b): it never shows an account a start would not take.

    [Fact]
    public async Task The_wiring_panel_shows_the_account_a_rotated_start_takes_and_the_one_its_default_named()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithDefault("fake", "account-1").WithRotation("fake", ["account-1", "account-2"]));
        Cool("account-1");

        var wiring = await Present().WiringAsync("fake", Config, null);

        Assert.Equal(("account-2", ChoiceFrom.Machine, "account-1", (string?)null), (wiring.Profile, wiring.ProfileFrom, wiring.RotatedFrom, wiring.Refusal));
    }

    [Fact]
    public async Task The_wiring_panel_says_no_rotation_where_none_happens()
    {
        Accounts("account-1");
        Wire(s => s.WithDefault("fake", "account-1"));

        var wiring = await Present().WiringAsync("fake", Config, null);

        Assert.Equal(("account-1", (string?)null), (wiring.Profile, wiring.RotatedFrom));
    }

    // ——— The words (§3.3, §3.6): which account a rotated start opened on, and why.

    [Fact]
    public void A_rotated_start_s_record_opens_saying_which_account_and_why()
    {
        var cooling = new CoolingEntry("claude-code", "account-1", Until, true, "weekly", Seen, "s1");
        var rotated = new RotatedStart("account-1", cooling, RotationWords.Why("claude-code", new AccountState("account-1", AccountReadiness.Cooling, cooling), Zone));

        Assert.Equal(
            $"opened on `account-2`: the `claude-code` account `account-1` is cooling until Oct 3, 16:02 ({Zone.Id}), as the agent said.",
            RotationWords.Opened("account-2", rotated));
    }

    [Fact]
    public void A_rotated_carry_on_s_record_opens_naming_the_cut_off_session_both_accounts_and_the_refused_turn()
    {
        var cooling = new CoolingEntry("claude-code", "account-1", Until, true, "weekly", Seen, "3f9c2a71");
        var rotated = new RotatedStart("account-1", cooling, RotationWords.Why("claude-code", new AccountState("account-1", AccountReadiness.Cooling, cooling), Zone));

        Assert.Equal(
            $"carried on from session `3f9c2a71` on `account-2`; the `claude-code` account `account-1` is cooling until Oct 3, "
            + $"16:02 ({Zone.Id}), as the agent said; its turn 1 was refused with 370,104 tokens of context.",
            RotationWords.CarriedOn("3f9c2a71", "account-2", rotated, turn: 1, used: 370_104));
        Assert.Equal(
            $"carried on from session `3f9c2a71` on `account-2`; the `claude-code` account `account-1` is cooling until Oct 3, "
            + $"16:02 ({Zone.Id}), as the agent said; its turn 2 was refused.",
            RotationWords.CarriedOn("3f9c2a71", "account-2", rotated, turn: 2, used: null));
        Assert.Equal(
            $"carried on from session `3f9c2a71` on `account-2`; the `claude-code` account `account-1` is cooling until Oct 3, "
            + $"16:02 ({Zone.Id}), as the agent said.",
            RotationWords.CarriedOn("3f9c2a71", "account-2", rotated, turn: null, used: null));
    }

    [Theory]
    [InlineData(AccountReadiness.Refused, "the `claude-code` account `account-1` was refused by its provider")]
    [InlineData(AccountReadiness.SignedOut, "the `claude-code` account `account-1` is not signed in")]
    public void Why_an_account_was_walked_past_names_it(AccountReadiness readiness, string why)
    {
        Assert.Equal(why, RotationWords.Why("claude-code", new AccountState("account-1", readiness), Zone));
    }

    [Fact]
    public void A_wait_over_an_order_some_of_it_signed_out_names_each()
    {
        var cooling = new CoolingEntry("fake", "account-2", Until, false, null, Seen, "s1");

        Assert.Equal(
            "no `fake` account this start may use is ready: `account-1` is not signed in, `account-2` is cooling until Oct 3, "
            + $"16:02 ({Zone.Id}); the first ready, `account-2`, at Oct 3, 16:02 ({Zone.Id}), Daoris's default: the agent named "
            + "no time. Daoris starts nothing on them until then.",
            RotationWords.Wait("fake", [new("account-1", AccountReadiness.SignedOut), new("account-2", AccountReadiness.Cooling, cooling)], Zone));
    }

    // ——— Said once the record is open (§3.3, §3.6): its first line, and the log's.

    private static HarnessSelection RotatedSelection(string to = "account-2")
    {
        var cooling = new CoolingEntry("claude-code", "account-1", Until, true, "weekly", Seen, "s1");
        return new HarnessSelection(null, to)
        {
            Rotated = new RotatedStart("account-1", cooling, RotationWords.Why("claude-code", new AccountState("account-1", AccountReadiness.Cooling, cooling), Zone)),
        };
    }

    [Fact]
    public void A_rotated_start_s_record_opens_with_its_line_and_the_log_says_account_rotated()
    {
        using var ledger = new StandInLedger();
        using var service = ledger.Client();
        var lines = new List<AccountLine>();
        service.AccountLined += lines.Add;
        var events = new SessionEvents(Path.Combine(_home, "sessions"));

        RotatedOpening.Say(service, events, "c7", "claude-code-acp", RotatedSelection(), carried: null);

        var first = Assert.Single(events.After("c7", 0).Events);
        Assert.Equal((SessionEventKind.Note, 1L), (first.Kind, first.Seq));
        Assert.Equal(RotationWords.Opened("account-2", RotatedSelection().Rotated!), first.Text);
        var line = Assert.Single(lines);
        Assert.Equal(
            new object?[] { "c7", "claude-code-acp", "account-1", "account-2", null },
            line.Data.Select(field => field.Value));
    }

    [Fact]
    public void A_rotated_carry_on_s_record_opens_naming_the_cut_off_session_and_its_refused_turn()
    {
        using var ledger = new StandInLedger();
        using var service = ledger.Client();
        var lines = new List<AccountLine>();
        service.AccountLined += lines.Add;
        var events = new SessionEvents(Path.Combine(_home, "sessions"));

        RotatedOpening.Say(service, events, "s2", "claude-code-acp", RotatedSelection(), new RotatedOpening.Carried("s1", Turn: 3, Used: 370_104));

        Assert.Equal(
            RotationWords.CarriedOn("s1", "account-2", RotatedSelection().Rotated!, 3, 370_104),
            Assert.Single(events.After("s2", 0).Events).Text);
        Assert.Equal("s1", Assert.Single(lines).Data.Single(field => field.Key == "carries").Value);
    }

    [Fact]
    public void A_start_that_did_not_rotate_says_nothing()
    {
        using var ledger = new StandInLedger();
        using var service = ledger.Client();
        var lines = new List<AccountLine>();
        service.AccountLined += lines.Add;
        var events = new SessionEvents(Path.Combine(_home, "sessions"));

        RotatedOpening.Say(service, events, "s2", "claude-code-acp", new HarnessSelection(null, "account-1"), new RotatedOpening.Carried("s1", null, null));

        Assert.Empty(events.After("s2", 0).Events);
        Assert.Empty(lines);
    }

    [Fact]
    public void The_cut_off_session_s_context_is_its_usage_high_water_from_the_record_where_no_usage_was_kept()
    {
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Usage, Used = 120_000, Size = 1_000_000 });
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Usage, Used = 370_104, Size = 1_000_000 });
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Usage, Used = 2_000, Size = 1_000_000 });
        var usage = new SessionUsage(_home);

        Assert.Equal(370_104L, RotatedOpening.ContextOf(null, events, "s1"));
        Assert.Null(RotatedOpening.ContextOf(usage, events, "s0"));

        usage.Record(new UsageEntry("s1", "engine", "claude-code-acp", "account-1", 400_000, 1_000_000, Seen));
        Assert.Equal(400_000L, RotatedOpening.ContextOf(usage, events, "s1"));
    }

    // ——— The log (§5.4): `account.rotated`, by profile name and nothing else of an account.

    [Fact]
    public void Account_rotated_carries_the_session_the_adapter_both_accounts_and_the_cut_off_session()
    {
        var line = AccountLine.Rotated("s2", "claude-code-acp", "account-1", "account-2", carries: "s1");

        Assert.Equal("account.rotated", line.Event);
        Assert.Equal(["session", "adapter", "from", "to", "carries"], line.Data.Select(field => field.Key));
        Assert.Equal(new object?[] { "s2", "claude-code-acp", "account-1", "account-2", "s1" }, line.Data.Select(field => field.Value));
        Assert.Null(AccountLine.Rotated("s2", "claude-code-acp", "account-1", "account-2", carries: null).Data.Single(f => f.Key == "carries").Value);
        Assert.Null(AccountLine.Rotated("s2", "claude-code-acp", "sk-ant not a name", "account-2", null).Data.Single(f => f.Key == "from").Value);
    }

    [Fact]
    public void The_machine_log_writes_account_rotated_through_the_client()
    {
        using var log = new MachineLog(_home, "desktop", () => _now);
        using var ledger = new StandInLedger();
        using var service = ledger.Client();
        using var watch = new SessionLog(log, service, new SessionEvents(Path.Combine(_home, "sessions")), () => _now);

        service.AccountSaid(AccountLine.Rotated("s2", "claude-code-acp", "account-1", "account-2", "s1"));

        var line = Directory.GetFiles(Path.Combine(_home, MachineLog.Folder)).SelectMany(StubFile.Lines)
            .Select(text => JsonDocument.Parse(text).RootElement.Clone())
            .Single(entry => entry.GetProperty("event").GetString() == "account.rotated");
        Assert.Equal(
            """{"session":"s2","adapter":"claude-code-acp","from":"account-1","to":"account-2","carries":"s1"}""",
            line.GetProperty("data").GetRawText());
    }
}
