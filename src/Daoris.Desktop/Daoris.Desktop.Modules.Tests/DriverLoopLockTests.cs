using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The desktop's loop takes the same home lock a terminal's does (DRV8a, D104). A stray headless loop took
/// a quest two seconds before the desktop's own; one live driver per home is the rule for both doors.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class DriverLoopLockTests : Bridge
{
    private string LockFile => Path.Combine(Home, "driver.lock");

    private DriverLoop Loop() =>
        new(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0") { HoldRetry = TimeSpan.FromMilliseconds(100) };

    [Fact]
    public async Task The_desktops_loop_holds_the_home_it_drives_and_lets_it_go()
    {
        var held = await Loop().HoldHomeAsync(CancellationToken.None);

        Assert.NotNull(held);
        var holder = DriverLock.HeldBy(Home);
        Assert.Equal(DriverKind.Desktop, holder!.Kind);
        Assert.Equal(Environment.ProcessId, holder.Pid);

        held!.Dispose();
        Assert.False(File.Exists(LockFile));
    }

    /// <summary>
    /// 🔴 A home a headless loop drives: the desktop's loop waits, says so once — naming the other driver —
    /// and starts the moment that one lets go.
    /// </summary>
    [Fact]
    public async Task A_home_a_headless_loop_drives_is_waited_for_and_said_once()
    {
        using (var self = Process.GetCurrentProcess())
        {
            File.WriteAllText(LockFile, new JsonObject
            {
                ["kind"] = "headless", ["pid"] = Environment.ProcessId,
                ["started"] = self.StartTime.ToUniversalTime().Ticks, ["since"] = "2026-09-30T08:02:13+00:00",
            }.ToJsonString());
        }

        using var closing = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var holding = Loop().HoldHomeAsync(closing.Token);

        await UntilAsync(() => Said().Any());
        await Task.Delay(500);
        Assert.False(holding.IsCompleted);
        var said = Assert.Single(Said());
        Assert.Contains("headless `daoris-driver`", said);
        Assert.Contains($"pid {Environment.ProcessId}", said);

        // The headless loop stops, and lets its home go.
        File.Delete(LockFile);
        using var held = await holding.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotNull(held);
        Assert.Equal(DriverKind.Desktop, DriverLock.HeldBy(Home)!.Kind);
    }

    /// <summary>Closing the shell while it waits ends the wait with nothing held.</summary>
    [Fact]
    public async Task Closing_while_waiting_holds_nothing()
    {
        using (var self = Process.GetCurrentProcess())
        {
            File.WriteAllText(LockFile, new JsonObject
            {
                ["kind"] = "headless", ["pid"] = Environment.ProcessId,
                ["started"] = self.StartTime.ToUniversalTime().Ticks, ["since"] = "2026-09-30T08:02:13+00:00",
            }.ToJsonString());
        }

        using var closing = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));

        Assert.Null(await Loop().HoldHomeAsync(closing.Token));
        Assert.Equal(DriverKind.Headless, DriverLock.HeldBy(Home)!.Kind);
    }

    private IEnumerable<string> Said() =>
        Raised.Where(m => m.Type == "DRIVER_ERROR")
            .Select(m => JsonSerializer.SerializeToElement(m.Payload).GetProperty("Message").GetString() ?? "");

    private static async Task UntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++) await Task.Delay(100);
        Assert.True(condition(), "the loop never said it was waiting");
    }
}
