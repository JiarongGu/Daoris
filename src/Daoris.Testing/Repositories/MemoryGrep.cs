using System.Text.RegularExpressions;
using Daoris.Ports.Repositories;

namespace Daoris.Testing;

/// <summary>
/// The search a memory repository answers as <c>git grep -I -F</c> does over a commit: each line of each text file the pathspecs name that
/// holds one of the names, in path order. A pathspec is a path, a folder ending in <c>/</c>, a glob whose <c>*</c> crosses folders, or one of
/// those after <c>:(exclude)</c>, <c>:!</c> or <c>:^</c>, which takes its files out.
/// </summary>
internal static class MemoryGrep
{
    internal static IReadOnlyList<GitMatch> Lines(MemoryCommit commit, IReadOnlyCollection<string> names, IReadOnlyList<string> pathspecs)
    {
        var include = pathspecs.Where(spec => Excluded(spec) is null).ToList();
        var exclude = pathspecs.Select(Excluded).OfType<string>().ToList();
        var found = new List<GitMatch>();
        foreach (var (path, blob) in commit.Tree.OrderBy(each => each.Key, StringComparer.Ordinal))
        {
            if (blob.Mode != MemoryBlob.File || blob.Content.Contains('\0', StringComparison.Ordinal)) continue;
            if ((include.Count > 0 && !include.Any(spec => Covers(path, spec))) || exclude.Any(spec => Covers(path, spec))) continue;
            var lines = blob.Content.Split('\n');
            for (var at = 0; at < lines.Length; at++)
            {
                if (at == lines.Length - 1 && lines[at].Length == 0) continue;
                if (names.Any(name => lines[at].Contains(name, StringComparison.Ordinal))) found.Add(new GitMatch(path, lines[at]));
            }
        }

        return found;
    }

    private static string? Excluded(string spec) =>
        spec.StartsWith(":(exclude)", StringComparison.Ordinal) ? spec[":(exclude)".Length..]
        : spec.StartsWith(":!", StringComparison.Ordinal) || spec.StartsWith(":^", StringComparison.Ordinal) ? spec[2..]
        : null;

    private static bool Covers(string path, string spec)
    {
        if (spec.EndsWith('/')) return path.StartsWith(spec, StringComparison.Ordinal);
        if (spec.Contains('*', StringComparison.Ordinal) || spec.Contains('?', StringComparison.Ordinal))
        {
            return Regex.IsMatch(path, "^" + Regex.Escape(spec).Replace("\\*", ".*", StringComparison.Ordinal).Replace("\\?", ".", StringComparison.Ordinal) + "$", RegexOptions.CultureInvariant);
        }

        return path == spec || path.StartsWith(spec + "/", StringComparison.Ordinal);
    }
}
