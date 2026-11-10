namespace Daoris.Driver;

/// <summary>
/// What a folder declares for itself: <c>daoris.json</c> and <c>daoris.lanes.json</c> at its root, read from disk, since its working
/// files are its only version (a git place's are read from its line, <see cref="LineDeclarationReader"/>). A file that is a link or a
/// folder is said and not read, as the line's are; one past the line's size bound is not read either.
/// </summary>
public static class FolderDeclarationReader
{
    private const long TextLimit = 512 * 1024;

    /// <summary>The two files at <paramref name="root"/>; no line and no commit, and each text null where the folder holds none.</summary>
    public static LineFiles Read(string root)
    {
        string? manifest = null, lanes = null, odd = null;
        foreach (var (name, assign) in new (string, Action<string>)[]
                 {
                     (DeclarationNames.Manifest, text => manifest = text),
                     (DeclarationNames.Lanes, text => lanes = text),
                 })
        {
            var path = Path.Combine(root, name);
            if (Directory.Exists(path))
            {
                odd ??= $"its {name} is not a file";
                continue;
            }

            if (!File.Exists(path)) continue;
            var info = new FileInfo(path);
            if (info.LinkTarget is not null)
            {
                odd ??= $"its {name} is a link";
                continue;
            }

            if (info.Length > TextLimit)
            {
                odd ??= $"its {name} is too large to read";
                continue;
            }

            assign(AtomicFile.ReadText(path));
        }

        return new(null, null, null) { Manifest = manifest, Lanes = lanes, FileProblem = odd };
    }
}
