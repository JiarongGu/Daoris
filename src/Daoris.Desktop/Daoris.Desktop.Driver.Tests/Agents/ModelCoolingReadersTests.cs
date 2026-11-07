using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The readers of a cool-off that know a start's model read the model's cool-off as the walk does (MODELROLE1k2): an account at its Opus
/// week is held for an Opus line and passed for a Sonnet one, in the roster's own-account and every-account reads, and in the accounts a
/// running session could move to. A reader with no model reads every cool-off, as the walk does for account default.
/// </summary>
public sealed class ModelCoolingReadersTests : IDisposable
{
    private static readonly DateTimeOffset Seen = new(2026, 10, 1, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Until = Seen.AddDays(2);
    private const string Nowhere = "daoris-modelrole1k2-no-such-command";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-model-readers-" + Guid.NewGuid().ToString("N")[..8]);

    public ModelCoolingReadersTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static CoolingEntry Entry(string account, string? model = "opus", DateTimeOffset? until = null) =>
        new("fake", account, until ?? Until, true, "weekly", Seen, "3f9c2a71", Model: model);

    private HarnessRoster Roster(params string[] accounts)
    {
        var settings = DaorisHome.Entry(_home, HomeEntry.Harnesses);
        foreach (var account in accounts) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", account));
        new HarnessSettings().WithDefault("fake", accounts[0]).Save(settings);
        var agent = new TestAgent(
            Binary: [Nowhere], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true,
            Limits: ClaudeLimits.Words, Windows: ClaudeWindows.Words, RunsInOwnHome: true);
        return new(TestDoors.Registry(TestDoors.Door("fake", agent)), settings) { Clock = () => Seen };
    }

    [Fact]
    public void An_account_at_its_opus_week_is_held_for_an_opus_line_and_passed_for_a_sonnet_one()
    {
        var roster = Roster("account-1");
        AccountCooling.Cool(_home, Entry("account-1"), Seen);

        Assert.Equal(Entry("account-1"), roster.CoolingOf("fake", "account-1", "opus"));
        Assert.Equal(Entry("account-1"), roster.CoolingOf("fake", "account-1", "claude-opus-4-5"));
        Assert.Null(roster.CoolingOf("fake", "account-1", "sonnet"));
        // A line at account default runs whatever the agent chooses, so the family's cool-off holds it.
        Assert.Equal(Entry("account-1"), roster.CoolingOf("fake", "account-1"));
    }

    [Fact]
    public void An_account_wide_cool_off_holds_every_line()
    {
        var roster = Roster("account-1");
        AccountCooling.Cool(_home, Entry("account-1", model: null), Seen);

        Assert.Equal(Entry("account-1", model: null), roster.CoolingOf("fake", "account-1", "opus"));
        Assert.Equal(Entry("account-1", model: null), roster.CoolingOf("fake", "account-1", "sonnet"));
    }

    [Fact]
    public void The_cool_offs_of_an_agent_a_line_waits_on_leave_out_a_family_it_does_not_run()
    {
        var roster = Roster("account-1", "account-2");
        AccountCooling.Cool(_home, Entry("account-1", until: Seen.AddDays(1)), Seen);
        AccountCooling.Cool(_home, Entry("account-2", model: null, until: Seen.AddDays(3)), Seen);

        Assert.Equal(["account-1", "account-2"], roster.CoolingsOf("fake", "opus").Select(entry => entry.Account));
        Assert.Equal(["account-2"], roster.CoolingsOf("fake", "sonnet").Select(entry => entry.Account));
        Assert.Equal(2, roster.CoolingsOf("fake").Count);
    }

    [Fact]
    public void An_account_at_its_opus_week_is_one_a_sonnet_line_could_move_to_and_an_opus_line_could_not()
    {
        var roster = Roster("account-1", "account-2");
        AccountCooling.Cool(_home, Entry("account-2"), Seen);

        bool Ready(string model) => roster.SwitchCandidates("fake", ["account-2"], ("fake", null, model)).Single().Ready;

        Assert.False(Ready("opus"));
        Assert.True(Ready("sonnet"));
    }
}
