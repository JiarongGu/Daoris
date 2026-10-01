using System.Collections;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Daoris.Driver;

/// <summary>
/// A shell under a Windows pseudo-console (CONSOLE4a, D96): the person's own terminal, behaving as a console
/// window does — prompts, colours, Ctrl+C, history, tab completion and full-screen programs.
/// </summary>
/// <remarks>
/// <para><b>A few Win32 calls, not a package</b> (D96's rejected list): <c>CreatePseudoConsole</c> over two
/// pipes, the shell started attached to it through <c>STARTUPINFOEX</c>, its output read as it arrives and
/// its input written as the person types. The console host it runs is headless, so no window appears.</para>
///
/// <para><b>Started suspended and joined to a <see cref="ProcessJob"/> before it runs</b>, so everything the
/// shell starts, detached or not, is in the job from its first instruction; closing the terminal ends the
/// job, and so does this process ending, since the job's handle closes with it.</para>
///
/// <para><b>An end is told once, after the last output.</b> The console host outlives its shell, holding
/// the output pipe open, so when the shell exits the pseudo-console is closed, which flushes its last frame
/// and breaks the pipe; the exit is told when the reader has read to the end. Closing it by hand takes the
/// same road: the job ends the shell, and the shell's exit closes the rest.</para>
///
/// <para><b>What it carries is the person's own</b>: nothing here logs a byte of it (D94).</para>
/// </remarks>
public sealed class PseudoConsole : ITerminal
{
    private readonly Action<int> _exited;
    private readonly FileStream _writer;
    private readonly ProcessJob _job;
    private readonly IntPtr _process;
    private readonly ManualResetEvent _processExited;
    private readonly Thread _reader;
    private readonly object _writing = new();
    // The shell's handle is ended by one side and released by the other: held under this, so a close that
    // races the shell's own exit never ends a handle already given back.
    private readonly object _handle = new();
    private bool _released;
    private RegisteredWaitHandle? _watch;
    private IntPtr _console;
    private int _told;
    private int _closed;
    private int _disposed;

    private PseudoConsole(
        IntPtr console, SafeFileHandle input, SafeFileHandle output, IntPtr process, ProcessJob job,
        Action<string> onOutput, Action<int> exited)
    {
        _console = console;
        _writer = new FileStream(input, FileAccess.Write, 1, isAsync: false);
        _process = process;
        _job = job;
        _exited = exited;

        _reader = new Thread(() => Pump(output, onOutput)) { IsBackground = true, Name = "daoris-terminal-output" };
        _reader.Start();

        _processExited = new ManualResetEvent(false) { SafeWaitHandle = new SafeWaitHandle(process, ownsHandle: false) };
        _watch = ThreadPool.RegisterWaitForSingleObject(_processExited, (_, _) => Ended(), null, Timeout.Infinite, executeOnlyOnce: true);
    }

    /// <summary>Start <paramref name="launch"/>'s shell under a new pseudo-console.</summary>
    /// <exception cref="PlatformNotSupportedException">Not on Windows.</exception>
    /// <exception cref="Win32Exception">The system would not make the console or start the shell; its message says why.</exception>
    public static PseudoConsole Start(TerminalLaunch launch, Action<string> output, Action<int> exited)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("a terminal needs the Windows pseudo-console.");

        if (!CreatePipe(out var inputRead, out var inputWrite, IntPtr.Zero, 0)) throw new Win32Exception();
        if (!CreatePipe(out var outputRead, out var outputWrite, IntPtr.Zero, 0))
        {
            var error = new Win32Exception();
            inputRead.Dispose();
            inputWrite.Dispose();
            throw error;
        }

        var result = CreatePseudoConsole(Size(launch.Columns, launch.Rows), inputRead, outputWrite, 0, out var console);
        // The console host holds its own copies of these two ends; ours would keep the pipes open past it.
        inputRead.Dispose();
        outputWrite.Dispose();
        if (result != 0)
        {
            inputWrite.Dispose();
            outputRead.Dispose();
            throw new Win32Exception(result);
        }

        var attributes = IntPtr.Zero;
        var environment = IntPtr.Zero;
        try
        {
            var size = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            attributes = Marshal.AllocHGlobal(size);
            if (!InitializeProcThreadAttributeList(attributes, 1, 0, ref size)
                || !UpdateProcThreadAttribute(attributes, 0, (IntPtr)PseudoConsoleAttribute, console, IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
            {
                throw new Win32Exception();
            }

            var startup = new StartupInfoEx { AttributeList = attributes };
            startup.StartupInfo.Size = Marshal.SizeOf<StartupInfoEx>();
            // 🔴 Standard handles named, and named as none: otherwise a shell started from a process whose
            // own are redirected (a test host, a service) writes to THOSE, and the console shows only its
            // own first frame. Measured: `cmd /c echo` under the test runner printed nothing here until this.
            startup.StartupInfo.Flags = UseStdHandles;
            environment = Marshal.StringToHGlobalUni(EnvironmentBlock(launch.Environment));

            if (!CreateProcessW(
                    null, new StringBuilder(CommandLine(launch.Shell.Path, launch.Shell.Arguments)), IntPtr.Zero, IntPtr.Zero,
                    false, ExtendedStartupInfoPresent | UnicodeEnvironment | Suspended, environment,
                    launch.Directory, ref startup, out var started))
            {
                throw new Win32Exception();
            }

            // Joined while it cannot yet have started anything, then let go.
            var job = ProcessJob.Hold(started.Process);
            ResumeThread(started.Thread);
            CloseHandle(started.Thread);
            return new PseudoConsole(console, inputWrite, outputRead, started.Process, job, output, exited);
        }
        catch
        {
            ClosePseudoConsole(console);
            inputWrite.Dispose();
            outputRead.Dispose();
            throw;
        }
        finally
        {
            if (attributes != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(attributes);
                Marshal.FreeHGlobal(attributes);
            }

            if (environment != IntPtr.Zero) Marshal.FreeHGlobal(environment);
        }
    }

    public void Write(string data)
    {
        if (Volatile.Read(ref _closed) != 0 || string.IsNullOrEmpty(data)) return;
        var bytes = Encoding.UTF8.GetBytes(data);
        lock (_writing)
        {
            try
            {
                _writer.Write(bytes);
                _writer.Flush();
            }
            catch (Exception error) when (error is IOException or ObjectDisposedException)
            {
                // The shell has gone: its end is told by its exit, not by a keystroke that found nobody.
            }
        }
    }

    public void Resize(int columns, int rows)
    {
        var console = Volatile.Read(ref _console);
        if (Volatile.Read(ref _closed) != 0 || console == IntPtr.Zero) return;
        ResizePseudoConsole(console, Size(columns, rows));
    }

    /// <summary>Close it: the shell and everything it started end, and the end is told as an exit would be.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        Volatile.Write(ref _closed, 1);
        // The shell itself first, in case the system refused the job; then the job, which ends the rest.
        lock (_handle)
        {
            if (!_released) TerminateProcess(_process, 1);
        }

        _job.Dispose();
    }

    /// <summary>The shell ended, on its own or closed: close the console so its last frame arrives, then tell it once.</summary>
    private void Ended()
    {
        int code;
        lock (_handle) code = GetExitCodeProcess(_process, out var exit) ? unchecked((int)exit) : -1;
        Volatile.Write(ref _closed, 1);
        CloseConsole();
        // The reader ends when the pipe does, which the close above breaks. Bounded, so a host that will not
        // let go cannot keep an end from being told.
        _reader.Join(TimeSpan.FromSeconds(5));

        // Whatever the shell left running ends with it: the terminal is over.
        _job.Dispose();
        Volatile.Read(ref _watch)?.Unregister(null);
        _processExited.Dispose();
        lock (_writing) _writer.Dispose();
        lock (_handle)
        {
            _released = true;
            CloseHandle(_process);
        }

        if (Interlocked.Exchange(ref _told, 1) == 0) _exited(code);
    }

    private void CloseConsole()
    {
        var console = Interlocked.Exchange(ref _console, IntPtr.Zero);
        if (console != IntPtr.Zero) ClosePseudoConsole(console);
    }

    /// <summary>Read the console's output as it comes, decoding UTF-8 across reads, until the pipe ends.</summary>
    private static void Pump(SafeFileHandle output, Action<string> onOutput)
    {
        var decoder = Encoding.UTF8.GetDecoder();
        var bytes = new byte[8192];
        var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
        using var stream = new FileStream(output, FileAccess.Read, 1, isAsync: false);
        while (true)
        {
            int read;
            try
            {
                read = stream.Read(bytes, 0, bytes.Length);
            }
            catch (IOException)
            {
                break; // A broken pipe is the console closing: the end of what there is to read.
            }

            if (read == 0) break;
            var count = decoder.GetChars(bytes, 0, read, chars, 0);
            if (count == 0) continue;
            try
            {
                onOutput(new string(chars, 0, count));
            }
            catch (Exception)
            {
                // A reader that failed is its own trouble; the console keeps being drained, or its close would wait on it.
            }
        }
    }

    /// <summary>
    /// A program and its arguments as one Windows command line, quoted as the C runtime reads it back: an
    /// argument that is empty or holds a space, a tab or a quote is quoted, and the backslashes before a quote
    /// (or before the closing one) are doubled.
    /// </summary>
    internal static string CommandLine(string program, IReadOnlyList<string> arguments)
    {
        var line = new StringBuilder();
        Append(line, program);
        foreach (var argument in arguments)
        {
            line.Append(' ');
            Append(line, argument);
        }

        return line.ToString();
    }

    private static void Append(StringBuilder line, string argument)
    {
        if (argument.Length > 0 && argument.IndexOfAny([' ', '\t', '"']) < 0)
        {
            line.Append(argument);
            return;
        }

        line.Append('"');
        var slashes = 0;
        foreach (var c in argument)
        {
            if (c == '\\')
            {
                slashes++;
                continue;
            }

            line.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
            slashes = 0;
            line.Append(c);
        }

        line.Append('\\', slashes * 2);
        line.Append('"');
    }

    /// <summary>
    /// This process's environment, then the tools' (TOOLS5, D121 §2.6), then <paramref name="added"/> over it, as the
    /// block CreateProcess reads: the person's own <c>git fetch</c> in a terminal runs the git Daoris's does.
    /// </summary>
    /// <remarks>
    /// Sorted by name without regard to case, as the system keeps its own block. The tools are read from the home the
    /// launch names, else this process's; with none, nothing is added.
    /// </remarks>
    internal static string EnvironmentBlock(IReadOnlyDictionary<string, string> added)
    {
        var variables = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string name && entry.Value is string value) variables[name] = value;
        }

        var home = added.FirstOrDefault(pair => string.Equals(pair.Key, DaorisHome.Variable, StringComparison.OrdinalIgnoreCase)).Value
            ?? DaorisHome.Resolve();
        if (home is not null)
        {
            var inherited = variables.TryGetValue(Tools.PathVariable, out var path) ? path : null;
            foreach (var (name, value) in Tools.ChildEnvironment(Tools.Read(home), home, inherited)) variables[name] = value;
        }

        foreach (var (name, value) in added) variables[name] = value;

        var block = new StringBuilder();
        foreach (var (name, value) in variables) block.Append(name).Append('=').Append(value).Append('\0');
        // The block ends in a second null; the copy into native memory adds the terminator after this one.
        return block.Append('\0').ToString();
    }

    private static Coord Size(int columns, int rows) => new()
    {
        X = (short)Math.Clamp(columns, 1, short.MaxValue),
        Y = (short)Math.Clamp(rows, 1, short.MaxValue),
    };

    private const int PseudoConsoleAttribute = 0x00020016;
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint UnicodeEnvironment = 0x00000400;
    private const uint Suspended = 0x00000004;
    private const int UseStdHandles = 0x00000100;

    [StructLayout(LayoutKind.Sequential)]
    private struct Coord
    {
        public short X;
        public short Y;
    }

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
    private static extern int CreatePseudoConsole(Coord size, SafeFileHandle input, SafeFileHandle output, uint flags, out IntPtr console);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int ResizePseudoConsole(IntPtr console, Coord size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void ClosePseudoConsole(IntPtr console);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreatePipe(out SafeFileHandle read, out SafeFileHandle write, IntPtr attributes, int size);

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
