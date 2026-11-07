namespace Daoris.Driver;

/// <summary>A carried conversation's files written into the next account's home: each beside its place first, then moved over it, so a copy cut short leaves the older file whole.</summary>
public static class CarriedFiles
{
    /// <summary>Copies one file over its place in the other home.</summary>
    public static void Replace(string source, string target)
    {
        var beside = target + ".carrying";
        File.Copy(source, beside, overwrite: true);
        File.Move(beside, target, overwrite: true);
    }

    /// <summary>Copies a folder's files over their places in the other home, its own folders with them.</summary>
    public static void Folder(string source, string target)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var placed = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(placed)!);
            Replace(file, placed);
        }
    }
}
