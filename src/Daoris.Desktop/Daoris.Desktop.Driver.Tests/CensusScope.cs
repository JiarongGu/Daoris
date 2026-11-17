namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a census judges. At a merge the merge tool writes the merge's changed paths to a file, one repository-relative path a line,
/// and names it in <c>CENSUS_SCOPE</c> (<c>tools/merge/census-scope.ts</c>): only a changed file can newly break a rule that holds of
/// each file alone, so a census marked diff-able (<see cref="Judging.Diff"/>) judges only the files of that scope. A census marked
/// whole (<see cref="Judging.Whole"/>) counts or compares across files (a table held both ways, a graph of the modules, a ratchet), or
/// judges no file at all, and judges the whole tree whatever the scope. With no scope (the CI after the landing, a developer's run)
/// every census judges the whole tree. Each test project marks its censuses in its own <c>CensusMarks</c>, and its census of censuses
/// holds that each census is marked once and each diff-able one reads through here. Linked into the modules' tests.
/// </summary>
internal static class CensusScope
{
    /// <summary>The variable that names the scope's file.</summary>
    internal const string Variable = "CENSUS_SCOPE";

    private static readonly Lazy<IReadOnlySet<string>?> Read = new(() => Load(Environment.GetEnvironmentVariable(Variable)));

    /// <summary>The run's scope, repository-relative paths with <c>/</c>; null when the run judges the whole tree.</summary>
    internal static IReadOnlySet<string>? Paths => Read.Value;

    /// <summary>The scope a file names, one path a line; none when no file is named. A named file that is missing is an error, never the whole tree.</summary>
    internal static IReadOnlySet<string>? Load(string? file) =>
        string.IsNullOrWhiteSpace(file)
            ? null
            : File.ReadAllLines(file).Select(line => line.Trim().Replace('\\', '/')).Where(line => line.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>How a census judges: the scope's files only, or the whole tree whatever the scope.</summary>
    internal enum Judging
    {
        Diff,
        Whole,
    }

    /// <summary>How <paramref name="census"/> is marked.</summary>
    /// <exception cref="InvalidOperationException">It is marked neither way, or both.</exception>
    internal static Judging MarkOf(Type census) => MarkOf(census.Name, CensusMarks.Diff, CensusMarks.Whole);

    /// <summary>How the census named <paramref name="name"/> is marked by the two lists.</summary>
    internal static Judging MarkOf(string name, IReadOnlyCollection<string> diff, IReadOnlyCollection<string> whole) =>
        (diff.Contains(name), whole.Contains(name)) switch
        {
            (true, false) => Judging.Diff,
            (false, true) => Judging.Whole,
            _ => throw new InvalidOperationException($"{name} is a census marked {(diff.Contains(name) ? "both diff-able and whole" : "neither diff-able nor whole")}: list it once, in CensusMarks.cs"),
        };

    internal static IReadOnlySet<string>? ScopeOf(Type census) => MarkOf(census) == Judging.Whole ? null : Paths;

    /// <summary>Of <paramref name="paths"/> (full or repository-relative), the ones <paramref name="census"/> judges in this run.</summary>
    internal static IEnumerable<string> Judged(Type census, IEnumerable<string> paths) => Judged(MarkOf(census), paths, path => path, Paths);

    /// <summary>
    /// Of <paramref name="items"/>, the ones a census marked <paramref name="judging"/> judges under <paramref name="scope"/>: every one
    /// for a whole census or a run with no scope, else those whose path the scope holds.
    /// </summary>
    internal static IEnumerable<T> Judged<T>(Judging judging, IEnumerable<T> items, Func<T, string> pathOf, IReadOnlySet<string>? scope) =>
        scope is null || judging == Judging.Whole ? items : items.Where(item => scope.Contains(Relative(pathOf(item))));

    /// <summary>A path as the scope holds it: relative to the workspace, with <c>/</c>.</summary>
    internal static string Relative(string path) =>
        (Path.IsPathRooted(path) ? Path.GetRelativePath(WorkspaceRoot.Folder, path) : path).Replace('\\', '/');

    /// <summary>
    /// The census classes the census of censuses finds unmarked or marked twice, and the marks that name no census: each line says
    /// which and what to do.
    /// </summary>
    internal static IReadOnlyList<string> Unmarked(IEnumerable<string> censuses, IReadOnlyCollection<string> diff, IReadOnlyCollection<string> whole)
    {
        var found = censuses.ToHashSet(StringComparer.Ordinal);
        return
        [
            .. found.Where(name => !diff.Contains(name) && !whole.Contains(name)).Order(StringComparer.Ordinal)
                .Select(name => $"{name} is a census marked neither diff-able nor whole: list it in CensusMarks.cs"),
            .. found.Where(name => diff.Contains(name) && whole.Contains(name)).Order(StringComparer.Ordinal)
                .Select(name => $"{name} is marked both diff-able and whole"),
            .. diff.Concat(whole).Where(name => !found.Contains(name)).Order(StringComparer.Ordinal)
                .Select(name => $"{name} is marked and is no census"),
        ];
    }

    /// <summary>The names of the classes of <paramref name="assembly"/> that carry the Census trait.</summary>
    internal static IEnumerable<string> Censuses(System.Reflection.Assembly assembly) =>
        assembly.GetTypes().Where(type => type.GetCustomAttributesData().Any(attribute =>
                attribute.AttributeType == typeof(TraitAttribute)
                && attribute.ConstructorArguments is [{ Value: "Category" }, { Value: "Census" }]))
            .Select(type => type.Name);
}
