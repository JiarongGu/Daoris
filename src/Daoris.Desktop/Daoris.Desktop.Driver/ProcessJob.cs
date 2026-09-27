using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Daoris.Driver;

/// <summary>
/// Everything a spawned harness starts, held together so it ends together (ORPHAN1).
/// </summary>
/// <remarks>
/// <para>🔴 Measured on FG5's run: a session started its repository's dev servers from a background
/// shell, its turn ended, the agent exited, and the servers kept running, holding two ports and a tree
/// whose session was over. <see cref="Process.Kill(bool)"/> walks a tree from a live root, and by then
/// there was no root: the agent had exited on its own and its descendants were nobody's children.</para>
///
/// <para>On Windows the process joins a job object that kills on close, and every process it starts
/// joins the same job, detached or not, unless the job allows breaking away, and this one does not.
/// Disposing the handle ends them all, and so does this process exiting, since the handle closes with
/// it. Elsewhere this is a no-op: the platform where the orphan was measured is the one it holds.</para>
///
/// <para>Best effort by construction. A process that exited before it could join, or a job the
/// system refused, leaves the session running exactly as it did before this existed.</para>
/// </remarks>
public sealed class ProcessJob : IDisposable
{
    private IntPtr _handle;

    private ProcessJob(IntPtr handle) => _handle = handle;

    /// <summary>Put <paramref name="process"/> and everything it will start into one job that ends on close.</summary>
    public static ProcessJob Hold(Process process)
    {
        if (!OperatingSystem.IsWindows()) return new ProcessJob(IntPtr.Zero);

        var job = CreateJobObject(IntPtr.Zero, null);
        if (job == IntPtr.Zero) return new ProcessJob(IntPtr.Zero);

        var limits = new JobObjectExtendedLimitInformation
        {
            BasicLimitInformation = new JobObjectBasicLimitInformation { LimitFlags = KillOnJobClose },
        };
        var size = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
        var joined = false;
        try
        {
            joined = SetInformationJobObject(job, ExtendedLimitInformation, ref limits, (uint)size)
                     && AssignProcessToJobObject(job, process.Handle);
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Exited before it could join, or its handle is not ours to take: nothing to hold.
        }

        if (joined) return new ProcessJob(job);

        CloseHandle(job);
        return new ProcessJob(IntPtr.Zero);
    }

    /// <summary>End every process still in the job.</summary>
    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (handle != IntPtr.Zero) CloseHandle(handle);
    }

    private const uint KillOnJobClose = 0x2000;
    private const int ExtendedLimitInformation = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(
        IntPtr job, int infoClass, ref JobObjectExtendedLimitInformation info, uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
