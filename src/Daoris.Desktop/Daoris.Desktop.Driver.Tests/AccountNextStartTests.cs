using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TOOL6e (D130 §3–§4, D125 §3.7) through <see cref="HarnessRoster.Next"/>: which account the next start of a scope would
/// take and why, read from the walk's own pieces — the scope <see cref="HarnessRoster.SelectAsync"/> reads, what it knows of
/// each account, its order, each account's cool-off and refusal — so the screen and the start never disagree, and asking
/// counts nothing.
/// </summary>
/// <remarks>
/// Nothing here starts a process: the agent is present by a file look on a command this test writes, as in
/// <see cref="AccountRotationGoalTests"/>, and <see cref="HarnessRoster.Next"/> asks nobody anything.
/// </remarks>
public sealed class AccountNextStartTests : IDisposable
{
    private static readonly DateTimeOffset Seen = new(2026, 10, 3, 15, 0, 0, TimeSpan.FromMinutes(345));

    private static readonly DateTimeOffset Until = new(2026, 10, 3, 16, 2, 0, TimeSpan.FromMinutes(345));

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-next-" + Guid.NewGuid().ToString("N")[..8]);

    private DateTimeOffset _now = Seen;

    public AccountNextStartTests()
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

    private HarnessToolchain Toolchain(string? accountOf = null) => new(
        Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true,
        AccountOf: accountOf, WeekFixed: true);

    private HarnessRoster Roster() =>
        new(new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["fake"] = new Adapter("fake", Toolchain()),
            ["fake-door"] = new Adapter("fake-door", Toolchain(accountOf: "fake")),
        }), Settings)
        {
            Clock = () => _now,
            Zone = TimeZoneInfo.Utc,
        };

    private static DriverConfig Config => DriverConfig.Empty with { Adapter = "fake" };

    private void Accounts(params string[] names)
    {
        foreach (var name in names) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", name));
    }

    private void Wire(Func<HarnessSettings, HarnessSettings> edit) => edit(new HarnessSettings()).Save(Settings);

    private void Cool(string? account, DateTimeOffset until) =>
        AccountCooling.Cool(_home, new CoolingEntry("fake", account, until, true, "weekly", _now.AddHours(-1), "s1"), _now);

    private static string Holds(NextStart next) =>
        string.Join(' ', next.Others.Select(held => $"{held.Account ?? "own"}={held.Hold}"));

    [Fact]
    public async Task The_next_start_takes_what_the_screen_says_and_asking_counts_nothing()
    {
        Accounts("account-1", "account-2", "account-3");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2", "account-3"]));
        var roster = Roster();
        roster.Look(
        [
            new SessionStarted("fake", "account-1", Seen.AddMinutes(-5), Running: true),
            // A door's session runs on its owner's account (AGT7).
            new SessionStarted("fake-door", "account-2", Seen.AddMinutes(-4), Running: true),
        ], roster.Mark());

        var said = roster.Next("fake", null);
        Assert.Equal(said, roster.Next("fake", null), new NextComparer());

        Assert.Equal(("account-3", NextReason.Fewest, "account-1"), (said.Account, said.Reason, said.Over));
        Assert.Equal("account-1=Ready account-2=Ready", Holds(said));
        var started = await roster.SelectAsync("fake", Config, null, null);
        Assert.Equal(said.Account, started.Profile);
        Assert.Equal(WalkStep.Fewest, started.Choice!.Step);
    }

    [Fact]
    public void The_owner_s_case_the_default_out_of_its_cool_off_and_started_on_least_recently()
    {
        Accounts("account-1", "account-2", "account-3");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2", "account-3"], "work").WithWorkspaceDefault("work", "fake", "account-1"));
        Cool("account-1", Until);
        var roster = Roster();
        roster.Look(
        [
            new SessionStarted("fake", "account-1", Seen.AddDays(-2), Running: false),
            new SessionStarted("fake", "account-2", Seen.AddHours(-2), Running: false),
            new SessionStarted("fake", "account-3", Seen.AddHours(-1), Running: false),
        ], roster.Mark());

        var cooling = roster.Next("fake", "work");
        Assert.Equal(("account-2", NextReason.LeastRecent), (cooling.Account, cooling.Reason));
        Assert.Equal(new AccountHeld("account-1", NextHold.Cooling, Until), cooling.Others[0]);

        _now = Until.AddMinutes(1);
        var offered = roster.Next("fake", "work");
        Assert.Equal(("account-1", NextReason.LeastRecent, "account-2"), (offered.Account, offered.Reason, offered.Over));
        Assert.Equal(Until, Assert.Single(AccountCooling.Offered(_home, _now)).Until);
    }

    [Fact]
    public void A_workspace_s_own_list_is_its_own_and_the_machine_s_accounts_are_not_used_there()
    {
        Accounts("account-1", "account-2", "account-3");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]).WithRotation("fake", ["account-3"], "work"));
        var roster = Roster();

        var work = roster.Next("fake", "work");
        Assert.Equal(("account-3", NextReason.Named), (work.Account, work.Reason));
        Assert.Equal("account-1=Outside account-2=Outside", Holds(work));

        // A workspace with nothing of its own reads this machine's scope.
        var lab = roster.Next("fake", "lab");
        Assert.Equal(("account-1", NextReason.List), (lab.Account, lab.Reason));
        Assert.Equal("account-2=Ready account-3=Outside", Holds(lab));
    }

    [Fact]
    public void A_refused_account_is_held_and_the_others_carry_the_start()
    {
        Accounts("account-1", "account-2");
        Wire(s => s.WithRotation("fake", ["account-1", "account-2"]));
        var roster = Roster();
        roster.Refuse("fake-door", "account-1", "refused");

        var next = roster.Next("fake", null);

        Assert.Equal(("account-2", NextReason.OnlyReady), (next.Account, next.Reason));
        Assert.Equal("account-1=Refused", Holds(next));
    }

    [Fact]
    public void Nothing_named_runs_on_the_tool_s_own_sign_in_and_waits_out_its_cool_off()
    {
        Accounts("account-1");
        var roster = Roster();

        var own = roster.Next("fake", null);
        Assert.Equal(((string?)null, NextReason.Own), (own.Account, own.Reason));
        Assert.Equal("account-1=Outside", Holds(own));

        Cool(null, Until);
        var waits = roster.Next("fake", null);
        Assert.Equal(((string?)null, NextReason.Waits, (DateTimeOffset?)Until), (waits.Account, waits.Reason, waits.When));
        Assert.Equal("own=Cooling account-1=Outside", Holds(waits));
    }

    /// <summary>Two answers are one when they take the same account for the same reason, holding the others alike.</summary>
    private sealed class NextComparer : IEqualityComparer<NextStart>
    {
        public bool Equals(NextStart? x, NextStart? y) =>
            x is not null && y is not null && x.Account == y.Account && x.Reason == y.Reason && x.Over == y.Over && x.When == y.When
            && x.Others.SequenceEqual(y.Others);

        public int GetHashCode(NextStart obj) => HashCode.Combine(obj.Account, obj.Reason);
    }
}
