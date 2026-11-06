using System.Globalization;
using System.Reflection;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The application a test starts as Daoris's browser: the one this project's reference to Daoris.Desktop.App builds,
/// in this test's own configuration, beside its engine. Never the newest of every build in the application's folder: a
/// release layout a rehearsal left there, from before the codecs engine, was the one started, on the standard CEF.
/// </summary>
internal static class BuiltApplication
{
    /// <summary>The executable of this configuration's build of the application.</summary>
    public static string Executable()
    {
        var configuration = typeof(BuiltApplication).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "Debug";
        // The project's folder is tracked; its build output is no file of this repository, so it is joined from the folder.
        var project = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.App");
        var output = Path.Combine(project, "bin", configuration);
        var built = Directory.Exists(output)
            ? Directory.EnumerateFiles(output, "Daoris.Desktop.exe", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
            : null;
        return built ?? throw new FileNotFoundException(
            $"no {configuration} build of Daoris.Desktop.App under {output}: this project references it, so build this project");
    }

    /// <summary>Which engine the executable lays beside it, by its library's size: the standard CEF's and the codecs package's differ.</summary>
    public static string Engine(string exe)
    {
        var library = new FileInfo(Path.Combine(Path.GetDirectoryName(exe)!, "libcef.dll"));
        return library.Exists
            ? $"the engine is {exe}, beside libcef.dll of {library.Length.ToString(CultureInfo.InvariantCulture)} bytes written {library.LastWriteTimeUtc:u}"
            : $"the engine is {exe}, with no libcef.dll beside it";
    }

    public static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }
}
