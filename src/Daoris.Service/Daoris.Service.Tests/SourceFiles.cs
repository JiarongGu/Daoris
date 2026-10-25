namespace Daoris.Service.Tests;

/// <summary>
/// A desktop source, found by name wherever it lies under its project's folder (FOUNDATION1 mod-0, D166 point 4): the driver's files move
/// into subdomain folders one row at a time, so a service test that reads one names the file and never the folder it is in. A copy of the
/// desktop tests' reader, which holds the same rule there: the test projects share no code. The build's own output (<c>bin/</c>,
/// <c>obj/</c>) is no source.
/// </summary>
internal static class SourceFiles
{
    /// <summary>The desktop driver library's project folder's name.</summary>
    public const string DriverProject = "Daoris.Desktop.Driver";

    /// <summary>The one driver source of this name, at any depth of the driver's folder.</summary>
    /// <exception cref="InvalidOperationException">No source has the name, or two do.</exception>
    public static string Driver(string name) => Find(DriverProject, name);

    /// <summary>The one file of this name under a desktop project's folder, at any depth, its build output aside.</summary>
    /// <exception cref="InvalidOperationException">No file has the name, or two do.</exception>
    public static string Find(string project, string name)
    {
        var folder = Path.Combine(WorkspaceFolder(), "src", "Daoris.Desktop", project);
        var found = Directory.EnumerateFiles(folder, name, SearchOption.AllDirectories)
            .Where(path => Path.GetRelativePath(folder, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0] is not ("bin" or "obj"))
            .Where(path => Path.GetFileName(path) == name)
            .Order(StringComparer.Ordinal)
            .ToList();
        return found.Count switch
        {
            1 => found[0],
            0 => throw new InvalidOperationException($"no {name} under {project}: a source read by name must be there, at any depth"),
            _ => throw new InvalidOperationException($"{name} is under {project} {found.Count} times, so its name does not say which: {string.Join(", ", found)}"),
        };
    }

    /// <summary>The workspace's root: the folder above the test binary that holds <c>daoris.json</c>.</summary>
    public static string WorkspaceFolder()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }
}
