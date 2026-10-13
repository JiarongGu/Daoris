using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SWITCH1c-ii (D165 point 3; the switch design §1.2, §2 and §4): a conversation over its account's usage curve moves on its own,
/// at its turn's end, to another account of the same agent that has room. What the runner judges: a turn just ended with nothing
/// queued, no hand-over waiting or taken, the scope's switch, the account's reading, the others read where the decision asks, and
/// the thirty minutes' hold from the record's <c>created</c> where it moved by the curve. The real client over an in-process ledger
/// and a stand-in agent whose usage question answers in memory, so nothing is spawned: the fast half. A conversation that ends and
/// another that starts are <c>CurveMoveProcessTests</c>'.
/// </summary>
public sealed class CurveMoveTests : IDisposable
{
    /// <summary>Ten minutes after the ledger's records were created (<c>2026-10-03T08:00:00Z</c>).</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 8, 10, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-curve-move-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    private DateTimeOffset _now = Now;

    private readonly List<string> _asked = [];

    // What each account's usage question answers, or null where it cannot be had.
    private readonly Dictionary<string, IReadOnlyList<WindowReading>?> _answers = new(StringComparer.OrdinalIgnoreCase);

    public CurveMoveTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
        File.WriteAllText(Command, "");
        foreach (var account in new[] { "account-1", "account-2", "account-3" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "talk", account));
        }

        new HarnessSettings().WithRotation("talk", ["account-1", "account-2", "account-3"]).Save(Settings);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Settings => Path.Combine(_home, "harnesses.json");

    private string Command => Path.Combine(_home, "talk-agent");

    /// <summary>
    /// A conversation's agent on the protocol door with accounts and a usage question, answered in memory: nothing is probed or
    /// spawned. It refuses to prepare a conversation, after its record is open, so a start ends that record failed.
    /// </summary>
    private sealed class Talk(string name, HarnessToolchain toolchain) : ISessionAdapter
    {
        public string Name => name;

        public SessionWire Wire => SessionWire.Acp;

        public bool Interactive => true;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("a judgement test spawns nothing");

        public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("a judgement test spawns nothing");
    }

    private HarnessRoster Roster()
    {
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["talk"] = new Talk("talk", new HarnessToolchain(
                Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "TALK_HOME", ProbeByPresence: true,
                Usage: new UsageQuestion("talk", ["talk-agent", "usage"], [], UsageKind.Claude))),
        });
        return new HarnessRoster(adapters, Settings)
        {
            Clock = () => _now,
            AskingUsage = (owner, account, _) =>
            {
                lock (_asked) _asked.Add($"{owner}/{account}");
                return Task.FromResult(_answers.GetValueOrDefault(account));
            },
        };
    }

    private string[] Asked()
    {
        lock (_asked) return [.. _asked];
    }

    /// <summary>A week half gone, and five hours with an hour left: the smart curve allows 70% of the week now, and 90% of the five hours.</summary>
    private IReadOnlyList<WindowReading> Windows(double weekly, double session = 0.10) =>
        [new WindowReading("session", session, _now.AddHours(1)), new WindowReading("weekly", weekly, _now.AddDays(3.5))];

    /// <summary>What an account said of its windows, <paramref name="secondsAgo"/> before now.</summary>
    private void Said(string account, double weekly, int secondsAgo = 30) =>
        AccountWindows.Said(_home, "talk", account, Windows(weekly), _now.AddSeconds(-secondsAgo), session: null, source: AccountWindows.SourceRead);

    [Fact]
    public void The_scope_s_accounts_are_candidates_with_what_each_said_and_whether_the_walk_finds_it_ready()
    {
        var roster = Roster();
        Said("account-3", 0.30);
        AccountCooling.Cool(_home, new CoolingEntry("talk", "account-2", _now.AddHours(2), Stated: true, "weekly", _now, Session: null), _now);

        var candidates = roster.SwitchCandidates("talk", ["account-1", "account-2", "account-3", "account-4"]);

        Assert.Equal(
            [("account-1", false, true), ("account-2", false, false), ("account-3", true, true), ("account-4", false, false)],
            candidates.Select(each => (each.Account, each.Said is not null, each.Ready)));
        Assert.Equal(0.30, candidates[2].Said!.Of("weekly")!.Used);
    }

    /// <summary>
    /// The read at a turn's end (the switch design §1.2): each ready account whose reading is older than the question's minute, never
    /// one cooling, and the same account not again within that minute, read or not.
    /// </summary>
    [Fact]
    public async Task The_read_at_a_turn_s_end_asks_each_ready_stale_account_once_a_minute()
    {
        var roster = Roster();
        Said("account-3", 0.30, secondsAgo: 120);
        Said("account-1", 0.30, secondsAgo: 10);
        AccountCooling.Cool(_home, new CoolingEntry("talk", "account-2", _now.AddHours(2), Stated: true, "weekly", _now, Session: null), _now);
        _answers["account-3"] = Windows(0.20);

        await roster.ReadUsageAsync("talk", "talk", ["account-1", "account-2", "account-3"], DriverConfig.Empty);
        await roster.ReadUsageAsync("talk", "talk", ["account-1", "account-2", "account-3"], DriverConfig.Empty);

        Assert.Equal(["talk/account-3"], Asked());
        Assert.Equal(0.20, AccountWindows.SaidOf(_home, "talk", "account-3", _now)!.Of("weekly")!.Used);
    }

    /// <summary>🔴 The hold reads when a record was made (SWITCH1c-ii): `created`, as the ledger answers it, on the record a run reads.</summary>
    [Fact]
    public async Task A_record_says_when_it_was_created()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("c0nv0001", "working", "talk", _root, profile: "account-1");
        using var client = ledger.Client();

        var record = (await client.RecordAsync("c0nv0001"))!;

        Assert.Equal(new DateTimeOffset(2026, 10, 3, 8, 0, 0, TimeSpan.Zero), record.Created);
    }
}
