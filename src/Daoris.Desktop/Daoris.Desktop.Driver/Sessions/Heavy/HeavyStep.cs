using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Daoris.Driver;

/// <summary>
/// One gate of a heavy run (HEAVYRUN1a): its command line through the platform's shell, in the gate's folder, writing to this
/// process's own output, held in the run's job from before it runs.
/// </summary>
/// <remarks>
/// <para><b>Started suspended, joined, then let go</b>, as a terminal's shell is (CONSOLE4a). <see cref="ProcessJob.Hold(Process)"/>
/// joins a process after it has started, and a shell that starts its command at once could start a grandchild in that moment, outside
/// the job. Joined before its first instruction, everything the gate starts is in the job, detached or not: the job allows no
/// breakaway. A gate that cannot join is ended unrun, and the run says so: a gate the run cannot end is not one it may start.</para>
///
/// <para><b>Its output is this process's own.</b> The gate is handed this process's standard handles, so a nine-minute build streams
/// through as it is said, never gathered whole (<c>ChildProcess.RunAsync</c> buffers a child's output, so it is not used here). Those
/// three handles alone are inherited, never another this process holds. Where this process has no console the gate gets one of its
/// own with no window, so a heavy run started without one opens none on the person's screen.</para>
///
/// <para>Off Windows there is no job: the gate runs through <c>/bin/sh</c>, and a stop ends its tree from its root, which reaches only
/// what is still its descendant.</para>
/// </remarks>
internal sealed class HeavyStep : IDisposable
{
    private readonly IntPtr _process;
    private readonly Process? _elsewhere;
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ManualResetEvent? _exitedSignal;
    private readonly RegisteredWaitHandle? _watch;

    // The handle is read by the exit's callback and closed by Dispose: never both at once.
    private readonly object _handleGate = new();
    private bool _disposed;

    private HeavyStep(IntPtr process)
    {
        _process = process;
        _exitedSignal = new ManualResetEvent(false) { SafeWaitHandle = new SafeWaitHandle(process, ownsHandle: false) };
        _watch = ThreadPool.RegisterWaitForSingleObject(_exitedSignal, (_, _) => Exited(), null, Timeout.Infinite, executeOnlyOnce: true);
    }

    private void Exited()
    {
        int code;
        lock (_handleGate)
        {
            if (_disposed) return;
            code = GetExitCodeProcess(_process, out var exit) ? unchecked((int)exit) : -1;
        }

        _exited.TrySetResult(code);
    }

    private HeavyStep(Process elsewhere)
    {
        _elsewhere = elsewhere;
        _ = WatchElsewhereAsync(elsewhere);
    }

    /// <summary>Start <paramref name="run"/> in <paramref name="directory"/>, this process's environment with <paramref name="added"/> over it.</summary>
    /// <param name="job">The run's job, which the gate joins before it runs; null off Windows.</param>
    /// <exception cref="DriverException">The gate would not start, or could not be held in the job, so it did not run.</exception>
    public static HeavyStep Start(ProcessJob? job, string run, string directory, IReadOnlyDictionary<string, string> added) =>
        OperatingSystem.IsWindows() ? StartHeld(job, run, directory, added) : StartElsewhere(run, directory, added);

    /// <summary>The gate's exit code, once it has exited.</summary>
    /// <exception cref="OperationCanceledException">Stopped first; the gate is not ended by this.</exception>
    public Task<int> ExitAsync(CancellationToken ct) => _exited.Task.WaitAsync(ct);

    /// <summary>Off Windows, end the gate's tree from its root; on Windows the run's job ends it, and this does nothing.</summary>
    public void Stop()
    {
        try
        {
            if (_elsewhere is { HasExited: false }) _elsewhere.Kill(entireProcessTree: true);
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception)
        {
            // Exited as it was asked to.
        }
    }

    public void Dispose()
    {
        lock (_handleGate)
        {
            if (_disposed) return;
            _disposed = true;
        }

        _watch?.Unregister(null);
        _exitedSignal?.Dispose();
        if (_process != IntPtr.Zero) CloseHandle(_process);
        _elsewhere?.Dispose();
    }

    /// <summary>The shell a gate's command line runs through: the system's <c>cmd.exe</c>, as the devkit's gates run.</summary>
    internal static string CommandLine(string run)
    {
        var named = Environment.GetEnvironmentVariable("ComSpec");
        var shell = named is { Length: > 0 } && Path.IsPathRooted(named) && File.Exists(named) ? named : Path.Combine(Environment.SystemDirectory, "cmd.exe");
        // `/s` takes what is between the first and the last quote as it is, so the gate's own quotes reach the shell untouched.
        return $"\"{shell}\" /d /s /c \"{run}\"";
    }

    /// <summary>This process's environment with <paramref name="added"/> over it, as the block CreateProcess reads, sorted as the system keeps its own.</summary>
    internal static string EnvironmentBlock(IReadOnlyDictionary<string, string> added)
    {
        var variables = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string name && entry.Value is string value) variables[name] = value;
        }

        foreach (var (name, value) in added) variables[name] = value;

        var block = new StringBuilder();
        foreach (var (name, value) in variables) block.Append(name).Append('=').Append(value).Append('\0');
        // The block ends in a second null; the copy into native memory adds the terminator after this one.
        return block.Append('\0').ToString();
    }

    private static HeavyStep StartHeld(ProcessJob? job, string run, string directory, IReadOnlyDictionary<string, string> added)
    {
        if (job is null) throw new DriverException("there is no job to hold the gate's processes in, so it was not run.");

        var inherited = new List<IntPtr>();
        var attributes = IntPtr.Zero;
        var handles = IntPtr.Zero;
        var environment = IntPtr.Zero;
        try
        {
            var startup = new StartupInfoEx();
            startup.StartupInfo.Size = Marshal.SizeOf<StartupInfoEx>();
            startup.StartupInfo.Flags = UseStdHandles;
            startup.StartupInfo.StdInput = Inheritable(StdInput, inherited);
            startup.StartupInfo.StdOutput = Inheritable(StdOutput, inherited);
            startup.StartupInfo.StdError = Inheritable(StdError, inherited);

            var flags = Suspended | UnicodeEnvironment;
            if (GetConsoleCP() == 0) flags |= NoWindow;
            if (inherited.Count > 0)
            {
                // Only the three handles are inherited: a handle another part of this process made inheritable for a moment, for a
                // child of its own, is never handed to the gate.
                handles = Marshal.AllocHGlobal(IntPtr.Size * inherited.Count);
                for (var at = 0; at < inherited.Count; at++) Marshal.WriteIntPtr(handles, at * IntPtr.Size, inherited[at]);

                var size = IntPtr.Zero;
                InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
                attributes = Marshal.AllocHGlobal(size);
                if (!InitializeProcThreadAttributeList(attributes, 1, 0, ref size)
                    || !UpdateProcThreadAttribute(attributes, 0, (IntPtr)HandleListAttribute, handles, (IntPtr)(IntPtr.Size * inherited.Count), IntPtr.Zero, IntPtr.Zero))
                {
                    throw new Win32Exception();
                }

                startup.AttributeList = attributes;
                flags |= ExtendedStartupInfoPresent;
            }

            environment = Marshal.StringToHGlobalUni(EnvironmentBlock(added));
            if (!CreateProcessW(
                    null, new StringBuilder(CommandLine(run)), IntPtr.Zero, IntPtr.Zero, inherited.Count > 0, flags, environment, directory,
                    ref startup, out var started))
            {
                throw new Win32Exception();
            }

            // Joined while it cannot yet have started anything, then let go.
            if (!job.Join(started.Process))
            {
                var error = Marshal.GetLastWin32Error();
                TerminateProcess(started.Process, 1);
                CloseHandle(started.Thread);
                CloseHandle(started.Process);
                throw new DriverException(
                    $"the gate could not be held in the run's job ({new Win32Exception(error).Message}), so what it starts could not be ended with it; it was not run.");
            }

            ResumeThread(started.Thread);
            CloseHandle(started.Thread);
            return new HeavyStep(started.Process);
        }
        catch (Win32Exception error)
        {
            throw new DriverException($"the gate would not start ({error.Message}).");
        }
        finally
        {
            if (attributes != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(attributes);
                Marshal.FreeHGlobal(attributes);
            }

            if (handles != IntPtr.Zero) Marshal.FreeHGlobal(handles);
            if (environment != IntPtr.Zero) Marshal.FreeHGlobal(environment);
            foreach (var handle in inherited) CloseHandle(handle);
        }
    }

    /// <summary>An inheritable copy of one of this process's standard handles, kept in <paramref name="made"/> to close once the gate has it; none where this process has none.</summary>
    private static IntPtr Inheritable(int which, List<IntPtr> made)
    {
        var own = GetStdHandle(which);
        if (own == IntPtr.Zero || own == new IntPtr(-1)) return IntPtr.Zero;
        var self = GetCurrentProcess();
        if (!DuplicateHandle(self, own, self, out var copy, 0, true, DuplicateSameAccess)) return IntPtr.Zero;
        made.Add(copy);
        return copy;
    }

    private static HeavyStep StartElsewhere(string run, string directory, IReadOnlyDictionary<string, string> added)
    {
        var info = new ProcessStartInfo("/bin/sh") { UseShellExecute = false, WorkingDirectory = directory };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add(run);
        foreach (var (name, value) in added) info.Environment[name] = value;
        try
        {
            return new HeavyStep(Process.Start(info) ?? throw new DriverException("the gate would not start."));
        }
        catch (Win32Exception error)
        {
            throw new DriverException($"the gate would not start ({error.Message}).");
        }
    }

    private async Task WatchElsewhereAsync(Process process)
    {
        await process.WaitForExitAsync().ConfigureAwait(false);
        _exited.TrySetResult(process.ExitCode);
    }

    private const int StdInput = -10;
    private const int StdOutput = -11;
    private const int StdError = -12;
    private const uint DuplicateSameAccess = 0x00000002;
    private const int HandleListAttribute = 0x00020002;
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint UnicodeEnvironment = 0x00000400;
    private const uint Suspended = 0x00000004;
    private const uint NoWindow = 0x08000000;
    private const int UseStdHandles = 0x00000100;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public IntPtr Reserved;
        public IntPtr Desktop;
        public IntPtr Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2Size;
        public IntPtr Reserved2;
        public IntPtr StdInput;
        public IntPtr StdOutput;
        public IntPtr StdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public IntPtr AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process;
        public IntPtr Thread;
        public int ProcessId;
        public int ThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int which);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetConsoleCP();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateHandle(
        IntPtr sourceProcess, IntPtr source, IntPtr targetProcess, out IntPtr target, uint access,
        [MarshalAs(UnmanagedType.Bool)] bool inherit, uint options);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(
        IntPtr list, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previous, IntPtr returnSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void DeleteProcThreadAttributeList(IntPtr list);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessW(
        string? application, StringBuilder commandLine, IntPtr processAttributes, IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint flags, IntPtr environment, string? directory,
        ref StartupInfoEx startup, out ProcessInformation information);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(IntPtr thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(IntPtr process, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
