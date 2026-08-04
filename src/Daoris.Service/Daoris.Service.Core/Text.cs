namespace Daoris.Knowledge;

/// <summary>
/// Text handling shared by every search.
/// </summary>
/// <remarks>
/// These were three near-identical excerpt functions and two separator lists, one per search
/// implementation — which is this project's own thesis showing up in its own code: the same logic
/// re-derived in three places, already diverging in the details (one returned null on no match,
/// another the whole body, a third a truncation with no ellipsis).
/// </remarks>
public static class Text
{
    /// <summary>
    /// Word separators. Hyphen included on purpose: `no-tmp-for-repo-files` should be findable by
    /// searching for "tmp", and a reader looking for one word of a hyphenated name is the common case.
    /// </summary>
    private static readonly char[] Separators =
        " \t\r\n.,;:!?()[]{}<>\"'`|/\\*_#=+~-".ToCharArray();

    /// <summary>
    /// Lower-cased words worth matching on. Two characters and under are dropped: they are almost all
    /// articles and prepositions, and they match everything, which is the same as matching nothing.
    /// </summary>
    public static List<string> Tokenize(string? text) =>
        (text ?? string.Empty)
            .ToLowerInvariant()
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length > 2)
            .ToList();

    /// <summary>
    /// A window of the body around the first matching term, so a result can show why it matched.
    /// </summary>
    /// <remarks>
    /// A result list that cannot show its reasoning gets treated as an oracle, which is exactly what
    /// it is not. When nothing matches — a semantic hit shares no literal term by definition — the
    /// opening of the body is still more useful than nothing.
    /// </remarks>
    public static string Excerpt(string body, IEnumerable<string>? terms = null, int window = 180)
    {
        body ??= string.Empty;

        var index = -1;
        foreach (var term in terms ?? [])
        {
            index = body.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) break;
        }

        if (index < 0)
        {
            return body.Length <= window
                ? Flatten(body)
                : Flatten(body[..window]) + "…";
        }

        // Start a little before the match so the term has context on both sides rather than sitting
        // at the very edge of the window.
        var start = Math.Max(0, index - window / 3);
        var length = Math.Min(window, body.Length - start);
        return (start > 0 ? "…" : string.Empty)
             + Flatten(body.Substring(start, length))
             + (start + length < body.Length ? "…" : string.Empty);
    }

    /// <summary>One line: an excerpt is shown inline, and embedded newlines break every caller's layout.</summary>
    private static string Flatten(string text) => text.Replace('\n', ' ').Replace('\r', ' ').Trim();
}
