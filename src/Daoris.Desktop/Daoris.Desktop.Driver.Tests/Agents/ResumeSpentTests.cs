using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A record's own account that its reading puts at a whole window is cooling for its resume as it is for the walk.</summary>
public sealed class ResumeSpentTests : IDisposable
{
    private static readonly DateTimeOffset Seen = new(2026, 10, 1, 14, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-resume-spent-" + Guid.NewGuid().ToString("N")[..8]);

    public ResumeSpentTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private HarnessRoster Roster()
    {
        var settings = DaorisHome.Entry(_home, HomeEntry.Harnesses);
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", "account-1"));
        new HarnessSettings().WithDefault("fake", "account-1").Save(settings);
        var agent = new TestAgent(
            Binary: ["daoris-resume-spent-no-such-command"], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true,
            Limits: ClaudeLimits.Words, Windows: ClaudeWindows.Words, RunsInOwnHome: true);
        return new(TestDoors.Registry(TestDoors.Door("fake", agent)), settings) { Clock = () => Seen };
    }

    [Fact]
    public async Task A_resume_on_an_account_whose_weekly_reading_is_whole_finds_it_cooling_until_the_reset()
    {
        var roster = Roster();
        var reset = Seen.AddDays(3);
        AccountWindows.Said(_home, "fake", "account-1", [new WindowReading("weekly", 1.0, reset, "clear", Credits: false)], Seen, "s0");
        var config = DriverConfig.Empty with { Actors = StaffedRoles.On("fake") };

        var resume = await roster.ResumeAsync("fake", config, null, "account-1", StartKind.Driven);

        Assert.Equal(reset, resume.Selection.Cooling?.Until);
    }
}
