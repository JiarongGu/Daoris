using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ROSTER1 (D150 §5.3, D125's TOOL6g note): an account is never read at the application's start, at a look, on a timer or
/// when a view opens. The roster reports what was last read (<see cref="AccountReads"/>), with when, so a restart and a cold
/// cache ask nothing; a start runs on an account never read, as on any unknown (SES3), and walks past one last read signed out.
/// </summary>
/// <remarks>
/// The agent is a script that logs every question it is asked, found by presence (<see cref="HarnessToolchain.ProbeByPresence"/>),
/// so the roster learns it is installed without starting it. A passing run starts no process: an account asked would start the
/// script and write a line, which is what each case counts.
/// </remarks>
public sealed class RosterReadTests : IDisposable
{
    private static readonly DateTimeOffset Ten = new(2026, 10, 4, 10, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-roster-read-" + Guid.NewGuid().ToString("N")[..8]);

    public RosterReadTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Script, """
            import fs from 'node:fs';
            import path from 'node:path';
            const home = process.env.READ_HOME;
            fs.appendFileSync(new URL('./asked.log', import.meta.url), process.argv.slice(2).join(' ') + ' ' + (home ? path.basename(home) : '(own)') + '\n');
            console.log('logged-out');
            """);
        foreach (var account in new[] { "account-1", "account-2" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "agent", account));
        }
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    /// <summary>The agent's set as one account, account-1: account-2's folder taken away.</summary>
    private void OneAccount() => Directory.Delete(HarnessSettings.ProfileHome(_home, "agent", "account-2"));

    private string Script => Path.Combine(_home, "agent.mjs");

    private string[] Asked => File.Exists(Path.Combine(_home, "asked.log")) ? File.ReadAllLines(Path.Combine(_home, "asked.log")) : [];

    private sealed class Adapter(string name, HarnessToolchain toolchain) : ISessionAdapter
    {
        public string Name => name;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new();

        public HarnessToolchain? Toolchain => toolchain;
    }

    private HarnessRoster Roster() => new(new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
    {
        ["agent"] = new Adapter("agent", new HarnessToolchain(
            Binary: ["node", Script],
            VersionArguments: ["--version"],
            ProfileVariable: "READ_HOME",
            LoginCheck: new LoginQuestion(["--login-state"], "logged-in", "logged-out", @"logged-in as (\S+)"),
            ProbeByPresence: true)),
    }), Settings) { Clock = () => Ten };

    private static DriverConfig Config => DriverConfig.Empty with { Adapter = "agent" };

    [Fact]
    public async Task Reading_the_roster_at_a_cold_cache_asks_no_account_and_says_none_was_read()
    {
        new HarnessSettings().WithDefault("agent", "account-1").WithRotation("agent", ["account-1", "account-2"]).Save(Settings);
        var roster = Roster();

        var report = await roster.ReportAsync("agent", Config);
        await roster.RosterAsync(Config);
        roster.Next("agent");
        await roster.WiringAsync("agent", Config, null);

        Assert.Empty(Asked);
        Assert.True(report!.Present);
        Assert.All(report.Profiles, profile => Assert.Equal((LoginState.Unknown, (DateTimeOffset?)null), (profile.Login, profile.Read)));
        Assert.Equal((LoginState.Unknown, (DateTimeOffset?)null), (report.OwnLogin, report.OwnRead));
    }

    /// <summary>A start at a cold cache asks nothing, and runs on an account never read, as on any unknown (SES3).</summary>
    [Fact]
    public async Task A_start_at_a_cold_cache_asks_no_account_and_runs_on_one_never_read()
    {
        new HarnessSettings().WithDefault("agent", "account-1").Save(Settings);

        var selection = await Roster().SelectAsync("agent", Config, null, null);

        Assert.True(selection.Allowed, selection.Refusal);
        Assert.Equal("account-1", selection.Profile);
        Assert.Empty(Asked);
    }

    /// <summary>What a press read before a restart is what the start meets: an account read signed out is walked past.</summary>
    [Fact]
    public async Task A_start_walks_past_an_account_last_read_signed_out_and_asks_nothing()
    {
        new HarnessSettings().WithDefault("agent", "account-1").WithRotation("agent", ["account-1", "account-2"]).Save(Settings);
        AccountReads.Keep(_home, "agent", "account-1", LoginState.Out, Ten);

        var selection = await Roster().SelectAsync("agent", Config, null, null);

        Assert.True(selection.Allowed, selection.Refusal);
        Assert.Equal("account-2", selection.Profile);
        Assert.Equal("account-1", selection.Rotated!.From);
        Assert.Empty(Asked);
    }

    /// <summary>
    /// Rewritten by POOL1a2 (D197 point 1): the machine's one named account was the start's only account, account-2 beside it in
    /// no list. Both are the set now, so the start would pass to account-2; the case is an agent whose set is the one account.
    /// </summary>
    [Fact]
    public async Task An_account_last_read_signed_out_holds_its_start_naming_the_sign_in_and_asks_nothing()
    {
        OneAccount();
        new HarnessSettings().WithDefault("agent", "account-1").Save(Settings);
        AccountReads.Keep(_home, "agent", "account-1", LoginState.Out, Ten);
        var roster = Roster();

        var selection = await roster.SelectAsync("agent", Config, null, null);

        Assert.False(selection.Allowed);
        Assert.Contains("daoris agent login agent --profile account-1", selection.Refusal);
        Assert.Equal(NextHold.SignedOut, roster.Next("agent").Others.Single(held => held.Account == "account-1").Hold);
        Assert.Empty(Asked);
    }

    /// <summary>
    /// HELPSTUCK1: a lock's folder beside the accounts is no account. The owner's machine listed Claude Code's lock folders as
    /// accounts, a start's walk went to one read signed in, and a conversation ran there. The report names each as a stray,
    /// saying whether it holds a sign-in (a reading that said so, or a sign-in's mark), so it is said rather than lost, and
    /// no start walks to it.
    /// </summary>
    [Fact]
    public async Task A_lock_s_folder_beside_the_accounts_is_a_stray_the_report_names_and_no_start_walks_to()
    {
        foreach (var stray in new[] { "Gmail.lock", "account-1.lock", "account-2.lock" })
        {
            Directory.CreateDirectory(Path.Combine(HarnessSettings.ProfileHome(_home, "agent", "account-1"), "..", stray));
        }

        AccountReads.Keep(_home, "agent", "account-1", LoginState.Out, Ten);
        AccountReads.Keep(_home, "agent", "account-2", LoginState.Out, Ten);
        AccountReads.Keep(_home, "agent", "account-2.lock", LoginState.In, Ten);
        var mark = ProbeLock.SignedInPathOf(_home, "agent", "account-1.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(mark)!);
        File.WriteAllText(mark, "{}");
        var roster = Roster();

        var report = (await roster.ReportAsync("agent", Config))!;
        var selection = await roster.SelectAsync("agent", Config, null, null);

        Assert.Equal(["account-1", "account-2"], report.Profiles.Select(profile => profile.Name));
        Assert.Equal(
            [new StrayFolder("Gmail.lock", SignedIn: false), new StrayFolder("account-1.lock", SignedIn: true), new StrayFolder("account-2.lock", SignedIn: true)],
            report.Strays);
        Assert.NotEqual("account-2.lock", selection.Profile);
        Assert.False(selection.Allowed);
        Assert.Empty(Asked);
    }

    /// <summary>
    /// The report lists the accounts on disk, each with what was last read of it and when, and the tool's own sign-in's; one
    /// never read says so. A removed account is gone from it at once, and a restart reads the same.
    /// </summary>
    [Fact]
    public async Task The_report_says_each_account_s_last_reading_and_when_and_a_restart_reads_the_same()
    {
        AccountReads.Keep(_home, "agent", "account-1", LoginState.In, Ten);
        AccountReads.Keep(_home, "agent", null, LoginState.Out, Ten.AddMinutes(1));

        var report = (await Roster().ReportAsync("agent", Config))!;

        Assert.Equal(["account-1", "account-2"], report.Profiles.Select(profile => profile.Name));
        Assert.Equal((LoginState.In, (DateTimeOffset?)Ten), (report.Profiles[0].Login, report.Profiles[0].Read));
        Assert.Equal((LoginState.Unknown, (DateTimeOffset?)null), (report.Profiles[1].Login, report.Profiles[1].Read));
        Assert.Equal((LoginState.Out, (DateTimeOffset?)Ten.AddMinutes(1)), (report.OwnLogin, report.OwnRead));

        HarnessSettings.RemoveProfile(_home, "agent", "account-2");
        var again = (await Roster().ReportAsync("agent", Config))!;
        Assert.Equal(["account-1"], again.Profiles.Select(profile => profile.Name));
        Assert.Equal(LoginState.In, again.Profiles[0].Login);
        Assert.Empty(Asked);
    }

    /// <summary>An account removed on the screen forgets its reading, so one made later under its name starts never read.</summary>
    [Fact]
    public async Task A_removed_account_s_reading_is_forgotten()
    {
        AccountReads.Keep(_home, "agent", "account-2", LoginState.In, Ten);
        var roster = Roster();

        roster.Removed("agent", "account-2");
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "agent", "account-2"));

        var report = (await roster.ReportAsync("agent", Config))!;
        Assert.Null(report.Profiles.Single(profile => profile.Name == "account-2").Read);
    }

    /// <summary>
    /// A person looking again lets a refused account through (AGT3b) and ends the tool's own home's cool-off (D125 §3.7), as an
    /// account action always has, and asks no account to do it. Its set is the one account (POOL1a2), as
    /// <see cref="An_account_last_read_signed_out_holds_its_start_naming_the_sign_in_and_asks_nothing"/> says why.
    /// </summary>
    [Fact]
    public async Task Looking_again_lets_a_refused_account_through_and_asks_nothing()
    {
        OneAccount();
        new HarnessSettings().WithDefault("agent", "account-1").Save(Settings);
        var roster = Roster();
        roster.Refuse("agent", "account-1", "the provider refused `account-1` (401).");
        Assert.False((await roster.SelectAsync("agent", Config, null, null)).Allowed);

        roster.LookedAgain();

        Assert.True((await roster.SelectAsync("agent", Config, null, null)).Allowed);
        Assert.Empty(Asked);
    }

    /// <summary>
    /// POOL1a3 (D197 point 3 and its acceptance note): the agent's <i>Read again</i> reads each account of Daoris's and never the
    /// tool's own sign-in, and still ends that agent's own home's cool-off, nothing asked, so the cool-off's line that sends the
    /// person to it holds. Another agent's stands until the roster's whole refresh, which asks no own sign-in either.
    /// </summary>
    [Fact]
    public async Task The_agent_s_read_again_ends_its_own_home_s_cool_off_and_asks_the_own_sign_in_nothing()
    {
        AccountCooling.Cool(_home, new CoolingEntry("agent", null, Ten.AddHours(2), true, "session", Ten, "s0"), Ten);
        AccountCooling.Cool(_home, new CoolingEntry("other", null, Ten.AddHours(2), true, "session", Ten, "s1"), Ten);
        var roster = Roster();

        await roster.ReportAsync("agent", Config, refresh: true);

        Assert.Equal(["--login-state account-1", "--login-state account-2"], Asked.Order(StringComparer.Ordinal));
        Assert.Equal(["other"], AccountCooling.Read(_home, Ten).Select(entry => entry.Agent));

        await roster.RosterAsync(Config, refresh: true);
        Assert.Empty(AccountCooling.Read(_home, Ten));
        Assert.DoesNotContain(Asked, line => line.EndsWith("(own)", StringComparison.Ordinal));
        Assert.Null(AccountReads.Of(_home, "agent").Own);
    }

    /// <summary>One account's read names an account the agent has; another is refused in a sentence, and nothing is asked.</summary>
    [Fact]
    public async Task Reading_an_account_the_agent_does_not_have_is_refused_naming_the_ones_it_has()
    {
        var refusal = await Assert.ThrowsAsync<DriverException>(() => Roster().ReportAsync("agent", Config, refresh: true, account: "typo"));

        Assert.Contains("`typo`", refusal.Message);
        Assert.Contains("account-1, account-2", refusal.Message);
        Assert.Empty(Asked);
    }
}
