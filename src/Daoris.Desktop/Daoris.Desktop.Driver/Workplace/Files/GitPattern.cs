using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A rule's paths read as git reads its ignore patterns (the tree-pool design §35.1, §36.2): a pattern with no slash but a trailing one
/// matches at any depth, a leading or inner <c>/</c> anchors it at the repository's top, a trailing <c>/</c> names a folder, <c>*</c>,
/// <c>?</c>, <c>[…]</c> and <c>**</c> as git reads them, <c>\</c> escapes, and a leading <c>!</c> takes back what an earlier pattern
/// of the same entry covered. A top is covered as git's ignore covers it: its folders from the top down and then the top, the last
/// match winning at each level, the first level whose last match is positive covering the top and everything under it, so no
/// <c>!</c> takes back anything under a covered folder. An entry kept in the first form (<see cref="FileRule.FirstForm"/>) is read
/// by the first form's own matcher.
/// </summary>
internal static class GitPattern
{
    private static readonly ConcurrentDictionary<(string Pattern, bool IgnoreCase), Compiled> Read = new();

    private static readonly char[] Wildcards = ['*', '?', '['];

    private sealed record Compiled(bool Negated, bool Folder, Regex Test);

    /// <summary>Whether <paramref name="rule"/> covers <paramref name="top"/>, an ignored top or a path, a folder ending in <c>/</c>.</summary>
    internal static bool Covers(FileRule rule, string top, bool ignoreCase) =>
        rule.FirstForm ? rule.Paths.Any(each => RoomPaths.Declared(each).IsMatch(top)) : Covers(rule.Paths, top, ignoreCase);

    /// <summary>Whether the entry's <paramref name="patterns"/> cover <paramref name="top"/> as git's ignore does.</summary>
    internal static bool Covers(IReadOnlyList<string> patterns, string top, bool ignoreCase)
    {
        if (top.Length == 0) return false;
        var compiled = patterns.Select(each => Read.GetOrAdd((each, ignoreCase), key => Compile(key.Pattern, key.IgnoreCase))).ToList();
        var parts = top.TrimEnd('/').Split('/');
        for (var depth = 1; depth <= parts.Length; depth++)
        {
            var level = string.Join('/', parts.Take(depth));
            var folder = depth < parts.Length || top.EndsWith('/');
            bool? last = null;
            foreach (var each in compiled)
            {
                if ((!each.Folder || folder) && each.Test.IsMatch(level)) last = !each.Negated;
            }

            if (last == true) return true;
        }

        return false;
    }

    /// <summary>
    /// The path an anchored pattern with no unescaped wildcard names, a folder ending in <c>/</c>; null for a pattern that opens with
    /// <c>!</c>, matches at any depth or holds a wildcard, and for an entry kept in the first form.
    /// </summary>
    internal static string? LiteralOf(FileRule rule, string pattern) => rule.FirstForm ? null : LiteralOf(pattern);

    internal static string? LiteralOf(string pattern)
    {
        if (pattern.StartsWith('!') || !Anchors(pattern)) return null;
        var text = new StringBuilder();
        for (var i = pattern.StartsWith('/') ? 1 : 0; i < pattern.Length; i++)
        {
            if (pattern[i] == '\\' && i + 1 < pattern.Length) text.Append(pattern[++i]);
            else if (Array.IndexOf(Wildcards, pattern[i]) >= 0) return null;
            else text.Append(pattern[i]);
        }

        return text.Length == 0 ? null : text.ToString();
    }

    /// <summary>A top written as the pattern that names it alone: a leading <c>/</c>, and git's wildcards and spaces escaped.</summary>
    internal static string Anchored(string top)
    {
        var text = new StringBuilder("/");
        foreach (var c in top)
        {
            if (c is '*' or '?' or '[' or '\\' or ' ') text.Append('\\');
            text.Append(c);
        }

        return text.ToString();
    }

    /// <summary>
    /// A path standing for what <paramref name="pattern"/> names, for a test that has no top to try: its literal part, the part before
    /// its first wildcard, with a name inside it where that part is a folder; null for a <c>!</c>.
    /// </summary>
    internal static string? Probe(string pattern)
    {
        if (pattern.StartsWith('!')) return null;
        var text = new StringBuilder();
        for (var i = pattern.StartsWith('/') ? 1 : 0; i < pattern.Length; i++)
        {
            if (pattern[i] == '\\' && i + 1 < pattern.Length) text.Append(pattern[++i]);
            else if (Array.IndexOf(Wildcards, pattern[i]) >= 0) break;
            else text.Append(pattern[i]);
        }

        return text.Length == 0 || text[^1] == '/' ? text + "_" : text.ToString();
    }

    /// <summary>Whether two entries can name one path: either covers the other's literal part (<see cref="Probe"/>).</summary>
    internal static bool Meet(FileRule one, FileRule other) =>
        other.Paths.Any(each => (other.FirstForm ? RoomPaths.Probe(each) : Probe(each)) is { } probe && Covers(one, probe, false))
        || one.Paths.Any(each => (one.FirstForm ? RoomPaths.Probe(each) : Probe(each)) is { } probe && Covers(other, probe, false));

    /// <summary>Whether <paramref name="entry"/> and a gates-file path can name one file, as a room's writes are checked against the rule file.</summary>
    internal static bool Meets(FileRule entry, string declared) =>
        Covers(entry, RoomPaths.Probe(declared), false)
        || entry.Paths.Any(each => (entry.FirstForm ? RoomPaths.Probe(each) : Probe(each)) is { } probe && RoomPaths.Declared(declared).IsMatch(probe));

    /// <summary>Why a pattern is not one the rule file takes in git's syntax, or null where it is.</summary>
    internal static string? Unfit(string pattern)
    {
        var text = pattern.StartsWith('!') ? pattern[1..] : pattern;
        if (text.Trim().Length == 0) return "is empty";
        if (text.StartsWith('#')) return "is a comment in git's syntax: write `\\#` for a name that opens with `#`";
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\\') continue;
            if (i + 1 == text.Length || text[i + 1] is not ('*' or '?' or '[' or ']' or '\\' or ' ' or '!' or '#')) return "is written with backslashes; paths use '/'";
            i++;
        }

        if (text.Contains(':', StringComparison.Ordinal)) return "is not repository-relative";
        var parts = text.Trim('/').Split('/');
        if (parts.Contains("..")) return "leaves the repository";
        return parts.Any(part => part is "" or ".") ? "has an empty or '.' part" : null;
    }

    /// <summary>Whether git reads the pattern from the repository's top: a slash before its end.</summary>
    private static bool Anchors(string pattern) => pattern.TrimEnd('/').Contains('/');

    private static Compiled Compile(string pattern, bool ignoreCase)
    {
        var negated = pattern.StartsWith('!');
        var text = Trimmed(negated ? pattern[1..] : pattern);
        var folder = text.EndsWith('/');
        if (folder) text = text[..^1];
        var anchored = text.Contains('/');
        if (text.StartsWith('/')) text = text[1..];
        var options = RegexOptions.CultureInvariant | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
        return new Compiled(negated, folder, new Regex((anchored ? "^" : "^(?:.*/)?") + Source(text) + "$", options));
    }

    /// <summary>The text with its trailing spaces dropped unless escaped, as git reads a line.</summary>
    private static string Trimmed(string text)
    {
        var end = text.Length;
        while (end > 0 && text[end - 1] == ' ' && !(end > 1 && text[end - 2] == '\\')) end--;
        return text[..end];
    }

    private static string Source(string text)
    {
        var source = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\\' && i + 1 < text.Length)
            {
                source.Append(Regex.Escape(text[++i].ToString()));
            }
            else if (c == '*')
            {
                var start = i;
                while (i + 1 < text.Length && text[i + 1] == '*') i++;
                var whole = i > start && (start == 0 || text[start - 1] == '/') && (i + 1 == text.Length || text[i + 1] == '/');
                if (!whole) source.Append("[^/]*");
                else if (i + 1 == text.Length) source.Append(".*");
                else
                {
                    source.Append("(?:.*/)?");
                    i++;
                }
            }
            else if (c == '?')
            {
                source.Append("[^/]");
            }
            else if (c == '[' && Bracket(text, i) is { } end)
            {
                source.Append(Class(text[(i + 1)..end]));
                i = end;
            }
            else
            {
                source.Append(Regex.Escape(c.ToString()));
            }
        }

        return source.ToString();
    }

    /// <summary>Where the bracket expression opened at <paramref name="at"/> closes, or null where it does not and <c>[</c> is itself.</summary>
    private static int? Bracket(string text, int at)
    {
        var i = at + 1;
        if (i < text.Length && text[i] is '!' or '^') i++;
        if (i < text.Length && text[i] == ']') i++;
        var end = text.IndexOf(']', i);
        return end < 0 ? null : end;
    }

    private static string Class(string inside)
    {
        var negated = inside.Length > 0 && inside[0] is '!' or '^';
        var body = new StringBuilder(negated ? "[^/" : "[");
        for (var i = negated ? 1 : 0; i < inside.Length; i++)
        {
            var c = inside[i];
            var range = c == '-' && i > (negated ? 1 : 0) && i + 1 < inside.Length;
            body.Append(range ? "-" : c is '\\' or ']' or '[' or '^' or '-' ? "\\" + c : c.ToString());
        }

        return body.Append(']').ToString();
    }
}
