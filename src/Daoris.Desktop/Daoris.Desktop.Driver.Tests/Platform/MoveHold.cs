using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A file held as Windows' own move holds the file it has just put in place (BINDRACE1, <see cref="Daoris.Driver.AtomicFile.ReadText"/>):
/// open for its deletion, sharing everything, so a test meets every run the moment a reader beside the move meets under load.
/// </summary>
internal static class MoveHold
{
    private const uint Delete = 0x00010000, ReadAttributes = 0x80, ShareAll = 0x7, OpenExisting = 3;

    /// <summary>The hold on <paramref name="path"/>, released when disposed. Windows only: elsewhere no open refuses another.</summary>
    public static SafeFileHandle Take(string path)
    {
        var handle = CreateFileW(path, Delete | ReadAttributes, ShareAll, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), $"`{path}` could not be held.");
        return handle;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
}
