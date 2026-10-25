using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The <c>look</c> topic's deadline (FOUNDATION1 mod-vii, the design §14.1), the rule the look held and the events own: <c>pollSeconds</c>
/// since the last whole look ended, a timer's granularity allowed. Handed the time since, never a clock.
/// </summary>
public sealed class LookDeadlineTests
{
    private static DriverConfig Polling(int seconds) => DriverConfig.Empty with { PollSeconds = seconds };

    [Fact]
    public void The_deadline_has_come_at_the_pace_less_a_timers_granularity_and_not_before()
    {
        var pace = TimeSpan.FromSeconds(15);

        Assert.False(LookDeadline.Due(TimeSpan.Zero, Polling(15)));
        Assert.False(LookDeadline.Due(pace - LookDeadline.Early - TimeSpan.FromTicks(1), Polling(15)));
        Assert.True(LookDeadline.Due(pace - LookDeadline.Early, Polling(15)));
        Assert.True(LookDeadline.Due(pace, Polling(15)));
        Assert.True(LookDeadline.Due(TimeSpan.FromHours(1), Polling(15)));
    }

    [Fact]
    public void The_pace_is_the_configurations_and_a_granularity_is_fifty_milliseconds()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(50), LookDeadline.Early);
        Assert.False(LookDeadline.Due(TimeSpan.FromSeconds(14), Polling(60)));
        Assert.True(LookDeadline.Due(TimeSpan.FromSeconds(60), Polling(60)));
    }
}
