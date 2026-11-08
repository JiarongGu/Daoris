using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Daoris.Driver;

/// <summary>A process's standard handles as their values in that process: what a terminal names to the running Daoris.</summary>
public readonly record struct StandardHandles(long Input, long Output, long Error)
{
    /// <summary>This process's, zero for one it does not have.</summary>
    public static StandardHandles OfThisProcess() =>
        OperatingSystem.IsWindows() ? new(Value(StdInput), Value(StdOutput), Value(StdError)) : default;

    private static long Value(int which) => GetStdHandle(which) is var handle && handle != new IntPtr(-1) ? (long)handle : 0;

    private const int StdInput = -10;
    private const int StdOutput = -11;
    private const int StdError = -12;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int which);
}

/// <summary>
/// A terminal's standard handles, copied into this process from the terminal's own: what a heavy run the running Daoris runs for a
/// terminal hands its gates, so they read and write where the terminal does, its console or its shell's pipe, as a gate the terminal
/// started itself would. The copies are this process's to close. Windows only.
/// </summary>
/// <remarks>
/// The terminal is opened to copy from by the process id its pipe connection was taken from, while the connection's own handle on it
/// keeps that id from being used again. Only the handles the terminal names are copied, and only from it, so it hands its gates
/// nothing it did not hold.
/// </remarks>
public sealed class TerminalHandles : IDisposable
{
    private int _closed;

    private TerminalHandles(IntPtr input, IntPtr output, IntPtr error, HeavyAsker asker)
    {
        Input = input;
        Output = output;
        Error = error;
        Asker = asker;
    }

    /// <summary>The terminal's standard input here; zero where it has none.</summary>
    public IntPtr Input { get; }

    /// <summary>The terminal's standard output here; zero where it has none.</summary>
    public IntPtr Output { get; }

    /// <summary>The terminal's standard error here; zero where it has none.</summary>
    public IntPtr Error { get; }

    /// <summary>The terminal's process, as a heavy run's slot names it.</summary>
    public HeavyAsker Asker { get; }

    /// <summary>
    /// Copies of <paramref name="named"/>, taken from the process <paramref name="pid"/>; null, with <paramref name="problem"/>, where
    /// it cannot be opened for it (another elevation's) or a handle it names does not copy.
    /// </summary>
    public static TerminalHandles? Take(int pid, StandardHandles named, out string? problem)
    {
        problem = null;
        if (!OperatingSystem.IsWindows())
        {
            problem = "a terminal's handles are taken on Windows alone";
            return null;
        }

        using var terminal = OpenProcess(DuplicateHandleAccess | QueryLimited, false, (uint)pid);
        if (terminal.IsInvalid)
        {
            problem = $"the terminal's process ({pid}) could not be opened to take its output ({new Win32Exception(Marshal.GetLastWin32Error()).Message})";
            return null;
        }

        if (!GetProcessTimes(terminal, out var created, out _, out _, out _))
        {
            problem = $"the terminal's process ({pid}) does not say when it started";
            return null;
        }

        var copies = new IntPtr[3];
        var values = new[] { named.Input, named.Output, named.Error };
        for (var at = 0; at < values.Length; at++)
        {
            if (values[at] == 0) continue;
            if (DuplicateHandle(terminal, new IntPtr(values[at]), GetCurrentProcess(), out copies[at], 0, false, DuplicateSameAccess)) continue;
            problem = $"the terminal's standard handles did not copy ({new Win32Exception(Marshal.GetLastWin32Error()).Message})";
            foreach (var copy in copies) if (copy != IntPtr.Zero) CloseHandle(copy);
            return null;
        }

        return new TerminalHandles(copies[0], copies[1], copies[2], new HeavyAsker(pid, DateTime.FromFileTimeUtc(created).Ticks));
    }

    /// <summary>A stream that writes to the terminal's standard output, for the run's own lines; null where it has none.</summary>
    public Stream? OutputStream() => Output == IntPtr.Zero ? null : new FileStream(new SafeFileHandle(Output, ownsHandle: false), FileAccess.Write, 1);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 1) return;
        foreach (var handle in new[] { Input, Output, Error }) if (handle != IntPtr.Zero) CloseHandle(handle);
    }

    private const uint DuplicateHandleAccess = 0x0040;
    private const uint QueryLimited = 0x1000;
    private const uint DuplicateSameAccess = 0x00000002;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, uint pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetProcessTimes(SafeProcessHandle process, out long created, out long exited, out long kernel, out long user);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DuplicateHandle(
        SafeProcessHandle sourceProcess, IntPtr source, IntPtr targetProcess, out IntPtr target, uint access, bool inherit, uint options);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
