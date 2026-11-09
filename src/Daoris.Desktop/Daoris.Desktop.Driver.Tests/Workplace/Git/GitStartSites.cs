using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A git start as the source writes it: its file, the words of each argument list it may be handed, or the expression passed on.</summary>
/// <param name="Lists">Each literal argument list the call may start, its elements as written; empty where it hands on an expression.</param>
/// <param name="Passed">The expression handed on as the whole argument list, where it is not a literal one.</param>
internal sealed record GitStartSite(string File, IReadOnlyList<IReadOnlyList<string>> Lists, string? Passed);

/// <summary>
/// Every call of the git gate's seams in the driver's sources (<c>GitAsync</c>, <c>GitBytesAsync</c>, <c>ReadGitAsync</c>,
/// <c>HeldGitAsync</c>), read for the argument lists it starts, so a census can hold each command and verb to the table.
/// </summary>
internal static partial class GitStartSites
{
    /// <summary>Each call in <paramref name="sources"/>, declarations left out.</summary>
    public static List<GitStartSite> In(IEnumerable<(string File, string Source)> sources)
    {
        var sites = new List<GitStartSite>();
        foreach (var (file, source) in sources)
        {
            var code = Code(source);
            foreach (Match call in Seam().Matches(code))
            {
                var arguments = Arguments(code, call.Index + call.Length);
                var at = call.Groups["seam"].Value == "HeldGitAsync" ? 2 : 1;
                if (arguments.Count <= at || arguments.Any(Declared().IsMatch)) continue;
                // A wrapper of the seam may take its list later than the seam does: the first literal list among the arguments is the one.
                var expression = arguments.Skip(1).FirstOrDefault(each => ListStart().IsMatch(each)) ?? arguments[at];
                var lists = Lists(expression);
                sites.Add(new GitStartSite(file, lists, lists.Count == 0 ? expression.Trim() : null));
            }
        }

        return sites;
    }

    /// <summary>The command and the word after it, where the list names its command by a literal: the first element past git's own options.</summary>
    public static (string? Command, string? Next, string? Held) Named(IReadOnlyList<string> list)
    {
        var at = 0;
        while (at < list.Count)
        {
            var element = list[at].Trim();
            if (element.StartsWith("..", StringComparison.Ordinal)) at++;
            else if (Literal(element) is { } word && word.StartsWith('-')) at += word is "-c" or "-C" or "--git-dir" or "--work-tree" or "--namespace" ? 2 : 1;
            else if (element.StartsWith("$\"-", StringComparison.Ordinal)) at++;
            else break;
        }

        if (at >= list.Count) return (null, null, null);
        if (Literal(list[at].Trim()) is not { } command) return (null, null, list[at].Trim());
        var next = list.Skip(at + 1).Select(each => each.Trim()).FirstOrDefault(each => Literal(each) is not { } word || !word.StartsWith('-'));
        // The word after the command is its verb only where its row reads one, so a held one is the census's concern only there.
        return (command, next is null ? null : Literal(next), next is not null && Literal(next) is null ? next : null);
    }

    private static string? Literal(string element) =>
        element.Length >= 2 && element[0] == '"' && element[^1] == '"' && !element[1..^1].Contains('"') ? element[1..^1] : null;

    private static List<IReadOnlyList<string>> Lists(string expression)
    {
        var lists = new List<IReadOnlyList<string>>();
        var depth = 0;
        for (var at = 0; at < expression.Length; at++)
        {
            var c = expression[at];
            if (c is '"' or '\'') at = SkipString(expression, at);
            else if (c is '(') depth++;
            else if (c is ')') depth--;
            else if (depth == 0 && c is '[' or '{' && (c == '[' || expression[..at].TrimEnd().EndsWith(']') || expression[..at].TrimEnd().EndsWith('>')))
            {
                var close = Close(expression, at);
                if (close < 0) break;
                lists.Add(Split(expression[(at + 1)..close]));
                at = close;
            }
        }

        return lists;
    }

    private static List<string> Arguments(string code, int open)
    {
        var close = Close(code, open - 1);
        return close < 0 ? [] : Split(code[open..close]);
    }

    private static int Close(string text, int open)
    {
        var depth = 0;
        for (var at = open; at < text.Length; at++)
        {
            var c = text[at];
            if (c is '"' or '\'') at = SkipString(text, at);
            else if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']' or '}' && --depth == 0) return at;
        }

        return -1;
    }

    private static List<string> Split(string inner)
    {
        var parts = new List<string>();
        var (depth, start) = (0, 0);
        for (var at = 0; at < inner.Length; at++)
        {
            var c = inner[at];
            if (c is '"' or '\'') at = SkipString(inner, at);
            else if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']' or '}' && depth > 0) depth--;
            else if (c == ',' && depth == 0)
            {
                parts.Add(inner[start..at]);
                start = at + 1;
            }
        }

        if (inner[start..].Trim().Length > 0) parts.Add(inner[start..]);
        return parts;
    }

    // The end of the literal opening at `at`: a char, a raw string, or a string whose interpolation holes may hold strings of their own.
    private static int SkipString(string text, int at)
    {
        if (text[at] == '\'')
        {
            var close = text.IndexOf('\'', at + (text[at + 1] == '\\' ? 3 : 2));
            return close < 0 ? text.Length : close;
        }

        if (string.CompareOrdinal(text, at, "\"\"\"", 0, 3) == 0)
        {
            var raw = text.IndexOf("\"\"\"", at + 3, StringComparison.Ordinal);
            return raw < 0 ? text.Length : raw + 2;
        }

        var prefix = text[Math.Max(0, at - 2)..at];
        var verbatim = prefix.Contains('@');
        var interpolated = prefix.Contains('$');
        var holes = 0;
        for (var end = at + 1; end < text.Length; end++)
        {
            var c = text[end];
            if (holes > 0)
            {
                if (c is '"' or '\'') end = SkipString(text, end);
                else if (c == '{') holes++;
                else if (c == '}') holes--;
            }
            else if (!verbatim && c == '\\') end++;
            else if (interpolated && c == '{')
            {
                if (end + 1 < text.Length && text[end + 1] == '{') end++;
                else holes++;
            }
            else if (c == '"')
            {
                if (verbatim && end + 1 < text.Length && text[end + 1] == '"') end++;
                else return end;
            }
        }

        return text.Length;
    }

    private static string Code(string source) => LineComment().Replace(source.Replace("\r\n", "\n"), "");

    [GeneratedRegex(@"(?<![\w.]Task<[^\n]*)\b(?<seam>GitAsync|GitBytesAsync|ReadGitAsync|HeldGitAsync)\s*\(")]
    private static partial Regex Seam();

    // A parameter's declaration: a type, then its name.
    [GeneratedRegex(@"^\s*(?:this\s+)?(?:string|int|bool|CancellationToken|IReadOnlyList<string>|IReadOnlyDictionary<string,\s*string>\??|TimeSpan\??|Func<[^\n]*>|string\?)\s+\w+\s*(?:=[^\n]*)?$")]
    private static partial Regex Declared();

    [GeneratedRegex(@"(?<![:""])//[^\n]*")]
    private static partial Regex LineComment();

    [GeneratedRegex(@"^\s*(?:\[|new\s*(?:string\s*)?\[\]|new\s+List<string>)")]
    private static partial Regex ListStart();
}
