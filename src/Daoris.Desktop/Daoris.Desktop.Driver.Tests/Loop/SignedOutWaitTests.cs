using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// UX6d1 (D150 §6.2, D130 §3.3, D125's TOOL6g note): a start held on accounts not signed in with none cooling waits for a
/// person, not a time, and the look reports it as a wait as it reports a cool-off's: no time, its signed-out accounts named,
/// an ask's intake among what it holds. Before, a wait was made only from a cool-off and a consideration names quests
/// alone, so an intake held so was the machine log's <c>starts.waiting</c> line and nothing else, and *What needs you*
/// could not list it.
/// </summary>
/// <remarks>
/// Nothing here starts a process. The agent is present by a file look (<see cref="HarnessToolchain.ProbeByPresence"/>), as
/// <c>AccountNamesSaidTests</c>' walk is; its accounts read signed out within the hour, so a look asks none of them
/// (ROSTER1); the quest's repository opens a tree per session, so its start asks git nothing before its selection holds it;
/// and the service's doors stand in (<see cref="StandInLedger"/>), its asks among them.
/// </remarks>
public sealed class SignedOutWaitTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Now = new(2026, 10, 4, 8, 58, 0, TimeSpan.FromMinutes(345));

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-signed-out-wait-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly AdapterSet _adapters;

    public SignedOutWaitTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
        _ledger.Register("engine", Path.Combine(_home, "engine"));
        foreach (var account in new[] { "account-1", "account-2" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", account));
        }

        new HarnessSettings().WithDefault("fake", "account-1").WithRotation("fake", ["account-1", "account-2"]).Save(Settings);
        _adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["fake"] = new Adapter("fake", new HarnessToolchain(
                Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true)),
        });
    }

    public void Dispose()
    {
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Settings => Path.Combine(_home, "harnesses.json");

    private string Command => Path.Combine(_home, "agent-here");

    private sealed class Adapter(string name, HarnessToolchain? toolchain) : ISessionAdapter
    {
        public string Name => name;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    private static DriverConfig Config => DriverConfig.Empty with
    {
        Drivable = ["engine"],
        Trees = ["engine"],
        Adapter = "fake",
        IntakeAdapter = "fake",
        Strikes = 1,
        PollSeconds = 1,
    };

    /// <summary>A roster whose every account of the list read signed out a moment ago (TOOL6g), and none cools.</summary>
    private HarnessRoster SignedOut()
    {
        var roster = new HarnessRoster(_adapters, Settings) { Clock = () => Now, Zone = Zone };
        roster.SignedOut("fake", "account-1");
        roster.SignedOut("fake", "account-2");
        return roster;
    }

    private Daoris.Driver.Driver Driver(ServiceClient service, HarnessRoster roster) =>
        new(service, Config, _adapters, _home, harnesses: roster);

    private const string NoneReady =
        "no `fake` account this start may use is ready: `account-1` is not signed in, `account-2` is not signed in.";

    // ——— The look reports the hold as a wait.

    [Fact]
    public async Task An_intake_held_on_signed_out_accounts_with_none_cooling_is_a_wait_with_no_time_naming_them()
    {
        using var service = _ledger.Client();
        _ledger.Ask("a1");

        var look = await Driver(service, SignedOut()).TickAsync().WaitAsync(Bound);

        Assert.Empty(look.Considerations);
        Assert.False(look.Progressed);
        Assert.Empty(_ledger.Sessions);
        var wait = Assert.Single(look.Waits);
        Assert.Equal(("fake", "fake", (string?)null, "default"), (wait.Adapter, wait.Agent, wait.Account, wait.Workspace));
        Assert.Equal(((DateTimeOffset?)null, false, (string?)null), (wait.Until, wait.Stated, wait.Name));
        Assert.Empty(wait.Quests);
        Assert.Equal(["a1"], wait.Asks);
        Assert.Equal(["ask #a1"], wait.Repositories);
        Assert.Equal(["account-1", "account-2"], wait.SignedOut);
        Assert.StartsWith(NoneReady, wait.Sentence);
    }

    [Fact]
    public async Task A_quest_and_an_intake_held_alike_are_one_wait_and_the_quest_s_consideration_still_names_its_accounts()
    {
        using var service = _ledger.Client();
        _ledger.Publish("q1", "engine");
        _ledger.Ask("a1");

        var look = await Driver(service, SignedOut()).TickAsync().WaitAsync(Bound);

        var sitting = Assert.Single(look.Considerations);
        Assert.Equal(StartVerdict.Blocked, sitting.Verdict);
        Assert.StartsWith(NoneReady, sitting.Reason);
        Assert.Equal(["account-1", "account-2"], sitting.SignedOut!.Accounts);
        var wait = Assert.Single(look.Waits);
        Assert.Null(wait.Until);
        Assert.Equal(["q1"], wait.Quests);
        Assert.Equal(["a1"], wait.Asks);
        Assert.Equal(["engine", "ask #a1"], wait.Repositories);
        Assert.Equal(["account-1", "account-2"], wait.SignedOut);
        // The quest's own sentence, as a cool-off's wait says its first quest's.
        Assert.Equal(sitting.Reason, wait.Sentence);
    }

    [Fact]
    public async Task The_log_still_says_the_wait_once_with_no_time_however_many_looks_it_lasts()
    {
        using var service = _ledger.Client();
        var lines = new List<AccountLine>();
        service.AccountLined += lines.Add;
        var roster = SignedOut();
        _ledger.Ask("a1");

        for (var look = 0; look < 3; look++) await Driver(service, roster).TickAsync().WaitAsync(Bound);

        var line = Assert.Single(lines, l => l.Event == "starts.waiting");
        var data = line.Data.ToDictionary(field => field.Key, field => field.Value);
        Assert.Equal(
            ("fake", null, "default", null, 1, "account-1,account-2"),
            (data["adapter"], data["account"], data["workspace"], data["until"], data["quests"], data["signedOut"]));
    }

    // ——— What reads a wait tells a signed-out one from a cool-off.

    private static AccountWait Waiting(params string[] signedOut) =>
        new("fake", "fake", null, "default", null, false, NoneReady)
        {
            Quests = ["q1"],
            Repositories = ["engine"],
            SignedOut = signedOut,
        };

    private static AccountWait OwnCooling(DateTimeOffset until) =>
        new("fake", "fake", null, "default", until, true, $"`fake`'s own sign-in is cooling until {until:MMM d}.")
        {
            Quests = ["q2"],
            Repositories = ["engine"],
        };

    private static TickReport Look(params AccountWait[] waits) => new([], [], Progressed: false) { Waits = waits };

    [Fact]
    public void The_attention_watch_says_a_signed_out_wait_once_per_set_of_accounts()
    {
        var watch = new AttentionWatch();

        Assert.Empty(watch.Observe(Look()));
        var said = Assert.Single(watch.Observe(Look(Waiting("account-1", "account-2"))));
        Assert.Empty(watch.Observe(Look(Waiting("account-2", "account-1"))));
        var another = Assert.Single(watch.Observe(Look(Waiting("account-1"))));

        Assert.Equal((AttentionKind.Waiting, "engine — waits for an account", NoneReady), (said.Kind, said.Headline, said.Detail));
        Assert.Equal(AttentionKind.Waiting, another.Kind);
    }

    [Fact]
    public void A_signed_out_wait_and_a_cool_off_of_the_own_sign_in_are_said_apart_and_neither_again()
    {
        var watch = new AttentionWatch();
        var until = Now.AddDays(2);
        watch.Observe(Look());

        Assert.Equal(2, watch.Observe(Look(Waiting("account-1"), OwnCooling(until))).Count);
        Assert.Empty(watch.Observe(Look(Waiting("account-1"), OwnCooling(until))));
        Assert.Empty(watch.Observe(Look(OwnCooling(until), Waiting("account-1"))));
    }

    [Fact]
    public void Words_a_signed_out_wait_holds_wait_in_its_sentence_never_until_a_reset()
    {
        var quest = new QuestView("q1", "game", "engine", "The work of #q1", "Stand-in work.", "Open");
        var verdict = new Consideration(quest, StartVerdict.Blocked, NoneReady);

        var held = WordsHold.Of(verdict, Waiting("account-1", "account-2"));

        Assert.Equal((WordsHold.Waits, NoneReady, (DateTimeOffset?)null), (held!.Why, held.Reason, held.Until));
        Assert.Equal(WordsHold.Cooling, WordsHold.Of(verdict, OwnCooling(Now))!.Why);
    }
}
