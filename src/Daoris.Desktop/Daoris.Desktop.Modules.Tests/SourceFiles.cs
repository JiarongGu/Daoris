namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A desktop project's sources, found by name wherever they lie under the project's folder (STRUCT5a, D166; the code-structure
/// design §6): the modules library's files sit in module folders, so a test that reads a source names the file and never
/// the folder it is in, and a test that walks the project walks every folder of it. The build's own output (<c>bin/</c>,
/// <c>obj/</c>) is no source. A copy of the driver tests' reader: the two test projects share no code.
/// </summary>
internal static class SourceFiles
{
    /// <summary>The modules library's project folder's name.</summary>
    public const string ModulesProject = "Daoris.Desktop.Modules";

    /// <summary>The driver library's project folder's name.</summary>
    public const string DriverProject = "Daoris.Desktop.Driver";

    /// <summary>The modules' tests' project folder's name.</summary>
    public const string ModulesTestsProject = "Daoris.Desktop.Modules.Tests";

    private static readonly string Desktop = Path.Combine(WorkspaceFolder(), "src", "Daoris.Desktop");

    /// <summary>A desktop project's folder, by the project's name: <c>src/Daoris.Desktop/&lt;project&gt;</c>.</summary>
    public static string Project(string project) => Path.Combine(Desktop, project);

    /// <summary>The one modules source of this name, at any depth of the modules' folder.</summary>
    /// <exception cref="InvalidOperationException">No source has the name, or two do.</exception>
    public static string Module(string name) => Find(ModulesProject, name);

    /// <summary>The one driver source of this name, at any depth of the driver's folder (FOUNDATION1 mod-0, D166 point 4).</summary>
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

    /// <summary>The workspace's root: the folder above the test binary that holds <c>daoris.json</c>.</summary>
    public static string WorkspaceFolder()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }
}
