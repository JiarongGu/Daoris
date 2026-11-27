namespace Daoris.Driver;

/// <summary>A folder Daoris made, removed whole: what an account's home and a home proof's copy are removed by.</summary>
public static class FolderRemoval
{
    /// <summary>
    /// <paramref name="folder"/> gone whole, a read-only file in it cleared first: a harness that clones into its home, or a copy of a
    /// dependency store, leaves read-only files a recursive delete refuses on Windows. A link inside is removed as a link, never followed:
    /// what it points at is not Daoris's.
    /// </summary>
    public static void Delete(string folder)
    {
        var walk = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = true,
        };
        foreach (var file in Directory.EnumerateFiles(folder, "*", walk))
        {
            var attributes = File.GetAttributes(file);
            if (attributes.HasFlag(FileAttributes.ReadOnly))
            {
                File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
            }
        }

        Directory.Delete(folder, recursive: true);
    }
}
