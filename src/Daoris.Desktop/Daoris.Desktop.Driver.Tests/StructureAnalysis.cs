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
    private sealed record TypeDescription(string Name, IReadOnlyList<string> Files);
    private readonly ConcurrentDictionary<INamedTypeSymbol, Lazy<TypeDescription>> _descriptions = new(SymbolEqualityComparer.Default);

    private int _typeDescriptionsRead;
    private int _symbolQueries;

    /// <summary>Materializations of a bound type's declaring files and nested name, counted independently of elapsed time.</summary>
    internal int TypeDescriptionsRead => _typeDescriptionsRead;

    internal int SymbolQueries => _symbolQueries;

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
        analysis.Read(trees, reuse: true);
        return analysis;
    }

    /// <summary>The original repeated work over the very same compilation, retained as an equivalence oracle.</summary>
    internal StructureAnalysis WithoutReuse()
    {
        var analysis = new StructureAnalysis(_compilation);
        analysis.Read(_compilation.SyntaxTrees.Where(tree => tree.FilePath != "<implicit usings>").ToList(), reuse: false);
        return analysis;
    }

    private void Read(List<SyntaxTree> trees, bool reuse)
    {
        // A name can only bind to a type that has its name, or an alias, or an attribute written without its suffix: so only those
        // names are asked of the compiler, which leaves what it binds untouched and the walk to a fraction of every name.
        var named = new HashSet<string>(StringComparer.Ordinal);
        var declarations = new Dictionary<string, int>(StringComparer.Ordinal);
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        var declared = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tree in trees)
        {
            var root = tree.GetRoot();
            foreach (var node in root.DescendantNodes())
            {
                switch (node)
                {
                    case BaseTypeDeclarationSyntax type:
                        Add(named, declarations, type.Identifier.ValueText);
                        if (type.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax) declared.Add(type.Identifier.ValueText);
                        break;
                    case DelegateDeclarationSyntax delegateType:
                        Add(named, declarations, delegateType.Identifier.ValueText);
                        if (delegateType.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax) declared.Add(delegateType.Identifier.ValueText);
                        break;
                    case UsingDirectiveSyntax { Alias: { } alias }:
                        named.Add(alias.Name.Identifier.ValueText);
                        aliases.Add(alias.Name.Identifier.ValueText);
                        break;
                }
            }
        }

        DeclaredTypes = declared;
        // Once a unique, unaliased source spelling has actually bound to a cross-file type, its later occurrences in that
        // file can add no edge. Ambiguous spellings (including attribute shorthand), aliases, failed and self-file bindings
        // still ask Roslyn. No conclusion about a member's spelling is made before a type has bound and its edge is kept.
        var unique = declarations.Where(each => each.Value == 1 && !aliases.Contains(each.Key)).Select(each => each.Key).ToHashSet(StringComparer.Ordinal);
        var edges = new ConcurrentBag<TypeEdge>();
        var rules = new ConcurrentBag<StaticRule>();
        var asyncFiles = new ConcurrentBag<string>();
        var globals = new ConcurrentBag<(string File, int Line, string Name)>();
        Parallel.ForEach(trees, tree =>
        {
            var model = _compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var satisfied = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.DescendantNodes())
            {
                switch (node)
                {
                    case SimpleNameSyntax name when named.Contains(name.Identifier.ValueText) && !(name is IdentifierNameSyntax { IsVar: true })
                        && (!reuse || !satisfied.Contains(name.Identifier.ValueText)):
                        if (Bind(model, tree, name, seen, edges, reuse) && unique.Contains(name.Identifier.ValueText)) satisfied.Add(name.Identifier.ValueText);
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

    private static void Add(HashSet<string> names, Dictionary<string, int> declarations, string name)
    {
        names.Add(name);
        declarations[name] = declarations.GetValueOrDefault(name) + 1;
        if (name.EndsWith("Attribute", StringComparison.Ordinal))
        {
            var shorthand = name[..^"Attribute".Length];
            names.Add(shorthand);
            declarations[shorthand] = declarations.GetValueOrDefault(shorthand) + 1;
        }
    }

    private static int Line(SyntaxTree tree, SyntaxNode node) => tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;

    private static string OwnerOf(SyntaxNode node) =>
        node.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? "";

    /// <summary>Takes the type a name binds to, when it is one a source declares in another file, as an edge.</summary>
    private bool Bind(SemanticModel model, SyntaxTree tree, SimpleNameSyntax name, HashSet<string> seen, ConcurrentBag<TypeEdge> edges, bool reuse)
    {
        Interlocked.Increment(ref _symbolQueries);
        var symbol = model.GetSymbolInfo(name).Symbol;
        // An attribute's name binds to its constructor: the type is its container's.
        if (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor && name.Ancestors().OfType<AttributeSyntax>().Any())
        {
            symbol = constructor.ContainingType;
        }

        if (symbol is not INamedTypeSymbol type) return false;
        var original = type.OriginalDefinition;
        var description = reuse
            ? _descriptions.GetOrAdd(original, static (symbol, analysis) => new(() => analysis.Describe(symbol)), this).Value
            : Describe(original);
        if (description.Files.Count == 0 || description.Files.Contains(tree.FilePath, StringComparer.Ordinal)) return false;
        if (seen.Add(description.Name)) edges.Add(new(tree.FilePath, Line(tree, name), description.Name, description.Files));
        return true;
    }

    private TypeDescription Describe(INamedTypeSymbol original)
    {
        Interlocked.Increment(ref _typeDescriptionsRead);
        var files = original.DeclaringSyntaxReferences.Select(reference => reference.SyntaxTree.FilePath)
            .Where(path => path != "<implicit usings>").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var parts = new Stack<string>();
        for (var at = original; at is not null; at = at.ContainingType) parts.Push(at.Name);
        return new(string.Join('.', parts), files);
    }
}
