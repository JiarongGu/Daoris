using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The one Roslyn analysis of the driver's own tree, shared by every structure case, over the compilation the declarations' censuses
/// read too (<see cref="DesktopCompilations.Of"/>): two classes that each compiled paid the gate's cost twice. The <c>Layer/</c>
/// folders need Lyntai's local build, so the analysis walks none of them and names none of their types.
/// </summary>
internal static class DriverTree
{
    private static readonly string Folder = SourceFiles.DriverProject + "/";

    /// <summary>The compilation the analysis reads, and the trees of it the analysis walks: made, not yet bound.</summary>
    public static readonly Lazy<(CSharpCompilation Compilation, IReadOnlyList<SyntaxTree> Walked)> Compiled = new(() =>
    {
        var compilation = DesktopCompilations.Of(SourceFiles.DriverProject);
        return (compilation, [.. DesktopCompilations.Trees(compilation)
            .Where(tree => tree.FilePath.StartsWith(Folder, StringComparison.Ordinal)
                && !("/" + tree.FilePath[Folder.Length..]).Contains("/Layer/", StringComparison.Ordinal))]);
    });

    public static readonly Lazy<(StructureMap Map, IReadOnlyList<(string Path, string Text)> Sources, IReadOnlyList<string> Tests, StructureAnalysis Analysis, TimeSpan Took)> Shared = new(() =>
    {
        var map = StructureMap.Read(File.ReadAllText(Path.Combine(
            WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "structure.json")));
        var tests = SourceFiles.All(SourceFiles.DriverTestsProject, "*.cs")
            .Select(path => Path.GetRelativePath(SourceFiles.Project(SourceFiles.DriverTestsProject), path).Replace(Path.DirectorySeparatorChar, '/'))
            .ToList();
        var clock = Stopwatch.StartNew();
        var (compilation, walked) = Compiled.Value;
        IReadOnlyList<(string Path, string Text)> sources = [.. walked.Select(tree => (tree.FilePath[Folder.Length..], tree.GetText().ToString()))];
        var analysis = StructureAnalysis.Over(compilation, walked, Folder);
        return (map, sources, tests, analysis, clock.Elapsed);
    });
}
