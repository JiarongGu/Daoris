using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TOOL6g: the install found every account signed out about ten seconds after the application started, one after another,
/// two seconds apart, each credential file left with empty tokens. The reading under test: several callers missed the probe
/// cache together, so two probes asked one account's status at the same moment, and with its access token expired both
/// refreshed with the same single-use refresh token. One won; the other was refused and signed the account out, wiping
/// the winner's new tokens too.
/// </summary>
/// <remarks>
/// <para>The agent is a script standing in for one that refreshes an expired token when asked its status, as an OAuth
/// client does: it reads the account's credential file, waits as a token request would, then spends the refresh token it
/// read, which succeeds once. The one that spends it first writes the rotated tokens; one that finds it already spent is
/// refused, and signs the account out by writing empty tokens. What the real agent does when asked its status with an
/// expired token was not measured here: the script is the reading, made runnable.</para>
/// <para>The first case is the control: two status questions at once, with nothing of Daoris between them, sign the
/// account out, so the script does reproduce the mechanism. The rest hold Daoris's probe to asking one account at a time,
/// across its callers, across two adapters onto one account, across another process holding the account's lock, and
/// never while a session of Daoris's runs on it.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class ProbeRaceTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-probe-race-" + Guid.NewGuid().ToString("N")[..8]);

    private string Settings => Path.Combine(_home, "harnesses.json");

    private string Script => Path.Combine(_home, "agent.mjs");

    private string Asked => Path.Combine(_home, "asked.log");

    public ProbeRaceTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Script, """
            import fs from 'node:fs';
            import path from 'node:path';
            const args = process.argv.slice(2);
            if (args[0] === '--version') { console.log('agent 1.0'); process.exit(0); }
            if (args[0] === 'auth' && args[1] === 'status') {
              const home = process.env.RACE_CONFIG_DIR;
              const asked = new URL('./asked.log', import.meta.url);
              fs.appendFileSync(asked, (home ? path.basename(home) : '(own)') + ' ' + Date.now() + '\n');
              const file = home && path.join(home, '.credentials.json');
              if (!file || !fs.existsSync(file)) { console.log(JSON.stringify({ loggedIn: false })); process.exit(0); }
              const read = JSON.parse(fs.readFileSync(file, 'utf8')).claudeAiOauth;
              if (!read.refreshToken) { console.log(JSON.stringify({ loggedIn: false })); process.exit(0); }
              if (read.expired) {
                await new Promise((resolve) => setTimeout(resolve, 1500));
                try {
                  fs.writeFileSync(path.join(home, 'spent-' + read.refreshToken), '', { flag: 'wx' });
                } catch {
                  await new Promise((resolve) => setTimeout(resolve, 100));
                  fs.writeFileSync(file, JSON.stringify({ claudeAiOauth: { accessToken: '', refreshToken: '' } }));
                  console.log(JSON.stringify({ loggedIn: false }));
                  process.exit(0);
                }
                const next = 'r' + (Number(read.refreshToken.slice(1)) + 1);
                fs.writeFileSync(file, JSON.stringify({ claudeAiOauth: { accessToken: 'a' + next.slice(1), refreshToken: next, expired: false } }));
              }
              console.log(JSON.stringify({ loggedIn: true, email: 'someone@example.invalid' }));
              process.exit(0);
            }
            console.log('agent: ' + args.join(' '));
            """);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private sealed class Adapter(string name, HarnessToolchain toolchain) : ISessionAdapter
    {
        public string Name => name;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new();

        public HarnessToolchain? Toolchain => toolchain;
    }

    private HarnessToolchain Toolchain(string? accountOf = null) => new(
        Binary: ["node", Script],
        VersionArguments: ["--version"],
        ProfileVariable: "RACE_CONFIG_DIR",
        LoginCheck: new LoginQuestion(["auth", "status"], @"""loggedIn""\s*:\s*true", @"""loggedIn""\s*:\s*false"),
        AccountOf: accountOf);

    /// <summary>The owner, and where asked a door onto it that asks the same question of the same accounts.</summary>
    private HarnessRoster Roster(bool door = false)
    {
        var adapters = new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["agent"] = new Adapter("agent", Toolchain()),
        };
        if (door) adapters["agent-door"] = new Adapter("agent-door", Toolchain(accountOf: "agent"));
        return new HarnessRoster(new AdapterSet(adapters), Settings);
    }

    private static DriverConfig Config => DriverConfig.Empty with { Adapter = "agent" };

    /// <summary>An account signed in with an access token that has expired, so the next status question refreshes it.</summary>
    private string Expired(string account)
    {
        var home = HarnessSettings.ProfileHome(_home, "agent", account);
        Directory.CreateDirectory(home);
        File.WriteAllText(Path.Combine(home, ".credentials.json"),
            JsonSerializer.Serialize(new { claudeAiOauth = new { accessToken = "a1", refreshToken = "r1", expired = true } }));
        return home;
    }

    private static string RefreshToken(string home) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(home, ".credentials.json")))
            .RootElement.GetProperty("claudeAiOauth").GetProperty("refreshToken").GetString()!;

    private int TimesAsked(string account) =>
        File.Exists(Asked) ? File.ReadAllLines(Asked).Count(line => line.StartsWith(account + " ", StringComparison.Ordinal)) : 0;

    [Fact]
    public async Task Two_status_questions_at_once_on_an_expired_account_sign_it_out()
    {
        var home = Expired("account-1");
        var toolchain = Toolchain();

        await Task.WhenAll(
            HarnessProbe.AskAsync(["node", Script], ["auth", "status"], home, toolchain, CancellationToken.None),
            HarnessProbe.AskAsync(["node", Script], ["auth", "status"], home, toolchain, CancellationToken.None));

        Assert.Equal("", RefreshToken(home));
    }

    [Fact]
    public async Task Two_probes_at_once_ask_an_account_once_and_leave_it_signed_in()
    {
        var home = Expired("account-1");
        var roster = Roster();

        var reports = await Task.WhenAll(
            roster.ReportAsync("agent", Config, refresh: true),
            roster.ReportAsync("agent", Config, refresh: true));

        Assert.Equal("r2", RefreshToken(home));
        Assert.All(reports, report => Assert.Equal(LoginState.In, Assert.Single(report!.Profiles).Login));
        Assert.Equal(1, TimesAsked("account-1"));
    }

    [Fact]
    public async Task An_owner_and_a_door_asking_the_same_accounts_never_ask_one_at_the_same_time()
    {
        var home = Expired("account-1");
        var roster = Roster(door: true);

        var reports = await Task.WhenAll(
            roster.ReportAsync("agent", Config, refresh: true),
            roster.ReportAsync("agent-door", Config, refresh: true));

        Assert.Equal("r2", RefreshToken(home));
        Assert.All(reports, report => Assert.Equal(LoginState.In, Assert.Single(report!.Profiles).Login));
    }

    [Fact]
    public async Task A_probe_waits_while_another_process_holds_the_account_s_lock()
    {
        var home = Expired("account-1");
        var held = ProbeLock.PathOf(_home, "agent", "account-1");
        Directory.CreateDirectory(Path.GetDirectoryName(held)!);
        File.WriteAllText(held, "{\"pid\":1}\n");
        var released = DateTimeOffset.UtcNow.AddSeconds(2);
        var release = Task.Run(async () =>
        {
            await Task.Delay(released - DateTimeOffset.UtcNow);
            File.Delete(held);
        });

        var report = await Roster().ReportAsync("agent", Config, refresh: true);
        await release;

        Assert.Equal(LoginState.In, Assert.Single(report!.Profiles).Login);
        var asked = File.ReadAllLines(Asked).Single(line => line.StartsWith("account-1 ", StringComparison.Ordinal));
        Assert.True(long.Parse(asked.Split(' ')[1]) >= released.ToUnixTimeMilliseconds() - 50, "the account was asked while another held its lock");
        Assert.Equal("r2", RefreshToken(home));
    }

    /// <summary>
    /// A start held on a signed-out account is tried at every look, and each try asked every account's status again, and
    /// the person's own sign-in's. Now the person's press reads each account once (ROSTER1: a look reads none), a start
    /// held on one asks nothing until a sign-in marks the account, then that account alone, and the hour's backstop asks it
    /// once more; the own sign-in is never the loop's to ask.
    /// </summary>
    [Fact]
    public async Task A_signed_out_account_is_asked_again_on_its_own_once_per_sign_out()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "agent", "account-1"));
        var two = HarnessSettings.ProfileHome(_home, "agent", "account-2");
        Directory.CreateDirectory(two);
        File.WriteAllText(Path.Combine(two, ".credentials.json"),
            JsonSerializer.Serialize(new { claudeAiOauth = new { accessToken = "a1", refreshToken = "r1", expired = false } }));
        new HarnessSettings().WithDefault("agent", "account-1").Save(Settings);
        var now = DateTimeOffset.UtcNow;
        var roster = new HarnessRoster(new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["agent"] = new Adapter("agent", Toolchain()),
        }), Settings) { Clock = () => now };

        await roster.ReportAsync("agent", Config, refresh: true);
        Assert.Equal((1, 1, 0), (TimesAsked("account-1"), TimesAsked("account-2"), TimesAsked("(own)")));
        for (var look = 0; look < 4; look++) Assert.False((await roster.SelectAsync("agent", Config, null, null)).Allowed);
        Assert.Equal((1, 1, 0), (TimesAsked("account-1"), TimesAsked("account-2"), TimesAsked("(own)")));

        // A sign-in marked after the word: that account alone, once.
        ProbeLock.MarkSignedIn(HarnessSettings.ProfileHome(_home, "agent", "account-1"), now);
        File.SetLastWriteTimeUtc(ProbeLock.SignedInPathOf(_home, "agent", "account-1"), now.AddSeconds(1).UtcDateTime);
        now += TimeSpan.FromSeconds(2);
        for (var look = 0; look < 3; look++) Assert.False((await roster.SelectAsync("agent", Config, null, null)).Allowed);
        Assert.Equal((2, 1, 0), (TimesAsked("account-1"), TimesAsked("account-2"), TimesAsked("(own)")));

        // The backstop, for a sign-in no door of Daoris's made.
        now += HarnessRoster.SignedOutAskedAgain;
        for (var look = 0; look < 3; look++) Assert.False((await roster.SelectAsync("agent", Config, null, null)).Allowed);
        Assert.Equal((3, 1, 0), (TimesAsked("account-1"), TimesAsked("account-2"), TimesAsked("(own)")));

        // The person's press asks the roster again, the own sign-in with it.
        await roster.RosterAsync(Config, refresh: true);
        Assert.Equal((4, 2, 1), (TimesAsked("account-1"), TimesAsked("account-2"), TimesAsked("(own)")));
    }

    /// <summary>
    /// ROSTER1 (D150 §5.3): a look, the page's roster and a cold cache ask no account; an account's own *Read again* asks that
    /// account alone, under its lock; and a restart starts from what it read, asking nothing.
    /// </summary>
    [Fact]
    public async Task A_press_on_one_account_reads_it_alone_and_a_look_or_a_restart_asks_nothing()
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "agent", "account-1"));
        var two = HarnessSettings.ProfileHome(_home, "agent", "account-2");
        Directory.CreateDirectory(two);
        File.WriteAllText(Path.Combine(two, ".credentials.json"),
            JsonSerializer.Serialize(new { claudeAiOauth = new { accessToken = "a1", refreshToken = "r1", expired = false } }));
        new HarnessSettings().WithDefault("agent", "account-2").Save(Settings);
        var roster = Roster();

        await roster.ReportAsync("agent", Config);
        await roster.RosterAsync(Config);
        Assert.True((await roster.SelectAsync("agent", Config, null, null)).Allowed);
        Assert.Equal((0, 0, 0), (TimesAsked("account-1"), TimesAsked("account-2"), TimesAsked("(own)")));

        var report = (await Roster().ReportAsync("agent", Config, refresh: true, account: "account-2"))!;

        Assert.Equal((0, 1, 0), (TimesAsked("account-1"), TimesAsked("account-2"), TimesAsked("(own)")));
        var read = report.Profiles.Single(profile => profile.Name == "account-2");
        Assert.Equal(LoginState.In, read.Login);
        Assert.NotNull(read.Read);
        Assert.Null(report.Profiles.Single(profile => profile.Name == "account-1").Read);
        Assert.Null(report.OwnRead);

        var restarted = (await Roster().ReportAsync("agent", Config))!;
        Assert.Equal(LoginState.In, restarted.Profiles.Single(profile => profile.Name == "account-2").Login);
        Assert.Equal((0, 1, 0), (TimesAsked("account-1"), TimesAsked("account-2"), TimesAsked("(own)")));
    }

    [Fact]
    public async Task An_account_a_session_of_Daoris_s_runs_on_is_not_asked_and_keeps_its_last_answer()
    {
        var home = Expired("account-1");
        var roster = Roster();
        var first = await roster.ReportAsync("agent", Config, refresh: true);
        Assert.Equal(LoginState.In, Assert.Single(first!.Profiles).Login);

        // Its record runs now, as the look reads it: the session's own process refreshes its token, so the probe keeps out.
        File.WriteAllText(Path.Combine(home, ".credentials.json"),
            JsonSerializer.Serialize(new { claudeAiOauth = new { accessToken = "a2", refreshToken = "r2", expired = true } }));
        roster.Look([new SessionStarted("agent", "account-1", DateTimeOffset.UtcNow, Running: true)], roster.Mark());
        var again = await roster.ReportAsync("agent", Config, refresh: true);

        Assert.Equal(LoginState.In, Assert.Single(again!.Profiles).Login);
        Assert.Equal(1, TimesAsked("account-1"));
        Assert.Equal("r2", RefreshToken(home));
    }
}
