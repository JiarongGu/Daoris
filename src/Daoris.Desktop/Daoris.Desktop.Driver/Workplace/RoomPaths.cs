using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The two path grammars a room's declaration is written in: the lanes file's, whose globs place tracked files and
/// whose <c>!</c> carves a narrower lane's files out of a wider one, and the gates file's, where a folder ends in <c>/</c> and holds
/// everything under it. Both are repository-relative with forward slashes; <c>*</c> stays in a folder, <c>**</c> crosses folders.
/// </summary>
internal static class RoomPaths
{
    /// <summary>A lane's paths as one test, as the merge tool's <c>laneMatcher</c> reads them: one glob holds it and no <c>!</c> glob does.</summary>
    internal static Func<string, bool> Lane(IEnumerable<string> paths)
    {
        var all = paths.ToList();
        var include = all.Where(path => !path.StartsWith('!')).Select(UnionJoins.Glob).ToList();
        var exclude = all.Where(path => path.StartsWith('!')).Select(path => UnionJoins.Glob(path[1..])).ToList();
        return path => include.Exists(glob => glob.IsMatch(path)) && !exclude.Exists(glob => glob.IsMatch(path));
    }

    /// <summary>A gates-file path as a test of a tracked path: a folder (ending <c>/</c>) holds everything under it.</summary>
    internal static Regex Declared(string path) => UnionJoins.Glob(path.EndsWith('/') ? path + "**" : path);

    /// <summary>Whether <paramref name="path"/> names one file: no wildcard, no folder.</summary>
    internal static bool IsFile(string path) => !path.EndsWith('/') && path.IndexOfAny(['*', '?', '{']) < 0;

    /// <summary>
    /// Whether two gates-file paths can name one file: either holds a file in the other's literal part, the part before its first
    /// wildcard, which is how an untracked output folder is found under another's.
    /// </summary>
    internal static bool Meet(string one, string other) => Declared(one).IsMatch(Probe(other)) || Declared(other).IsMatch(Probe(one));

    /// <summary>
    /// A path standing for what <paramref name="path"/> names, for a test that has no tracked file to try: the path itself for one
    /// file, else a name inside its literal part.
    /// </summary>
    internal static string Probe(string path)
    {
        if (IsFile(path)) return path;
        var head = path[..(path.IndexOfAny(['*', '?', '{']) is var at and >= 0 ? at : path.Length)];
        return head.Length == 0 || head.EndsWith('/') ? head + "_" : head;
    }

    /// <summary>Why a declared path is not a repository-relative one written with forward slashes, or null where it is.</summary>
    internal static string? Unfit(string path)
    {
        if (path.Length == 0) return "is empty";
        if (path.Contains('\\', StringComparison.Ordinal)) return "is written with backslashes; paths use '/'";
        if (path.StartsWith('/') || path.Contains(':', StringComparison.Ordinal)) return "is not repository-relative";
        var parts = path.TrimEnd('/').Split('/');
        if (parts.Contains("..")) return "leaves the repository";
        return parts.Any(part => part is "" or ".") ? "has an empty or '.' part" : null;
    }
}
