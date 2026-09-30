using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// One live driver loop per home (DRV8a, D104). A stray headless loop took a quest two seconds before the
/// desktop's own: the quest lock held, but a second loop on one home is almost never meant.
/// </summary>
/// <remarks>
/// A lock naming THIS test process is a live driver as far as any acquisition can tell — which is what a
/// loop elsewhere on the machine looks like, without spawning one.
/// </remarks>
public sealed class DriverLockTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-driver-lock-" + Guid.NewGuid().ToString("N")[..8]);

    private string LockFile => Path.Combine(_home, "driver.lock");

    public DriverLockTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The file a live driver writes, by hand: the file is the contract (twins.md).</summary>
    private void Plant(string kind, int pid, long started, string since = "2026-09-30T08:02:13+00:00") =>
        File.WriteAllText(LockFile, new JsonObject
        {
            ["kind"] = kind, ["pid"] = pid, ["started"] = started, ["since"] = since,
        }.ToJsonString());

    private static long OurStart()
    {
        using var self = Process.GetCurrentProcess();
        return self.StartTime.ToUniversalTime().Ticks;
    }

    [Fact]
    public void A_free_home_is_taken_named_after_the_driver_and_let_go_on_release()
    {
        var held = DriverLock.TryAcquire(_home, DriverKind.Headless, out var holder);

        Assert.NotNull(held);
        Assert.Null(holder);
        Assert.True(held!.Holds);
        var named = DriverLock.HeldBy(_home);
        Assert.Equal(DriverKind.Headless, named!.Kind);
        Assert.Equal(Environment.ProcessId, named.Pid);

        held.Dispose();
        Assert.False(File.Exists(LockFile));
        Assert.Null(DriverLock.HeldBy(_home));
    }

    /// <summary>🔴 A home a live driver holds is refused, and the refusal names it: which door, its process, and since when.</summary>
    [Fact]
    public void A_home_another_live_driver_holds_is_refused_naming_it()
    {
        Plant("desktop", Environment.ProcessId, OurStart());

        var held = DriverLock.TryAcquire(_home, DriverKind.Headless, out var holder);

        Assert.Null(held);
        Assert.Equal(DriverKind.Desktop, holder!.Kind);
        Assert.Equal(Environment.ProcessId, holder.Pid);
        Assert.Equal(DateTimeOffset.Parse("2026-09-30T08:02:13+00:00"), holder.Since);
        var said = DriverLock.Refusal(_home, holder);
        Assert.Contains("the desktop", said);
        Assert.Contains($"pid {Environment.ProcessId}", said);
        Assert.Contains("since", said);
        Assert.Contains("--share", said);
    }

    /// <summary>The same for two loops in one run: the second never takes a home the first still holds.</summary>
    [Fact]
    public void A_second_loop_on_a_held_home_is_refused_until_the_first_lets_go()
    {
        using var first = DriverLock.TryAcquire(_home, DriverKind.Desktop, out _);
        Assert.NotNull(first);

        Assert.Null(DriverLock.TryAcquire(_home, DriverKind.Headless, out var holder));
        Assert.Equal(DriverKind.Desktop, holder!.Kind);

        first!.Dispose();
        using var second = DriverLock.TryAcquire(_home, DriverKind.Headless, out _);
        Assert.NotNull(second);
    }

    /// <summary>
    /// `--share`, the rare deliberate case: beside a live driver it runs without the lock and says whose
    /// home it shares — and letting go leaves the first driver's lock where it is.
    /// </summary>
    [Fact]
    public void Sharing_runs_beside_a_live_driver_and_leaves_its_lock_alone()
    {
        Plant("desktop", Environment.ProcessId, OurStart());
        var before = File.ReadAllText(LockFile);

        var beside = DriverLock.Share(_home, DriverKind.Headless);

        Assert.False(beside.Holds);
        Assert.Equal(DriverKind.Desktop, beside.Beside!.Kind);
        beside.Dispose();
        Assert.Equal(before, File.ReadAllText(LockFile));
    }

    /// <summary>Sharing a home nobody drives takes it, so the next loop is told of this one.</summary>
    [Fact]
    public void Sharing_a_free_home_takes_it()
    {
        using var shared = DriverLock.Share(_home, DriverKind.Headless);

        Assert.True(shared.Holds);
        Assert.Null(shared.Beside);
        Assert.Equal(DriverKind.Headless, DriverLock.HeldBy(_home)!.Kind);
    }

    /// <summary>
    /// 🔴 A stale lock never blocks: its process gone, its process id now another process's (a start
    /// time that does not match), or a file that does not read. Each is replaced by the loop that asks.
    /// </summary>
    [Theory]
    [InlineData("gone")]
    [InlineData("reused")]
    [InlineData("torn")]
    public void A_stale_lock_never_blocks(string how)
    {
        switch (how)
        {
            case "gone":
                Plant("headless", Exited(), OurStart());
                break;
            case "reused":
                Plant("headless", Environment.ProcessId, OurStart() - TimeSpan.FromMinutes(5).Ticks);
                break;
            default:
                File.WriteAllText(LockFile, "{ \"kind\": \"headless\", \"pid\": ");
                break;
        }

        Assert.Null(DriverLock.HeldBy(_home));
        using var held = DriverLock.TryAcquire(_home, DriverKind.Desktop, out var holder);

        Assert.NotNull(held);
        Assert.Null(holder);
        Assert.Equal(DriverKind.Desktop, DriverLock.HeldBy(_home)!.Kind);
    }

    /// <summary>A release takes away only the lock it wrote — never one another driver has written since.</summary>
    [Fact]
    public void A_release_removes_only_the_lock_it_wrote()
    {
        var held = DriverLock.TryAcquire(_home, DriverKind.Headless, out _);
        Plant("desktop", Environment.ProcessId, OurStart());

        held!.Dispose();

        Assert.True(File.Exists(LockFile));
        Assert.Equal(DriverKind.Desktop, DriverLock.HeldBy(_home)!.Kind);
    }

    /// <summary>A process id no process has any more: one this test started and waited out.</summary>
    private static int Exited()
    {
        using var process = Process.Start(new ProcessStartInfo("dotnet", ["--version"])
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
        })!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return process.Id;
    }
}
