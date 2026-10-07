using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CODEXUSE1 through the roster: where a door carries none of its agent's windows, a start's walk over a list asks each
/// listed account's windows of the agent's own server and keeps them in <c>windows.json</c> as a door's frame is kept, so
/// switching before the limit, the start's line of what each account said, <c>daoris agent list</c> and the page read a
/// Codex account as they read a Claude Code one. A person's press reads them too; a look never does.
/// </summary>
/// <remarks>
/// Nothing here starts a process: the agent is present by a file look on a command this test writes, and its server is an
/// in-process stand-in (<see cref="HarnessRoster.AskingUsage"/>) asked under the account's lock, as the real one is. The real
/// server's spawn is <c>CodexUsageProcessTests</c>, in the <c>Process</c> half.
/// </remarks>
public sealed class CodexUsageRosterTests : IDisposable
{
    private static readonly DateTimeOffset Seen = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-codexuse-" + Guid.NewGuid().ToString("N")[..8]);

    private DateTimeOffset _now = Seen;

    private readonly List<string> _asked = [];

    // What each account's server answers: its readings, or null where it cannot be had (a refusal, a timeout, no binary).
    private readonly Dictionary<string, IReadOnlyList<WindowReading>?> _answers = new(StringComparer.OrdinalIgnoreCase);

    public CodexUsageRosterTests()
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

        public SessionWire Wire => SessionWire.Acp;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    private static readonly UsageQuestion Question = new("fake", ["fake-agent", "app-server"], CodexUsage.Question.Recorded);

    private HarnessToolchain Toolchain(UsageQuestion? usage = null, string? accountOf = null) => new(
        Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true,
        AccountOf: accountOf, Usage: usage);

    private HarnessRoster Roster() =>
        new(new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            // The agent declares no question of its own and has no door of its own here: its door asks for it (AGT7), as
            // `codex-acp` does for `codex`.
            ["fake"] = new Adapter("fake", Toolchain()),
            ["fake-door"] = new Adapter("fake-door", Toolchain(Question, accountOf: "fake")),
            // An agent whose door carries its windows asks nothing.
            ["quiet"] = new Adapter("quiet", Toolchain()),
        }), Settings)
        {
            Clock = () => _now,
            AskingUsage = (owner, account, _) =>
            {
                lock (_asked) _asked.Add($"{owner}/{account}");
                return Task.FromResult(_answers.GetValueOrDefault(account));
            },
        };

    private static DriverConfig Config => DriverConfig.Empty with { Adapter = "fake-door" };

    private void Accounts(params string[] names)
    {
        foreach (var name in names) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", name));
    }

    private void Wire(Func<HarnessSettings, HarnessSettings> edit) => edit(new HarnessSettings()).Save(Settings);

    /// <summary>What the account's server answers: its five-hour window's use and its week's, as Codex's answer maps them.</summary>
    private void Answers(string account, double session, double weekly) =>
        _answers[account] = [new WindowReading("session", session, _now.AddHours(3)), new WindowReading("weekly", weekly, _now.AddDays(4))];

    private string[] Asked()
    {
        lock (_asked) return [.. _asked];
    }

    [Fact]
    public async Task A_start_reads_each_listed_account_s_windows_and_passes_one_near_its_limit_as_it_passes_Claude_s()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]));
        // The first is near by its week, which only Codex's secondary window says.
        Answers("account-1", 0.20, 0.95);
        Answers("account-2", 0.10, 0.30);

        var selection = await Roster().SelectAsync("fake-door", Config, null, null);

        Assert.Equal(["fake/account-1", "fake/account-2"], Asked().Order(StringComparer.Ordinal));
        Assert.Equal("account-2", selection.Profile);
        // Near reads the number, at or over the scope's near (90%), whichever window it is in.
        Assert.Equal(WalkStep.Near, selection.Choice?.Step);
        Assert.Equal("`account-1` has used 95% of its weekly limit, at or over the 90% that counts as near", selection.Choice?.Clause);
        Assert.Equal(
            "What each account said: `account-1` just now, 20% of its session limit and 95% of its weekly limit used, near at 90%; "
            + "`account-2` just now, 10% of its session limit and 30% of its weekly limit used.",
            selection.SaidLine);

        // Kept where a door's frame is kept, under the account's owner, so every reader of `windows.json` reads it.
        var kept = AccountWindows.SaidOf(_home, "fake", "account-1", _now)!;
        Assert.Equal(0.95, kept.Of("weekly")!.Used);
        Assert.Equal(_now.AddDays(4), kept.Of("weekly")!.Reset);
        Assert.Null(kept.Of("weekly")!.Session);
    }

    [Fact]
    public async Task Both_near_is_a_pass_never_a_wait()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]));
        Answers("account-1", 0.95, 0.15);
        Answers("account-2", 0.99, 0.15);

        var selection = await Roster().SelectAsync("fake-door", Config, null, null);

        Assert.True(selection.Allowed, selection.Refusal);
        Assert.Equal("account-1", selection.Profile);
    }

    [Fact]
    public async Task A_reading_that_cannot_be_had_leaves_the_account_unknown_never_spent()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]));
        _answers["account-1"] = null;
        Answers("account-2", 0.10, 0.10);

        var selection = await Roster().SelectAsync("fake-door", Config, null, null);

        Assert.True(selection.Allowed, selection.Refusal);
        Assert.Null(AccountWindows.SaidOf(_home, "fake", "account-1", _now));
        Assert.Contains("`account-1` nothing yet", selection.SaidLine);
    }

    [Fact]
    public async Task A_reading_is_asked_again_only_once_it_is_older_than_fresh_and_an_unknown_one_waits_as_long()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]));
        Answers("account-1", 0.10, 0.10);
        _answers["account-2"] = null;
        var roster = Roster();

        await StartAsync(roster);
        _now += CodexUsage.Fresh - TimeSpan.FromSeconds(1);
        await StartAsync(roster);
        Assert.Equal(2, Asked().Length);

        _now += TimeSpan.FromSeconds(2);
        await StartAsync(roster);
        Assert.Equal(4, Asked().Length);

        // A restart reads the reading's age from `windows.json`, so a fresh one is not asked again.
        await StartAsync(Roster());
        Assert.Equal(["fake/account-2"], Asked().Skip(4));
    }

    /// <summary>
    /// A start, then a look that finds its session ended: a start chosen on an account is one of its sessions until a look
    /// (TOOL6b), and an account a session runs on is not asked (TOOL6g).
    /// </summary>
    private async Task StartAsync(HarnessRoster roster)
    {
        var selection = await roster.SelectAsync("fake-door", Config, null, null);
        Assert.True(selection.Allowed, selection.Refusal);
        roster.Look([], roster.Mark());
    }

    [Fact]
    public async Task An_account_a_start_was_just_chosen_on_is_not_asked_until_a_look_finds_its_session_ended()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]));
        Answers("account-1", 0.10, 0.10);
        Answers("account-2", 0.20, 0.20);
        var roster = Roster();

        var first = await roster.SelectAsync("fake-door", Config, null, null);
        _now += CodexUsage.Fresh + TimeSpan.FromMinutes(1);
        await roster.SelectAsync("fake-door", Config, null, null);

        Assert.Equal(["fake/account-1", "fake/account-2"], Asked().Take(2).Order(StringComparer.Ordinal));
        Assert.DoesNotContain($"fake/{first.Profile}", Asked().Skip(2));
    }

    [Fact]
    public async Task Nothing_is_asked_of_a_cooling_a_key_a_signed_out_or_a_busy_account_nor_of_one_outside_the_list()
    {
        Accounts("cooling", "out", "busy", "listed", "outside");
        // An account that is a key is not a plan's, so it has no windows to read.
        var keyed = HarnessKeys.Add(_home, "fake", "sk-test-0000");
        Wire(s => s.WithRotation("fake", ["cooling", keyed, "out", "busy", "listed"]));
        AccountCooling.Cool(_home, new CoolingEntry("fake", "cooling", _now.AddHours(2), true, "session", _now, "s0"), _now);
        AccountReads.Keep(_home, "fake", "out", LoginState.Out, _now);
        var roster = Roster();
        roster.Look([new SessionStarted("fake-door", "busy", _now.AddMinutes(-3), Running: true)], roster.Mark());

        await roster.SelectAsync("fake-door", Config, null, null);

        Assert.Equal(["fake/listed"], Asked());
    }

    [Fact]
    public async Task A_look_a_panel_and_a_pick_ask_nothing_and_an_agent_whose_door_carries_its_windows_asks_nothing()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]).WithRotation("quiet", ["account-1"]));
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "quiet", "account-1"));
        var roster = Roster();

        roster.Next("fake", null);
        await roster.WiringAsync("fake-door", Config, null);
        await roster.SelectAsync("fake-door", Config, null, chosen: "account-2");
        await roster.SelectAsync("quiet", DriverConfig.Empty with { Adapter = "quiet" }, null, null);

        Assert.Empty(Asked());
    }

    [Fact]
    public async Task A_press_reads_every_account_s_windows_and_an_account_s_press_reads_that_one()
    {
        Accounts("account-1", "account-2");
        Answers("account-1", 0.20, 0.30);
        Answers("account-2", 0.40, 0.50);
        var roster = Roster();

        await roster.ReportAsync("fake-door", Config, refresh: true, account: "account-2");
        Assert.Equal(["fake/account-2"], Asked());

        await roster.ReportAsync("fake-door", Config, refresh: true);
        Assert.Equal(["fake/account-1", "fake/account-2"], Asked().Skip(1).Order(StringComparer.Ordinal));
        Assert.Equal(0.30, AccountWindows.SaidOf(_home, "fake", "account-1", _now)!.Of("weekly")!.Used);

        // A look reads what was kept and asks nothing.
        await roster.ReportAsync("fake-door", Config);
        Assert.Equal(3, Asked().Length);
    }

    [Fact]
    public void The_question_is_its_owner_s_through_its_door_and_an_agent_speaks_where_either_its_door_or_its_server_says()
    {
        var adapters = AdapterSet.Built();
        var roster = new HarnessRoster(adapters, Path.Combine(Path.GetTempPath(), "daoris-codexuse-unused", "harnesses.json"));

        Assert.Equal(
            ["codex-acp"],
            adapters.Names.Where(name => adapters.Resolve(name).Toolchain is { Usage: not null }).Order(StringComparer.Ordinal));
        Assert.Same(CodexUsage.Question, roster.UsageOf("codex-acp"));
        Assert.Null(roster.UsageOf("claude-code"));
        Assert.True(roster.Speaks("codex-acp"));
        Assert.True(roster.Speaks("claude-code-acp"));
        Assert.False(roster.Speaks("dsh"));
    }
}
