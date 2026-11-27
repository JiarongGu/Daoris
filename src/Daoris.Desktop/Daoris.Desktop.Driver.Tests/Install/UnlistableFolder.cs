using System.Security.AccessControl;
using System.Security.Principal;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>Folders that cannot be listed, as a home in use can hold.</summary>
internal static class UnlistableFolder
{
    /// <summary>
    /// A junction at <paramref name="folder"/> that leads to itself, replacing the folder: listed through, it fails as a folder that cannot
    /// be listed does; false where the platform makes no junction.
    /// </summary>
    public static bool Loop(string folder)
    {
        if (!OperatingSystem.IsWindows()) return false;
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        DependencyLinks.Link(folder, folder);
        return true;
    }

    /// <summary>
    /// A folder this process is denied listing, as one another account keeps is, until it is disposed; null where the volume lists it
    /// regardless (a volume that grants its owner listing whatever the rule says).
    /// </summary>
    public static IDisposable? Denied(string folder)
    {
        if (!OperatingSystem.IsWindows()) return null;
        var info = new DirectoryInfo(folder);
        info.Create();
        var rule = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ListDirectory, AccessControlType.Deny);
        var rules = info.GetAccessControl();
        rules.AddAccessRule(rule);
        info.SetAccessControl(rules);
        var undo = new Undo(() =>
        {
            if (!OperatingSystem.IsWindows() || !Directory.Exists(folder)) return;
            var held = info.GetAccessControl();
            held.RemoveAccessRule(rule);
            info.SetAccessControl(held);
        });
        try
        {
            _ = Directory.EnumerateFileSystemEntries(folder).ToList();
        }
        catch (UnauthorizedAccessException)
        {
            return undo;
        }

        undo.Dispose();
        return null;
    }

    private sealed class Undo(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }
}
