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

    /// <summary>
    /// Which file record <paramref name="path"/> is a name of: equal for the names of one record, so a reader of sizes counts a link once;
    /// null where the platform names none or the file cannot be opened.
    /// </summary>
    internal static string? IdentityOf(string path)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1);
            return GetFileInformationByHandle(stream.SafeFileHandle, out var info) ? $"{info.VolumeSerialNumber:x}:{info.FileIndexHigh:x}:{info.FileIndexLow:x}" : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public uint CreatedLow;
        public uint CreatedHigh;
        public uint AccessedLow;
        public uint AccessedHigh;
        public uint WrittenLow;
        public uint WrittenHigh;
        public uint VolumeSerialNumber;
        public uint SizeHigh;
        public uint SizeLow;
        public uint Links;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle file, out FileInformation information);

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int LinkUnix(string existing, string link);
}
