using System.Collections.Concurrent;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>One name in a file that binds to a type some driver file declares: where, which type, and the files that declare it.</summary>
internal sealed record TypeEdge(string File, int Line, string Type, IReadOnlyList<string> DeclaredIn);

/// <summary>A static method of a driver file that answers a <c>bool</c>: a rule, by the shape the gate reads in Loop (§14.5 rule 4).</summary>
internal sealed record StaticRule(string File, int Line, string Member);

/// <summary>
/// What the structure gate reads of the driver's sources through Roslyn's semantic model (FOUNDATION1 mod-0, §14.5): one compilation of
/// every source over the base library and the store's SQLite package, which are all the driver references, and, in it, each name that binds to a type a source declares
/// as an edge from the file that names it to the file that declares the type. The binding is the compiler's, so every spelling of a
/// reference is one (plain, namespace-qualified, <c>global::</c>-qualified, through an alias or a static import), a member's occurrence is
/// the member's and no type's whatever it is called, and a <c>record struct</c> is a struct. A comment, a string and a <c>cref</c> are no
/// code and name nothing. The compile is held in tests alone: the driver, the GitDoor and everything that ships take no package for it.
/// </summary>
internal sealed class StructureAnalysis
{
    private static readonly string[] ImplicitUsings =
    [
        "System", "System.Collections.Generic", "System.IO", "System.Linq", "System.Net.Http", "System.Threading", "System.Threading.Tasks",
    ];

    /// <summary>The base library the driver compiles against: the managed assemblies of the runtime this test host runs on.</summary>
    private static readonly Lazy<IReadOnlyList<MetadataReference>> BaseLibrary = new(() =>
    {
        var references = new List<MetadataReference>();
        foreach (var path in Directory.EnumerateFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"))
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var image = new PEReader(stream);
                if (!image.HasMetadata || !image.GetMetadataReader().IsAssembly) continue;
            }
            catch (BadImageFormatException)
            {
                continue;
            }

            references.Add(MetadataReference.CreateFromFile(path));
        }

        // The driver's one package, the desktop store's (FOUNDATION1 z-i): the SQLite library `DesktopStore` and its reader name.
        references.Add(MetadataReference.CreateFromFile(typeof(Microsoft.Data.Sqlite.SqliteConnection).Assembly.Location));
        return references;
    });

    private readonly CSharpCompilation _compilation;

    private StructureAnalysis(CSharpCompilation compilation)
    {
        _compilation = compilation;
    }

    /// <summary>Every name bound to a type another file declares, once per file and type, in order of file and line.</summary>
    public IReadOnlyList<TypeEdge> Edges { get; private set; } = [];

    /// <summary>Every static <c>bool</c> method, <c>Type.Member</c>, by file.</summary>
    public IReadOnlyList<StaticRule> Rules { get; private set; } = [];

    /// <summary>The files that hold an <c>async</c> keyword in code.</summary>
    public IReadOnlySet<string> AsyncFiles { get; private set; } = new HashSet<string>();

    /// <summary>The <c>global using</c> directives: the file, its line and the namespace or type each names.</summary>
    public IReadOnlyList<(string File, int Line, string Name)> GlobalUsings { get; private set; } = [];

    /// <summary>Each top-level type a source declares, by name.</summary>
    public IReadOnlySet<string> DeclaredTypes { get; private set; } = new HashSet<string>();

    /// <summary>The compiler's errors over the sources, by id, with a count and one example each: what the binding could not resolve.</summary>
    public IReadOnlyDictionary<string, (int Count, string Example)> Errors()
    {
        var found = new SortedDictionary<string, (int Count, string Example)>(StringComparer.Ordinal);
        foreach (var diagnostic in _compilation.GetDiagnostics().Where(each => each.Severity == DiagnosticSeverity.Error))
        {
            found.TryGetValue(diagnostic.Id, out var seen);
            found[diagnostic.Id] = (seen.Count + 1, seen.Count == 0 ? diagnostic.ToString() : seen.Example);
        }

        return found;
    }

    /// <summary>The analysis of sources, as (path, text) pairs, each path relative to the driver's folder with <c>/</c> between its parts.</summary>
    public static StructureAnalysis Of(IReadOnlyList<(string Path, string Text)> sources)
    {
        var options = new CSharpParseOptions(LanguageVersion.Latest);
        var implicitUsings = CSharpSyntaxTree.ParseText(
            string.Join('\n', ImplicitUsings.Select(each => $"global using global::{each};")), options, "<implicit usings>");
        var trees = sources.Select(source => CSharpSyntaxTree.ParseText(source.Text, options, source.Path)).ToList();
        var compilation = CSharpCompilation.Create(
            "Daoris.Desktop.Driver",
            [implicitUsings, .. trees],
            BaseLibrary.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable, allowUnsafe: true));
        var analysis = new StructureAnalysis(compilation);
        analysis.Read(trees);
        return analysis;
    }

    private void Read(List<SyntaxTree> trees)
    {
        // A name can only bind to a type that has its name, or an alias, or an attribute written without its suffix: so only those
        // names are asked of the compiler, which leaves what it binds untouched and the walk to a fraction of every name.
        var named = new HashSet<string>(StringComparer.Ordinal);
        var declared = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tree in trees)
        {
            var root = tree.GetRoot();
            foreach (var node in root.DescendantNodes())
            {
                switch (node)
                {
                    case BaseTypeDeclarationSyntax type:
                        Add(named, type.Identifier.ValueText);
                        if (type.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax) declared.Add(type.Identifier.ValueText);
                        break;
                    case DelegateDeclarationSyntax delegateType:
                        Add(named, delegateType.Identifier.ValueText);
                        if (delegateType.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax) declared.Add(delegateType.Identifier.ValueText);
                        break;
                    case UsingDirectiveSyntax { Alias: { } alias }:
                        named.Add(alias.Name.Identifier.ValueText);
                        break;
                }
            }
        }

        DeclaredTypes = declared;
        var edges = new ConcurrentBag<TypeEdge>();
        var rules = new ConcurrentBag<StaticRule>();
        var asyncFiles = new ConcurrentBag<string>();
        var globals = new ConcurrentBag<(string File, int Line, string Name)>();
        Parallel.ForEach(trees, tree =>
        {
            var model = _compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.DescendantNodes())
            {
                switch (node)
                {
                    case SimpleNameSyntax name when named.Contains(name.Identifier.ValueText) && !(name is IdentifierNameSyntax { IsVar: true }):
                        Bind(model, tree, name, seen, edges);
                        break;
                    case MethodDeclarationSyntax method when method.Modifiers.Any(SyntaxKind.StaticKeyword)
                        && !method.Modifiers.Any(SyntaxKind.AsyncKeyword)
                        && method.ReturnType.ToString() is "bool" or "bool?":
                        rules.Add(new(tree.FilePath, Line(tree, method), $"{OwnerOf(method)}.{method.Identifier.ValueText}"));
                        break;
                    case UsingDirectiveSyntax { GlobalKeyword.RawKind: not 0 } global:
                        globals.Add((tree.FilePath, Line(tree, global), global.NamespaceOrType.ToString().Replace("global::", "", StringComparison.Ordinal)));
                        break;
                }
            }

            if (root.DescendantTokens().Any(token => token.IsKind(SyntaxKind.AsyncKeyword))) asyncFiles.Add(tree.FilePath);
        });

        Edges = edges.OrderBy(edge => edge.File, StringComparer.Ordinal).ThenBy(edge => edge.Line).ThenBy(edge => edge.Type, StringComparer.Ordinal).ToList();
        Rules = rules.OrderBy(rule => rule.File, StringComparer.Ordinal).ThenBy(rule => rule.Line).ToList();
        AsyncFiles = asyncFiles.ToHashSet(StringComparer.Ordinal);
        GlobalUsings = globals.OrderBy(use => use.File, StringComparer.Ordinal).ThenBy(use => use.Line).ToList();
    }

    private static void Add(HashSet<string> names, string name)
    {
        names.Add(name);
        if (name.EndsWith("Attribute", StringComparison.Ordinal)) names.Add(name[..^"Attribute".Length]);
    }

    private static int Line(SyntaxTree tree, SyntaxNode node) => tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;

    private static string OwnerOf(SyntaxNode node) =>
        node.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? "";

    /// <summary>Takes the type a name binds to, when it is one a source declares in another file, as an edge.</summary>
    private static void Bind(SemanticModel model, SyntaxTree tree, SimpleNameSyntax name, HashSet<string> seen, ConcurrentBag<TypeEdge> edges)
    {
        var symbol = model.GetSymbolInfo(name).Symbol;
        // An attribute's name binds to its constructor: the type is its container's.
        if (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor && name.Ancestors().OfType<AttributeSyntax>().Any())
        {
            symbol = constructor.ContainingType;
        }

        if (symbol is not INamedTypeSymbol type) return;
        var original = type.OriginalDefinition;
        var files = original.DeclaringSyntaxReferences.Select(reference => reference.SyntaxTree.FilePath)
            .Where(path => path != "<implicit usings>").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (files.Count == 0 || files.Contains(tree.FilePath, StringComparer.Ordinal)) return;
        var parts = new Stack<string>();
        for (var at = original; at is not null; at = at.ContainingType) parts.Push(at.Name);
        var spelled = string.Join('.', parts);
        if (seen.Add(spelled)) edges.Add(new(tree.FilePath, Line(tree, name), spelled, files));
    }
}
