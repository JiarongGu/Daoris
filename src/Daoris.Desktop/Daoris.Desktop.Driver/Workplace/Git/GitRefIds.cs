namespace Daoris.Driver;

/// <summary>
/// A history read that names <c>HEAD</c> or a full ref name, asked by the ids git's files give them (<see cref="GitRefReads"/>), so the
/// object memo keys it (<see cref="GitObjectArguments.Qualifies"/>): the same read of a ref that has not moved is kept, one of a moved ref
/// names another id and is asked afresh. Nothing is kept by the name.
/// </summary>
/// <remarks>
/// Only where the read, so written, is one the memo keeps: any other word, option or path leaves the arguments as the caller wrote them, as
/// does a name the files cannot say whole (absent, odd, a reftable). An id for a name is read at the ask, so the answer is what the ref
/// named then, as git would have read it a moment later.
/// </remarks>
internal static class GitRefIds
{
    private static readonly string[] Verbs = ["merge-base", "rev-list", "log", "cherry", "ls-tree"];

    /// <summary><paramref name="arguments"/> with each <c>HEAD</c> and full ref name in a revision word as its id; the same list where nothing can be.</summary>
    internal static IReadOnlyList<string> Resolved(string folder, IReadOnlyList<string> arguments)
    {
        var at = 0;
        while (at < arguments.Count && arguments[at].StartsWith('-')) at += arguments[at] == "-c" ? 2 : 1;
        if (at >= arguments.Count || !Verbs.Contains(arguments[at])) return arguments;
        if (!arguments.Skip(at + 1).Any(word => !word.StartsWith('-') && Names(word))) return arguments;
        var ended = false;
        var changed = new List<string>(arguments.Count);
        for (var each = 0; each < arguments.Count; each++)
        {
            var word = arguments[each];
            ended |= word == "--";
            changed.Add(each <= at || ended || word.StartsWith('-') ? word : Ended(folder, word));
        }

        return GitObjectArguments.Qualifies(changed) ? changed : arguments;
    }

    private static bool Names(string word) => word.Split("..", StringSplitOptions.None).Any(end => end.TrimStart('^') is "HEAD" || end.StartsWith("refs/", StringComparison.Ordinal));

    private static string Ended(string folder, string word)
    {
        var dots = word.IndexOf("..", StringComparison.Ordinal);
        if (dots < 0) return Id(folder, word);
        var tail = word.AsSpan(dots).StartsWith("...") ? "..." : "..";
        return Id(folder, word[..dots]) + tail + Id(folder, word[(dots + tail.Length)..]);
    }

    private static string Id(string folder, string end)
    {
        var not = end.StartsWith('^') ? "^" : "";
        var name = end[not.Length..];
        if (name != "HEAD" && !name.StartsWith("refs/", StringComparison.Ordinal)) return end;
        string[] ask = name == "HEAD" ? ["rev-parse", "HEAD"] : ["rev-parse", "--verify", "--quiet", name];
        return GitRefReads.Asked(folder, ask)?.Answer is { Code: 0 } found && found.Stdout.Trim() is { Length: > 0 } id ? not + id : end;
    }
}
