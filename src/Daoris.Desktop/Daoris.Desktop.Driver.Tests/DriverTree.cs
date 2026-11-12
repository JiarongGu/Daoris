using System.Diagnostics;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The one Roslyn analysis of the driver's own tree, shared by every structure case over it, over the one compilation of the driver's
/// sources the declarations' censuses read too (<see cref="DesktopCompilations.Of"/>): the compile is the gate's cost, and two classes
/// that each made their own paid it twice. The <c>Layer/</c> folders name Lyntai's assembly, which a build without Lyntai's local
/// build does not compile, so the analysis walks none of them and names none of their types.
/// </summary>
internal static class DriverTree
{
    public static readonly Lazy<(StructureMap Map, IReadOnlyList<(string Path, string Text)> Sources, IReadOnlyList<string> Tests, StructureAnalysis Analysis, TimeSpan Took)> Shared = new(() =>
    {
        var map = StructureMap.Read(File.ReadAllText(Path.Combine(
            WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "structure.json")));
        var tests = SourceFiles.All(SourceFiles.DriverTestsProject, "*.cs")
            .Select(path => Path.GetRelativePath(SourceFiles.Project(SourceFiles.DriverTestsProject), path).Replace(Path.DirectorySeparatorChar, '/'))
            .ToList();
        var clock = Stopwatch.StartNew();
        var compilation = DesktopCompilations.Of(SourceFiles.DriverProject);
        var folder = SourceFiles.DriverProject + "/";
        var walked = DesktopCompilations.Trees(compilation)
            .Where(tree => tree.FilePath.StartsWith(folder, StringComparison.Ordinal)
                && !("/" + tree.FilePath[folder.Length..]).Contains("/Layer/", StringComparison.Ordinal))
            .ToList();
        IReadOnlyList<(string Path, string Text)> sources = [.. walked.Select(tree => (tree.FilePath[folder.Length..], tree.GetText().ToString()))];
        var analysis = StructureAnalysis.Over(compilation, walked, folder);
        return (map, sources, tests, analysis, clock.Elapsed);
    });
}
