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
///
/// <para>A heavy run (HEAVYRUN1a) holds its gates the other way round, and not as a best effort: an empty job made first
/// (<see cref="Create"/>), each gate started suspended and joined before it runs, its count read (<see cref="Active"/>), and
/// everything in it ended and waited for (<see cref="Terminate"/>, <see cref="WaitEmptyAsync"/>) before the run lets go of its
/// slot. A gate that could not join is not run.</para>
/// </remarks>
public sealed class ProcessJob : IDisposable
{
    private IntPtr _handle;

    private ProcessJob(IntPtr handle) => _handle = handle;

    /// <summary>Put <paramref name="process"/> and everything it will start into one job that ends on close.</summary>
    public static ProcessJob Hold(Process process) => Hold(() => process.Handle);

    /// <summary>
    /// The same, for a process this library started itself and holds the handle of: a terminal's shell,
    /// started suspended under its pseudo-console (CONSOLE4a) and joined before it can start anything.
    /// </summary>
    internal static ProcessJob Hold(IntPtr processHandle) => Hold(() => processHandle);

    private static ProcessJob Hold(Func<IntPtr> processHandle)
    {
        if (!OperatingSystem.IsWindows()) return new ProcessJob(IntPtr.Zero);

        var job = CreateJobObject(IntPtr.Zero, null);
        if (job == IntPtr.Zero) return new ProcessJob(IntPtr.Zero);

        var joined = false;
        try
        {
            joined = KillOnClose(job) && AssignProcessToJobObject(job, processHandle());
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Exited before it could join, or its handle is not ours to take: nothing to hold.
        }

        if (joined) return new ProcessJob(job);

        CloseHandle(job);
        return new ProcessJob(IntPtr.Zero);
    }

    /// <summary>
    /// An empty job that ends what it holds on close, for processes this library starts suspended and joins (<see cref="Join"/>)
    /// before they can start anything: a heavy run's gates (HEAVYRUN1a). Named where <paramref name="name"/> is given, so the next
    /// holder of the run's slot can find one a dead holder left (<see cref="EndNamedAsync"/>). Null off Windows, where there is no
    /// job, and where the system refused one or the name is already another's: a job that is not wholly this one's holds nothing.
    /// </summary>
    internal static ProcessJob? Create(string? name = null)
    {
        if (!OperatingSystem.IsWindows()) return null;

        var job = CreateJobObject(IntPtr.Zero, name);
        if (job == IntPtr.Zero) return null;
        if (Marshal.GetLastWin32Error() == AlreadyExists || !KillOnClose(job))
        {
            CloseHandle(job);
            return null;
        }

        return new ProcessJob(job);
    }

    /// <summary>Put a process this library started suspended into the job, before it can start anything. False where it could not.</summary>
    internal bool Join(IntPtr processHandle)
    {
        var handle = Volatile.Read(ref _handle);
        return handle != IntPtr.Zero && AssignProcessToJobObject(handle, processHandle);
    }

    /// <summary>
    /// How many processes the job holds now, those of jobs nested in it among them; null where there is no job, or the count did not
    /// read. A process leaves the count as it exits, whoever still holds a handle to it.
    /// </summary>
    public int? Active
    {
        get
        {
            var handle = Volatile.Read(ref _handle);
            if (handle == IntPtr.Zero) return null;
            return QueryInformationJobObject(
                handle, BasicAccountingInformation, out var accounting, (uint)Marshal.SizeOf<JobObjectBasicAccountingInformation>(), IntPtr.Zero)
                ? (int)accounting.ActiveProcesses
                : null;
        }
    }

    /// <summary>Have every process in the job end now, the job left open: <see cref="WaitEmptyAsync"/> waits until they have.</summary>
    public void Terminate(uint exitCode = 1)
    {
        var handle = Volatile.Read(ref _handle);
        if (handle != IntPtr.Zero) TerminateJobObject(handle, exitCode);
    }

    /// <summary>
    /// Until the job holds no live process. Never timed: a run's slot is let go only once this returns (HEAVYRUN1a), and a bound would
    /// let it go beside what is still running. A count that does not read ends the wait, and the handle's close ends what is left.
    /// </summary>
    public async Task WaitEmptyAsync()
    {
        while (Active is > 0) await Task.Delay(EmptyPoll).ConfigureAwait(false);
    }

    /// <summary>Whether a job by that name is still there: one some process still holds a handle to.</summary>
    internal static bool Exists(string name)
    {
        if (!OperatingSystem.IsWindows()) return false;
        var job = OpenJobObject(JobQuery, false, name);
        if (job == IntPtr.Zero) return false;
        CloseHandle(job);
        return true;
    }

    /// <summary>
    /// How many processes the job by that name holds, opened for its count alone (COAGENT1b: a restart observing a partner's job); null
    /// where no job answers to the name or its count did not read, which is never "none".
    /// </summary>
    internal static int? ActiveOf(string name)
    {
        if (!OperatingSystem.IsWindows()) return null;
        var handle = OpenJobObject(JobQuery, false, name);
        if (handle == IntPtr.Zero) return null;
        using var job = new ProcessJob(handle);
        return job.Active;
    }

    /// <summary>
    /// End the job a dead holder of a heavy-run slot named, if it is still there, and wait until it holds nothing (HEAVYRUN1a): true
    /// where it was. A name no job answers to is a job whose last handle closed, which ended what it held as it closed.
    /// </summary>
    /// <exception cref="System.ComponentModel.Win32Exception">It is there and would not open: cannot tell, which is never "ended".</exception>
    internal static async Task<bool> EndNamedAsync(string name)
    {
        if (!OperatingSystem.IsWindows()) return false;

        var handle = OpenJobObject(JobQuery | JobTerminate, false, name);
        if (handle == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            if (error is FileNotFound or InvalidName) return false;
            throw new System.ComponentModel.Win32Exception(error);
        }

        using var job = new ProcessJob(handle);
        job.Terminate();
        await job.WaitEmptyAsync().ConfigureAwait(false);
        return true;
    }

    /// <summary>End every process still in the job.</summary>
    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (handle != IntPtr.Zero) CloseHandle(handle);
    }

    private static bool KillOnClose(IntPtr job)
    {
        var limits = new JobObjectExtendedLimitInformation
        {
            BasicLimitInformation = new JobObjectBasicLimitInformation { LimitFlags = KillOnJobClose },
        };
        return SetInformationJobObject(job, ExtendedLimitInformation, ref limits, (uint)Marshal.SizeOf<JobObjectExtendedLimitInformation>());
    }

    private static readonly TimeSpan EmptyPoll = TimeSpan.FromMilliseconds(25);

    private const uint KillOnJobClose = 0x2000;
    private const int BasicAccountingInformation = 1;
    private const int ExtendedLimitInformation = 9;
    private const uint JobQuery = 0x0004;
    private const uint JobTerminate = 0x0008;
    private const int FileNotFound = 2;
    private const int InvalidName = 123;
    private const int AlreadyExists = 183;

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicAccountingInformation
    {
        public long TotalUserTime;
        public long TotalKernelTime;
        public long ThisPeriodTotalUserTime;
        public long ThisPeriodTotalKernelTime;
        public uint TotalPageFaultCount;
        public uint TotalProcesses;
        public uint ActiveProcesses;
        public uint TotalTerminatedProcesses;
    }

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

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenJobObject(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryInformationJobObject(
        IntPtr job, int infoClass, out JobObjectBasicAccountingInformation info, uint length, IntPtr returned);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateJobObject(IntPtr job, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
