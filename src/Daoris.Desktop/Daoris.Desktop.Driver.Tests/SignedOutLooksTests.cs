using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TOOL6g through real looks: the owner's install, where two accounts read signed out and the third cooled, held a start at
/// every look, and every look asked the agent again about every account and the person's own sign-in. Watched on the
/// install: the status question three times in 45 seconds while two starts were refused, none once both were paused.
/// </summary>
/// <remarks>
/// Over the real client and planner with the service's doors standing in (<see cref="StandInLedger"/>), as
/// <c>AccountRotationHoldTests</c> is, with the stub's command a script that answers its version and its status question
/// and writes each to a log. A probe of the agent asks its version once, so the version lines count the probes; a status
/// line with no account home is the tool's own sign-in. Nothing is spawned beyond those questions: the start is held.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class SignedOutLooksTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private const int Looks = 5;

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-signed-out-looks-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly DateTimeOffset _now = new(2026, 10, 4, 8, 58, 0, TimeSpan.FromMinutes(345));

    public SignedOutLooksTests()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Path.Combine(_home, "engine"));
        foreach (var account in new[] { "account-1", "account-2", "gmail" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "stub", account));
        }

        new HarnessSettings().WithDefault("stub", "account-1").WithRotation("stub", ["account-1", "account-2", "gmail"])
            .Save(Path.Combine(_home, "harnesses.json"));
        AccountCooling.Cool(_home, new CoolingEntry("stub", "gmail", _now.AddDays(2), true, "weekly", _now, "s0"), _now);
        File.WriteAllText(Script, """
            import fs from 'node:fs';
            import path from 'node:path';
            const log = process.argv[2];
            const home = process.env.DAORIS_STUB_CONFIG_DIR;
            if (process.argv.includes('--version')) { fs.appendFileSync(log, 'version\n'); console.log('stub 1.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) {
              fs.appendFileSync(log, 'status ' + (home ? path.basename(home) : '(own)') + '\n');
              console.log('logged-out');
              process.exit(0);
            }
            process.exit(1);
            """);
    }

    public void Dispose()
    {
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Script => Path.Combine(_home, "stub.mjs");

    private string Log => Path.Combine(_home, "asked.log");

    private DriverConfig Config => DriverConfig.Empty with
    {
        Drivable = ["engine"],
        Trees = ["engine"],
        Adapter = "stub",
        Strikes = 1,
        PollSeconds = 1,
        Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Script, Log] },
    };

    private IReadOnlyList<string> Asked => File.Exists(Log) ? File.ReadAllLines(Log) : [];

    [Fact]
    public async Task A_start_held_on_signed_out_accounts_asks_the_agent_once_not_at_every_look_and_never_the_own_sign_in()
    {
        using var service = _ledger.Client();
        var roster = new HarnessRoster(AdapterSet.Built(), Path.Combine(_home, "harnesses.json")) { Clock = () => _now, Zone = Zone };
        _ledger.Publish("q1", "engine");

        TickReport? last = null;
        for (var look = 0; look < Looks; look++)
        {
            last = await new Daoris.Driver.Driver(service, Config, AdapterSet.Built(), _home, harnesses: roster).TickAsync().WaitAsync(Bound);
        }

        var sitting = Assert.Single(last!.Considerations);
        Assert.Equal(StartVerdict.Blocked, sitting.Verdict);
        // One probe in five looks: its version asked once, and each account's status once.
        Assert.Equal(1, Asked.Count(line => line == "version"));
        Assert.Equal(["status account-1", "status account-2", "status gmail"], Asked.Where(line => line.StartsWith("status ", StringComparison.Ordinal)).Order());
        Assert.DoesNotContain("status (own)", Asked);

        // And the hold says which accounts are not signed in, and the sign-in for each, beside the wait on gmail.
        Assert.Equal(
            $"no `stub` account this start may use is ready: `account-1` is not signed in, `account-2` is not signed in, `gmail` "
            + $"is cooling until Oct 6, 08:58 ({Zone.Id}); the first ready, `gmail`, at Oct 6, 08:58 ({Zone.Id}), as the agent "
            + "said. Daoris starts nothing on them until then. A sign-in starts it sooner: `daoris agent login stub --profile "
            + "account-1`, `daoris agent login stub --profile account-2`, or Settings → Agents.",
            sitting.Reason);
        Assert.Equal("stub", sitting.SignedOut!.Agent);
        Assert.Equal(["account-1", "account-2"], sitting.SignedOut.Accounts);
        var wait = Assert.Single(last.Waits);
        Assert.Equal("gmail", wait.Account);
        Assert.Equal(["account-1", "account-2"], wait.SignedOut);
    }

    [Fact]
    public async Task With_nothing_cooling_every_account_not_signed_in_is_named_and_the_log_says_so_once()
    {
        AccountCooling.End(_home, "stub", "gmail", _now);
        using var service = _ledger.Client();
        var lines = new List<AccountLine>();
        service.AccountLined += lines.Add;
        var roster = new HarnessRoster(AdapterSet.Built(), Path.Combine(_home, "harnesses.json")) { Clock = () => _now, Zone = Zone };
        _ledger.Publish("q1", "engine");

        TickReport? last = null;
        for (var look = 0; look < 3; look++)
        {
            last = await new Daoris.Driver.Driver(service, Config, AdapterSet.Built(), _home, harnesses: roster).TickAsync().WaitAsync(Bound);
        }

        var sitting = Assert.Single(last!.Considerations);
        Assert.Equal(StartVerdict.Blocked, sitting.Verdict);
        Assert.Equal(
            "no `stub` account this start may use is ready: `account-1` is not signed in, `account-2` is not signed in, `gmail` is "
            + "not signed in. A sign-in starts it: `daoris agent login stub --profile account-1`, `daoris agent login stub "
            + "--profile account-2`, `daoris agent login stub --profile gmail`, or Settings → Agents.",
            sitting.Reason);
        Assert.Equal(["account-1", "account-2", "gmail"], sitting.SignedOut!.Accounts);
        Assert.Empty(last.Waits);

        // Written once in three looks, with no time and the accounts by name.
        var line = Assert.Single(lines, l => l.Event == "starts.waiting");
        var data = line.Data.ToDictionary(field => field.Key, field => field.Value);
        Assert.Equal(("stub", null, null, "account-1,account-2,gmail"), (data["adapter"], data["account"], data["until"], data["signedOut"]));
    }

    [Fact]
    public async Task A_sign_in_at_the_terminal_is_asked_about_at_the_next_look_that_account_alone()
    {
        using var service = _ledger.Client();
        var roster = new HarnessRoster(AdapterSet.Built(), Path.Combine(_home, "harnesses.json")) { Clock = () => _now, Zone = Zone };
        _ledger.Publish("q1", "engine");
        await new Daoris.Driver.Driver(service, Config, AdapterSet.Built(), _home, harnesses: roster).TickAsync().WaitAsync(Bound);
        var before = Asked.Count;

        // The terminal's `daoris agent login stub --profile account-2` marks its sign-in when it ends, a minute on by the
        // roster's clock.
        ProbeLock.MarkSignedIn(HarnessSettings.ProfileHome(_home, "stub", "account-2"), _now);
        File.SetLastWriteTimeUtc(ProbeLock.SignedInPathOf(_home, "stub", "account-2"), _now.AddMinutes(1).UtcDateTime);
        await new Daoris.Driver.Driver(service, Config, AdapterSet.Built(), _home, harnesses: roster).TickAsync().WaitAsync(Bound);
        await new Daoris.Driver.Driver(service, Config, AdapterSet.Built(), _home, harnesses: roster).TickAsync().WaitAsync(Bound);

        Assert.Equal(["status account-2"], Asked.Skip(before));
    }
}
