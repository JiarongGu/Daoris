using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Daoris.Driver;

/// <summary>
/// The links a placement makes again inside its tree: a junction on Windows for a folder, which needs no privilege and is what npm
/// makes there, and a symbolic link relative to where it stands elsewhere and for a file.
/// </summary>
internal static class DependencyLinks
{
    /// <summary>A link at <paramref name="path"/> to the folder <paramref name="target"/>.</summary>
    internal static void Link(string path, string target) => Make(path, path, target, directory: true);

    /// <summary>A link made at <paramref name="path"/> that will stand at <paramref name="final"/> once its folder is renamed into place.</summary>
    internal static void Make(string path, string final, string target, bool directory)
    {
        if (directory && OperatingSystem.IsWindows())
        {
            Junction(Path.GetFullPath(path), Path.GetFullPath(target));
            return;
        }

        var relative = Path.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(final))!, target);
        if (directory) Directory.CreateSymbolicLink(path, relative);
        else File.CreateSymbolicLink(path, relative);
    }

    /// <summary>Where a link leads, read off the link and never through it; null where it names nothing.</summary>
    internal static string? Leads(FileSystemInfo link) =>
        link.LinkTarget is { Length: > 0 } target ? Path.GetFullPath(Path.Combine(Path.GetDirectoryName(link.FullName)!, StripPrefix(target))) : null;

    /// <summary>Whether <paramref name="path"/> is <paramref name="folder"/> or inside it.</summary>
    internal static bool Within(string folder, string path)
    {
        var within = Path.GetRelativePath(folder, path);
        return within == "." || !(within == ".." || within.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(within));
    }

    private static string StripPrefix(string path) =>
        path.StartsWith(@"\??\UNC\", StringComparison.Ordinal) ? @"\\" + path[8..]
        : path.StartsWith(@"\??\", StringComparison.Ordinal) || path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path[4..]
        : path;

    /// <summary>
    /// A junction at <paramref name="path"/> to <paramref name="target"/>: an empty folder given a mount point's reparse data, as
    /// <c>mklink /J</c> and npm make one, with no process started. A junction that could not be made leaves no folder.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void Junction(string path, string target)
    {
        target = StripPrefix(target);
        var substitute = Encoding.Unicode.GetBytes(target.StartsWith(@"\\", StringComparison.Ordinal) ? @"\??\UNC\" + target[2..] : @"\??\" + target);
        var print = Encoding.Unicode.GetBytes(target);
        // The mount point's reparse data: its tag and length, then the two names' offsets and lengths, then each name and its NUL.
        var data = 8 + substitute.Length + 2 + print.Length + 2;
        var buffer = new byte[8 + data];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0), 0xA0000003);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(4), checked((ushort)data));
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(8), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(10), checked((ushort)substitute.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(12), checked((ushort)(substitute.Length + 2)));
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(14), checked((ushort)print.Length));
        substitute.CopyTo(buffer, 16);
        print.CopyTo(buffer, 16 + substitute.Length + 2);

        Directory.CreateDirectory(path);
        try
        {
            using var handle = CreateFileW(
                path.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + path[2..] : @"\\?\" + path,
                GenericWrite, 0, IntPtr.Zero, OpenExisting, BackupSemantics | OpenReparsePoint, IntPtr.Zero);
            if (handle.IsInvalid) throw new IOException($"could not open {path} to make it a link: {new Win32Exception(Marshal.GetLastPInvokeError()).Message}");
            if (!DeviceIoControl(handle, SetReparsePoint, buffer, buffer.Length, IntPtr.Zero, 0, out _, IntPtr.Zero))
            {
                throw new IOException($"could not make {path} a link: {new Win32Exception(Marshal.GetLastPInvokeError()).Message}");
            }
        }
        catch
        {
            try
            {
                Directory.Delete(path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // The placement it was part of is discarded whole.
            }

            throw;
        }
    }

    private const uint GenericWrite = 0x40000000;
    private const uint OpenExisting = 3;
    private const uint BackupSemantics = 0x02000000;
    private const uint OpenReparsePoint = 0x00200000;
    private const uint SetReparsePoint = 0x000900A4;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device, uint code, byte[] input, int inputSize, IntPtr output, int outputSize, out int returned, IntPtr overlapped);
}
