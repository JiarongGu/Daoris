using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A step that meets a file another process holds for an instant is started again, never made the check's unknown.</summary>
public sealed class HeavyStepRetryTests
{
    private static IOException Held() => new("in use", unchecked((int)0x80070020));

    [Fact]
    public async Task A_start_held_off_for_an_instant_is_taken_again_and_runs()
    {
        if (!OperatingSystem.IsWindows()) return;
        var tried = 0;

        var started = await HeavyStep.RetriedAsync(() => ++tried < 3 ? throw Held() : "run", TimeSpan.Zero, new MovedClock(), CancellationToken.None);

        Assert.Equal(("run", 3), (started, tried));
    }

    [Fact]
    public async Task A_start_held_off_past_its_tries_is_the_error_it_was()
    {
        if (!OperatingSystem.IsWindows()) return;
        var tried = 0;

        await Assert.ThrowsAsync<IOException>(() => HeavyStep.RetriedAsync<string>(() => { tried++; throw Held(); }, TimeSpan.Zero, new MovedClock(), CancellationToken.None));

        Assert.Equal(HeavyStep.Tries, tried);
    }

    [Fact]
    public async Task Any_other_io_error_is_not_taken_again()
    {
        var tried = 0;

        await Assert.ThrowsAsync<IOException>(() => HeavyStep.RetriedAsync<string>(() => { tried++; throw new IOException("disk"); }, TimeSpan.Zero, new MovedClock(), CancellationToken.None));

        Assert.Equal(1, tried);
    }
}
