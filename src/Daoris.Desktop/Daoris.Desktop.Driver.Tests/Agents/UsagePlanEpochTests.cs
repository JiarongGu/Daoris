using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// One plan, said in two words (a status's tier <c>max_20x</c>, a usage reading's type <c>max</c>), is one binding epoch of the usage
/// history; another plan is another.
/// </summary>
public sealed class UsagePlanEpochTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-planepoch-" + Guid.NewGuid().ToString("N")[..8]);

    public UsagePlanEpochTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly WindowSeen Week = new("seven_day", "weekly", null, 10080, 0.14, 0.01, Now.AddHours(100), UsageHistory.ResetSaid);

    private void Keep(string account, int minutes, string? plan) =>
        UsageHistory.Kept(
            _home,
            new ReadingFrom("claude-code", account, "claude-code-acp", UsageHistory.SourceFrame, 1, Now.AddMinutes(minutes), "s1", plan, plan is null ? null : "get_usage"),
            [Week],
            Now.AddMinutes(minutes));

    [Fact]
    public void A_tier_and_its_type_alternating_stay_one_epoch_and_another_plan_begins_the_next()
    {
        Keep("account-1", 0, "max_20x");
        Keep("account-1", 1, "max");
        UsageHistory.Login(_home, "claude-code", "account-1", LoginState.In, "max_20x", Now.AddMinutes(2));
        Keep("account-1", 3, "max_20x");
        Keep("account-1", 4, "max");
        Assert.Equal(1, UsageHistory.EpochOf(_home, "claude-code", "account-1"));

        Keep("account-1", 5, "team");
        Assert.Equal(2, UsageHistory.EpochOf(_home, "claude-code", "account-1"));
    }

    [Fact]
    public void A_type_first_learned_and_then_its_tier_is_one_epoch_and_the_tier_is_kept()
    {
        Keep("account-2", 0, "max");
        Keep("account-2", 1, "max_5x");
        Keep("account-2", 2, "max");
        Assert.Equal(1, UsageHistory.EpochOf(_home, "claude-code", "account-2"));

        UsageHistory.Login(_home, "claude-code", "account-2", LoginState.In, "max_20x", Now.AddMinutes(3));
        Assert.Equal(2, UsageHistory.EpochOf(_home, "claude-code", "account-2"));
    }
}
