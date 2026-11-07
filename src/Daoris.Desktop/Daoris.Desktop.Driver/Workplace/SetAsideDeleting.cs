namespace Daoris.Driver;

/// <summary>A folder deleted with everything in it, never following a link.</summary>
internal static class SetAsideDeleting
{
    /// <summary>
    /// <paramref name="folder"/> deleted with everything in it, never following a link: a link goes as a link, its own
    /// entry alone, so what it points at stays wherever it is; a read-only file, as git's objects are, is made writable first, and
    /// only a file that is one. A folder or file that cannot be deleted throws, and what was deleted before it stays deleted. A
    /// tree's open discards an unfinished copy of its checkout's dependencies with it too. The bytes its files held.
    /// </summary>
    /// <remarks>
    /// 🔴 KEEP1a: it checks its own argument, and each folder again right before it reads it, and refuses one that is a link:
    /// handed a folder that is, or became, a junction, it would have read the junction's target. What still crosses is a folder replaced
    /// by a junction between that check and the read, under a trees home only Daoris writes.
    /// </remarks>
    /// <exception cref="IOException">It is a link, or something in it could not be deleted.</exception>
    internal static long DeleteNeverFollowing(string folder)
    {
        // A folder that is not there reads every attribute set (-1): its enumeration below says it is missing, as it always did.
        var attributes = new DirectoryInfo(folder).Attributes;
        if ((int)attributes != -1 && attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException($"{folder} is a link, so nothing is deleted through it");
        }

        long bytes = 0;
        var options = new EnumerationOptions { IgnoreInaccessible = false, AttributesToSkip = 0, RecurseSubdirectories = false };
        foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", options).ToList())
        {
            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                // Windows takes a junction or a folder's link off with the folder call, which removes the link and never reaches
                // into it; elsewhere a link to a folder is a file to unlink.
                if (entry is DirectoryInfo && OperatingSystem.IsWindows()) Directory.Delete(entry.FullName, recursive: false);
                else File.Delete(entry.FullName);
            }
            else if (entry is DirectoryInfo inner)
            {
                bytes += DeleteNeverFollowing(inner.FullName);
            }
            else
            {
                if (entry.Attributes.HasFlag(FileAttributes.ReadOnly)) entry.Attributes &= ~FileAttributes.ReadOnly;
                bytes += ((FileInfo)entry).Length;
                entry.Delete();
                if (SetAsideFaults.Current?.StopInDelete == true) throw new SetAsideStop();
            }
        }

        Directory.Delete(folder, recursive: false);
        return bytes;
    }
}
