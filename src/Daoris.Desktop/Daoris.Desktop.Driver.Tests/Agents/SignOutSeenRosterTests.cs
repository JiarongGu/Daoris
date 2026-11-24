using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A store emptied by a write reads signed out, with its cause, where a person looks: the roster's report and the terminal's list.</summary>
public sealed class SignOutSeenRosterTests : IDisposable
{
    private static readonly DateTimeOffset Ten = new(2026, 11, 24, 5, 10, 15, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-sign-out-seen-roster-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _account;

    public SignOutSeenRosterTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Binary, "");
        _account = HarnessSettings.ProfileHome(_home, "agent", "account-1");
        Directory.CreateDirectory(_account);
        new HarnessSettings().WithDefault("agent", "account-1").Save(Settings);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    private string Binary => Path.Combine(_home, "agent-binary");

    private static DriverConfig Config => DriverConfig.Empty with { Actors = StaffedRoles.On("agent") };

    private HarnessRoster Roster() =>
        new(TestDoors.Registry(new Dictionary<string, Provider>(StringComparer.OrdinalIgnoreCase)
        {
            ["agent"] = TestDoors.Door("agent", new TestAgent(
                Binary: [Binary],
                VersionArguments: ["--version"],
                ProfileVariable: "READ_HOME",
                LoginCheck: new LoginQuestion(["--login-state"], "logged-in", "logged-out", @"logged-in as (\S+)"),
                ProbeByPresence: true,
                StoredSignIn: new ClaudeStoredSignIn())),
        }), Settings) { Clock = () => Ten };

    [Fact]
    public async Task A_store_emptied_by_a_write_is_signed_out_in_the_roster_and_the_list_with_its_cause_at_the_next_look()
    {
        File.WriteAllText(Path.Combine(_account, ".credentials.json"), """{"claudeAiOauth":{"accessToken":"a","refreshToken":"r","expiresAt":1}}""");
        AccountReads.Keep(_home, "agent", "account-1", LoginState.In, Ten - TimeSpan.FromHours(3));
        var roster = Roster();
        using var writes = new CredentialWrites(roster, _ => { });
        writes.Follow();

        var temp = Path.Combine(_account, ".credentials.json.tmp.1");
        await File.WriteAllTextAsync(temp, """{"claudeAiOauth":{"accessToken":"","refreshToken":"","expiresAt":0}}""");
        File.Move(temp, Path.Combine(_account, ".credentials.json"), overwrite: true);
        await Poll.Until(() => AccountReads.Of(_home, "agent").Accounts.GetValueOrDefault("account-1") is { Login: LoginState.Out }, () => "not read out");

        var report = (await roster.ReportAsync("agent", Config))!;
        var profile = report.Profiles.Single();
        Assert.Equal(LoginState.Out, profile.Login);
        Assert.Equal((SignInStanding.Out, true, SignInCause.Outside), (profile.SignIn!.Standing, profile.SignIn.NotByPerson, profile.SignIn.By));

        var lines = AgentListCommand.AccountLines(
            "agent", roster.Adapters.AgentNamed("agent")!, report, roster.Settings, _home, roster.Adapters, Ten, TimeZoneInfo.Utc);
        Assert.Contains(lines, line => line.Contains("outside Daoris", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(SignInCause.Session, "a session of Daoris's")]
    [InlineData(SignInCause.Probe, "asking its status")]
    [InlineData(SignInCause.SignIn, "a sign-in Daoris ran")]
    [InlineData(SignInCause.Outside, "outside Daoris")]
    public void The_terminal_words_each_cause(string by, string words) =>
        Assert.Contains(words, AgentAccountLines.SignedOutBy(by, Ten, TimeZoneInfo.Utc), StringComparison.Ordinal);
}
