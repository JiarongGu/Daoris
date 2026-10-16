using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Daoris.Driver;

/// <summary>
/// The change watch's source on the machine (FABRIC-EVENT2, the design §A4.1): one <see cref="FileSystemWatcher"/> per folder,
/// with a 64 KB buffer, telling names, writes and sizes. A rename is told as the name it left, deleted, then the name it
/// arrived under, since a replace is a deletion and then a rename (§A5, measured); an <c>Error</c> is a loss.
/// </summary>
/// <remarks>
/// A folder's identity is read when it is opened, so a reconciliation can find it replaced at its path, which no watcher
/// reports: the volume and file id on Windows, its creation time elsewhere.
/// </remarks>
public sealed class FolderWatcherSource : IChangeSource
{
    /// <summary>The buffer each watcher holds, which the system pins: the most it allows.</summary>
    public const int Buffer = 64 * 1024;

    public IChangeHandle? Open(string folder, bool subfolders, IChangeSink sink)
    {
        if (!Directory.Exists(folder) || FolderIdentity.Of(folder) is not { } identity) return null;
        FileSystemWatcher? watcher = null;
        try
        {
            watcher = new FileSystemWatcher(folder)
            {
                InternalBufferSize = Buffer,
                IncludeSubdirectories = subfolders,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            watcher.Created += (_, e) => sink.Moved(Name(e.Name), ChangeKind.Created);
            watcher.Changed += (_, e) => sink.Moved(Name(e.Name), ChangeKind.Changed);
            watcher.Deleted += (_, e) => sink.Moved(Name(e.Name), ChangeKind.Deleted);
            watcher.Renamed += (_, e) =>
            {
                sink.Moved(Name(e.OldName), ChangeKind.Deleted);
                sink.Moved(Name(e.Name), ChangeKind.Renamed);
            };
            watcher.Error += (_, e) =>
                sink.Lost(e.GetException() is InternalBufferOverflowException ? "overflow" : e.GetException()?.GetType().Name ?? "error");
            watcher.EnableRaisingEvents = true;
            return new Handle(watcher, folder, identity);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or PlatformNotSupportedException)
        {
            watcher?.Dispose();
            return null;
        }
    }

    private static string Name(string? name) => (name ?? "").Replace('\\', '/');

    private sealed class Handle(FileSystemWatcher watcher, string folder, string identity) : IChangeHandle
    {
        public bool Same => FolderIdentity.Of(folder) == identity;

        public void Dispose()
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
    }
}

/// <summary>A folder's identity at its path: what changes when it is deleted and made again, or another moved onto it.</summary>
internal static class FolderIdentity
{
    private const uint ReadAttributes = 0x80;
    private const uint ShareAll = 0x7;
    private const uint OpenExisting = 3;
    private const uint BackupSemantics = 0x02000000;

    /// <summary>The identity of the folder at <paramref name="folder"/> now, or null where there is none.</summary>
    public static string? Of(string folder)
    {
        try
        {
            if (!Directory.Exists(folder)) return null;
            if (!OperatingSystem.IsWindows())
            {
                return Directory.GetCreationTimeUtc(folder).Ticks.ToString(CultureInfo.InvariantCulture);
            }

            using var handle = CreateFileW(folder, ReadAttributes, ShareAll, IntPtr.Zero, OpenExisting, BackupSemantics, IntPtr.Zero);
            if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var information)) return null;
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{information.VolumeSerialNumber:x8}-{information.FileIndexHigh:x8}{information.FileIndexLow:x8}");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Packed at four: a FILETIME is two DWORDs, so the times sit at offsets 4, 12 and 20, not padded to eight.
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct FileInformation
    {
        public uint FileAttributes;
        public long CreationTime;
        public long LastAccessTime;
        public long LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);
}
