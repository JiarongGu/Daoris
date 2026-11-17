using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a semantic census binds under a scope (<see cref="CensusScope"/>, D313): of a project's sources, the scope's own and each that
/// names a type a scope file declares, wherever that file compiles, found from a reference map read from the syntax alone. Binding, not
/// compiling, is a semantic census's cost, and a compilation cannot be kept on disk, so a scoped census binds the trees a change can
/// reach and no other, and a project none of whose sources is reached is not compiled. A source names a type when it spells the type's
/// name, the attribute shorthand of an <c>…Attribute</c> or a global alias of it; a change that reaches a file through no spelling of a
/// type (an extension method called, a deleted file's type spelled elsewhere) is the whole tree's to judge, which the run with no
/// scope does. With no scope every tree is judged.
/// </summary>
internal static class CensusReach
{
    private const string Desktop = "src/Daoris.Desktop/";

    private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<IReadOnlySet<string>, IReadOnlySet<string>>> Compiled = new();

    private static readonly ConcurrentDictionary<(string Project, IReadOnlySet<string> Scope), bool> Projects = new();

    /// <summary>
    /// Of <paramref name="trees"/> (trees of <paramref name="compilation"/>, each named by its path under the desktop tree), those a
    /// census under <paramref name="scope"/> binds: all of them with no scope, else the reached ones, found once for each compilation.
    /// </summary>
    internal static IReadOnlyList<SyntaxTree> Trees(Compilation compilation, IReadOnlyList<SyntaxTree> trees, IReadOnlySet<string>? scope)
    {
        if (scope is null) return trees;
        var reached = Compiled.GetValue(compilation, _ => new(ReferenceEqualityComparer.Instance))
            .GetOrAdd(scope, each => Of([.. DesktopCompilations.Trees(compilation).Select(tree => (tree.FilePath, tree.GetText().ToString()))], each));
        return [.. trees.Where(tree => reached.Contains(tree.FilePath))];
    }

    /// <summary>Whether <paramref name="scope"/> reaches a source of the desktop project <paramref name="project"/>, read from its files without compiling it.</summary>
    internal static bool Reaches(string project, IReadOnlySet<string> scope) => Projects.GetOrAdd((project, scope), each =>
        Of([.. DesktopCompilations.Sources(each.Project).Select(path => (DesktopCompilations.Relative(path), SourceTexts.Of(path)))], each.Scope).Count > 0);

    /// <summary>
    /// The files under the desktop tree a census under <paramref name="scope"/> judges across <paramref name="compilations"/>, so a list's
    /// row for a file is judged only where the file was: every file with no scope, else each reached tree's and each the scope names (a
    /// file the change deleted among them).
    /// </summary>
    internal static Func<string, bool> Judges(IEnumerable<Compilation> compilations, IReadOnlySet<string>? scope)
    {
        if (scope is null) return _ => true;
        var files = compilations.SelectMany(compilation => Trees(compilation, [.. DesktopCompilations.Trees(compilation)], scope)).Select(tree => tree.FilePath)
            .Concat(scope.Where(path => path.StartsWith(Desktop, StringComparison.Ordinal)).Select(path => path[Desktop.Length..]))
            .ToHashSet(StringComparer.Ordinal);
        return files.Contains;
    }

    /// <summary>The census classes of <paramref name="assembly"/> that bind the compilations: each carries a fixture that warms them.</summary>
    internal static IEnumerable<string> Semantic(System.Reflection.Assembly assembly) =>
        assembly.GetTypes().Where(type => type.GetInterfaces().Any(face => face.IsGenericType && face.GetGenericTypeDefinition() == typeof(IClassFixture<>)
                && face.GetGenericArguments()[0] is var fixture && (fixture == typeof(DesktopCompilationsWarm) || fixture == typeof(DriverTreeWarm))))
            .Select(type => type.Name);

    /// <summary>
    /// What the census of censuses finds of the semantic censuses (<paramref name="semantic"/>): one marked whole that does not say why
    /// in <paramref name="why"/>, and a reason that names no semantic census marked whole, or says nothing.
    /// </summary>
    internal static IReadOnlyList<string> Unexplained(IEnumerable<string> semantic, IReadOnlyCollection<string> whole, IReadOnlyDictionary<string, string> why)
    {
        var found = semantic.ToHashSet(StringComparer.Ordinal);
        return
        [
            .. found.Where(name => whole.Contains(name) && !why.ContainsKey(name)).Order(StringComparer.Ordinal)
                .Select(name => $"{name} binds the compilations and is marked whole: read the scope through CensusReach, or say in CensusMarks.SemanticWhole why its rule compares across the tree"),
            .. why.Keys.Where(name => !found.Contains(name) || !whole.Contains(name)).Order(StringComparer.Ordinal)
                .Select(name => $"{name} says why it stays whole and is no semantic census marked whole"),
            .. why.Where(each => string.IsNullOrWhiteSpace(each.Value)).Select(each => each.Key).Order(StringComparer.Ordinal)
                .Select(name => $"{name} stays whole and says no reason"),
        ];
    }

    /// <summary>
    /// The paths of <paramref name="sources"/> (each a path under the desktop tree and its text) the scope reaches: each the scope holds,
    /// and each that names a type a scope file declares, the file read from <paramref name="sources"/> or else from the desktop tree.
    /// </summary>
    internal static IReadOnlySet<string> Of(IReadOnlyList<(string Path, string Text)> sources, IReadOnlySet<string> scope)
    {
        var facts = ReferenceMap.Of([.. sources.Select(source => source.Text)]);
        var held = sources.Select((source, at) => (source.Path, at)).ToDictionary(each => each.Path, each => each.at, StringComparer.Ordinal);
        var changed = scope.Where(path => path.StartsWith(Desktop, StringComparison.Ordinal) && path.EndsWith(".cs", StringComparison.Ordinal))
            .Select(path => path[Desktop.Length..]).ToList();
        var elsewhere = changed.Where(path => !held.ContainsKey(path)).Select(path => Path.Combine(DesktopCompilations.Desktop, path)).Where(File.Exists).Select(SourceTexts.Of).ToList();
        var names = changed.Where(held.ContainsKey).Select(path => facts[held[path]]).Concat(ReferenceMap.Of(elsewhere))
            .SelectMany(each => each.Declares).ToHashSet(StringComparer.Ordinal);
        // A global alias of a reached type is spelled in place of its name in any file, so it is a name of the type too.
        for (var grown = true; grown;)
        {
            grown = false;
            foreach (var (alias, target) in facts.SelectMany(each => each.Aliases))
                grown |= target.Any(names.Contains) && names.Add(alias);
        }

        return sources.Where((source, at) => scope.Contains(Desktop + source.Path) || facts[at].Spells.Overlaps(names))
            .Select(source => source.Path).ToHashSet(StringComparer.Ordinal);
    }
}

/// <summary>What one tree's syntax says of names: the types it declares (an attribute's shorthand too), each name it spells in code, and its global aliases.</summary>
internal sealed record SpelledNames(IReadOnlySet<string> Declares, IReadOnlySet<string> Spells, IReadOnlyList<(string Alias, IReadOnlyList<string> Target)> Aliases)
{
    /// <summary>The names <paramref name="root"/> declares, spells and aliases.</summary>
    internal static SpelledNames Read(SyntaxNode root)
    {
        var declares = new HashSet<string>(StringComparer.Ordinal);
        var aliases = new List<(string, IReadOnlyList<string>)>();
        foreach (var node in root.DescendantNodes(node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax))
        {
            switch (node)
            {
                case BaseTypeDeclarationSyntax type:
                    Declare(declares, type.Identifier.ValueText);
                    break;
                case DelegateDeclarationSyntax type:
                    Declare(declares, type.Identifier.ValueText);
                    break;
                case UsingDirectiveSyntax { GlobalKeyword.RawKind: not 0, Alias: { } alias } global:
                    aliases.Add((alias.Name.Identifier.ValueText, [.. Identifiers(global.NamespaceOrType)]));
                    break;
            }
        }

        return new(declares, Identifiers(root).ToHashSet(StringComparer.Ordinal), aliases);
    }

    private static void Declare(HashSet<string> declares, string name)
    {
        declares.Add(name);
        if (name.EndsWith("Attribute", StringComparison.Ordinal) && name.Length > "Attribute".Length) declares.Add(name[..^"Attribute".Length]);
    }

    private static IEnumerable<string> Identifiers(SyntaxNode node) =>
        node.DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken)).Select(token => token.ValueText);

    /// <summary>The line the map keeps for these names under <paramref name="key"/>: tab-separated, each list space-separated, an alias as <c>alias=target,…</c>.</summary>
    internal string Line(string key) =>
        string.Join('\t', key, string.Join(' ', Declares.Order(StringComparer.Ordinal)), string.Join(' ', Spells.Order(StringComparer.Ordinal)),
            string.Join(' ', Aliases.Select(each => $"{each.Alias}={string.Join(',', each.Target)}")));

    /// <summary>The key and names a map's line holds, or null for a line of another shape.</summary>
    internal static (string Key, SpelledNames Names)? Parse(string line)
    {
        var parts = line.Split('\t');
        if (parts.Length != 4) return null;
        static string[] Words(string part) => part.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return (parts[0], new(Words(parts[1]).ToHashSet(StringComparer.Ordinal), Words(parts[2]).ToHashSet(StringComparer.Ordinal),
            [.. Words(parts[3]).Select(each => each.Split('=', 2)).Where(each => each.Length == 2).Select(each => (each[0], (IReadOnlyList<string>)each[1].Split(',')))]));
    }
}

/// <summary>
/// The reference map: each tree's <see cref="SpelledNames"/>, read from its syntax and kept under <c>_fixtures/census-reach/</c> by the hash
/// of its text and the map's format, so a run reads again only the trees whose text changed. The file holds the trees the last run
/// read, written whole to a scratch name and moved over the old, so a run beside it reads one or the other.
/// </summary>
internal static class ReferenceMap
{
    /// <summary>The map's format, part of every key: a change to what a line holds makes every key new.</summary>
    internal const string Format = "census-reach/1";

    /// <summary>The map's file in the workspace's scratch.</summary>
    internal static string Stored => Path.Combine(WorkspaceRoot.Folder, "_fixtures", "census-reach", "names.tsv");

    private static readonly Lazy<ConcurrentDictionary<string, SpelledNames>> Kept = new(() => Load(Stored));

    private static readonly ConcurrentDictionary<string, byte> Used = new(StringComparer.Ordinal);

    private static readonly object Writing = new();

    private static int s_read;

    /// <summary>How many trees this run read from their syntax, not from the map.</summary>
    internal static int Read => s_read;

    /// <summary>The key of a tree whose text is <paramref name="text"/>: the hash of the format and the text.</summary>
    internal static string Key(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Format + "\n" + text)))[..32];

    private static readonly CSharpParseOptions Parse = new(LanguageVersion.Latest);

    /// <summary>Each source's names, in order, from the map where its key is there, else parsed from its text, the map written again when any was parsed.</summary>
    internal static IReadOnlyList<SpelledNames> Of(IReadOnlyList<string> texts)
    {
        var kept = Kept.Value;
        var before = s_read;
        SpelledNames[] names = [.. texts.AsParallel().AsOrdered().Select(text =>
        {
            var key = Key(text);
            Used.TryAdd(key, 0);
            return kept.GetOrAdd(key, _ =>
            {
                Interlocked.Increment(ref s_read);
                return SpelledNames.Read(CSharpSyntaxTree.ParseText(text, Parse).GetRoot());
            });
        })];
        if (s_read != before) Save(Stored, kept.Where(each => Used.ContainsKey(each.Key)));
        return names;
    }

    /// <summary>The map <paramref name="file"/> holds; empty when there is none.</summary>
    internal static ConcurrentDictionary<string, SpelledNames> Load(string file)
    {
        var map = new ConcurrentDictionary<string, SpelledNames>(StringComparer.Ordinal);
        if (!File.Exists(file)) return map;
        foreach (var line in File.ReadLines(file))
        {
            if (SpelledNames.Parse(line) is { } entry) map[entry.Key] = entry.Names;
        }

        return map;
    }

    /// <summary>Writes <paramref name="entries"/> to <paramref name="file"/> whole, through a scratch name moved over the old.</summary>
    internal static void Save(string file, IEnumerable<KeyValuePair<string, SpelledNames>> entries)
    {
        lock (Writing)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var scratch = $"{file}.{Guid.NewGuid():N}";
            File.WriteAllLines(scratch, entries.OrderBy(each => each.Key, StringComparer.Ordinal).Select(each => each.Value.Line(each.Key)));
            File.Move(scratch, file, overwrite: true);
        }
    }
}
