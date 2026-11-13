namespace Daoris.Desktop.Driver.Tests;

/// <summary>The driver's host as the desktop's build lays it beside a test project's own build.</summary>
internal static class HostBuild
{
    /// <summary>The host's build beside this test's own; <c>DriverStartFailureTests</c> starts it too.</summary>
    internal static string Dll()
    {
        // This test's build is <tests>/bin/<configuration>/<framework>/; the host's sits the same way.
        var build = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var framework = build.Name;
        var configuration = build.Parent!.Name;
        var desktop = build.Parent.Parent!.Parent!.Parent!.FullName;
        var dll = Path.Combine(desktop, "Daoris.Desktop.Driver.Host", "bin", configuration, framework, "daoris-driver.dll");
        return File.Exists(dll) ? dll : throw new FileNotFoundException("the host is built before these tests; it was not found", dll);
    }
}
