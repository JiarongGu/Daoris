namespace Daoris.Knowledge;

/// <summary>One heading and the text beneath it, up to the next heading of the same level.</summary>
/// <param name="Heading">The heading text, with its leading hashes and whitespace removed.</param>
/// <param name="Body">Everything under the heading, trimmed.</param>
/// <param name="First">The body's first line in the text split, from 1; 0 when the body is empty (ORIENT2e).</param>
/// <param name="Last">The body's last line, the same.</param>
public readonly record struct MarkdownSection(string Heading, string Body, int First = 0, int Last = 0);

/// <summary>
/// Splits a markdown document into its sections at a chosen heading level.
/// </summary>
public static class MarkdownSections
{
    /// <summary>
    /// Split at the given heading level, returning one section per heading. Text before the first
    /// heading is preamble and is dropped: it describes the file, not any entry in it.
    /// </summary>
    /// <remarks>
    /// Headings are only recognised at the start of a line and outside fenced code blocks — a
    /// document about markdown, or one quoting a changelog, otherwise splits itself apart at
    /// headings that were only ever examples. This repository's own decision log contains exactly
    /// such a fence, so the naive version fails on the first real input. A fence closes as CommonMark
    /// closes one, on its own character at least as long (<see cref="MarkdownFence"/>, ORIENT2h3), so a
    /// fence quoting a shorter one holds the quote whole.
    /// </remarks>
    public static IReadOnlyList<MarkdownSection> Split(string markdown, int level = 2) => Walk(markdown, level).Sections;

    /// <summary>
    /// The text before the first heading at the given level, trimmed, which <see cref="Split"/> drops:
    /// empty when the document opens with one, and the whole document when it has none.
    /// </summary>
    /// <remarks>
    /// A log's preamble describes the file, but a README's is the part a newcomer reads first, what the
    /// repository is (WSSETUP8; D124 §5). Found by the same walk, so a heading inside a fence ends neither.
    /// </remarks>
    public static string Preamble(string markdown, int level = 2) => Walk(markdown, level).Preamble.Body;

    /// <summary>
    /// <see cref="Preamble"/> as a section with no heading, with its lines (ORIENT2e): the part a README's
    /// reader titles itself.
    /// </summary>
    public static MarkdownSection PreambleAt(string markdown, int level = 2) => Walk(markdown, level).Preamble;

    /// <summary>
    /// The text of the document's first heading at any level, trimmed, or null when it has none: what a record
    /// in a folder is titled by (DOC8c; D134 §5).
    /// </summary>
    /// <remarks>
    /// Any level, since an ADR opens with <c>#</c> where a decision split out of a log keeps its <c>##</c>. Read
    /// as <see cref="Split"/> reads a heading: at the start of a line, hashes and a space, never inside a fence;
    /// and never inside a leading frontmatter block, where <c># …</c> is a comment among the fields. A heading
    /// with no words names nothing, so the next one is read.
    /// </remarks>
    public static string? FirstHeading(string? markdown)
    {
        var lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        var start = 0;
        if (lines[0] == "---")
        {
            var close = Array.IndexOf(lines, "---", 1);
            if (close > 0) start = close + 1;
        }

        var fence = new MarkdownFence();
        foreach (var raw in lines.Skip(start))
        {
            if (fence.Holds(raw)) continue;

            var hashes = raw.Length - raw.TrimStart('#').Length;
            if (hashes is < 1 or > 6 || raw.Length == hashes || raw[hashes] != ' ') continue;
            var heading = raw[hashes..].Trim();
            if (heading.Length > 0) return heading;
        }
        return null;
    }

    private static (MarkdownSection Preamble, List<MarkdownSection> Sections) Walk(string? markdown, int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        var marker = new string('#', level) + ' ';

        var preamble = new List<(int Line, string Text)>();
        var sections = new List<MarkdownSection>();
        string? heading = null;
        var body = new List<(int Line, string Text)>();
        var fence = new MarkdownFence();
        var line = 0;

        foreach (var raw in (markdown ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
        {
            line++;
            if (!fence.Holds(raw) && raw.StartsWith(marker, StringComparison.Ordinal))
            {
                if (heading is not null) sections.Add(Build(heading, body));
                heading = raw[marker.Length..].Trim();
                body.Clear();
                continue;
            }

            (heading is null ? preamble : body).Add((line, raw));
        }

        if (heading is not null) sections.Add(Build(heading, body));
        return (Build(string.Empty, preamble), sections);
    }

    /// <summary>
    /// A section from its lines: the text trimmed, and the lines it then is, from its first line that holds
    /// anything to its last, so line <c>i</c> of the body is line <c>First + i</c> of the text.
    /// </summary>
    internal static MarkdownSection Build(string heading, IReadOnlyList<(int Line, string Text)> lines)
    {
        var first = -1;
        var last = -1;
        for (var at = 0; at < lines.Count; at++)
        {
            if (lines[at].Text.Trim().Length == 0) continue;
            if (first < 0) first = at;
            last = at;
        }

        return first < 0
            ? new(heading, string.Empty)
            : new(heading, string.Join('\n', lines.Skip(first).Take(last - first + 1).Select(l => l.Text)).Trim(),
                lines[first].Line, lines[last].Line);
    }
}
