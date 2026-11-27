using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Daoris.Driver;

/// <summary>
/// A hard link: a second name for one file record, which is how a tree's dependencies cost no bytes beside the store's entry
/// (<see cref="DependencyStore.Place"/>). The platform makes it (<c>CreateHardLinkW</c> on Windows, <c>link</c> elsewhere); one it
/// refuses (another volume, a file system with none, the record's limit of names) is said as false, and the caller copies.
/// </summary>
internal static class FileLinks
{
    /// <summary>The test seam: where set, every link is refused, as a volume that makes none refuses it. Production sets none.</summary>
    internal static readonly AsyncLocal<bool> Declined = new();

    /// <summary>A hard link at <paramref name="link"/> to the file <paramref name="existing"/>; false where the platform refused it.</summary>
    internal static bool Make(string link, string existing)
    {
        if (Declined.Value) return false;
        try
        {
            return OperatingSystem.IsWindows()
                ? CreateHardLinkW(Prefixed(link), Prefixed(existing), IntPtr.Zero)
                : LinkUnix(existing, link) == 0;
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or Win32Exception)
        {
            return false;
        }
    }

    private static string Prefixed(string path)
    {
        var full = Path.GetFullPath(path);
        return full.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + full[2..] : @"\\?\" + full;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string link, string existing, IntPtr security);

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int LinkUnix(string existing, string link);
}
