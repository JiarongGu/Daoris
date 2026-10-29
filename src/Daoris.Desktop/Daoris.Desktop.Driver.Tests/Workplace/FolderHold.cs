using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A folder held open by a handle the test lets go of when it chooses (TREECLEANFREE1): the stand-in for what outlives a
/// holder's exit on Windows, a scanner's or an indexer's read of the folder. Which opens it refuses is the share given:
/// <see cref="Exclusive"/> refuses every other open, so even listing the folder fails; <see cref="NoDelete"/> is a working
/// folder's own, which lists and refuses only the removal.
/// </summary>
internal static class FolderHold
{
    /// <summary>No sharing: the folder cannot be listed or removed while this is held.</summary>
    public const uint Exclusive = 0;

    /// <summary>Read and write sharing, no delete: as a process's working folder is held, the folder lists and cannot go.</summary>
    public const uint NoDelete = 0x1 | 0x2;

    private const uint ListDirectory = 0x1, OpenExisting = 3, BackupSemantics = 0x02000000;

    /// <summary>The hold on <paramref name="folder"/>, released when disposed. Windows only: elsewhere no open refuses another.</summary>
    public static SafeFileHandle Take(string folder, uint share)
    {
        var handle = CreateFileW(folder, ListDirectory, share, IntPtr.Zero, OpenExisting, BackupSemantics, IntPtr.Zero);
        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), $"`{folder}` could not be held.");
        return handle;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
}
