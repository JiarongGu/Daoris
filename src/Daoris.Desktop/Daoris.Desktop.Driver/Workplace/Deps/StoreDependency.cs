namespace Daoris.Driver;

/// <summary>
/// A dependency the store acts on: an effective <c>dependency</c> rule's path that names an install and a key, its folder, the folder
/// its install runs in (the manifest's, beside its path) and the rule, filled from its signature.
/// </summary>
internal sealed record StoreDependency(string Path, FileRule Rule)
{
    /// <summary>Its folder, repository-relative, <c>/</c>-separated.</summary>
    internal string Folder => Path.TrimEnd('/');

    /// <summary>The folder its install runs in, repository-relative: empty for the top.</summary>
    internal string Runs => Folder.LastIndexOf('/') is var slash and > 0 ? Folder[..slash] : "";

    internal IReadOnlyList<string> Caches => Rule.Caches ?? [];
}

/// <summary>The dependencies of a tree in rule order, and the rules they were read from.</summary>
internal sealed record StoreDependencies(IReadOnlyList<StoreDependency> Each, FileRules Rules, IReadOnlyList<string> Problems, FileFloorFacts Floor);

/// <summary>
/// The effective <c>dependency</c> rules of a tree at its commit (row 6a's layers: the found one, the repository's at the commit, the
/// person's, the floor over all), in rule order: each path a rule names or git lists ignored, whose effective rule is a dependency that
/// acts, ordered by where its deciding rule stands in the layers.
/// </summary>
internal static class StoreDependencyRules
{
    internal static async Task<StoreDependencies> ReadAsync(string home, string tree, string repository, string commit, CancellationToken ct)
    {
        var reading = await FileAnalysis.ReadAsync(tree, commit, FileSignatures.Shipped, [], ct).ConfigureAwait(false);
        var found = FoundFiles.Read(home, repository);
        var own = await RepositoryFilesReader.AtAsync(tree, commit, ct).ConfigureAwait(false);
        var person = PersonFiles.Read(home);
        var rules = FileRules.Of(found.Reading?.Rules ?? reading.Found.Rules, own.Rules, person, repository, reading.Signature);
        var paths = reading.Tops.Concat(rules.Rules.SelectMany(each => each.Rule.Paths).Where(path => path.IndexOfAny(['*', '?', '{', '[']) < 0))
            .Where(path => path.EndsWith('/'))
            .Distinct(StringComparer.Ordinal);
        var order = rules.Rules.Select(each => each.Rule).ToList();
        var each = rules.Over(paths, reading.Floor)
            .Where(file => file is { Kind: FileKind.Dependency, Idle: null, Floor: null, Rule: not null })
            .Select(file => (Order: order.FindLastIndex(rule => rule.Matches(file.Path) && rule.Kind == FileKind.Dependency), Dependency: new StoreDependency(file.Path, file.Rule!)))
            .OrderBy(pair => pair.Order).ThenBy(pair => pair.Dependency.Path, StringComparer.Ordinal)
            .Select(pair => pair.Dependency)
            .ToList();
        return new StoreDependencies(each, rules, [.. found.Problems, .. own.Problems, .. person.Problems], reading.Floor);
    }
}
