namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A desktop project's sources, found by name wherever they lie under the project's folder (STRUCT1, D166; the code-structure
/// design §6): the driver's files move into module folders one module per branch, so a test that reads a source names the
/// file and never the folder it is in, and a test that walks a project walks every folder of it. The build's own output
/// (<c>bin/</c>, <c>obj/</c>) is no source. <c>SourceFilesTests</c> holds it; the CLI's <c>driver-names.test.ts</c> holds
/// that each name a test reads through it is one file.
/// </summary>
internal static class SourceFiles
{
    /// <summary>The driver library's project folder's name.</summary>
    public const string DriverProject = "Daoris.Desktop.Driver";

    /// <summary>The driver's tests' project folder's name, whose folders mirror the modules that have moved (STRUCT2a).</summary>
    public const string DriverTestsProject = "Daoris.Desktop.Driver.Tests";

    private static readonly string Desktop = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop");

    /// <summary>A desktop project's folder, by the project's name: <c>src/Daoris.Desktop/&lt;project&gt;</c>.</summary>
    public static string Project(string project) => Path.Combine(Desktop, project);

    /// <summary>The one driver source of this name, at any depth of the driver's folder.</summary>
    /// <exception cref="InvalidOperationException">No source has the name, or two do.</exception>
    public static string Driver(string name) => Find(DriverProject, name);

    /// <summary>The one file of this name under a desktop project's folder, at any depth, its build output aside.</summary>
    /// <exception cref="InvalidOperationException">No file has the name, or two do.</exception>
    public static string Find(string project, string name) =>
        Single(project, name, All(project, name).Where(path => Path.GetFileName(path) == name));

    /// <summary>Every file under a desktop project's folder whose name matches the pattern, at any depth, its build output aside, in order.</summary>
    public static IEnumerable<string> All(string project, string pattern)
    {
        var folder = Project(project);
        return Directory.EnumerateFiles(folder, pattern, SearchOption.AllDirectories)
            .Where(path => Path.GetRelativePath(folder, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0] is not ("bin" or "obj"))
            .Order(StringComparer.Ordinal);
    }

    /// <summary>
    /// The one CLI source of this name, at any depth of <c>src/Daoris.Cli/src/</c> but its command rows' <c>cli/</c>, whose
    /// files share their verbs' names: the CLI's sources sit in module folders too (STRUCT5d, D166).
    /// </summary>
    /// <exception cref="InvalidOperationException">No source has the name, or two do.</exception>
    public static string Cli(string name)
    {
        var folder = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Cli", "src");
        return Single("Daoris.Cli", name, Directory.EnumerateFiles(folder, name, SearchOption.AllDirectories)
            .Where(path => Path.GetRelativePath(folder, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0] != "cli"));
    }

    /// <summary>The one file found by a name, or the refusal that says why the name names none.</summary>
    internal static string Single(string project, string name, IEnumerable<string> found)
    {
        var files = found.ToList();
        return files.Count switch
        {
            1 => files[0],
            0 => throw new InvalidOperationException($"no {name} under {project}: a source read by name must be there, at any depth"),
            _ => throw new InvalidOperationException($"{name} is under {project} {files.Count} times, so its name does not say which: {string.Join(", ", files)}"),
        };
    }
}
