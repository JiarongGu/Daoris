using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An account a pick or a hand-over names whose folder is gone is not run on: a start would spawn against a configuration home
/// that does not exist. The person's pick is refused, never moved to another account; a handed quest's account walks on within the
/// agent's accounts, as it does where the account is cooling or signed out.
/// </summary>
public sealed class PickedAccountGoneTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-gone-" + Guid.NewGuid().ToString("N")[..8]);

    public PickedAccountGoneTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
        foreach (var name in new[] { "account-1", "account-2" }) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", name));
        new HarnessSettings().WithDefault("fake", "account-1").WithRotation("fake", ["account-1", "account-2"]).Save(Settings);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    private string Command => Path.Combine(_home, "agent-here");

    private HarnessRoster Roster() =>
        new(TestDoors.Registry([TestDoors.Door("fake", new TestAgent(
            Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true), transport: Transport.Native)]), Settings);

    private static DriverConfig Config => DriverConfig.Empty with { Actors = StaffedRoles.On("fake") };

    [Fact]
    public async Task A_pick_whose_folder_is_gone_is_refused_naming_the_account_and_nothing_is_run_on_it()
    {
        var selection = await Roster().SelectAsync("fake", Config, null, "acct-3");

        Assert.False(selection.Allowed);
        Assert.Null(selection.Profile);
        Assert.Contains("acct-3", selection.Refusal);
        Assert.False(Directory.Exists(HarnessSettings.ProfileHome(_home, "fake", "acct-3")));
    }

    [Fact]
    public async Task A_pick_that_is_here_still_runs()
    {
        var selection = await Roster().SelectAsync("fake", Config, null, "account-2");

        Assert.True(selection.Allowed);
        Assert.Equal("account-2", selection.Profile);
    }

    [Fact]
    public async Task A_handed_account_whose_folder_is_gone_walks_on_within_the_agent_s_accounts()
    {
        var (selection, passed) = await Roster().SelectHandedAsync("fake", Config, null, "acct-3", StartKind.Driven);

        Assert.Equal("acct-3", passed);
        Assert.True(selection.Allowed);
        Assert.Equal("account-1", selection.Profile);
    }
}
