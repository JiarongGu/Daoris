using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// USAGEPROBE1 through the roster (D165, D179; D130 point 9): a start's walk asks an account's maker for its windows only at
/// the set points, an account never read, one unread for the interval, one whose projection nears its curve, and one a limit or
/// a credit's word was said of; otherwise it ranks on what the readings already say and starts without a probe. The probes are
/// counted through the in-process stand-in, as <c>CodexUsageRosterTests</c> counts them.
/// </summary>
/// <remarks>Nothing here starts a process: the agent is present by a file look, and its server is a stand-in.</remarks>
public sealed class UsageProbeRosterTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-usageprobe-" + Guid.NewGuid().ToString("N")[..8]);

    private DateTimeOffset _now = Start;

    private readonly List<string> _asked = [];

    private readonly Dictionary<string, IReadOnlyList<WindowReading>?> _answers = new(StringComparer.OrdinalIgnoreCase);

    public UsageProbeRosterTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Command => Path.Combine(_home, "agent-here");

    private sealed class Adapter(string name, HarnessToolchain? toolchain) : ISessionAdapter
    {
        public string Name => name;

        public SessionWire Wire => SessionWire.Acp;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    private static readonly UsageQuestion Question = new("fake", ["fake-agent", "app-server"], CodexUsage.Question.Recorded);

    private HarnessRoster Roster(string? home = null) =>
        new(new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["fake"] = new Adapter("fake", new HarnessToolchain(
                Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true)),
            ["fake-door"] = new Adapter("fake-door", new HarnessToolchain(
                Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true,
                AccountOf: "fake", Usage: Question, Limits: ClaudeLimits.Words)),
        }), DaorisHome.Entry(home ?? _home, HomeEntry.Harnesses))
        {
            Clock = () => _now,
            AskingUsage = (owner, account, _) =>
            {
                lock (_asked) _asked.Add($"{owner}/{account}");
                return Task.FromResult(_answers.GetValueOrDefault(account));
            },
        };

    private static DriverConfig Config => DriverConfig.Empty with { Adapter = "fake-door" };

    private void Accounts(string home, params string[] names)
    {
        foreach (var name in names) Directory.CreateDirectory(HarnessSettings.ProfileHome(home, "fake", name));
        new HarnessSettings().WithRotation("fake", [.. names]).Save(DaorisHome.Entry(home, HomeEntry.Harnesses));
    }

    private string[] Asked()
    {
        lock (_asked) return [.. _asked];
    }

    private IReadOnlyList<WindowReading> Windows(double session, double weekly, bool credits = false) =>
        [new WindowReading("session", session, Start.AddHours(3), Credits: credits), new WindowReading("weekly", weekly, Start.AddDays(4))];

    /// <summary>A reading kept as a read keeps it, <paramref name="minutesAgo"/> before now.</summary>
    private void Read(string account, double session, double weekly, double minutesAgo, bool credits = false, string? home = null) =>
        AccountWindows.Said(
            home ?? _home, "fake", account, Windows(session, weekly, credits), _now.AddMinutes(-minutesAgo), session: null,
            source: AccountWindows.SourceRead);

    /// <summary>What the account's server answers now.</summary>
    private void Answers(string account, double session, double weekly) => _answers[account] = Windows(session, weekly);

    /// <summary>
    /// The account's five-hour window measured in the usage history: a window that has turned over rose from 10% to 30% on two
    /// million reference-equivalent tokens of Daoris's turns, so one token spends a ten-millionth of the window.
    /// </summary>
    private void Measured(string account)
    {
        var gone = _now.AddHours(-1);
        Kept(account, _now.AddHours(-5), gone, 0.10);
        Kept(account, _now.AddHours(-3), gone, 0.30);
        Spent(account, 2_000_000, minutesAgo: 4 * 60, seq: 1);
    }

    private void Kept(string account, DateTimeOffset at, DateTimeOffset reset, double used) =>
        UsageHistory.Kept(
            _home, new ReadingFrom("fake", account, "fake-door", CodexUsage.Method, 0, at),
            [new WindowSeen("300-minute", "session", null, 300, used, 0.01, reset, UsageHistory.ResetSaid)], at);

    /// <summary>A turn of Daoris's on the account, its door reporting each turn's own tokens, in a session of the account's own.</summary>
    private void Spent(string account, long work, double minutesAgo, long seq)
    {
        var ended = _now.AddMinutes(-minutesAgo);
        UsageHistory.Turned(
            _home, new TurnBinding("fake", account, "fake-door", UsageHistory.PerTurn),
            new TurnSeen($"3f9c2a7{account[^1]}", seq, null, ended, new TurnTokens(work, null, null, null), null, false), ended);
    }

    /// <summary>A start, then a look that finds its session ended, so the next start may ask its account again.</summary>
    private async Task<HarnessSelection> StartAsync(HarnessRoster roster)
    {
        var selection = await roster.SelectAsync("fake-door", Config, null, null);
        Assert.True(selection.Allowed, selection.Refusal);
        roster.Look([], roster.Mark());
        return selection;
    }

    [Fact]
    public async Task A_start_with_fresh_enough_projections_asks_nothing()
    {
        Accounts(_home, "account-1", "account-2");
        Read("account-1", 0.10, 0.20, minutesAgo: 10);
        Read("account-2", 0.20, 0.30, minutesAgo: 10);
        var roster = Roster();

        var selection = await StartAsync(roster);
        // Older than either reader's floor, and still nothing asked a minute later or five.
        _now += CodexUsage.Fresh + TimeSpan.FromMinutes(1);
        await StartAsync(roster);

        Assert.Empty(Asked());
        Assert.Equal("account-1", selection.Profile);
        Assert.StartsWith("What each account said: `account-1` 10 min ago, 10% of its session limit", selection.SaidLine);
    }

    [Fact]
    public async Task A_projection_nearing_its_curve_asks_once_and_the_start_decides_on_the_reading()
    {
        Accounts(_home, "account-1", "account-2");
        Read("account-1", 0.50, 0.20, minutesAgo: 10);
        Read("account-2", 0.60, 0.30, minutesAgo: 10);
        Measured("account-1");
        // 3.5 million since its reading: 50% projected to 85%, within the margin of the smart session's 90.
        Spent("account-1", 3_500_000, minutesAgo: 5, seq: 2);
        Answers("account-1", 0.95, 0.20);
        var roster = Roster();

        var selection = await StartAsync(roster);

        Assert.Equal(["fake/account-1"], Asked());
        // Over its curve by the reading the question had, so passed for the other (D130 point 9: the reading judged it).
        Assert.Equal("account-2", selection.Profile);
        Assert.Equal(WalkStep.Near, selection.Choice?.Step);
        Assert.Equal("`account-1` has used 95% of its session limit, over its curve's 90%", selection.Choice?.Clause);

        // Asked once: the next start, within the reader's floor, rests on that reading.
        await StartAsync(roster);
        Assert.Single(Asked());
    }

    [Fact]
    public async Task A_projection_nearing_its_curve_whose_reading_says_room_starts_on_it_as_the_reading_says()
    {
        Accounts(_home, "account-1", "account-2");
        Read("account-1", 0.50, 0.20, minutesAgo: 10);
        Read("account-2", 0.60, 0.30, minutesAgo: 10);
        Measured("account-1");
        Spent("account-1", 3_500_000, minutesAgo: 5, seq: 2);
        Answers("account-1", 0.55, 0.20);

        var selection = await StartAsync(Roster());

        Assert.Equal(["fake/account-1"], Asked());
        Assert.Equal("account-1", selection.Profile);
        // The reading is newer than every turn: nothing projected is left to say.
        Assert.DoesNotContain("projected", selection.SaidLine);
    }

    [Fact]
    public async Task A_projection_over_its_curve_whose_reading_cannot_be_had_never_makes_the_account_near_and_is_said_projected()
    {
        Accounts(_home, "account-1", "account-2");
        Read("account-1", 0.50, 0.20, minutesAgo: 10);
        Read("account-2", 0.60, 0.30, minutesAgo: 10);
        Measured("account-1");
        Spent("account-1", 5_000_000, minutesAgo: 5, seq: 2);
        _answers["account-1"] = null;

        var selection = await StartAsync(Roster());

        Assert.Equal(["fake/account-1"], Asked());
        // Projected to the whole window, read at 50%: the reading ranks it, and it has the most room by its reading.
        Assert.Equal("account-1", selection.Profile);
        Assert.NotEqual(WalkStep.Near, selection.Choice?.Step);
        Assert.Contains(
            "`account-1` 10 min ago, 50% of its session limit and 20% of its weekly limit used, "
            + "about 100% of its session limit by now, projected from Daoris's turns since;",
            selection.SaidLine);
        var projected = selection.AccountsSaid!.Parts.Single(part => part.Code == NoteCodes.OpeningSaidProjected.Code);
        Assert.Equal((100, "session"), (projected.Value("projected"), projected.Value("window")));
    }

    [Fact]
    public async Task An_account_Daoris_spent_on_since_its_reading_by_what_nothing_weighs_is_asked_and_an_idle_one_is_not()
    {
        Accounts(_home, "account-1", "account-2", "account-3");
        Read("account-1", 0.10, 0.20, minutesAgo: 10);
        Read("account-2", 0.20, 0.30, minutesAgo: 10);
        Read("account-3", 0.30, 0.30, minutesAgo: 10);
        Measured("account-1");
        Measured("account-2");
        // The second's week measured too, beside the same two million: every window a turn since moved is weighed.
        foreach (var (hoursAgo, used) in new[] { (5, 0.10), (3, 0.20) })
        {
            var at = _now.AddHours(-hoursAgo);
            UsageHistory.Kept(
                _home, new ReadingFrom("fake", "account-2", "fake-door", CodexUsage.Method, 0, at),
                [new WindowSeen("10080-minute", "weekly", null, 10080, used, 0.01, Start.AddDays(4), UsageHistory.ResetSaid)], at);
        }

        // A door whose turns' meaning is unknown (Codex's, D179 point 8) ran on the first since its reading; the second had a turn
        // its ratios weigh; the third ran nothing.
        UsageHistory.Turned(
            _home, new TurnBinding("fake", "account-1", "fake-door", UsageHistory.MeaningUnknown),
            new TurnSeen("9b1e7c20", 1, null, _now.AddMinutes(-5), new TurnTokens(1000, 10, null, null), null, false), _now);
        Spent("account-2", 100_000, minutesAgo: 5, seq: 3);
        Answers("account-1", 0.10, 0.20);

        await StartAsync(Roster());

        Assert.Equal(["fake/account-1"], Asked());
    }

    [Fact]
    public async Task A_stale_account_is_asked_once_at_its_interval()
    {
        // The second read later, so the first alone is stale at its interval.
        Accounts(_home, "account-1", "account-2");
        Read("account-1", 0.10, 0.20, minutesAgo: 1);
        Read("account-2", 0.20, 0.30, minutesAgo: 0);
        Answers("account-1", 0.10, 0.20);
        var roster = Roster();

        _now += UsageProjection.Interval - TimeSpan.FromMinutes(2);
        await StartAsync(roster);
        Assert.Empty(Asked());

        _now += TimeSpan.FromMinutes(1);
        await StartAsync(roster);
        Assert.Equal(["fake/account-1"], Asked());

        _now += CodexUsage.Fresh + TimeSpan.FromMinutes(1);
        await StartAsync(roster);
        Assert.Single(Asked(), asked => asked == "fake/account-1");
    }

    /// <summary>
    /// USAGEONE1: a set of one has no choice a reading could change, so its start asks nothing, never read or stale; a
    /// resume held to the tool's own home neither.
    /// </summary>
    [Fact]
    public async Task A_set_of_one_and_a_resume_in_its_own_home_ask_nothing()
    {
        Accounts(_home, "account-1");
        var roster = Roster();

        Assert.Equal("account-1", (await StartAsync(roster)).Profile);
        _now += UsageProjection.Interval + TimeSpan.FromMinutes(1);
        await StartAsync(roster);
        Assert.Empty(Asked());

        // Two accounts, neither read: a resume of a record begun in the tool's own home goes on there, and reads neither.
        Accounts(_home, "account-1", "account-2");
        var resumed = await Roster().ResumeAsync("fake-door", Config, null, account: null, StartKind.Driven);
        Assert.Empty(Asked());
        Assert.True(resumed.Selection.Allowed, resumed.Selection.Refusal);
    }

    [Fact]
    public async Task A_limit_s_sentence_asks_once_its_account_is_ready_again_even_after_a_restart()
    {
        Accounts(_home, "account-1", "account-2");
        Read("account-1", 0.10, 0.20, minutesAgo: 10);
        Read("account-2", 0.20, 0.30, minutesAgo: 10);
        AccountCooling.Cool(_home, new CoolingEntry("fake", "account-1", _now.AddMinutes(5), true, "session", _now, "s0"), _now);

        // Cooling, it is asked nothing: a start on a spent account spends nothing (TOOL4d).
        Assert.Equal("account-2", (await StartAsync(Roster())).Profile);
        Assert.Empty(Asked());

        // Its cool-off over, read from the file by a roster that never heard the limit.
        _now += TimeSpan.FromMinutes(6);
        Answers("account-1", 0.10, 0.20);
        var roster = Roster();
        await StartAsync(roster);
        Assert.Equal(["fake/account-1"], Asked());

        // Its reading is newer than the limit now: asked no more.
        _now += CodexUsage.Fresh + TimeSpan.FromMinutes(1);
        await StartAsync(roster);
        Assert.Single(Asked());
    }

    [Fact]
    public async Task A_limit_heard_then_ended_by_the_person_s_try_now_asks()
    {
        Accounts(_home, "account-1", "account-2");
        Read("account-1", 0.10, 0.20, minutesAgo: 10);
        Read("account-2", 0.20, 0.30, minutesAgo: 10);
        Answers("account-1", 0.10, 0.20);
        var roster = Roster();

        Assert.NotNull(roster.Limited("fake-door", "account-1", "You've hit your weekly limit", "s1"));
        Assert.True(roster.Ready("fake-door", "account-1"));
        await StartAsync(roster);

        Assert.Equal(["fake/account-1"], Asked());
    }

    [Fact]
    public async Task A_credit_s_word_on_its_reading_asks()
    {
        Accounts(_home, "account-1", "account-2");
        Read("account-1", 0.10, 0.20, minutesAgo: 10, credits: true);
        Read("account-2", 0.20, 0.30, minutesAgo: 10);
        Answers("account-1", 0.10, 0.20);

        await StartAsync(Roster());

        Assert.Equal(["fake/account-1"], Asked());
    }

    [Fact]
    public async Task The_walk_s_choices_where_readings_are_fresh_are_unchanged()
    {
        // The same readings, once just asked by the start itself and once read ten minutes before it and asked nothing: the
        // same account, by the same step. None is near its curve, which would be asked in either.
        var other = Path.Combine(_home, "other");
        Directory.CreateDirectory(other);
        (string Account, double Session, double Weekly)[] said = [("account-1", 0.40, 0.40), ("account-2", 0.20, 0.30), ("account-3", 0.10, 0.45)];
        Accounts(_home, [.. said.Select(each => each.Account)]);
        Accounts(other, [.. said.Select(each => each.Account)]);
        foreach (var (account, session, weekly) in said)
        {
            Answers(account, session, weekly);
            Read(account, session, weekly, minutesAgo: 10, home: other);
        }

        var asked = await Roster().SelectAsync("fake-door", Config, null, null);
        Assert.Equal(3, Asked().Length);
        var held = await Roster(other).SelectAsync("fake-door", Config, null, null);
        Assert.Equal(3, Asked().Length);

        Assert.Equal("account-2", asked.Profile);
        Assert.Equal(WalkStep.Pace, asked.Choice?.Step);
        Assert.Equal(asked.Profile, held.Profile);
        Assert.Equal(asked.Choice?.Step, held.Choice?.Step);
        Assert.Equal(asked.Choice?.Clause, held.Choice?.Clause);
    }
}
