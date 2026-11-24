using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The person's on/off switch on an account, part of the base account record every agent shares
/// (<c>accounts.json</c>) and read by the one account walk. An account switched off stays signed in and is read as before, but no
/// start runs on it and no move goes to it; a session on it goes on to its turn's end, and the next resume moves it. Every case
/// runs for two agents, since the switch is the account's, never one harness's.
/// </summary>
/// <remarks>Nothing here starts a process: the stand-in agents are present by a file look and ask nobody who is signed in.</remarks>
public sealed class AccountSwitchTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.Utc;

    private static readonly DateTimeOffset Seen = new(2026, 11, 24, 3, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-account-switch-" + Guid.NewGuid().ToString("N")[..8]);

    public AccountSwitchTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    public static TheoryData<string> Agents => ["fake", "fake-two"];

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    private string Command => Path.Combine(_home, "agent-here");

    private HarnessRoster Roster() =>
        new(TestDoors.Registry(
            [
                TestDoors.Door("fake", new TestAgent(Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true)),
                TestDoors.Door("fake-two", new TestAgent(Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_TWO_HOME", ProbeByPresence: true)),
            ]), Settings)
        {
            Clock = () => Seen,
            Zone = Zone,
        };

    private static DriverConfig Config(string agent) => ActorChoices.With(DriverConfig.Empty, "build", [new ActorChoice(agent)]);

    /// <summary>Two accounts of the agent, the first beginning its order.</summary>
    private void Accounts(string agent)
    {
        foreach (var name in new[] { "account-1", "account-2" }) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, agent, name));
        new HarnessSettings().WithDefault(agent, "account-1").WithRotation(agent, ["account-1", "account-2"]).Save(Settings);
    }

    private void Switch(string agent, string account, bool off) =>
        AccountSwitch.Set(_home, agent, HarnessSettings.Profiles(_home, agent), account, off);

    // ——— The record: the account's own entry, beside its name, written by the person's switch alone.

    [Theory]
    [MemberData(nameof(Agents))]
    public void The_switch_is_kept_in_the_account_record_beside_the_name_and_read_back(string agent)
    {
        Accounts(agent);
        var accounts = HarnessSettings.Profiles(_home, agent);
        AccountNames.Rename(_home, agent, accounts, "account-1", "work");

        Assert.True(AccountSwitch.Set(_home, agent, accounts, "account-1", off: true));
        Assert.False(AccountSwitch.Set(_home, agent, accounts, "account-1", off: true));

        Assert.True(AccountSwitch.IsOff(_home, agent, "account-1"));
        Assert.False(AccountSwitch.IsOff(_home, agent, "account-2"));
        Assert.Equal("work", AccountNames.NameOf(_home, agent, "account-1"));
        var entry = JsonNode.Parse(File.ReadAllText(AccountNames.PathOf(_home)))![agent]!["account-1"]!;
        Assert.Equal("work", (string?)entry["name"]);
        Assert.True((bool?)entry["off"]);

        Assert.True(AccountSwitch.Set(_home, agent, accounts, "account-1", off: false));
        Assert.False(AccountSwitch.IsOff(_home, agent, "account-1"));
        Assert.Equal("work", AccountNames.NameOf(_home, agent, "account-1"));
    }

    [Theory]
    [MemberData(nameof(Agents))]
    public void Switching_on_an_account_with_no_name_leaves_no_entry_behind(string agent)
    {
        Accounts(agent);
        var accounts = HarnessSettings.Profiles(_home, agent);

        AccountSwitch.Set(_home, agent, accounts, "account-2", off: true);
        AccountSwitch.Set(_home, agent, accounts, "account-2", off: false);

        Assert.False(File.Exists(AccountNames.PathOf(_home)) && File.ReadAllText(AccountNames.PathOf(_home)).Contains("account-2", StringComparison.Ordinal));
    }

    [Fact]
    public void An_account_that_is_not_here_is_refused_and_nothing_is_written()
    {
        Accounts("fake");

        var refused = Assert.Throws<AccountNameException>(
            () => AccountSwitch.Set(_home, "fake", HarnessSettings.Profiles(_home, "fake"), "account-9", off: true));

        Assert.Equal(AccountNameProblemKind.Missing, refused.Problem.Kind);
        Assert.False(File.Exists(AccountNames.PathOf(_home)));
    }

    [Fact]
    public void Removing_an_account_forgets_its_switch_with_its_name()
    {
        Accounts("fake");
        AccountSwitch.Set(_home, "fake", HarnessSettings.Profiles(_home, "fake"), "account-1", off: true);

        AccountNames.Forget(_home, "fake", "account-1");

        Assert.False(AccountSwitch.IsOff(_home, "fake", "account-1"));
    }

    // ——— The walk: never started on, never moved to, while another is ready; chosen again once switched on.

    [Theory]
    [MemberData(nameof(Agents))]
    public async Task A_roster_with_one_account_off_never_starts_on_it_while_another_is_ready(string agent)
    {
        Accounts(agent);
        var roster = Roster();
        Switch(agent, "account-1", off: true);

        for (var start = 0; start < 5; start++)
        {
            var selection = await roster.SelectAsync(agent, Config(agent), null, null);

            Assert.True(selection.Allowed);
            Assert.Equal("account-2", selection.Profile);
        }

        Assert.Equal(new AccountHeld("account-1", NextHold.Off), Assert.Single(roster.Next(agent).Others));
    }

    [Theory]
    [MemberData(nameof(Agents))]
    public async Task Switching_it_on_makes_it_chosen_again(string agent)
    {
        Accounts(agent);
        var roster = Roster();
        Switch(agent, "account-1", off: true);
        Assert.Equal("account-2", (await roster.SelectAsync(agent, Config(agent), null, null)).Profile);

        Switch(agent, "account-1", off: false);

        Assert.Equal("account-1", (await roster.SelectAsync(agent, Config(agent), null, null)).Profile);
    }

    [Theory]
    [MemberData(nameof(Agents))]
    public async Task An_account_switched_off_is_read_as_before_and_the_agent_page_says_it_is_off(string agent)
    {
        Accounts(agent);
        var roster = Roster();
        Switch(agent, "account-1", off: true);

        var next = roster.Next(agent);

        Assert.Equal("account-2", next.Account);
        Assert.Equal(new AccountHeld("account-1", NextHold.Off), Assert.Single(next.Others));
        var report = await roster.ReportAsync(agent, Config(agent));
        Assert.True(report!.Profiles.Single(profile => profile.Name == "account-1").Off);
        Assert.False(report.Profiles.Single(profile => profile.Name == "account-2").Off);
        // The switch is no sign-in state: no reading of it is written.
        Assert.Null(AccountReads.Of(_home, agent).Accounts.GetValueOrDefault("account-1"));
    }

    [Theory]
    [MemberData(nameof(Agents))]
    public async Task A_start_held_on_the_only_account_says_it_is_off_and_how_to_switch_it_on(string agent)
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, agent, "account-1"));
        new HarnessSettings().WithDefault(agent, "account-1").Save(Settings);
        var roster = Roster();
        Switch(agent, "account-1", off: true);

        var held = await roster.SelectAsync(agent, Config(agent), null, null);

        Assert.False(held.Allowed);
        Assert.Equal(AccountReadiness.Off, held.NotReady);
        Assert.Contains($"`daoris agent account on {agent} account-1`", held.Refusal);
        Assert.Equal("held.account-off", held.RefusalNote!.Parts[0].Code);
    }

    [Theory]
    [MemberData(nameof(Agents))]
    public async Task Every_account_off_holds_the_start_naming_each(string agent)
    {
        Accounts(agent);
        var roster = Roster();
        Switch(agent, "account-1", off: true);
        Switch(agent, "account-2", off: true);

        var held = await roster.SelectAsync(agent, Config(agent), null, null);

        Assert.False(held.Allowed);
        Assert.Contains("`account-1` is switched off, `account-2` is switched off", held.Refusal);
    }

    [Theory]
    [MemberData(nameof(Agents))]
    public async Task A_person_s_pick_of_an_account_switched_off_is_refused_never_moved(string agent)
    {
        Accounts(agent);
        var roster = Roster();
        Switch(agent, "account-1", off: true);

        var held = await roster.SelectAsync(agent, Config(agent), null, "account-1");

        Assert.False(held.Allowed);
        Assert.Equal(AccountReadiness.Off, held.NotReady);
    }

    // ——— A session on it when it is switched off: it ends its turn, and the next resume moves it.

    [Theory]
    [MemberData(nameof(Agents))]
    public async Task A_session_on_it_when_switched_off_ends_its_turn_and_moves(string agent)
    {
        Accounts(agent);
        var roster = Roster();
        var before = await roster.ResumeAsync(agent, Config(agent), null, "account-1", StartKind.Driven, line: LineKind.DrivenOpen);
        Assert.Equal((NextHold.Ready, "account-1"), (before.Own, before.Selection.Profile));

        Switch(agent, "account-1", off: true);
        var after = await roster.ResumeAsync(agent, Config(agent), null, "account-1", StartKind.Driven, line: LineKind.DrivenOpen);

        Assert.Equal(NextHold.Off, after.Own);
        Assert.True(after.Switched);
        Assert.False(after.Waits);
        Assert.Equal("account-2", after.Selection.Profile);
    }

    [Theory]
    [MemberData(nameof(Agents))]
    public async Task A_conversation_on_it_moves_to_another_account_at_its_next_turn(string agent)
    {
        Accounts(agent);
        var roster = Roster();
        Switch(agent, "account-1", off: true);

        var after = await roster.ResumeAsync(agent, Config(agent), null, "account-1", StartKind.Conversation, line: LineKind.Conversation(carries: true));

        Assert.Equal((NextHold.Off, true, "account-2"), (after.Own, after.Switched, after.Selection.Profile));
    }

    [Theory]
    [MemberData(nameof(Agents))]
    public async Task A_session_on_it_with_no_other_account_ready_is_held_not_waited_for_a_reset(string agent)
    {
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, agent, "account-1"));
        new HarnessSettings().WithDefault(agent, "account-1").Save(Settings);
        var roster = Roster();
        Switch(agent, "account-1", off: true);

        var held = await roster.ResumeAsync(agent, Config(agent), null, "account-1", StartKind.Driven, line: LineKind.DrivenOpen);

        Assert.Equal(NextHold.Off, held.Own);
        Assert.False(held.Selection.Allowed);
        Assert.False(held.Waits);
        Assert.Equal(NextHold.Off, roster.OwnHold(agent, "account-1", StartKind.Driven));
        Assert.Equal(LineRepair.SwitchOn, LineAccount.RepairOf(NextHold.Off, notOffered: false));
    }

    [Theory]
    [MemberData(nameof(Agents))]
    public async Task A_resume_of_an_account_still_on_is_untouched_by_another_account_being_off(string agent)
    {
        Accounts(agent);
        var roster = Roster();
        Switch(agent, "account-2", off: true);

        var resume = await roster.ResumeAsync(agent, Config(agent), null, "account-1", StartKind.Driven, line: LineKind.DrivenOpen);

        Assert.Equal((NextHold.Ready, false, "account-1"), (resume.Own, resume.Switched, resume.Selection.Profile));
    }

    // ——— The two real agents: the same switch, read the same way.

    [Theory]
    [InlineData("claude-code")]
    [InlineData("codex")]
    public void The_built_agents_read_the_switch_the_same_way(string agent)
    {
        foreach (var name in new[] { "account-1", "account-2" }) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, agent, name));
        new HarnessSettings().WithRotation(agent, ["account-1", "account-2"]).Save(Settings);
        var roster = new HarnessRoster(ProviderRegistry.Built(), Settings) { Clock = () => Seen, Zone = Zone };

        AccountSwitch.Set(_home, agent, HarnessSettings.Profiles(_home, agent), "account-1", off: true);
        var next = roster.Next(agent);

        Assert.Equal(new AccountHeld("account-1", NextHold.Off), Assert.Single(next.Others));
        Assert.Equal("account-2", next.Account);
    }
}
