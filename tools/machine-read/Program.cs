using System.Runtime.InteropServices;
using System.Text;


// machine-read memory                  -> {"physical":<bytes>,"commit":<bytes>,"total":<bytes>}
// machine-read processes [pid ...]     -> [{"pid","ppid","created","name","path","command"}, ...]
//
// Windows only. One call to the system for the whole process table (`NtQuerySystemInformation`, which names every process, its
// parent, its creation time and its image name without opening any), then, for each process the caller can open, its full path
// and command line: the two come from the process itself, so one the caller may not open (another user's, an elevated or a
// protected one) answers null for them, as it answers nobody. `created` is epoch milliseconds. With pids named, only those rows.
// Exit 0 answered, 1 the system refused, 2 usage, 3 not Windows.
if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("machine-read: this reads Windows' own calls and runs on Windows alone");
    return 3;
}

try
{
    switch (args.FirstOrDefault())
    {
        case "memory" when args.Length == 1:
            return Win.WriteMemory();
        case "processes" when args.Skip(1).All(each => uint.TryParse(each, out _)):
            return Win.WriteProcesses(args.Skip(1).Select(uint.Parse).ToHashSet());
        default:
            Console.Error.WriteLine("usage: machine-read memory | machine-read processes [pid ...]");
            return 2;
    }
}
catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
{
    Console.Error.WriteLine($"machine-read: {error.Message}");
    return 1;
}

internal static class Win
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int SystemProcessInformation = 5;
    private const int ProcessCommandLineInformation = 60;
    private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);
    private const int StatusBufferOverflow = unchecked((int)0x80000005);
    private const int StatusBufferTooSmall = unchecked((int)0xC0000023);
    private const long UnixEpochAsFileTime = 116444736000000000;

    public static int WriteMemory()
    {
        // MEMORYSTATUSEX is 64 bytes; the layout is fixed by the system, so no reflection is paid for its size.
        var status = new MemoryStatusEx { Length = 64 };
        if (!GlobalMemoryStatusEx(ref status)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        Emit($"{{\"physical\":{status.AvailPhys},\"commit\":{status.AvailPageFile},\"total\":{status.TotalPhys}}}");
        return 0;
    }

    public static int WriteProcesses(HashSet<uint> only)
    {
        if (IntPtr.Size != 8) throw new InvalidOperationException("the process table's layout is read for 64-bit Windows");
        var json = new StringBuilder("[");
        foreach (var row in Table())
        {
            if (only.Count != 0 && !only.Contains(row.Pid)) continue;
            var (path, command) = Details(row.Pid);
            if (json.Length > 1) json.Append(',');
            json.Append("{\"pid\":").Append(row.Pid).Append(",\"ppid\":").Append(row.Parent).Append(",\"created\":").Append(row.Created);
            Text(json.Append(",\"name\":"), row.Name);
            Text(json.Append(",\"path\":"), path);
            Text(json.Append(",\"command\":"), command);
            json.Append('}');
        }

        Emit(json.Append(']').ToString());
        return 0;
    }

    /// <summary>The text on standard output as UTF-8, with no newline: what a caller reads whole.</summary>
    private static void Emit(string text)
    {
        var bytes = new UTF8Encoding(false).GetBytes(text);
        using var output = Console.OpenStandardOutput();
        output.Write(bytes, 0, bytes.Length);
    }

    /// <summary>A JSON string (or null) appended: quotes, backslashes and control characters escaped, everything else as it is.</summary>
    private static void Text(StringBuilder json, string? text)
    {
        if (text is null)
        {
            json.Append("null");
            return;
        }

        json.Append('"');
        foreach (var each in text)
        {
            switch (each)
            {
                case '"': json.Append("\\\""); break;
                case '\\': json.Append("\\\\"); break;
                case < ' ': json.Append("\\u").Append(((int)each).ToString("x4")); break;
                default: json.Append(each); break;
            }
        }

        json.Append('"');
    }

    private sealed record Row(uint Pid, uint Parent, long Created, string Name);

    /// <summary>The system's table, read as SYSTEM_PROCESS_INFORMATION entries (x64): the buffer grows until the system says it fits.</summary>
    private static IEnumerable<Row> Table()
    {
        var size = 1 << 20;
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            while (true)
            {
                var status = NtQuerySystemInformation(SystemProcessInformation, buffer, size, out var needed);
                if (status == StatusInfoLengthMismatch)
                {
                    size = Math.Max(needed, size) + (1 << 16);
                    buffer = Marshal.ReAllocHGlobal(buffer, size);
                    continue;
                }

                if (status < 0) throw new System.ComponentModel.Win32Exception($"NtQuerySystemInformation answered 0x{status:X8}");
                break;
            }

            var rows = new List<Row>();
            for (var at = 0; ;)
            {
                var entry = buffer + at;
                var pid = (uint)Marshal.ReadInt64(entry, 80);
                var parent = (uint)Marshal.ReadInt64(entry, 88);
                var created = Marshal.ReadInt64(entry, 32);
                var length = (ushort)Marshal.ReadInt16(entry, 56);
                var text = Marshal.ReadIntPtr(entry, 64);
                var name = pid == 0 ? "System Idle Process" : text == IntPtr.Zero ? "" : Marshal.PtrToStringUni(text, length / 2);
                rows.Add(new Row(pid, parent, created == 0 ? 0 : (created - UnixEpochAsFileTime) / 10_000, name));
                var next = Marshal.ReadInt32(entry, 0);
                if (next == 0) break;
                at += next;
            }

            return rows;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>A process's full image path and command line, each null where the caller may not read the process or it ended meanwhile.</summary>
    private static (string? Path, string? Command) Details(uint pid)
    {
        if (pid == 0) return (null, null);
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero) return (null, null);
        try
        {
            return (ImagePath(handle), CommandLine(handle));
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private static string? ImagePath(IntPtr handle)
    {
        var text = new StringBuilder(1024);
        var size = text.Capacity;
        return QueryFullProcessImageNameW(handle, 0, text, ref size) ? text.ToString(0, size) : null;
    }

    private static string? CommandLine(IntPtr handle)
    {
        var size = 1024;
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var status = NtQueryInformationProcess(handle, ProcessCommandLineInformation, buffer, size, out var needed);
                if (status is StatusInfoLengthMismatch or StatusBufferOverflow or StatusBufferTooSmall)
                {
                    size = Math.Max(needed, size) + 256;
                    buffer = Marshal.ReAllocHGlobal(buffer, size);
                    continue;
                }

                if (status < 0) return null;
                var length = (ushort)Marshal.ReadInt16(buffer, 0);
                var text = Marshal.ReadIntPtr(buffer, 8);
                return text == IntPtr.Zero ? "" : Marshal.PtrToStringUni(text, length / 2);
            }

            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(IntPtr handle, uint flags, StringBuilder text, ref int size);

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int information, IntPtr buffer, int length, out int needed);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr handle, int information, IntPtr buffer, int length, out int needed);
}
