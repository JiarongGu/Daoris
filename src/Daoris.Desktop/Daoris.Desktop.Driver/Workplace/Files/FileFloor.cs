namespace Daoris.Driver;

/// <summary>
/// What a tree's floor is read over: the paths git tracks at its commit, and the ignored tops that are or hold another repository or
/// tree (a <c>.git</c> below the top, a linked worktree, a bare repository).
/// </summary>
internal sealed record FileFloorFacts(IReadOnlyCollection<string> Tracked, IReadOnlyCollection<string> Repositories)
{
    /// <summary>The checkout's <c>core.ignorecase</c>, read once with its tops: whether a pattern matches a top by case, as git does.</summary>
    internal bool IgnoreCase { get; init; }
}

/// <summary>
/// The built-in floor (the tree-pool design §30.1), read after every layer, which no rule, role or allow sets aside and none of
/// which asks: a path git tracks is git's; another repository or tree is never touched; a harness's or an editor's settings file, or
/// a provider's project configuration, is never copied. It keeps only what prevents losing work.
/// </summary>
internal static class FileFloor
{
    /// <summary>The settings a copy would widen a session's permissions with, past the ones Daoris composes for it: shipped data.</summary>
    private static IReadOnlyList<string> Settings => FileSignatures.Shipped.Settings;

    internal const string Tracked = "git tracks it";

    internal const string Repository = "another repository or tree: never touched";

    internal const string NeverCopied = "a harness's or an editor's settings: never copied";

    /// <summary>The first tracked path <paramref name="path"/>, a top read as a path and never a pattern, names or holds, or null.</summary>
    internal static string? TrackedIn(string path, IReadOnlyCollection<string> tracked) =>
        !path.EndsWith('/') ? tracked.Contains(path) ? path : null
        : tracked.FirstOrDefault(each => each.StartsWith(path, StringComparison.Ordinal));

    /// <summary>Whether <paramref name="path"/> is, holds or lies inside a top that is or holds another repository or tree.</summary>
    internal static bool Held(string path, IReadOnlyCollection<string> repositories) =>
        repositories.Any(top => path.StartsWith(top, StringComparison.Ordinal) || (path.EndsWith('/') && top.StartsWith(path, StringComparison.Ordinal)));

    /// <summary>Whether <paramref name="path"/> is, or is a folder holding, a settings file the floor never copies.</summary>
    internal static bool IsSettings(string path) => Settings.Any(each =>
        path == each || path.EndsWith("/" + each, StringComparison.Ordinal)
        || (path.EndsWith('/') && (each.StartsWith(path, StringComparison.Ordinal) || Folder(each) is { Length: > 0 } folder && path.EndsWith("/" + folder, StringComparison.Ordinal))));

    /// <summary>
    /// Whether <paramref name="folder"/> has a bare repository's shape: a <c>HEAD</c> file and <c>objects/</c> and <c>refs/</c> folders.
    /// The shape alone holds it, whatever git would answer of it, since holding a folder that is not one costs a held slot and missing
    /// one loses its refs.
    /// </summary>
    internal static bool LooksBare(string folder) =>
        File.Exists(Path.Combine(folder, "HEAD")) && Directory.Exists(Path.Combine(folder, "objects")) && Directory.Exists(Path.Combine(folder, "refs"));

    private static string Folder(string path) => path.LastIndexOf('/') is var slash and >= 0 ? path[..(slash + 1)] : "";
}
