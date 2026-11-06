using System.Diagnostics;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The one Roslyn analysis of the driver's own tree, shared by every structure case over it: the compile is the gate's cost, and two
/// classes that each made their own paid it twice. The <c>Layer/</c> folders name Lyntai's assembly, which the analysis's references
/// do not carry and a build without Lyntai's local build does not compile, so they are left out.
/// </summary>
internal static class DriverTree
{
    public static readonly Lazy<(StructureMap Map, IReadOnlyList<(string Path, string Text)> Sources, IReadOnlyList<string> Tests, StructureAnalysis Analysis, TimeSpan Took)> Shared = new(() =>
    {
        var map = StructureMap.Read(File.ReadAllText(Path.Combine(
            WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "structure.json")));
        var driver = SourceFiles.Project(SourceFiles.DriverProject);
        var sources = SourceFiles.All(SourceFiles.DriverProject, "*.cs")
            .Select(path => (Path.GetRelativePath(driver, path).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllText(path)))
            .Where(source => !("/" + source.Item1).Contains("/Layer/", StringComparison.Ordinal))
            .ToList();
        var tests = SourceFiles.All(SourceFiles.DriverTestsProject, "*.cs")
            .Select(path => Path.GetRelativePath(SourceFiles.Project(SourceFiles.DriverTestsProject), path).Replace(Path.DirectorySeparatorChar, '/'))
            .ToList();
        var clock = Stopwatch.StartNew();
        var analysis = StructureAnalysis.Of(sources);
        return (map, sources, tests, analysis, clock.Elapsed);
    });
}
