using System.Runtime.InteropServices;

namespace Daoris.Driver;

/// <summary>The machine's free memory, read once, in bytes: free physical memory and free commit, each null where it cannot be read.</summary>
public sealed record MemoryReading(long? Physical, long? Commit)
{
    /// <summary>The lower of the two read, or null where neither reads.</summary>
    public long? Least => Physical is { } physical && Commit is { } commit ? Math.Min(physical, commit) : Physical ?? Commit;
}

/// <summary>
/// The machine's free memory as the driver reads it before it admits another check (<see cref="CheckAdmission"/>): on Windows
/// <c>GlobalMemoryStatusEx</c>'s free physical memory and free commit (the commit limit less the commit charge), the two the merge tool
/// reads before a heavy gate (<c>tools/merge-branch/memory.mjs</c>); on Linux <c>/proc/meminfo</c>'s available memory, and its free commit
/// only under strict overcommit, the one case it holds a process to it; elsewhere nothing, which admits no second check.
/// </summary>
public static class MachineMemory
{
    /// <summary>This machine's reading now, or null where nothing could be read.</summary>
    public static MemoryReading? Read()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
                return GlobalMemoryStatusEx(ref status) ? new MemoryReading((long)status.AvailPhys, (long)status.AvailPageFile) : null;
            }

            if (OperatingSystem.IsLinux()) return Meminfo(File.ReadAllText("/proc/meminfo"), File.ReadAllText("/proc/sys/vm/overcommit_memory").Trim() == "2");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DllNotFoundException or EntryPointNotFoundException)
        {
        }

        return null;
    }

    /// <summary><c>/proc/meminfo</c> read: <c>MemAvailable</c>, and <c>CommitLimit</c> less <c>Committed_AS</c> where <paramref name="strict"/>.</summary>
    internal static MemoryReading? Meminfo(string text, bool strict)
    {
        long? Field(string name)
        {
            var line = text.Split('\n').FirstOrDefault(each => each.StartsWith(name + ":", StringComparison.Ordinal));
            var number = line?[(name.Length + 1)..].Trim().Split(' ')[0];
            return long.TryParse(number, out var kilobytes) ? kilobytes * 1024 : null;
        }

        var physical = Field("MemAvailable");
        var commit = strict && Field("CommitLimit") is { } limit && Field("Committed_AS") is { } committed ? Math.Max(0, limit - committed) : (long?)null;
        return physical is null && commit is null ? null : new MemoryReading(physical, commit);
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
}
