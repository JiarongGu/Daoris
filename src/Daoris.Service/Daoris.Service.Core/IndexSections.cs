namespace Daoris.Knowledge;

/// <summary>
/// One entry of a file of a repository's declared index: a run of prose under a heading, or one row of a table; where
/// it sits, its text, and the lines it is.
/// </summary>
/// <param name="Title">The headings above it and its own, joined by <c>›</c>, then a row's label; the file's path for text before any heading.</param>
/// <param name="Body">The text under its heading, trimmed, its heading left out; a row's line as written.</param>
/// <param name="Lines">The lines of the file the body is: line <c>i</c> of the body is line <c>First + i</c>.</param>
/// <param name="Opening">Whether it is prose before any heading: the file's own, which no heading names.</param>
/// <param name="Label">A row's label, its first cell with text and without its code marks; null for prose.</param>
public readonly record struct IndexSection(string Title, string Body, LineSpan Lines, bool Opening = false, string? Label = null);

/// <summary>
/// A file of a repository's declared index of where things are, split at its headings, each table row an entry of
/// its own (ORIENT2e, ORIENT2h; D151 §6, the orientation design §3.1).
/// </summary>
/// <remarks>
/// <para>At every heading, of every level, where a log splits at one level: an index is written to be looked up,
/// and its sections are the places a lookup lands, a lane's table or a bridge's routes, however deep the
/// generator put them. Each section is titled by the headings above it too, so a search for a file's title
/// finds its sections and a hit says where it sits.</para>
///
/// <para><b>A table is read a row at a time</b> (ORIENT2h): one row answers "where is X", and read as part of its
/// section a matching row is weighed against every row beside it. Measured on this repository's index, a route's
/// row in a 102-row section did not reach the first page for a question naming the route, below five short
/// sections (D151's ORIENT2e note), which is ORIENT1c's finding about sections met again. A row is titled by its
/// section's headings and its label, so the title's weight goes to the name the question asked for, and its body
/// is its line as written, so it names that line. The header and its separator are the table's, never a row and
/// never prose: a column's name is in every row of it and tells one from another in none. The prose around a
/// table stays a section, each run of it between tables an entry of its own, since an entry is one run of lines.</para>
///
/// <para>The headings and the table are read as <see cref="MarkdownSections"/> and <see cref="IndexRows"/> read
/// them, at the start of a line and never inside a fence. Each entry keeps its lines, so a hit names
/// <c>path:first-last</c> and a range reads part of it. A row in an index names lines in another file (an
/// outline's <c>412-417 test …</c>); the entry's lines are where the row sits in the index, and the body keeps the
/// row's own as written. A list stays in its section: an outline's item means nothing without the items above it,
/// and the line as written does not name them.</para>
///
/// <para>Any generator's markdown reads this way: the service parses headings, tables and text, never one format
/// every repository's generator must write (the design's §7).</para>
/// </remarks>
public static class IndexSections
{
    /// <summary>Every section with text and every table row with a label, in the order the file holds them.</summary>
    /// <param name="markdown">The file's text, LF.</param>
    /// <param name="fallbackTitle">What text before any heading is titled by: the file's path.</param>
    /// <param name="firstLine">The line of the file the text starts on, from 1, when a read trimmed lines above it.</param>
    public static IReadOnlyList<IndexSection> Read(string markdown, string fallbackTitle, int firstLine = 1)
    {
        var sections = new List<IndexSection>();
        var above = new List<(int Level, string Text)>();
        var title = fallbackTitle;
        var opening = true;
        var body = new List<(int Line, string Text)>();
        var inFence = false;
        var lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Split('\n');

        for (var at = 0; at < lines.Length; at++)
        {
            var raw = lines[at];
            var trimmed = raw.TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                inFence = !inFence;
            }
            else if (!inFence && Heading(raw) is { } heading)
            {
                Flush();
                opening = false;
                while (above.Count > 0 && above[^1].Level >= heading.Level) above.RemoveAt(above.Count - 1);
                above.Add(heading);
                title = string.Join(" › ", above.Select(h => h.Text).Where(text => text.Length > 0));
                if (title.Length == 0) title = fallbackTitle;
                continue;
            }
            else if (!inFence && TableAt(lines, at))
            {
                Flush();
                // Past the header and its separator, each line that opens with a pipe is a row, to the first that does not.
                for (at += 2; at < lines.Length && lines[at].TrimStart().StartsWith('|'); at++)
                {
                    if (Label(lines[at]) is not { } label) continue;
                    var line = firstLine + at;
                    sections.Add(new IndexSection($"{title} › {label}", lines[at].Trim(), new LineSpan(line, line), Label: label));
                }
                at--;   // the line that ended the table is read by the loop's own step
                continue;
            }

            body.Add((firstLine + at, raw));
        }

        Flush();
        return sections;

        // A heading with nothing under it before the next says nothing a section does not.
        void Flush()
        {
            var built = MarkdownSections.Build(title, body);
            if (built.Body.Length > 0) sections.Add(new IndexSection(title, built.Body, new LineSpan(built.First, built.Last), opening));
            body.Clear();
        }
    }

    /// <summary>A heading's level and text: one to six hashes at the line's start, then a space.</summary>
    private static (int Level, string Text)? Heading(string raw)
    {
        var hashes = raw.Length - raw.TrimStart('#').Length;
        return hashes is >= 1 and <= 6 && raw.Length > hashes && raw[hashes] == ' '
            ? (hashes, raw[hashes..].Trim())
            : null;
    }

    /// <summary>
    /// Whether a table's header opens at this line: a line that opens with a pipe, then a separator of dashes under
    /// each column. A pipe with no separator under it is prose, as markdown reads it.
    /// </summary>
    private static bool TableAt(string[] lines, int at)
    {
        if (at + 1 >= lines.Length) return false;
        if (!lines[at].TrimStart().StartsWith('|') || !lines[at + 1].TrimStart().StartsWith('|')) return false;
        var separator = IndexRows.Cells(lines[at + 1].Trim());
        return separator.Count > 0 && separator.All(IndexRows.IsRule);
    }

    /// <summary>
    /// What a row is about: its first cell with text, without its code marks, as a generated index puts the name it
    /// answers for first; null for a row with none, which names nothing.
    /// </summary>
    private static string? Label(string row) =>
        IndexRows.Cells(row.Trim()).Select(IndexRows.Plain).FirstOrDefault(cell => cell.Length > 0);
}
