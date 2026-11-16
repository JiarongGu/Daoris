using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An account's sign-in state is read once, by <see cref="SignInStates"/>, from its last reading: signed in, signed out since a
/// time (and whether the person did it), its check not answered, or never read. The pick ranks an account whose check did not
/// answer last until one answers, and its opening line says so; the account page shows the same state; a sign-out the person
/// did not make is one notice.
/// </summary>
/// <remarks>
/// A check that does not answer within its patience is kept as a reading whose word is unknown (<see cref="HarnessProbe"/>'s
/// timed-out question is asked, and answers nothing): each case writes that reading as the timed-out question does.
/// </remarks>
public sealed class SignInStateTests : IDisposable
{
    private static readonly DateTimeOffset Ten = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-sign-in-state-" + Guid.NewGuid().ToString("N")[..8]);

    public SignInStateTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Binary, "");
        foreach (var account in new[] { "account-1", "account-2" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "agent", account));
        }

        new HarnessSettings().WithDefault("agent", "account-1").WithRotation("agent", ["account-1", "account-2"]).Save(Settings);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    private string Binary => Path.Combine(_home, "agent-binary");

    private HarnessRoster Roster() =>
        new(TestDoors.Registry(new Dictionary<string, Provider>(StringComparer.OrdinalIgnoreCase)
        {
            ["agent"] = TestDoors.Door("agent", new TestAgent(
                Binary: [Binary],
                VersionArguments: ["--version"],
                ProfileVariable: "READ_HOME",
                LoginCheck: new LoginQuestion(["--login-state"], "logged-in", "logged-out", @"logged-in as (\S+)"),
                ProbeByPresence: true)),
        }), Settings) { Clock = () => Ten, Asking = NoAsk };

    /// <summary>No account is asked its status here: every reading is the file's.</summary>
    private static Task<LoginState> NoAsk(string owner, string account, CancellationToken ct) =>
        throw new InvalidOperationException($"`{owner}` account `{account}` was asked its status.");

    private static DriverConfig Config => DriverConfig.Empty with { Actors = StaffedRoles.On("agent") };

    /// <summary>
    /// 2026-11-16: a session opened on an account whose sign-in check did not answer within its patience, its sign-in expired, and
    /// failed `Authentication required`. The list begins at that account, the other is signed in: the start runs on the other.
    /// </summary>
    [Fact]
    public async Task An_account_whose_check_did_not_answer_is_never_picked_over_a_signed_in_one()
    {
        AccountReads.Keep(_home, "agent", "account-1", LoginState.Unknown, Ten - TimeSpan.FromMinutes(3));
        AccountReads.Keep(_home, "agent", "account-2", LoginState.In, Ten - TimeSpan.FromHours(5));

        var selection = await Roster().SelectAsync("agent", Config, null, null);

        Assert.True(selection.Allowed, selection.Refusal);
        Assert.Equal("account-2", selection.Profile);
        var step = selection.Steps![0];
        Assert.Equal(OpeningWhy.Unanswered, step.Why);
        Assert.Contains(step.Values, value => value is ("over", "account-1"));
        Assert.Contains("sign-in check did not answer", selection.Rotated!.Why);
    }

    /// <summary>Ranked last, never refused: with every other account not ready, the start runs on it and says why.</summary>
    [Fact]
    public async Task An_account_whose_check_did_not_answer_runs_last_and_its_line_says_so()
    {
        AccountReads.Keep(_home, "agent", "account-1", LoginState.Out, Ten - TimeSpan.FromMinutes(30));
        AccountReads.Keep(_home, "agent", "account-2", LoginState.Unknown, Ten - TimeSpan.FromMinutes(3));

        var selection = await Roster().SelectAsync("agent", Config, null, null);

        Assert.True(selection.Allowed, selection.Refusal);
        Assert.Equal("account-2", selection.Profile);
        Assert.Contains(selection.Steps!, step => step.Why == OpeningWhy.UnansweredSelf);
    }

    /// <summary>Until a check answers: a reading that answers puts it back in its place.</summary>
    [Fact]
    public async Task A_check_that_answers_puts_the_account_back_in_its_place()
    {
        AccountReads.Keep(_home, "agent", "account-1", LoginState.Unknown, Ten - TimeSpan.FromMinutes(3));
        AccountReads.Keep(_home, "agent", "account-1", LoginState.In, Ten - TimeSpan.FromMinutes(1));
        AccountReads.Keep(_home, "agent", "account-2", LoginState.In, Ten - TimeSpan.FromHours(5));

        var selection = await Roster().SelectAsync("agent", Config, null, null);

        Assert.Equal("account-1", selection.Profile);
    }

    /// <summary>
    /// The one unit: each word, a sign-out's since kept across readings that say the same, and a sign-out after a reading that
    /// said signed in, which nobody at Daoris made (Daoris never signs an account out, and removing one deletes it).
    /// </summary>
    [Fact]
    public void The_state_is_read_once_from_the_last_reading()
    {
        Assert.Equal(SignInStanding.Unread, SignInStates.Of(null).Standing);
        Assert.Equal(SignInStanding.Unanswered, SignInStates.Of(new AccountRead(LoginState.Unknown, Ten)).Standing);

        AccountReads.Keep(_home, "agent", "account-1", LoginState.In, Ten - TimeSpan.FromHours(2));
        AccountReads.Keep(_home, "agent", "account-1", LoginState.Out, Ten - TimeSpan.FromHours(1));
        AccountReads.Keep(_home, "agent", "account-1", LoginState.Out, Ten);
        AccountReads.Keep(_home, "agent", "account-2", LoginState.Out, Ten);

        var reads = AccountReads.Of(_home, "agent").Accounts;
        var out1 = SignInStates.Of(reads["account-1"]);
        Assert.Equal((SignInStanding.Out, Ten - TimeSpan.FromHours(1), true), (out1.Standing, out1.Since, out1.NotByPerson));
        var out2 = SignInStates.Of(reads["account-2"]);
        Assert.Equal((SignInStanding.Out, Ten, false), (out2.Standing, out2.Since, out2.NotByPerson));
    }

    /// <summary>The page's state is the pick's: the report carries the unit's word and its since.</summary>
    [Fact]
    public async Task The_report_carries_the_same_state_the_pick_reads()
    {
        AccountReads.Keep(_home, "agent", "account-1", LoginState.In, Ten - TimeSpan.FromHours(2));
        AccountReads.Keep(_home, "agent", "account-1", LoginState.Out, Ten - TimeSpan.FromHours(1));
        AccountReads.Keep(_home, "agent", "account-2", LoginState.Unknown, Ten);

        var report = await Roster().ReportAsync("agent", Config);

        var one = report!.Profiles.Single(profile => profile.Name == "account-1").SignIn!;
        Assert.Equal((SignInStanding.Out, Ten - TimeSpan.FromHours(1), true), (one.Standing, one.Since, one.NotByPerson));
        Assert.Equal(SignInStanding.Unanswered, report.Profiles.Single(profile => profile.Name == "account-2").SignIn!.Standing);
    }

    /// <summary>
    /// A sign-out the person did not make is one notice, told once whatever the looks; an account whose first reading is
    /// signed out (a folder nobody signed in to) is none.
    /// </summary>
    [Fact]
    public void A_sign_out_the_person_did_not_make_is_one_notice()
    {
        AccountReads.Keep(_home, "agent", "account-1", LoginState.In, Ten - TimeSpan.FromHours(2));
        AccountReads.Keep(_home, "agent", "account-1", LoginState.Out, Ten - TimeSpan.FromHours(1));
        AccountReads.Keep(_home, "agent", "account-1", LoginState.Out, Ten);
        AccountReads.Keep(_home, "agent", "account-2", LoginState.Out, Ten);

        var occurrences = SignOutNotices.Occurrences(_home, Ten);

        var notice = Assert.Single(occurrences).Notice;
        Assert.Equal((SignOutNotices.Kind, "agent", "account-1", "out"), (notice.Kind, notice.Route, notice.Code, notice.State));
        Assert.Equal(Ten - TimeSpan.FromHours(1), notice.At);
        Assert.Equal(1, new News(_home).Claim(SignOutNotices.Teller, occurrences, null, Ten).Told.Count);
        Assert.Empty(new News(_home).Claim(SignOutNotices.Teller, SignOutNotices.Occurrences(_home, Ten), null, Ten).Told);
    }
}
