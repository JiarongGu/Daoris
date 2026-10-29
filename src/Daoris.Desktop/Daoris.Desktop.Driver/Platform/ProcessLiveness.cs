using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// Whether a process named by its id and its start time still runs as the process it named: the home lease's test of a live holder
/// (HOMELEASE1), and since FOUNDATION1f-i the move record's test of a live claim (the foundation design §3.3, §13.4, §19.1), one test for both
/// so a move is taken over from a run exactly when the lease would judge that run's process gone.
/// </summary>
/// <remarks>
/// <para>A process is alive while its id runs under a start time within a second of the one it named; an id no process has, or one that
/// exited as it was looked at, is gone; an id running under another start time is another process, so the one named is gone.</para>
/// <para><b>A start time it may not read counts as alive</b>: nothing can tell, and the lease's <i>cannot tell</i> holds, so neither a lease
/// nor a move is ever taken from a process merely because its start was hidden.</para>
/// <para>The home is one machine's: an id of another machine's process reads as gone (§19.1 point 5). Compiled into the launcher with the lease,
/// so it uses the base library alone.</para>
/// </remarks>
public static class ProcessLiveness
{
    /// <summary>Whether the process <paramref name="pid"/> runs as the one that started at <paramref name="started"/> (UTC ticks), within a second.</summary>
    public static bool Alive(int pid, long started)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return Math.Abs(process.StartTime.ToUniversalTime().Ticks - started) < TimeSpan.TicksPerSecond;
        }
        catch (ArgumentException)
        {
            // No process has that id now.
            return false;
        }
        catch (InvalidOperationException)
        {
            // Exited as we looked.
            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Its start time is not ours to read: cannot tell, so it holds.
            return true;
        }
    }
}
