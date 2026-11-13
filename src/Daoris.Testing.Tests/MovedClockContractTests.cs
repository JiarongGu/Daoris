using Daoris.Ports.Clocks;

namespace Daoris.Testing.Tests;

/// <summary>The fake's side of the clock contract: the cases the machine's clock passes too, run on a clock moved by hand.</summary>
public sealed class MovedClockContractTests : ClockContract
{
    private readonly MovedClock _clock = new() { VirtualTimestamp = true };
    private readonly DateTimeOffset _epoch;

    public MovedClockContractTests() => _epoch = _clock.GetUtcNow();

    protected override TimeProvider Backend => _clock;

    protected override QuietClock Quiet => new(() => _clock.GetUtcNow() - _epoch, stopsInSleep: true);

    protected override Task PassAsync(TimeSpan by)
    {
        _clock.Advance(by);
        return Task.CompletedTask;
    }
}
