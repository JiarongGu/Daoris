using System.ComponentModel;
using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-i (D212; the foundation design §3.3, §13.4, §19.1): the lease's test of a live process, taken out of <c>HomeLease</c> so the
/// move record's claim reads a run's liveness by the same test, never a copy of it. A process is alive while its id runs under the start
/// time it named, within a second; an id no process has, or one running under another start, is gone; a process whose start time cannot be
/// read counts as alive, since nothing can tell.
/// </summary>
/// <remarks>Nothing here starts a process: this test process is a live one, and an id no process has is a gone one.</remarks>
public sealed class ProcessLivenessTests
{
    /// <summary>A process id no process has: Windows hands out multiples of four, from the bottom.</summary>
    private const int NoProcess = 0x7FFFFFFC;

    private static (int Pid, long Started) Self()
    {
        using var self = Process.GetCurrentProcess();
        return (self.Id, self.StartTime.ToUniversalTime().Ticks);
    }

    [Fact]
    public void A_process_running_under_the_start_it_named_is_alive_within_a_second()
    {
        var (pid, started) = Self();

        Assert.True(ProcessLiveness.Alive(pid, started));
        Assert.True(ProcessLiveness.Alive(pid, started + TimeSpan.TicksPerSecond / 2));
        Assert.True(ProcessLiveness.Alive(pid, started - TimeSpan.TicksPerSecond / 2));
    }

    [Fact]
    public void An_id_running_under_another_start_is_another_process_and_the_one_named_is_gone()
    {
        var (pid, started) = Self();

        Assert.False(ProcessLiveness.Alive(pid, started + TimeSpan.TicksPerSecond));
        Assert.False(ProcessLiveness.Alive(pid, started - 5 * TimeSpan.TicksPerSecond));
    }

    [Fact]
    public void An_id_no_process_has_is_gone()
    {
        Assert.False(ProcessLiveness.Alive(NoProcess, 0));
    }

    [Fact]
    public void A_process_whose_start_time_cannot_be_read_counts_as_alive()
    {
        // Windows' idle process runs under id 0 and refuses its start to every caller: the lease's "cannot tell". The desktop is Windows'.
        if (!OperatingSystem.IsWindows()) return;

        using (var idle = Process.GetProcessById(0)) Assert.Throws<Win32Exception>(() => idle.StartTime);

        Assert.True(ProcessLiveness.Alive(0, 0));
    }
}
