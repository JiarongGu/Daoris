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

    /// <summary>Runs <paramref name="run"/> on <paramref name="clock"/>, moving it by exactly each wait the code makes.</summary>
    private static async Task<T> DriveAsync<T>(MovedClock clock, Task<T> run, Action<int, TimeSpan>? waited = null)
    {
        var waits = 0;
        for (var spins = 0; !run.IsCompleted; spins++)
        {
            Assert.True(spins < 10_000_000, "the run never finished");
            if (clock.Waiting)
            {
                var wait = clock.Pending[0] - clock.GetUtcNow();
                waited?.Invoke(++waits, wait);
                clock.Advance(wait);
            }

            await Task.Yield();
        }

        return await run;
    }

    private static string HeldLog()
    {
        var folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "gate-log-held", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "gate-07-reached.log");
    }

    [Fact]
    public async Task A_gate_log_held_open_by_another_handle_for_a_moment_is_written_after_the_wait()
    {
        if (!OperatingSystem.IsWindows()) return;
        var log = HeldLog();
        try
        {
            var holder = new FileStream(log, FileMode.Create, FileAccess.Write, FileShare.Read);
            var clock = new MovedClock();

            var kept = await DriveAsync(clock, HeavyStep.WriteLogAsync(log, "error: it would not start: no\n", clock), (waits, _) =>
            {
                if (waits == 2) holder.Dispose();
            });

            Assert.True(kept);
            Assert.Equal("error: it would not start: no\n", File.ReadAllText(log));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(log)!, recursive: true);
        }
    }

    [Fact]
    public async Task A_gate_log_held_past_every_try_is_not_written_and_throws_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        var log = HeldLog();
        try
        {
            using var holder = new FileStream(log, FileMode.Create, FileAccess.Write, FileShare.Read);
            var clock = new MovedClock();

            Assert.False(await DriveAsync(clock, HeavyStep.WriteLogAsync(log, "words", clock)));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(log)!, recursive: true);
        }
    }

    [Fact]
    public async Task The_wait_between_tries_doubles_so_a_hold_of_a_few_seconds_is_waited_out()
    {
        if (!OperatingSystem.IsWindows()) return;
        var clock = new MovedClock();
        var tried = 0;
        var waits = new List<TimeSpan>();

        var run = HeavyStep.RetriedAsync<string>(() => { tried++; throw Held(); }, TimeSpan.FromMilliseconds(100), clock, CancellationToken.None);
        await Assert.ThrowsAsync<IOException>(() => DriveAsync(clock, run, (_, wait) => waits.Add(wait)));

        Assert.Equal(HeavyStep.Tries, tried);
        Assert.Equal([100, 200, 400, 800, 1600], waits.Select(wait => (int)wait.TotalMilliseconds));
    }

    [Fact]
    public async Task Any_other_io_error_is_not_taken_again()
    {
        var tried = 0;

        await Assert.ThrowsAsync<IOException>(() => HeavyStep.RetriedAsync<string>(() => { tried++; throw new IOException("disk"); }, TimeSpan.Zero, new MovedClock(), CancellationToken.None));

        Assert.Equal(1, tried);
    }
}
