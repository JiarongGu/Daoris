using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Daoris.Driver;

/// <summary>
/// A program started suspended and joined to a job before its first instruction, its three streams this process's pipes: an act's effect step, whose id and start are journaled while it cannot yet run, then let go by <see cref="Resume"/>.
/// </summary>
/// <remarks>
/// As <see cref="HeavyStep"/>'s gates are held: <c>CreateProcessW</c> with <c>CREATE_SUSPENDED</c>, the job joined, and only then the thread
/// resumed, so every process it starts (a hook, the hook's children) is born in the job, which allows no breakaway and ends them all with its
/// last handle. Only its three pipe ends are inherited. A child that could not join is ended unrun. Windows only: elsewhere there is no job,
/// and an act is not run unheld.
/// </remarks>
internal sealed class HeldChild : IDisposable
{
    private readonly IntPtr _process;
    private IntPtr _thread;
    private readonly AnonymousPipeServerStream _input;
    private readonly Task<string> _stdout;
    private readonly Task<string> _stderr;
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ManualResetEvent _exitedSignal;
    private readonly RegisteredWaitHandle _watch;
    private readonly object _handleGate = new();
    private bool _disposed;

    private HeldChild(IntPtr process, IntPtr thread, int pid, long started, AnonymousPipeServerStream input, AnonymousPipeServerStream output,
        AnonymousPipeServerStream error)
    {
        (_process, _thread, Pid, Started, _input) = (process, thread, pid, started, input);
        _stdout = Task.Run(() => ReadWhole(output));
        _stderr = Task.Run(() => ReadWhole(error));
        _exitedSignal = new ManualResetEvent(false) { SafeWaitHandle = new SafeWaitHandle(process, ownsHandle: false) };
        _watch = ThreadPool.RegisterWaitForSingleObject(_exitedSignal, (_, _) => Exited(), null, Timeout.Infinite, executeOnlyOnce: true);
    }

    /// <summary>The child's process id.</summary>
    public int Pid { get; }

    /// <summary>When the child was made, in UTC ticks: with its id, what tells it from a later process of the same id (<see cref="ProcessLiveness"/>).</summary>
    public long Started { get; }

    /// <summary>
    /// <paramref name="info"/>'s program and arguments started suspended in <paramref name="job"/>, in its working folder with its environment;
    /// its input a pipe <see cref="RunAsync"/> writes, its output and error read whole.
    /// </summary>
    /// <exception cref="DriverException">It would not start, or could not be held in the job, so it did not run.</exception>
    public static HeldChild Start(ProcessJob job, ProcessStartInfo info, bool input)
    {
        if (!OperatingSystem.IsWindows()) throw new DriverException("there is no job to hold the step's processes in off Windows, so it was not run.");
        var stdin = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        var stdout = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        var stderr = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        var handles = IntPtr.Zero;
        var attributes = IntPtr.Zero;
        var environment = IntPtr.Zero;
        try
        {
            IntPtr[] inherited = [stdin.ClientSafePipeHandle.DangerousGetHandle(), stdout.ClientSafePipeHandle.DangerousGetHandle(), stderr.ClientSafePipeHandle.DangerousGetHandle()];
            handles = Marshal.AllocHGlobal(IntPtr.Size * inherited.Length);
            for (var at = 0; at < inherited.Length; at++) Marshal.WriteIntPtr(handles, at * IntPtr.Size, inherited[at]);
            var size = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            attributes = Marshal.AllocHGlobal(size);
            if (!InitializeProcThreadAttributeList(attributes, 1, 0, ref size)
                || !UpdateProcThreadAttribute(attributes, 0, (IntPtr)HandleListAttribute, handles, (IntPtr)(IntPtr.Size * inherited.Length), IntPtr.Zero, IntPtr.Zero))
            {
                throw new Win32Exception();
            }

            var startup = new StartupInfoEx { AttributeList = attributes };
            startup.StartupInfo.Size = Marshal.SizeOf<StartupInfoEx>();
            startup.StartupInfo.Flags = UseStdHandles;
            (startup.StartupInfo.StdInput, startup.StartupInfo.StdOutput, startup.StartupInfo.StdError) = (inherited[0], inherited[1], inherited[2]);
            environment = Marshal.StringToHGlobalUni(Block(info));
            var directory = string.IsNullOrEmpty(info.WorkingDirectory) ? null : info.WorkingDirectory;
            if (!CreateProcessW(null, new StringBuilder(CommandLine(info)), IntPtr.Zero, IntPtr.Zero, true,
                    Suspended | UnicodeEnvironment | NoWindow | ExtendedStartupInfoPresent, environment, directory, ref startup, out var made))
            {
                throw new Win32Exception();
            }

            // Joined while it cannot yet have started anything; it stays suspended until its id is journaled.
            if (!job.Join(made.Process))
            {
                var error = Marshal.GetLastWin32Error();
                TerminateProcess(made.Process, 1);
                CloseHandle(made.Thread);
                CloseHandle(made.Process);
                throw new DriverException($"the step could not be held in its act's job ({new Win32Exception(error).Message}), so it was not run.");
            }

            stdin.DisposeLocalCopyOfClientHandle();
            stdout.DisposeLocalCopyOfClientHandle();
            stderr.DisposeLocalCopyOfClientHandle();
            if (!input) stdin.Dispose();
            var started = GetProcessTimes(made.Process, out var creation, out _, out _, out _) ? DateTime.FromFileTimeUtc(creation).Ticks : DateTime.UtcNow.Ticks;
            return new HeldChild(made.Process, made.Thread, made.ProcessId, started, stdin, stdout, stderr);
        }
        catch (Win32Exception error)
        {
            stdin.Dispose();
            stdout.Dispose();
            stderr.Dispose();
            throw new DriverException($"the step would not start ({error.Message}).");
        }
        catch
        {
            stdin.Dispose();
            stdout.Dispose();
            stderr.Dispose();
            throw;
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
        }
    }

    /// <summary>Let the child run, once its id is journaled.</summary>
    public void Resume()
    {
        var thread = Interlocked.Exchange(ref _thread, IntPtr.Zero);
        if (thread == IntPtr.Zero) return;
        ResumeThread(thread);
        CloseHandle(thread);
    }

    /// <summary>
    /// <paramref name="input"/> written, as BOM-less UTF-8, and the input closed; the write runs beside the child, which reads it as it
    /// goes, and ends early where the child closes the pipe.
    /// </summary>
    public Task WriteAsync(string? input) => Task.Run(async () =>
    {
        if (!_input.CanWrite) return;
        try
        {
            if (input is not null) await _input.WriteAsync(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(input)).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException)
        {
            // The pipe closed under the write, or the child was let go of: its exit is the answer that counts.
        }
        finally
        {
            _input.Dispose();
        }
    });

    /// <summary>
    /// The exit awaited for <paramref name="bound"/> at most: the exit code and both streams, or null for the code where the bound passed
    /// first. The child is not ended by this: its job ends it.
    /// </summary>
    public async Task<(int? Exit, string Stdout, string Stderr)> ExitAsync(TimeSpan bound)
    {
        if (await Task.WhenAny(_exited.Task, Task.Delay(bound)).ConfigureAwait(false) != _exited.Task) return (null, "", "");
        // A hook's child that outlives git holds the pipes open: its words are not waited for past a moment.
        await Task.WhenAny(Task.WhenAll(_stdout, _stderr), Task.Delay(StreamGrace)).ConfigureAwait(false);
        return (_exited.Task.Result, _stdout.IsCompletedSuccessfully ? _stdout.Result : "", _stderr.IsCompletedSuccessfully ? _stderr.Result : "");
    }

    private static readonly TimeSpan StreamGrace = TimeSpan.FromSeconds(2);

    /// <summary>A pipe read to its end, which comes once every process holding its other end has closed it, then let go of.</summary>
    private static string ReadWhole(Stream pipe)
    {
        using var reader = new StreamReader(pipe, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>Whether the child has exited.</summary>
    public bool HasExited => _exited.Task.IsCompleted;

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

    public void Dispose()
    {
        lock (_handleGate)
        {
            if (_disposed) return;
            _disposed = true;
        }

        _watch.Unregister(null);
        _exitedSignal.Dispose();
        var thread = Interlocked.Exchange(ref _thread, IntPtr.Zero);
        if (thread != IntPtr.Zero) CloseHandle(thread);
        CloseHandle(_process);
        _input.Dispose();
    }

    /// <summary>The program and its arguments as one command line, each quoted as the C runtime reads it back.</summary>
    internal static string CommandLine(ProcessStartInfo info)
    {
        var line = new StringBuilder(Quote(info.FileName));
        foreach (var argument in info.ArgumentList) line.Append(' ').Append(Quote(argument));
        return line.ToString();
    }

    /// <summary>One argument quoted where it must be: backslashes doubled before a quote and at its end, quotes escaped.</summary>
    internal static string Quote(string argument)
    {
        if (argument.Length > 0 && argument.IndexOfAny([' ', '\t', '\n', '\v', '"']) < 0) return argument;
        var quoted = new StringBuilder("\"");
        for (var at = 0; at < argument.Length; at++)
        {
            var slashes = 0;
            while (at < argument.Length && argument[at] == '\\')
            {
                slashes++;
                at++;
            }

            if (at == argument.Length)
            {
                quoted.Append('\\', slashes * 2);
                break;
            }

            if (argument[at] == '"') quoted.Append('\\', slashes * 2 + 1).Append('"');
            else quoted.Append('\\', slashes).Append(argument[at]);
        }

        return quoted.Append('"').ToString();
    }

    /// <summary>The info's environment as the block <c>CreateProcessW</c> reads, sorted as the system keeps its own.</summary>
    private static string Block(ProcessStartInfo info)
    {
        var block = new StringBuilder();
        foreach (var (name, value) in info.Environment.Where(each => each.Value is not null).OrderBy(each => each.Key, StringComparer.OrdinalIgnoreCase))
        {
            block.Append(name).Append('=').Append(value).Append('\0');
        }

        return block.Append('\0').ToString();
    }

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
    private static extern bool GetProcessTimes(IntPtr process, out long creation, out long exit, out long kernel, out long user);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
