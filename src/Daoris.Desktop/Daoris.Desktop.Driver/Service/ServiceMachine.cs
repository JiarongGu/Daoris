namespace Daoris.Driver;

/// <summary>
/// What Service reads and writes of this machine beside the service's HTTP (STRUCT3d, D166's note): a variable of the
/// environment, a file's text where there is one, a file written whole, whether a file is there, and the folder above that
/// holds a marker. Service's Application reads the remotes map, the knowledge connector and the registry's records through
/// these, as each read them before.
/// </summary>
public static class ServiceMachine
{
    /// <summary>A variable of this process's environment, or null where it is unset.</summary>
    public static string? Variable(string name) => Environment.GetEnvironmentVariable(name);

    /// <summary>A file's text, or null where there is no file; a file that does not read throws as a read does.</summary>
    public static string? TextIfThere(string? path) => File.Exists(path) ? File.ReadAllText(path) : null;

    /// <summary>Whether a file is there.</summary>
    public static bool Exists(string path) => File.Exists(path);

    /// <summary>A file written whole, atomically (beside, then renamed), the folder it goes in made first.</summary>
    public static void WriteText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        AtomicFile.WriteText(path, text);
    }

    /// <summary>A file of <paramref name="folder"/>'s written whole, atomically, the folder made first.</summary>
    public static void WriteIn(string folder, string name, string text)
    {
        Directory.CreateDirectory(folder);
        AtomicFile.WriteText(Path.Combine(folder, name), text);
    }

    /// <summary>The nearest folder at or above <paramref name="from"/> holding a file named <paramref name="marker"/>, whole; null for none.</summary>
    public static string? FolderWith(string from, string marker)
    {
        var directory = new DirectoryInfo(from);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, marker)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName;
    }
}
