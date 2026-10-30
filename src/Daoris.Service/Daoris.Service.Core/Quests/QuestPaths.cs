namespace Daoris.Knowledge;

/// <summary>
/// The paths an ask or a quest declares it touches (INTEGRATE1l, the integration design §9 point 1): repository-relative globs, as git names
/// a path, which a start compares with the repository's other open work. A fact of the work, never a machine's path.
/// </summary>
public static class QuestPaths
{
    /// <summary>The most globs one publish declares.</summary>
    public const int Most = 32;

    /// <summary>The longest glob, in characters.</summary>
    public const int Longest = 300;

    /// <summary>
    /// The globs as they are kept, once each in ordinal order, and why they are refused: a blank one, one past <see cref="Longest"/>, more than
    /// <see cref="Most"/>, and any that could name a place outside the repository (rooted, a drive, a backslash, a <c>.</c> or <c>..</c> part).
    /// </summary>
    public static (IReadOnlyList<string> Paths, string? Refusal) Judge(IReadOnlyList<string?>? given)
    {
        if (given is null || given.Count == 0) return ([], null);
        var kept = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var each in given)
        {
            var path = each?.Trim() ?? "";
            if (Unfit(path)) return ([], Refused(path));
            kept.Add(path);
        }

        return kept.Count > Most
            ? ([], $"At most {Most} paths are declared — {kept.Count} were given.")
            : ([.. kept], null);
    }

    /// <summary>Whether a glob read back from a store or the wire is one a publish would keep.</summary>
    public static bool Fits(string? path) => path is not null && path == path.Trim() && !Unfit(path);

    private static bool Unfit(string path) =>
        path.Length == 0 || path.Length > Longest || path.StartsWith('/') || path.Contains('\\') || path.Contains(':')
        || path.Split('/').Any(part => part is "." or "..");

    private static string Refused(string path) =>
        $"A path is a repository-relative glob with `/` between its parts and no `.` or `..` part, at most {Longest} characters — "
        + $"`{(path.Length > 60 ? path[..60] + "…" : path)}` is not one.";
}
