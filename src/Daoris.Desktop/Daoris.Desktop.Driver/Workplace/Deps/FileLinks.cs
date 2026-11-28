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
                : (OperatingSystem.IsMacOS() ? LinkMac(existing, link) : LinkLinux(existing, link)) == 0;
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

    /// <summary>
    /// A read-only file's name removed with the record's mark left as it is, as Node's own delete does: the removal is POSIX-semantic
    /// and ignores the mark, so the other names of a linked file stay read-only. <paramref name="names"/> is how many names the record had
    /// before; false where this volume or system cannot, and the caller clears the mark and deletes.
    /// </summary>
    internal static bool Unlink(string path, out uint names)
    {
        names = 0;
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            using var handle = CreateFileW(Prefixed(path), DeleteAccess | ReadAttributes, ShareAll, IntPtr.Zero, OpenExisting, OpenReparsePoint, IntPtr.Zero);
            if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var info)) return false;
            names = info.Links;
            var flags = DispositionDelete | DispositionPosix | DispositionIgnoreReadOnly;
            return SetFileInformationByHandle(handle, FileDispositionInfoEx, ref flags, sizeof(uint));
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or Win32Exception)
        {
            return false;
        }
    }

    private const uint DeleteAccess = 0x10000;
    private const uint ReadAttributes = 0x80;
    private const uint ShareAll = 7;
    private const uint OpenExisting = 3;
    private const uint OpenReparsePoint = 0x00200000;
    private const int FileDispositionInfoEx = 21;
    private const uint DispositionDelete = 0x1;
    private const uint DispositionPosix = 0x2;
    private const uint DispositionIgnoreReadOnly = 0x10;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFileW(
        string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle file, int information, ref uint data, int size);

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

    [DllImport("libc.so.6", EntryPoint = "link", SetLastError = true)]
    private static extern int LinkLinux(string existing, string link);

    [DllImport("libSystem", EntryPoint = "link", SetLastError = true)]
    private static extern int LinkMac(string existing, string link);
}
