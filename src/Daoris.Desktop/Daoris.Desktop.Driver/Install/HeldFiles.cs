using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Daoris.Driver;

/// <summary>A process found holding an entry open (DEPLOYBUSY1): its id, its name, when it started, and its command line.</summary>
public sealed record FileHolder(int Pid, string Name, DateTimeOffset? Started = null, string? Command = null)
{
    /// <summary>The most of a command line <see cref="InFull"/> carries.</summary>
    private const int CommandMost = 300;

    /// <summary>The holder in a terminal's words: <c>git (pid 1234)</c>.</summary>
    public string Said => $"{Name} (pid {Pid})";

    /// <summary>
    /// The holder with when it started and its command line, for the swap's journal: <c>git (pid 1234) started
    /// 03:00:20Z: git status</c>. A name alone can mislead: a program an installer replaced as it ran is named by its
    /// rollback file.
    /// </summary>
    public string InFull =>
        Said
        + (Started is { } started ? " started " + started.ToString("HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture) : "")
        + (Command is { Length: > 0 } command ? ": " + (command.Length > CommandMost ? command[..CommandMost] + "…" : command) : "");
}

/// <summary>An entry found held open, by its full path, and the processes holding it.</summary>
public sealed record HeldEntry(string Path, IReadOnlyList<FileHolder> By);

/// <summary>
/// What holds a file or a folder open, where Windows can tell (DEPLOYBUSY1): a move the swap is refused names the entry that
/// was held and the processes holding it, so a <c>busy</c> says who, not only that.
/// </summary>
/// <remarks>
/// <para>Each entry is asked with <c>NtQueryInformationFile</c>'s <c>FileProcessIdsUsingFileInformation</c>, the question the
/// Restart Manager asks too, on a handle opened for the entry's attributes alone, which no sharing refuses. Asked of a
/// folder, it answers a process whose working folder it is as well as one with the folder open: the Restart Manager's
/// <c>RmGetList</c> takes files alone, and a working folder is what holds a folder against a move with no file open in it.
/// Asked of each entry rather than of the whole list at once, so the answer names the file and not only the process.</para>
///
/// <para>Only on the path where a move was refused, so it costs nothing when nothing is held. Elsewhere than Windows, and
/// wherever the call fails, nothing is found, and a hold is said as before.</para>
///
/// <para><b>Compiled into the launcher too</b>, beside <see cref="InstallSwap"/>: the base library and Windows' own calls
/// alone.</para>
/// </remarks>
public static class HeldFiles
{
    /// <summary>How many entries a search opens at most: a folder of an application's files is a few hundred.</summary>
    private const int Walk = 20_000;

    /// <summary>
    /// The first entries found held at <paramref name="path"/>, itself first and then everything within it, each with the
    /// processes that hold it; empty where none is found, or where the system cannot tell.
    /// </summary>
    public static IReadOnlyList<HeldEntry> Find(string path, int most = 3)
    {
        var found = new List<HeldEntry>();
        if (!OperatingSystem.IsWindows() || most <= 0) return found;
        try
        {
            if (!Directory.Exists(path) && !File.Exists(path)) return found;

            // Listed whole before any is asked: a walk under way holds the folder it is reading open, and would find itself.
            foreach (var entry in Entries(path).Take(Walk).ToList())
            {
                if (found.Count >= most) break;
                var pids = ProcessesUsing(entry);
                if (pids.Count > 0) found.Add(new HeldEntry(entry, pids.Select(Holder).ToList()));
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // What was found before the walk failed is still what was found.
        }

        return found;
    }

    /// <summary>The path itself, then, for a folder, everything within it, hidden and system entries included.</summary>
    private static IEnumerable<string> Entries(string path)
    {
        yield return path;
        if (!Directory.Exists(path)) yield break;
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 };
        foreach (var entry in Directory.EnumerateFileSystemEntries(path, "*", options)) yield return entry;
    }

    /// <summary>The ids of the processes holding <paramref name="path"/> open; empty where none does or Windows cannot tell.</summary>
    public static IReadOnlyList<int> ProcessesUsing(string path)
    {
        if (!OperatingSystem.IsWindows()) return [];
        using var handle = CreateFileW(
            path, FileReadAttributes, FileShareAll, IntPtr.Zero, OpenExisting, BackupSemantics | OpenReparsePoint, IntPtr.Zero);
        if (handle.IsInvalid) return [];

        for (var size = 1024; size <= 1 << 20; size *= 4)
        {
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                var status = NtQueryInformationFile(handle, out _, buffer, (uint)size, FileProcessIdsUsingFileInformation);
                if (status == StatusInfoLengthMismatch || status == StatusBufferOverflow) continue;
                if (status != 0) return [];

                // FILE_PROCESS_IDS_USING_FILE_INFORMATION: a ULONG count, then that many ULONG_PTR ids, pointer-aligned.
                var count = Marshal.ReadInt32(buffer);
                var pids = new List<int>(count);
                for (var index = 0; index < count && IntPtr.Size * (index + 2) <= size; index++)
                {
                    pids.Add((int)Marshal.ReadIntPtr(buffer, IntPtr.Size * (index + 1)));
                }

                return pids;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        return [];
    }

    /// <summary>
    /// A process by its id: its name, when it started and its command line, as far as this account may read them. Asked of
    /// the one process, never through <see cref="Process.GetProcessById(int)"/>, which reads every process on the machine
    /// to answer for one, a third of a second each time on a busy machine (measured), and a hold is asked every few seconds.
    /// </summary>
    private static FileHolder Holder(int pid)
    {
        var process = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (process == IntPtr.Zero) return new FileHolder(pid, NameByList(pid));
        try
        {
            var name = new char[1024];
            var length = (uint)name.Length;
            var image = QueryFullProcessImageNameW(process, 0, name, ref length) ? new string(name, 0, (int)length) : null;
            DateTimeOffset? started = GetProcessTimes(process, out var creation, out _, out _, out _)
                ? DateTimeOffset.FromFileTime(creation).ToUniversalTime()
                : null;
            return new FileHolder(
                pid, image is null ? NameByList(pid) : System.IO.Path.GetFileNameWithoutExtension(image), started, CommandLineOf(process));
        }
        finally
        {
            CloseHandle(process);
        }
    }

    /// <summary>The name the process list gives, where the process itself cannot be opened; <c>ended</c> when it is gone.</summary>
    private static string NameByList(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.ProcessName;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            return "ended";
        }
    }

    /// <summary>A process's command line, or null where this account may not read it (ProcessCommandLineInformation, Windows 8.1 on).</summary>
    private static string? CommandLineOf(IntPtr process)
    {
        for (var size = 4096; size <= 1 << 20; size *= 4)
        {
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                var status = NtQueryInformationProcess(process, ProcessCommandLineInformation, buffer, (uint)size, out _);
                if (status == StatusInfoLengthMismatch || status == StatusBufferOverflow || status == StatusBufferTooSmall) continue;
                if (status != 0) return null;

                // A UNICODE_STRING: its length in bytes, then, pointer-aligned, where its characters are.
                var length = (ushort)Marshal.ReadInt16(buffer);
                var characters = Marshal.ReadIntPtr(buffer, IntPtr.Size);
                return characters == IntPtr.Zero ? null : Marshal.PtrToStringUni(characters, length / 2);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        return null;
    }

    private const uint FileReadAttributes = 0x80;
    private const uint FileShareAll = 0x1 | 0x2 | 0x4;
    private const uint OpenExisting = 3;
    private const uint BackupSemantics = 0x02000000;
    private const uint OpenReparsePoint = 0x00200000;
    private const int FileProcessIdsUsingFileInformation = 47;
    private const int ProcessCommandLineInformation = 60;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);
    private const int StatusBufferOverflow = unchecked((int)0x80000005);
    private const int StatusBufferTooSmall = unchecked((int)0xC0000023);

    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock
    {
        public IntPtr Status;
        public IntPtr Information;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationFile(SafeFileHandle file, out IoStatusBlock status, IntPtr information, uint length, int kind);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr process, int kind, IntPtr information, uint length, out uint returned);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageNameW(IntPtr process, uint flags, [Out] char[] name, ref uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetProcessTimes(IntPtr process, out long creation, out long exit, out long kernel, out long user);
}
