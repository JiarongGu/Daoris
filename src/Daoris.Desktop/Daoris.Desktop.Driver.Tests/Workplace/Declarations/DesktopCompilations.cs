using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The desktop projects that ship, each one compilation of its own sources and the driver's files it links, for the declarations'
/// source censuses. Each binds against the base library and every assembly the driver's tests carry, the driver's own build among them
/// for a project that is not the driver; a type of a package only the modules or the application take does not bind, and a census
/// that needs a name to bind says where one does not. A file is named by its path under the desktop tree, so a linked file is the
/// driver's wherever it compiles.
/// </summary>
internal static class DesktopCompilations
{
    /// <summary>Every desktop project that ships, each checked by the boundary.</summary>
    internal static readonly ImmutableArray<string> Shipping =
    [
        "Daoris.Desktop.Driver", "Daoris.Desktop.Modules", "Daoris.Desktop.Driver.Host", "Daoris.Desktop.App",
        "Daoris.Desktop.Launcher", "Daoris.Desktop.GitDoor", "Daoris.Desktop.PieceStart", "Daoris.Desktop.OwnerStandIn",
    ];

    /// <summary>The desktop tree's folder.</summary>
    internal static readonly string Desktop = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop");

    private static readonly CSharpParseOptions Parse = new(LanguageVersion.Latest);

    private static readonly SyntaxTree ImplicitUsings = CSharpSyntaxTree.ParseText(
        string.Join('\n', new[] { "System", "System.Collections.Generic", "System.IO", "System.Linq", "System.Net.Http", "System.Threading", "System.Threading.Tasks", "Daoris.Ports.Browser", "Daoris.Ports.Clocks", "Daoris.Ports.Store", "System.Data.Common" }
            .Select(each => $"global using global::{each};")), Parse, "<implicit usings>");

    private static readonly Lazy<ImmutableArray<CSharpCompilation>> Built = new(() => [.. Shipping.AsParallel().AsOrdered().Select(Of)]);

    // What the tests' own build carries beside the base library: the driver, the contracts and the packages they bring.
    private static readonly Lazy<ImmutableArray<(string Name, MetadataReference Reference)>> Carried = new(() =>
    {
        var named = StructureAnalysis.BaseLibrary.Value.OfType<PortableExecutableReference>()
            .Select(each => Path.GetFileName(each.FilePath)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var self = Path.GetFileName(typeof(DesktopCompilations).Assembly.Location);
        return [.. Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll")
            .Where(path => !named.Contains(Path.GetFileName(path)) && !string.Equals(Path.GetFileName(path), self, StringComparison.OrdinalIgnoreCase) && Managed(path))
            .Select(path => (Path.GetFileNameWithoutExtension(path), (MetadataReference)MetadataReference.CreateFromFile(path)))];
    });

    /// <summary>Each shipping project's compilation, made once a run; the driver's is the one the structure classes read too.</summary>
    internal static ImmutableArray<CSharpCompilation> Each => Built.Value;

    /// <summary>A path's form in every census: relative to the desktop tree, <c>/</c> between its parts.</summary>
    internal static string Relative(string path) => Path.GetRelativePath(Desktop, path).Replace('\\', '/');

    /// <summary>The sources <paramref name="project"/> compiles: its own, its build output aside, and each file its project links.</summary>
    internal static IReadOnlyList<string> Sources(string project)
    {
        var folder = SourceFiles.Project(project);
        var linked = XDocument.Load(Path.Combine(folder, project + ".csproj")).Descendants("Compile")
            .Select(item => (string?)item.Attribute("Include")).OfType<string>()
            .Select(include => Path.GetFullPath(Path.Combine(folder, include.Replace('\\', '/'))));
        return [.. SourceFiles.All(project, "*.cs").Concat(linked).Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static readonly ConcurrentDictionary<string, Lazy<CSharpCompilation>> Compiled = new(StringComparer.Ordinal);

    private static readonly ConcurrentDictionary<string, int> Made = new(StringComparer.Ordinal);

    /// <summary>How many compilations of <paramref name="project"/>'s sources this run has made.</summary>
    internal static int Compiles(string project) => Made.GetValueOrDefault(project);

    /// <summary>The one compilation of <paramref name="project"/>'s sources a run, made at its first ask and read by every census after.</summary>
    internal static CSharpCompilation Of(string project) => Compiled.GetOrAdd(project, named => new(() =>
    {
        Made.AddOrUpdate(named, 1, (_, made) => made + 1);
        return Compile(named, [.. Sources(named).AsParallel().AsOrdered().Select(path => (Relative(path), SourceTexts.Of(path)))], named != "Daoris.Desktop.Driver");
    })).Value;

    /// <summary>A compilation of <paramref name="sources"/> (each a path under the desktop tree and its text), named as an assembly.</summary>
    internal static CSharpCompilation Compile(string assembly, IEnumerable<(string Path, string Text)> sources, bool driverBuilt)
    {
        var references = StructureAnalysis.BaseLibrary.Value.Concat(Carried.Value
            .Where(each => driverBuilt || each.Name != "Daoris.Desktop.Driver").Select(each => each.Reference));
        return CSharpCompilation.Create(assembly,
            [ImplicitUsings, .. sources.AsParallel().AsOrdered().Select(source => CSharpSyntaxTree.ParseText(source.Text, Parse, source.Path))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable, allowUnsafe: true));
    }

    /// <summary>The source trees of <paramref name="compilation"/>, the implicit usings aside.</summary>
    internal static IEnumerable<SyntaxTree> Trees(Compilation compilation) =>
        compilation.SyntaxTrees.Where(tree => tree.FilePath != "<implicit usings>");

    /// <summary>The one-based line <paramref name="node"/> starts on.</summary>
    internal static int Line(SyntaxNode node) => node.SyntaxTree.GetLineSpan(node.Span).StartLinePosition.Line + 1;

    private static bool Managed(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var image = new PEReader(stream);
            return image.HasMetadata && image.GetMetadataReader().IsAssembly;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }
}
