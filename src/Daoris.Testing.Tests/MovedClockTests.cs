namespace Daoris.Testing.Tests;

public sealed class MovedClockTests
{
    [Fact]
    public async Task A_delay_ends_only_when_the_clock_is_moved_past_it()
    {
        var clock = new MovedClock();
        var delay = Task.Delay(TimeSpan.FromMinutes(5), clock, CancellationToken.None);
        await clock.Timed;

        clock.Advance(TimeSpan.FromMinutes(4));
        Assert.False(delay.IsCompleted);
        Assert.True(clock.Waiting);

        clock.Advance(TimeSpan.FromMinutes(1));
        await delay;
        Assert.False(clock.Waiting);
    }

    [Fact]
    public void The_timestamps_follow_the_clock_only_where_asked()
    {
        var virtualClock = new MovedClock { VirtualTimestamp = true };
        var before = virtualClock.GetTimestamp();
        virtualClock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(TimeSpan.FromSeconds(30), virtualClock.GetElapsedTime(before, virtualClock.GetTimestamp()));
        Assert.Equal(System.Diagnostics.Stopwatch.Frequency, new MovedClock().TimestampFrequency);
    }
}
