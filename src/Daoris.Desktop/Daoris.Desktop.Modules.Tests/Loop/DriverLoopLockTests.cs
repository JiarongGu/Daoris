using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The desktop's loop takes the same home lease a terminal's does (DRV8a, D104; HOMELEASE1). A stray headless loop took
/// a quest two seconds before the desktop's own; one live driver per home is the rule for both doors, and one writer per home
/// is the lease's: a terminal's verb acting alone holds it for its act, and the desktop's start waits on that hold.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class DriverLoopLockTests : Bridge
{
    private string LeaseFile => HomeLease.PathIn(Home);

    private DriverLoop Loop(TimeSpan? tellAfter = null) =>
        new(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0")
        {
            HoldRetry = TimeSpan.FromMilliseconds(100), TellAfter = tellAfter ?? HomeLease.Wait,
        };

    /// <summary>A lease by hand, naming this test process: a live holder elsewhere on the machine, without starting one.</summary>
    private void Plant(string kind, string? what = null)
    {
        using var self = Process.GetCurrentProcess();
        var lease = new JsonObject
        {
            ["kind"] = kind, ["pid"] = Environment.ProcessId,
            ["started"] = self.StartTime.ToUniversalTime().Ticks, ["since"] = "2026-09-30T08:02:13+00:00",
        };
        if (what is not null) lease["what"] = what;
        Directory.CreateDirectory(Path.GetDirectoryName(LeaseFile)!);
        File.WriteAllText(LeaseFile, lease.ToJsonString());
    }

    [Fact]
    public async Task The_desktops_loop_holds_the_home_it_drives_and_lets_it_go()
    {
        var held = await Loop().HoldHomeAsync(CancellationToken.None);

        Assert.NotNull(held);
        var holder = HomeLease.HeldBy(Home);
        Assert.Equal(LeaseKind.Desktop, holder!.Kind);
        Assert.Equal(Environment.ProcessId, holder.Pid);

        held!.Dispose();
        Assert.False(File.Exists(LeaseFile));
    }

    /// <summary>
    /// 🔴 A home a headless loop drives: the desktop's loop waits, says so once — naming the other driver —
    /// and starts the moment that one lets go.
    /// </summary>
    [Fact]
    public async Task A_home_a_headless_loop_drives_is_waited_for_and_said_once()
    {
        Plant("headless");

        using var closing = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var holding = Loop().HoldHomeAsync(closing.Token);

        await UntilAsync(() => Said().Any());
        await Task.Delay(500);
        Assert.False(holding.IsCompleted);
        var said = Assert.Single(Said());
        Assert.Contains("headless `daoris-driver`", said);
        Assert.Contains($"pid {Environment.ProcessId}", said);

        // The headless loop stops, and lets its home go.
        File.Delete(LeaseFile);
        using var held = await holding.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotNull(held);
        Assert.Equal(LeaseKind.Desktop, HomeLease.HeldBy(Home)!.Kind);
    }

    /// <summary>
    /// 🔴 HOMELEASE1: the desktop starting while a terminal's verb holds the home for its act waits on that hold. It says nothing
    /// for a hold as long as a press, and once the hold has lasted its wait, says so once, naming the verb; it takes the home the
    /// moment the verb lets go.
    /// </summary>
    [Fact]
    public async Task The_desktops_start_waits_on_a_verbs_hold_and_says_so_once_it_lasts()
    {
        Plant("verb", "daoris-driver trees sync");

        using var closing = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var holding = Loop(tellAfter: TimeSpan.FromMilliseconds(800)).HoldHomeAsync(closing.Token);

        await Task.Delay(300);
        Assert.False(holding.IsCompleted);
        Assert.Empty(Said());

        await UntilAsync(() => Said().Any());
        var said = Assert.Single(Said());
        Assert.Contains("a terminal's `daoris-driver trees sync`", said);
        Assert.Contains("has not let it go", said);

        File.Delete(LeaseFile);
        using var held = await holding.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotNull(held);
        Assert.Equal(LeaseKind.Desktop, HomeLease.HeldBy(Home)!.Kind);
    }

    /// <summary>Closing the shell while it waits ends the wait with nothing held.</summary>
    [Fact]
    public async Task Closing_while_waiting_holds_nothing()
    {
        Plant("headless");

        using var closing = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));

        Assert.Null(await Loop().HoldHomeAsync(closing.Token));
        Assert.Equal(LeaseKind.Headless, HomeLease.HeldBy(Home)!.Kind);
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
