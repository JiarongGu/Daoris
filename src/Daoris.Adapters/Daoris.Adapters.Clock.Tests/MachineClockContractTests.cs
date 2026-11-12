using Daoris.Ports.Clocks;
using Daoris.Testing;
using Xunit;

namespace Daoris.Adapters.Clock.Tests;

/// <summary>The real side of the clock contract: the same cases the fake passes, on the machine's clock.</summary>
[Trait("Needs", "Clock")]
public sealed class MachineClockContractTests : ClockContract
{
    protected override TimeProvider Backend => MachineClock.Time;

    protected override QuietClock Quiet => MachineClock.Quiet;

    protected override Task PassAsync(TimeSpan by) => Task.Delay(by);
}

/// <summary>What only the machine's clock says.</summary>
[Trait("Needs", "Clock")]
public sealed class MachineClockTests
{
    [Fact]
    public void The_machines_quiet_clock_stops_in_sleep_where_the_platform_can_say_so()
    {
        Assert.Equal(OperatingSystem.IsWindows(), MachineClock.Quiet.StopsInSleep);
    }

    [Fact]
    public void The_time_is_the_platforms_system_clock()
    {
        Assert.Same(TimeProvider.System, MachineClock.Time);
    }
}
