using System.Diagnostics.CodeAnalysis;

namespace Daoris.Driver;

/// <summary>
/// The one way the driver clips text to a bound, makes it one line and quotes it (DEDUP7). About forty copies each wrote
/// <c>text[..N] + "…"</c> with a limit of their own, the off-by-three differing, and a quoted block eleven times with the
/// indent and the trim drifting. Each caller keeps its own limit and its own bytes: the parameters name what the copies
/// differed in, so none of them moved.
/// </summary>
public static class TextBounds
{
    /// <summary>The mark a clip leaves where it cut.</summary>
    public const string Ellipsis = "…";

    /// <summary>
    /// <paramref name="text"/> as it is when it fits <paramref name="limit"/>; else its first <paramref name="keep"/>
    /// characters (the limit when not given: a caller whose mark must fit inside the limit keeps three fewer) and the
    /// mark. <paramref name="trimEnd"/> drops the space before the mark; <paramref name="wholePairs"/> never cuts a
    /// surrogate pair in half.
    /// </summary>
    public static string Clip(string text, int limit, int? keep = null, bool trimEnd = false, bool wholePairs = false) =>
        text.Length <= limit ? text : Cut(text, keep ?? limit, trimEnd, wholePairs);

    /// <summary>
    /// The first <paramref name="keep"/> characters and the mark, whether or not the text is longer: for the caller whose own
    /// test decided that it is cut (a line that ran past its bound while it was read).
    /// </summary>
    public static string Cut(string text, int keep, bool trimEnd = false, bool wholePairs = false)
    {
        var end = wholePairs && keep > 0 && keep <= text.Length && char.IsHighSurrogate(text[keep - 1]) ? keep - 1 : keep;
        var head = text[..end];
        return (trimEnd ? head.TrimEnd() : head) + Ellipsis;
    }

    /// <summary>
    /// The end of <paramref name="text"/> when it is longer than <paramref name="limit"/>: the mark, then the last
    /// <paramref name="limit"/> characters without the space they may open with (the last words, not the first).
    /// </summary>
    public static string ClipStart(string text, int limit) =>
        text.Length <= limit ? text : Ellipsis + text[^limit..].TrimStart();

    /// <summary>
    /// A field cut to a limit saying how long it was, never silently shortened; null stays null.
    /// </summary>
    [return: NotNullIfNotNull(nameof(text))]
    public static string? ClipCounted(string? text, int limit) =>
        text is null || text.Length <= limit ? text : $"{text[..limit]}{Ellipsis} ({text.Length} chars)";

    /// <summary>
    /// Line breaks of every kind as one space each, and (unless <paramref name="trim"/> is false) none at either end.
    /// </summary>
    public static string OneLine(string text, bool trim = true)
    {
        var line = text.ReplaceLineEndings(" ");
        return trim ? line.Trim() : line;
    }

    /// <summary>Every run of white space, line breaks among them, as one space, and none at either end; null as nothing.</summary>
    public static string Fold(string? text) =>
        string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// <paramref name="words"/> as a block quote, each line (the line endings made LF) led by <c>&gt; </c> after the
    /// <paramref name="indent"/>. A blank line is a bare <c>&gt;</c> unless <paramref name="bareBlank"/> is false, and
    /// <paramref name="trimEnd"/> drops the blank lines at the end first. No newline follows the last line.
    /// </summary>
    public static string Quote(string words, string indent = "", bool bareBlank = true, bool trimEnd = false)
    {
        var lines = words.ReplaceLineEndings("\n");
        if (trimEnd) lines = lines.TrimEnd('\n');
        return string.Join("\n", lines.Split('\n').Select(line => bareBlank && line.Length == 0 ? $"{indent}>" : $"{indent}> {line}"));
    }
}
