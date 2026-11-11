using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A wait's kept-aside clause is said only of a kept account that is ready, which its agent's own reading counts too.</summary>
public sealed class KeptAsideSpentTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Seen = new(2026, 10, 1, 14, 0, 0, TimeSpan.FromMinutes(345));

    private static readonly DateTimeOffset Until = new(2026, 10, 3, 16, 2, 0, TimeSpan.FromMinutes(345));

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-kept-" + Guid.NewGuid().ToString("N")[..8]);

    public KeptAsideSpentTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
        foreach (var account in new[] { "account-1", "account-2" }) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", account));
        new HarnessSettings().WithRotation("fake", ["account-1", "account-2"]).WithUse("fake", new UseChange(Keep: "account-2"))
            .Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Command => Path.Combine(_home, "agent-here");

    private HarnessRoster Roster() =>
        new(TestDoors.Registry(new Dictionary<string, Provider>(StringComparer.OrdinalIgnoreCase)
        {
            ["fake"] = TestDoors.Door("fake", new TestAgent(
                Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true, WeekFixed: true)),
        }), DaorisHome.Entry(_home, HomeEntry.Harnesses))
        {
            Clock = () => Seen,
            Zone = Zone,
        };

    private static DriverConfig Config => DriverConfig.Empty with { Actors = StaffedRoles.On("fake") };

    private async Task<HarnessSelection> WaitAsync(bool keptSpent)
    {
        AccountCooling.Cool(_home, new CoolingEntry("fake", "account-1", Until, true, "weekly", Seen, "s1"), Seen);
        if (keptSpent)
        {
            AccountWindows.Said(_home, "fake", "account-2", [new WindowReading("session", 1.0, Seen.AddHours(2), "clear")], Seen.AddMinutes(-20), "s0");
        }

        return await Roster().SelectAsync("fake", Config, "work", null);
    }

    [Fact]
    public async Task A_wait_says_a_ready_kept_account_is_kept_aside()
    {
        var held = await WaitAsync(keptSpent: false);

        Assert.Equal(["held.cooling-account", "held.kept-aside"], NoteAssert.Codes(held.RefusalNote!.Parts));
    }

    [Fact]
    public async Task A_wait_does_not_say_a_kept_account_its_agent_said_is_spent_is_kept_aside()
    {
        var held = await WaitAsync(keptSpent: true);

        Assert.Equal(["held.cooling-account"], NoteAssert.Codes(held.RefusalNote!.Parts));
    }
}
