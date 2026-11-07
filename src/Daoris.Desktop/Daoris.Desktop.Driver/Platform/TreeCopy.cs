namespace Daoris.Driver;

/// <summary>A folder copied file by file; it names nothing of what the folder holds.</summary>
public static class TreeCopy
{
    /// <summary>Every file and folder under <paramref name="from"/> into <paramref name="to"/>; a link is neither followed nor copied, as the CLI's copy does.</summary>
    public static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var entry in new DirectoryInfo(from).EnumerateFileSystemInfos())
        {
            if (entry.LinkTarget is not null) continue;
            var target = Path.Combine(to, entry.Name);
            if (entry is DirectoryInfo directory) Copy(directory.FullName, target);
            else if (entry is FileInfo file) file.CopyTo(target);
        }
    }
}
