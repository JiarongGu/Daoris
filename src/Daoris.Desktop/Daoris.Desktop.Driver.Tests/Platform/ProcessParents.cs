using System.Runtime.InteropServices;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Who started a process, as Windows' process snapshot says: the tests' reading of a real process's parents, where the product's own
/// is the desktop's (<c>SystemProcesses</c>), which the driver's tests do not reference.
/// </summary>
internal static class ProcessParents
{
    /// <summary>The process that started <paramref name="pid"/>; null where it no longer runs.</summary>
    public static int? ParentOf(int pid) => Snapshot().TryGetValue(pid, out var parent) ? parent : null;

    /// <summary>
    /// <paramref name="pid"/> and its parents, nearest first, up to <paramref name="depth"/> of them, ending at this test's own process
    /// or at a parent no longer running.
    /// </summary>
    public static IReadOnlyList<int> AncestorsOf(int pid, int depth = 8)
    {
        var parents = Snapshot();
        List<int> ancestors = [pid];
        var at = pid;
        while (ancestors.Count <= depth && parents.TryGetValue(at, out var parent) && parent > 0 && parent != Environment.ProcessId
            && !ancestors.Contains(parent))
        {
            ancestors.Add(parent);
            at = parent;
        }

        return ancestors;
    }

    private static Dictionary<int, int> Snapshot()
    {
        Dictionary<int, int> parents = [];
        var snapshot = CreateToolhelp32Snapshot(0x00000002, 0);
        if (snapshot == new IntPtr(-1)) return parents;
        try
        {
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
            for (var more = Process32FirstW(snapshot, ref entry); more; more = Process32NextW(snapshot, ref entry))
            {
                parents[(int)entry.ProcessId] = (int)entry.ParentProcessId;
            }

            return parents;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public IntPtr DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriorityBase;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint process);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
