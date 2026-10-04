using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ROSTER1b (D150 §5.3; D125's TOOL6g and ROSTER1 notes): a start the agent refused for its sign-in reads its account signed
/// out in <c>reads.json</c>, with its time, as a reading would; the next start walks past it, as rotation walks past any
/// account read signed out, and a held start names it and its sign-in; the refusal is never one of the quest's strikes, and
/// its record says why it ended; and a later sign-in through Daoris lets the account run again, as TOOL6g's mark does.
/// </summary>
/// <remarks>
/// Nothing here starts a process. The walk runs on an agent present by a file look that asks nobody whether an account is
/// signed in (<see cref="HarnessToolchain.ProbeByPresence"/>), so every account is ready unless a reading, a cool-off or a
/// refusal says otherwise; the conclusion runs on the protocol stub, which reads the stub's words as its owner's (AGT7).
/// A refused start through real ticks, its next start on another account, is <c>SignInRefusalTickTests</c>, in the
/// <c>Process</c> half.
/// </remarks>
public sealed class SignInRefusedStartTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Seen = new(2026, 10, 4, 14, 0, 0, TimeSpan.FromMinutes(345));

    /// <summary>The install's refusal (2026-10-04), as the protocol door says it.</summary>
    private const string Refusal = "the ACP agent refused the call: Authentication required";

    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-signin-start-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private DateTimeOffset _now = Seen;

    public SignInRefusedStartTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
        _ledger.Register("engine", Path.Combine(_home, "engine"));
    }

    public void Dispose()
    {
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Settings => Path.Combine(_home, "harnesses.json");

    /// <summary>A file that is there, so the agent is present by a look and nothing runs.</summary>
    private string Command => Path.Combine(_home, "agent-here");

    private sealed class Adapter(string name, HarnessToolchain? toolchain) : ISessionAdapter
    {
        public string Name => name;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    /// <summary>An agent with accounts, present by a look, with no login question: only a reading says an account is signed out.</summary>
    private HarnessRoster Fake() =>
        new(new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["fake"] = new Adapter("fake", new HarnessToolchain(
                Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true)),
        }), Settings)
        {
            Clock = () => _now,
            Zone = Zone,
        };

    private HarnessRoster Built() => new(AdapterSet.Built(), Settings) { Clock = () => _now, Zone = Zone };

    private static DriverConfig FakeConfig => DriverConfig.Empty with { Adapter = "fake" };

    private static DriverConfig Config => DriverConfig.Empty with
    {
        Drivable = ["engine"],
        Trees = ["engine"],
        Adapter = "acp-stub",
        // One failure would park it: a refused sign-in must not.
        Strikes = 1,
        PollSeconds = 1,
    };

    private Daoris.Driver.Driver Driver(ServiceClient service, HarnessRoster roster) =>
        new(service, Config, AdapterSet.Built(), _home, harnesses: roster);

    private void Accounts(string agent, params string[] names)
    {
        foreach (var name in names) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, agent, name));
    }

    private void Wire(Func<HarnessSettings, HarnessSettings> edit) => edit(new HarnessSettings()).Save(Settings);

    private AccountRead? ReadOf(string agent, string account) => AccountReads.Of(_home, agent).Accounts.GetValueOrDefault(account);

    // ——— The reading (D150 §5.3): signed out, with when, as a reading would be.

    [Fact]
    public async Task A_start_refused_for_its_sign_in_reads_its_account_signed_out_with_its_time()
    {
        Accounts("fake", "account-1");
        var roster = Fake();

        roster.SignedOut("fake", "account-1");

        Assert.Equal(new AccountRead(LoginState.Out, Seen), ReadOf("fake", "account-1"));
        var report = await roster.ReportAsync("fake", FakeConfig);
        var account = Assert.Single(report!.Profiles);
        Assert.Equal((LoginState.Out, (DateTimeOffset?)Seen), (account.Login, account.Read));
    }

    /// <summary>The tool's own sign-in reads signed out the same way; a door's account is its owner's (AGT7).</summary>
    [Fact]
    public void The_own_sign_in_and_a_door_s_account_read_signed_out_as_their_owner_s()
    {
        Accounts("stub", "account-1");
        var roster = Built();

        roster.SignedOut("acp-stub", "account-1");
        roster.SignedOut("acp-stub", null);

        Assert.Equal(new AccountRead(LoginState.Out, Seen), ReadOf("stub", "account-1"));
        Assert.Equal(new AccountRead(LoginState.Out, Seen), AccountReads.Of(_home, "stub").Own);
        Assert.Empty(AccountReads.Of(_home, "acp-stub").Accounts);
    }

    // ——— The next start (TOOL6g's walk): it walks past the account, and a hold names it and its sign-in.

    [Fact]
    public async Task The_next_start_walks_past_it_to_another_account_of_the_list()
    {
        Accounts("fake", "account-1", "account-2");
        Wire(s => s.WithDefault("fake", "account-1").WithRotation("fake", ["account-1", "account-2"]));
        var roster = Fake();

        roster.SignedOut("fake", "account-1");
        var selection = await roster.SelectAsync("fake", FakeConfig, null, null);

        Assert.True(selection.Allowed);
        Assert.Equal("account-2", selection.Profile);
        Assert.Equal(("account-1", "the `fake` account `account-1` is not signed in"), (selection.Rotated!.From, selection.Rotated.Why));
        Assert.Equal(NextHold.SignedOut, Assert.Single(roster.Next("fake", null).Others, held => held.Account == "account-1").Hold);
    }

    [Fact]
    public async Task A_start_held_on_it_names_it_and_its_sign_in()
    {
        Accounts("fake", "account-1");
        Wire(s => s.WithDefault("fake", "account-1"));
        var roster = Fake();

        roster.SignedOut("fake", "account-1");
        var held = await roster.SelectAsync("fake", FakeConfig, null, null);

        Assert.False(held.Allowed);
        Assert.Equal(AccountReadiness.SignedOut, held.NotReady);
        Assert.Equal(
            "the `fake` account `account-1` is not signed in, so a session would have nothing to run as — "
            + "`daoris agent login fake --profile account-1` runs the agent's own sign-in into it. Daoris manages the directory "
            + "and the name; the credential stays in the agent's own store.",
            held.Refusal);
        Assert.Equal(["account-1"], held.SignedOut!.Accounts);
    }

    [Fact]
    public async Task A_list_whose_every_account_was_refused_names_each_and_its_sign_in()
    {
        Accounts("fake", "account-1", "account-2");
        Wire(s => s.WithDefault("fake", "account-1").WithRotation("fake", ["account-1", "account-2"]));
        var roster = Fake();

        roster.SignedOut("fake", "account-1");
        roster.SignedOut("fake", "account-2");
        var held = await roster.SelectAsync("fake", FakeConfig, null, null);

        Assert.False(held.Allowed);
        Assert.Equal(
            "no `fake` account this start may use is ready: `account-1` is not signed in, `account-2` is not signed in. A sign-in "
            + "starts it: `daoris agent login fake --profile account-1`, `daoris agent login fake --profile account-2`, or Agents → "
            + "the agent's page → Accounts.",
            held.Refusal);
        Assert.Equal(["account-1", "account-2"], held.SignedOut!.Accounts);
    }

    // ——— Cleared (TOOL6g): a sign-in through Daoris marks it, and the next start asks it again; a newer reading stands.

    [Fact]
    public async Task A_later_sign_in_through_Daoris_lets_it_run_again()
    {
        Accounts("fake", "account-1");
        Wire(s => s.WithDefault("fake", "account-1"));
        var roster = Fake();
        roster.SignedOut("fake", "account-1");

        ProbeLock.MarkSignedIn(HarnessSettings.ProfileHome(_home, "fake", "account-1"), DateTimeOffset.UtcNow);
        var selection = await roster.SelectAsync("fake", FakeConfig, null, null);

        // Asked again once the mark is newer than the refusal: this agent has no login question, so unknown, which runs.
        Assert.True(selection.Allowed);
        Assert.Equal("account-1", selection.Profile);
    }

    [Fact]
    public async Task A_sign_in_marked_before_the_refusal_does_not_clear_it()
    {
        Accounts("fake", "account-1");
        Wire(s => s.WithDefault("fake", "account-1"));
        ProbeLock.MarkSignedIn(HarnessSettings.ProfileHome(_home, "fake", "account-1"), DateTimeOffset.UtcNow);
        _now = DateTimeOffset.UtcNow.AddMinutes(10);
        var roster = Fake();

        roster.SignedOut("fake", "account-1");
        var held = await roster.SelectAsync("fake", FakeConfig, null, null);

        Assert.Equal(AccountReadiness.SignedOut, held.NotReady);
    }

    [Fact]
    public async Task A_newer_reading_that_says_signed_in_lets_it_run_again()
    {
        Accounts("fake", "account-1");
        Wire(s => s.WithDefault("fake", "account-1"));
        var roster = Fake();
        roster.SignedOut("fake", "account-1");

        AccountReads.Keep(_home, "fake", "account-1", LoginState.In, Seen.AddMinutes(1));
        var selection = await roster.SelectAsync("fake", FakeConfig, null, null);

        Assert.Equal(("account-1", true), (selection.Profile, selection.Allowed));
    }

    // ——— The conclusion: the door's refusal, read by the table; the record says why; the account reads signed out.

    [Fact]
    public void A_failed_start_the_door_refused_for_its_sign_in_reads_its_account_signed_out_and_says_why()
    {
        using var service = _ledger.Client();
        var roster = Built();
        var failed = Observation.Conclude(0, "Open", turnFailed: Refusal);

        var conclusion = Driver(service, roster).AccountSignedOut(
            failed, AdapterSet.Built().Resolve("acp-stub"), new HarnessSelection(null, "account-2"), Refusal);

        Assert.Equal("failed", conclusion.State);
        Assert.Equal(
            $"{failed.Note} The agent refused the `stub` account `account-2` for its sign-in, so it reads signed out and Daoris "
            + "starts nothing more on it until it is signed in: `daoris agent login stub --profile account-2`, or Agents → the "
            + "agent's page → Accounts.",
            conclusion.Note);
        var line = conclusion.Parts![^1];
        Assert.Equal(("account.signed-out", "stub"), (line.Code, (string?)line.Value("owner")));
        Assert.Equal(new AccountRead(LoginState.Out, Seen), ReadOf("stub", "account-2"));
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("stopped")]
    [InlineData("stood-down")]
    [InlineData("awaiting-person")]
    public void An_ending_that_is_not_a_failure_reads_nothing(string state)
    {
        using var service = _ledger.Client();
        var ended = new SessionConclusion(state, "it ended.");

        var conclusion = Driver(service, Built()).AccountSignedOut(
            ended, AdapterSet.Built().Resolve("acp-stub"), new HarnessSelection(null, "account-2"), Refusal);

        Assert.Equal(ended, conclusion);
        Assert.False(File.Exists(AccountReads.PathOf(_home)));
    }

    [Fact]
    public void A_failure_the_table_does_not_recognise_reads_nothing()
    {
        using var service = _ledger.Client();
        var failed = new SessionConclusion("failed", "the agent's turn failed before it took its quest: Internal error: Overloaded");

        var conclusion = Driver(service, Built()).AccountSignedOut(
            failed, AdapterSet.Built().Resolve("acp-stub"), new HarnessSelection(null, "account-2"),
            "the ACP agent refused the call: Internal error: Overloaded");

        Assert.Equal(failed, conclusion);
        Assert.False(File.Exists(AccountReads.PathOf(_home)));
    }

    /// <summary>
    /// The tool's own sign-in is never asked by a walk (TOOL6g), so a reading alone would start it into the same refusal at
    /// every look: it is held as AGT3b holds a refused one, until a person looks again.
    /// </summary>
    [Fact]
    public async Task The_tool_s_own_sign_in_refused_is_held_until_a_person_looks_again()
    {
        using var service = _ledger.Client();
        var roster = Built();
        var failed = Observation.Conclude(0, "Open", turnFailed: Refusal);

        var conclusion = Driver(service, roster).AccountSignedOut(
            failed, AdapterSet.Built().Resolve("acp-stub"), new HarnessSelection(null), Refusal);

        Assert.Equal("account.signed-out-own", conclusion.Parts![^1].Code);
        Assert.EndsWith(
            "The agent refused `stub`'s own sign-in, so Daoris starts nothing more on it until you sign in again at your terminal "
            + "and read it again on the agent's page in Agents.",
            conclusion.Note);
        Assert.Equal(LoginState.Out, AccountReads.Of(_home, "stub").Own!.Login);

        var held = await roster.SelectAsync("acp-stub", DriverConfig.Empty with { Adapter = "acp-stub" }, null, null);
        Assert.Equal(AccountReadiness.Refused, held.NotReady);
        Assert.Equal(
            "an earlier session found that the agent refused `stub`'s own sign-in, so Daoris starts nothing more on it until you "
            + "sign in again at your terminal and read it again on the agent's page in Agents.",
            held.Refusal);

        roster.LookedAgain();
        var looked = await roster.SelectAsync("acp-stub", DriverConfig.Empty with { Adapter = "acp-stub" }, null, null);
        Assert.Null(looked.NotReady);
    }

    /// <summary>AGT3b's refused credential on the native door reads the account signed out too, beside its hold.</summary>
    [Fact]
    public async Task A_credential_its_provider_refused_reads_its_account_signed_out_beside_AGT3b_s_hold()
    {
        using var service = _ledger.Client();
        Accounts("stub", "account-1");
        var roster = Built();
        var transcript = Path.Combine(_home, "s1.log");
        File.WriteAllText(transcript, "working on it\nFailed to authenticate. API Error: 401 API key is invalid.\n");

        var conclusion = Driver(service, roster).AccountRefused(
            new SessionConclusion("failed", "exit 1 before taking its quest."), AdapterSet.Built().Resolve("stub"),
            new HarnessSelection(null, "account-1"), transcript);

        Assert.Equal("account.refused", conclusion.Parts![^1].Code);
        Assert.Equal(new AccountRead(LoginState.Out, Seen), ReadOf("stub", "account-1"));
        Wire(s => s.WithDefault("stub", "account-1"));
        var held = await roster.SelectAsync("stub", DriverConfig.Empty with { Adapter = "stub" }, null, null);
        Assert.Equal(AccountReadiness.Refused, held.NotReady);
    }

    // ——— Never a strike (D58 as D125 amends it, and now for an account's sign-in): the fault is the account's.

    /// <summary>The refused start's record, as the driver writes it: failed, its note saying the account's sign-in.</summary>
    private async Task<string> RefusedAsync(ServiceClient service, HarnessRoster roster, bool accountLine = true)
    {
        _ledger.Publish("q1", "engine");
        var (id, _) = await service.OpenSessionAsync("q1", "acp-stub", null, "account-1", Path.Combine(_home, "engine"), null);
        await service.AdvanceAsync(id!, "working");
        var failed = Observation.Conclude(0, "Open", turnFailed: Refusal);
        var concluded = accountLine
            ? Driver(service, roster).AccountSignedOut(failed, AdapterSet.Built().Resolve("acp-stub"), new HarnessSelection(null, "account-1"), Refusal)
            : failed;
        await service.AdvanceAsync(id!, concluded.State, note: concluded.Note, parts: concluded.Parts);
        return id!;
    }

    [Fact]
    public async Task A_start_refused_for_its_sign_in_is_never_a_strike_and_its_record_says_why()
    {
        using var service = _ledger.Client();
        var id = await RefusedAsync(service, Built());

        var record = _ledger.Session(id);
        Assert.Equal("failed", record["state"]!.GetValue<string>());
        Assert.Contains("for its sign-in", record["note"]!.GetValue<string>());
        Assert.Empty(ServiceClient.ReadStrikes(JsonSerializer.Serialize(_ledger.Sessions)));
    }

    /// <summary>
    /// A look after the refusal, one failure parking a quest: it is not parked. Its start holds before anything is spawned,
    /// on the protocol stub with nothing named to run, which is a hold of its own; the same failure without the account's line
    /// parks it, so the line is what spares it.
    /// </summary>
    [Fact]
    public async Task A_look_after_the_refusal_does_not_park_the_quest()
    {
        using var service = _ledger.Client();
        var roster = Built();
        await RefusedAsync(service, roster);

        var look = await Driver(service, roster).TickAsync().WaitAsync(Bound);

        var sitting = Assert.Single(look.Considerations);
        Assert.NotEqual(StartVerdict.Exhausted, sitting.Verdict);
        Assert.Contains("`acp-stub` is not installed", sitting.Reason);
    }

    [Fact]
    public async Task The_same_failure_without_the_account_s_line_is_a_strike()
    {
        using var service = _ledger.Client();
        var roster = Built();
        await RefusedAsync(service, roster, accountLine: false);

        var look = await Driver(service, roster).TickAsync().WaitAsync(Bound);

        Assert.Equal(StartVerdict.Exhausted, Assert.Single(look.Considerations).Verdict);
        Assert.Equal(1, ServiceClient.ReadStrikes(JsonSerializer.Serialize(_ledger.Sessions))["q1"]);
    }
}
