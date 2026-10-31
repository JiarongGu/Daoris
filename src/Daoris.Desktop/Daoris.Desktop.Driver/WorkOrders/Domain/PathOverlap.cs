namespace Daoris.Driver;

/// <summary>
/// An overlap the planner found at a start (INTEGRATE1l, the integration design §9 point 1): the earlier quest of the repository whose
/// declared or changed paths meet this one's, and this quest's declared paths that meet them.
/// </summary>
public sealed record OverlapFact(string Quest, IReadOnlyList<string> Paths)
{
    /// <summary>The sentence a reason and a word say it in, <c>overlaps `#q`: `a` · `b`</c>.</summary>
    public string Said => $"overlaps `#{Quest}`: {string.Join(" · ", Paths.Select(path => $"`{path}`"))}";
}

/// <summary>
/// Whether declared globs meet another unit's work (INTEGRATE1l): a glob meets a changed path it matches (<see cref="KindPaths"/>), and
/// another glob where either matches the other or their literal folders nest, so two declarations that may touch one file are taken to.
/// </summary>
public static class PathOverlap
{
    /// <summary>The globs of <paramref name="mine"/> that meet the other unit's declared globs or its changed paths, in their order.</summary>
    public static IReadOnlyList<string> Meet(IReadOnlyList<string> mine, IReadOnlyList<string> declared, IReadOnlyList<string> changed) =>
        [.. mine.Where(glob => changed.Any(path => KindPaths.Matches(glob, path)) || declared.Any(other => GlobsMeet(glob, other)))];

    /// <summary>The changed paths two units share, each once in ordinal order.</summary>
    public static IReadOnlyList<string> Shared(IReadOnlyList<string> one, IReadOnlyList<string> other) =>
        [.. one.Intersect(other, StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    private static bool GlobsMeet(string one, string other)
    {
        if (KindPaths.Matches(one, other) || KindPaths.Matches(other, one)) return true;
        var (a, b) = (Folder(one), Folder(other));
        return (Wild(one) || Wild(other)) && (a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal));
    }

    private static bool Wild(string glob) => glob.IndexOfAny(['*', '?', '{', '[']) >= 0;

    /// <summary>The folders before a glob's first wildcard, ending at its last <c>/</c>: <c>src/frame/</c> of <c>src/frame/*.cs</c>.</summary>
    private static string Folder(string glob)
    {
        var wild = glob.IndexOfAny(['*', '?', '{', '[']);
        var literal = wild < 0 ? glob : glob[..wild];
        return literal[..(literal.LastIndexOf('/') + 1)];
    }
}
