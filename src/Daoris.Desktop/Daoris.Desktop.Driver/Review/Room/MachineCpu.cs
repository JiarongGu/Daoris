using System.Runtime.InteropServices;

namespace Daoris.Driver;

/// <summary>The processors' time since the machine started, cumulative: the time they were busy and all of it. Busy is read between two readings, never from one.</summary>
public sealed record CpuTicks(long Busy, long Total);

/// <summary>The machine's processor time as the room reads it (<see cref="RoomLedger"/>): <c>GetSystemTimes</c> on Windows, <c>/proc/stat</c> on Linux, nothing elsewhere.</summary>
public static class MachineCpu
{
    /// <summary>This machine's reading now, or null where it cannot be read.</summary>
    public static CpuTicks? Read()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return GetSystemTimes(out var idle, out var kernel, out var user)
                    ? new CpuTicks(kernel + user - idle, kernel + user)
                    : null;
            }

            if (OperatingSystem.IsLinux()) return Stat(File.ReadLines("/proc/stat").FirstOrDefault() ?? "");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DllNotFoundException or EntryPointNotFoundException)
        {
        }

        return null;
    }

    /// <summary><c>/proc/stat</c>'s first line: user, nice, system, idle, iowait, irq, softirq, steal; idle and iowait are not busy.</summary>
    internal static CpuTicks? Stat(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5 || parts[0] != "cpu" || !parts.Skip(1).Take(8).All(part => long.TryParse(part, out _))) return null;
        var values = parts.Skip(1).Take(8).Select(long.Parse).ToList();
        var total = values.Sum();
        return new CpuTicks(total - values[3] - (values.Count > 4 ? values[4] : 0), total);
    }

    /// <summary>The processors' busy between two readings, 0 to 1, or null where they do not advance.</summary>
    public static double? Between(CpuTicks? before, CpuTicks? after) =>
        before is not null && after is not null && after.Total > before.Total
            ? Math.Clamp((after.Busy - before.Busy) / (double)(after.Total - before.Total), 0, 1)
            : null;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
}
