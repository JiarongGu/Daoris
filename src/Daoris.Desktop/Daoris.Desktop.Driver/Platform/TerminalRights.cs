using System.Runtime.InteropServices;

namespace Daoris.Driver;

/// <summary>
/// Whether a terminal holds this process's rights: neither a restricted token nor an app container, and at this process's
/// integrity level. What this process starts for a terminal runs with this process's rights, so a terminal held in a sandbox, or
/// at another level, is refused and runs its work itself, inside what holds it. Windows only.
/// </summary>
public static class TerminalRights
{
    /// <summary>Null where the process <paramref name="terminal"/> (opened at least to query) holds this process's rights; else why not.</summary>
    public static string? Differ(IntPtr terminal)
    {
        if (Of(terminal) is not { } theirs) return "the terminal's rights could not be read";
        if (Of(GetCurrentProcess()) is not { } ours) return "Daoris's own rights could not be read";
        if (theirs.Restricted) return "the terminal runs on a restricted token, a sandbox's";
        if (theirs.Container) return "the terminal runs in an app container";
        return theirs.Integrity == ours.Integrity ? null : "the terminal runs at another integrity level than Daoris";
    }

    private static (bool Restricted, bool Container, uint Integrity)? Of(IntPtr process)
    {
        if (!OpenProcessToken(process, TokenQuery, out var token)) return null;
        try
        {
            var container = Read(token, TokenIsAppContainer, buffer => (bool?)(Marshal.ReadInt32(buffer) != 0));
            var integrity = Read(token, TokenIntegrityLevel, buffer =>
            {
                var sid = Marshal.ReadIntPtr(buffer);
                var count = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
                return count == 0 ? (uint?)null : (uint)Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(count - 1)));
            });
            return container is { } inside && integrity is { } level ? (IsTokenRestricted(token), inside, level) : null;
        }
        finally
        {
            CloseHandle(token);
        }
    }

    private static T? Read<T>(IntPtr token, int what, Func<IntPtr, T?> read)
    {
        GetTokenInformation(token, what, IntPtr.Zero, 0, out var size);
        if (size == 0) return default;
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            return GetTokenInformation(token, what, buffer, size, out _) ? read(buffer) : default;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private const uint TokenQuery = 0x0008;
    private const int TokenIntegrityLevel = 25;
    private const int TokenIsAppContainer = 29;

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr token, int what, IntPtr buffer, int size, out int returned);

    [DllImport("advapi32.dll")]
    private static extern bool IsTokenRestricted(IntPtr token);

    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);

    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
