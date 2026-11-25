using System.Runtime.InteropServices;

namespace Daoris.Driver;

/// <summary>This machine's memory as the reserve's row shows it (<see cref="MachineUse"/>), read here and handed down to the settings by their callers.</summary>
public static class MachineUses
{
    private const long FreshMilliseconds = 5000;

    private static readonly object Gate = new();

    private static (long At, string Home, int? Reserve, MachineUse Use)? _last;

    /// <summary>This machine now, read at most every few seconds: the share walks every process, which a state read each look would repeat.</summary>
    public static MachineUse Read(string home, int? reserveGb)
    {
        lock (Gate)
        {
            if (_last is { } last && last.Home == home && last.Reserve == reserveGb && Environment.TickCount64 - last.At < FreshMilliseconds) return last.Use;
            var reading = MachineMemory.Read();
            var ceiling = CheckRules.Of(null, Environment.ProcessorCount, null, Environment.GetEnvironmentVariable, () => null).Ceiling;
            var why = RoomWhys.Read(home, reading, reserveGb, RoomWorld.Machine.Clock(), ceiling);
            var use = new MachineUse(reading?.Total, reading?.Commit ?? reading?.Physical, DaorisShare.Read(home), why.Keep) { Why = why };
            _last = (Environment.TickCount64, home, reserveGb, use);
            return use;
        }
    }

}

/// <summary>
/// Daoris's own share of the machine's commit: the processes holding a heavy run's slot and this process, with every process each started,
/// their private memory summed (on Windows its private usage, the commit it holds; on Linux the data it maps).
/// </summary>
public static class DaorisShare
{
    /// <summary>The share now: this process and every holder of a slot under <paramref name="home"/>, each with its children; null where processes cannot be read.</summary>
    public static long? Read(string home)
    {
        try
        {
            var roots = new HashSet<int> { Environment.ProcessId };
            foreach (var (_, holder) in HeavySlot.Holders(home)) roots.Add(holder.Pid);
            foreach (var holder in VerifySlot.Look(home).Holders) roots.Add(holder.Pid);
            return Of(roots, Parents(), Commit);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    /// <summary>The resident memory of the same trees and of every place held in the admission: the unit of the machine's physical use.</summary>
    public static long? ReadResident(string home)
    {
        try
        {
            var roots = new HashSet<int> { Environment.ProcessId };
            foreach (var (_, holder) in HeavySlot.Holders(home)) roots.Add(holder.Pid);
            foreach (var holder in VerifySlot.Look(home).Holders) roots.Add(holder.Pid);
            foreach (var hold in MachineAdmission.Look(home).Holds) roots.Add(hold.Pid);
            return Of(roots, Parents(), Resident);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    /// <summary>The commit of every process in the trees under <paramref name="roots"/>, each counted once, from each process's parent.</summary>
    public static long? Of(IEnumerable<int> roots, IReadOnlyDictionary<int, int>? parents, Func<int, long?> commit)
    {
        if (parents is null) return null;
        var children = parents.GroupBy(pair => pair.Value, pair => pair.Key).ToDictionary(group => group.Key, group => group.ToList());
        var seen = new HashSet<int>();
        var pending = new Stack<int>(roots.Where(parents.ContainsKey));
        long sum = 0;
        while (pending.TryPop(out var pid))
        {
            if (!seen.Add(pid)) continue;
            sum += commit(pid) ?? 0;
            if (children.TryGetValue(pid, out var below)) foreach (var child in below) pending.Push(child);
        }

        return sum;
    }

    /// <summary>One process's private memory, asked of that process alone (never through <c>Process</c>, whose lookup reads every process on the machine); null where it is gone or not ours to read.</summary>
    private static long? Commit(int pid) => Held(pid, false);

    private static long? Resident(int pid) => Held(pid, true);

    private static long? Held(int pid, bool resident)
    {
        try
        {
            if (OperatingSystem.IsWindows()) return WindowsHeld(pid, resident);
            if (!OperatingSystem.IsLinux()) return null;
            var field = resident ? "VmRSS:" : "VmData:";
            var line = File.ReadLines($"/proc/{pid}/status").FirstOrDefault(each => each.StartsWith(field, StringComparison.Ordinal));
            return line is not null && long.TryParse(line[field.Length..].Trim().Split(' ')[0], out var kilobytes) ? kilobytes * 1024 : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static long? WindowsHeld(int pid, bool resident)
    {
        var handle = OpenProcess(QueryLimited, false, (uint)pid);
        if (handle == IntPtr.Zero) return null;
        try
        {
            var counters = new ProcessMemory { Size = (uint)Marshal.SizeOf<ProcessMemory>() };
            return GetProcessMemoryInfo(handle, ref counters, counters.Size) ? (long)(resident ? counters.WorkingSet : counters.PrivateUsage) : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    /// <summary>Each process's parent, or null where none can be read here.</summary>
    internal static IReadOnlyDictionary<int, int>? Parents()
    {
        if (OperatingSystem.IsWindows()) return WindowsParents();
        if (!OperatingSystem.IsLinux()) return null;
        var parents = new Dictionary<int, int>();
        foreach (var folder in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(folder), out var pid)) continue;
            try
            {
                var stat = File.ReadAllText(Path.Combine(folder, "stat"));
                var after = stat[(stat.LastIndexOf(')') + 2)..].Split(' ');
                if (int.TryParse(after[1], out var parent)) parents[pid] = parent;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
            }
        }

        return parents;
    }

    private static Dictionary<int, int>? WindowsParents()
    {
        var snapshot = CreateToolhelp32Snapshot(SnapProcess, 0);
        if (snapshot == InvalidHandle) return null;
        try
        {
            var parents = new Dictionary<int, int>();
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
            for (var more = Process32First(snapshot, ref entry); more; more = Process32Next(snapshot, ref entry)) parents[(int)entry.Id] = (int)entry.ParentId;
            return parents;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    private const uint SnapProcess = 2;

    private const uint QueryLimited = 0x1000;

    private static readonly IntPtr InvalidHandle = new(-1);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size;
        public uint Usage;
        public uint Id;
        public UIntPtr DefaultHeap;
        public uint Module;
        public uint Threads;
        public uint ParentId;
        public int PriorityBase;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessMemory
    {
        public uint Size;
        public uint PageFaults;
        public UIntPtr PeakWorkingSet;
        public UIntPtr WorkingSet;
        public UIntPtr QuotaPeakPagedPool;
        public UIntPtr QuotaPagedPool;
        public UIntPtr QuotaPeakNonPagedPool;
        public UIntPtr QuotaNonPagedPool;
        public UIntPtr Pagefile;
        public UIntPtr PeakPagefile;
        public UIntPtr PrivateUsage;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint pid);

    [DllImport("kernel32.dll", EntryPoint = "K32GetProcessMemoryInfo", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessMemoryInfo(IntPtr process, ref ProcessMemory counters, uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint process);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
