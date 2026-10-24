using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Daoris.Driver;
using Microsoft.Win32.SafeHandles;

namespace Daoris.Desktop;

/// <summary>One process as the system says it, read by its id: its parent, its creation, its image, its arguments after the image, and a variable of its environment.</summary>
public sealed record ProcessFacts(int Id, int Parent, DateTime Created, string? Image, IReadOnlyList<string>? Arguments, Func<string, string?> Variable);

/// <summary>Where the walk reads processes: the system's (<see cref="SystemProcesses"/>), or a test's tree.</summary>
public interface IProcessTable
{
    /// <summary>The pipe client's own process id, or null where it is unknown.</summary>
    int? IdOf(PipeClientProcess? client);

    /// <summary>The process <paramref name="id"/> as the system says it now, or null where it cannot be read.</summary>
    ProcessFacts? Read(int id);
}

/// <summary>The judge's look at what a role's act came from (D209 §6.1): null where it is the holder's harness as Daoris started it, else why not.</summary>
public interface IHolderWalk
{
    string? Check(PipeClientProcess? client, HolderProcess? harness);
}

/// <summary>
/// The configuration walk (STEWARDROLE1a-i, D209 §6.1, Codex's final read, finding 13): at each admission of every route of the census's
/// <c>Role</c> class, from the verb's process up its parents, through the shell the harness's Bash tool ran it in, to <b>the holder's
/// harness process, by the id and creation time the terminal module recorded when it started it</b>; then that process's image, its
/// argument vector after the image and its <see cref="HolderTerminal.ConfigVariable"/>, read from the system, and the configuration
/// directory's owned files. The judge does not trust the printed line: a walk that does not reach the harness, or finds any of these
/// other than Daoris started it with, is refused <see cref="RoleWhy.HolderConfig"/>.
/// </summary>
public sealed class HolderWalk(IProcessTable processes) : IHolderWalk
{
    /// <summary>How far up a verb's parents the harness may be: the verb, the shell the Bash tool ran it in, and a few between.</summary>
    public const int Depth = 12;

    /// <summary>The system's walk.</summary>
    public static HolderWalk System { get; } = new(new SystemProcesses());

    public string? Check(PipeClientProcess? client, HolderProcess? harness)
    {
        if (harness is null) return "no harness was started for this holder";
        if (processes.IdOf(client) is not { } id) return "the verb's process is unknown";
        var child = processes.Read(id);
        for (var level = 0; level < Depth && child is not null; level++)
        {
            if (child.Id == harness.Id && child.Created == harness.Created) return Harness(child, harness);
            if (child.Parent <= 0) break;
            var parent = processes.Read(child.Parent);
            // A parent created after its child is another process given the id since: the walk ends there.
            if (parent is null || parent.Created > child.Created) break;
            child = parent;
        }

        return "the verb did not run inside the holder's harness";
    }

    private static string? Harness(ProcessFacts harness, HolderProcess started)
    {
        if (harness.Image is not { } image || !Same(image, started.Image)) return "the harness's image is not the managed binary Daoris started";
        if (harness.Arguments is not { } arguments || !arguments.SequenceEqual(HolderTerminal.SessionArguments, StringComparer.Ordinal))
        {
            return "the harness's argument vector is not the holder's";
        }

        if (harness.Variable(HolderTerminal.ConfigVariable) is not { } config || !Same(config, started.Config))
        {
            return $"the harness's {HolderTerminal.ConfigVariable} is not the holder's configuration directory";
        }

        return HolderTerminal.Owned(started.Config) is { } changed ? $"the holder's configuration directory holds a changed or loading `{changed}`" : null;
    }

    private static bool Same(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}

/// <summary>
/// The system's processes, read by id on Windows: the parent and the command line by <c>NtQueryInformationProcess</c>, the creation by
/// <c>GetProcessTimes</c>, the image by <c>QueryFullProcessImageNameW</c>, and a variable from the process's own environment block, read
/// from its parameters in its memory. Null wherever the system would not say.
/// </summary>
public sealed class SystemProcesses : IProcessTable
{
    public int? IdOf(PipeClientProcess? client)
    {
        if (!OperatingSystem.IsWindows() || client?.Handle is not { IsInvalid: false, IsClosed: false } handle) return null;
        var id = GetProcessId(handle);
        return id == 0 ? null : (int)id;
    }

    public ProcessFacts? Read(int id)
    {
        if (!OperatingSystem.IsWindows()) return null;
        using var process = OpenProcess(QueryLimited | VmRead, false, (uint)id);
        if (process.IsInvalid)
        {
            // A process that will not open for its memory still names its parent and its line.
            using var limited = OpenProcess(QueryLimited, false, (uint)id);
            return limited.IsInvalid ? null : Facts(id, limited, environment: false);
        }

        return Facts(id, process, environment: true);
    }

    [SupportedOSPlatform("windows")]
    private static ProcessFacts? Facts(int id, SafeProcessHandle process, bool environment)
    {
        if (NtQueryInformationProcess(process, BasicInformation, out var basic, Marshal.SizeOf<BasicInformationOf>(), out _) != 0) return null;
        if (!GetProcessTimes(process, out var created, out _, out _, out _)) return null;
        var block = environment ? EnvironmentOf(process, basic.PebBaseAddress) : null;
        return new ProcessFacts(
            id, (int)basic.InheritedFromUniqueProcessId, DateTime.FromFileTimeUtc(created), ImageOf(process), ArgumentsOf(process),
            name => block?.GetValueOrDefault(name));
    }

    private static string? ImageOf(SafeProcessHandle process)
    {
        var text = new StringBuilder(32768);
        var length = text.Capacity;
        return QueryFullProcessImageNameW(process, 0, text, ref length) ? text.ToString(0, length) : null;
    }

    /// <summary>The process's command line, split as the C runtime splits it, the image's element left out.</summary>
    private static IReadOnlyList<string>? ArgumentsOf(SafeProcessHandle process)
    {
        var size = 0;
        NtQueryInformationProcess(process, CommandLineInformation, IntPtr.Zero, 0, out size);
        if (size <= 0) return null;
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (NtQueryInformationProcess(process, CommandLineInformation, buffer, size, out _) != 0) return null;
            var line = Marshal.PtrToStructure<NativeString>(buffer);
            return line.Buffer == IntPtr.Zero ? null : Split(Marshal.PtrToStringUni(line.Buffer, line.Length / 2));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>A command line split as <c>CommandLineToArgvW</c> splits it, its first element (the image) left out.</summary>
    public static IReadOnlyList<string>? Split(string line)
    {
        var argv = CommandLineToArgvW(line, out var count);
        if (argv == IntPtr.Zero) return null;
        try
        {
            var parts = new List<string>(count);
            for (var at = 1; at < count; at++) parts.Add(Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, at * IntPtr.Size)) ?? "");
            return parts;
        }
        finally
        {
            LocalFree(argv);
        }
    }

    /// <summary>
    /// The process's environment block, read from its memory: the PEB's process parameters, their <c>Environment</c> pointer and
    /// <c>EnvironmentSize</c> (x64 offsets <c>0x20</c>, <c>0x80</c>, <c>0x3F0</c>). Null where any read fails or this is not a 64-bit process.
    /// </summary>
    private static Dictionary<string, string>? EnvironmentOf(SafeProcessHandle process, IntPtr peb)
    {
        if (IntPtr.Size != 8 || peb == IntPtr.Zero) return null;
        if (ReadPointer(process, peb + 0x20) is not { } parameters || parameters == IntPtr.Zero) return null;
        if (ReadPointer(process, parameters + 0x80) is not { } environment || environment == IntPtr.Zero) return null;
        if (ReadPointer(process, parameters + 0x3F0) is not { } sized) return null;
        var size = (long)sized;
        if (size <= 0 || size > 1 << 22) return null;
        var bytes = new byte[size];
        if (!ReadProcessMemory(process, environment, bytes, (IntPtr)size, out var read) || (long)read != size) return null;
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Encoding.Unicode.GetString(bytes).Split('\0'))
        {
            // A name never starts at its own `=`, but the system's per-drive entries (`=C:=C:\`) do: those are no variable.
            if (entry.Length < 2) continue;
            var equals = entry.IndexOf('=', 1);
            if (equals <= 0) continue;
            variables.TryAdd(entry[..equals], entry[(equals + 1)..]);
        }

        return variables;
    }

    private static IntPtr? ReadPointer(SafeProcessHandle process, IntPtr at)
    {
        var bytes = new byte[IntPtr.Size];
        return ReadProcessMemory(process, at, bytes, IntPtr.Size, out var read) && (int)read == IntPtr.Size
            ? (IntPtr)BitConverter.ToInt64(bytes)
            : null;
    }

    private const uint QueryLimited = 0x1000;
    private const uint VmRead = 0x0010;
    private const int BasicInformation = 0;
    private const int CommandLineInformation = 60;

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicInformationOf
    {
        public IntPtr ExitStatus;
        public IntPtr PebBaseAddress;
        public IntPtr AffinityMask;
        public IntPtr BasePriority;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetProcessId(SafeProcessHandle process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(SafeProcessHandle process, out long created, out long exited, out long kernel, out long user);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(SafeProcessHandle process, uint flags, StringBuilder name, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(SafeProcessHandle process, IntPtr address, byte[] buffer, IntPtr size, out IntPtr read);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(SafeProcessHandle process, int kind, out BasicInformationOf information, int length, out int returned);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(SafeProcessHandle process, int kind, IntPtr information, int length, out int returned);

    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CommandLineToArgvW(string line, out int count);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
