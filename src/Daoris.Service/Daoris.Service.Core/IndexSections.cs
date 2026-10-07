namespace Daoris.Knowledge;

/// <summary>
/// One entry of a file of a repository's declared index: a run of prose under a heading, or one row of a table; where
/// it sits, its text, and the lines it is.
/// </summary>
/// <param name="Title">The headings above it and its own, joined by <c>›</c>, then a row's label named by its column (<c>Route: STATE</c>); the file's path for text before any heading.</param>
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
/// section's headings and its label, named by its column as ORIENT1c names a cell
/// (<c>Bridge routes › DAORIS.DRIVER (102) › Route: SESSION_GO_ON_NEW</c>), so the title's weight goes to the name
/// the question asked for and to what the name is: the header is no line of the row, and without the column's
/// name <i>sync verb</i> did not reach the <c>sync</c> row when measured. Its body is its line as written, so it
/// names that line. The header and its separator are no entry: a column's name tells no row from another. The
/// prose around a table stays a section, each run of it between tables an entry of its own, since an entry is one
/// run of lines.</para>
///
/// <para>The headings and the table are read as <see cref="MarkdownSections"/> and <see cref="IndexRows"/> read
/// them, at the start of a line and never inside a fence, which closes as CommonMark closes one
/// (<see cref="MarkdownFence"/>, ORIENT2h3). Each entry keeps its lines, so a hit names
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
        var fence = new MarkdownFence();
        var lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Split('\n');

        for (var at = 0; at < lines.Length; at++)
        {
            var raw = lines[at];
            // A fence's text is its section's (ORIENT2h3: closed as CommonMark closes it). A row opens with a pipe and a
            // fence never does, so the rows a table reads past below leave the fence as it was.
            if (fence.Holds(raw))
            {
                body.Add((firstLine + at, raw));
                continue;
            }

            if (Heading(raw) is { } heading)
            {
                Flush();
                opening = false;
                while (above.Count > 0 && above[^1].Level >= heading.Level) above.RemoveAt(above.Count - 1);
                above.Add(heading);
                title = string.Join(" › ", above.Select(h => h.Text).Where(text => text.Length > 0));
                if (title.Length == 0) title = fallbackTitle;
                continue;
            }
            if (TableAt(lines, at))
            {
                Flush();
                var columns = IndexRows.Cells(lines[at].Trim()).Select(IndexRows.Plain).ToList();
                // Past the header and its separator, each line that opens with a pipe is a row, to the first that does not.
                for (at += 2; at < lines.Length && lines[at].TrimStart().StartsWith('|'); at++)
                {
                    if (Label(lines[at]) is not { } label) continue;
                    // Named by its column (ORIENT2h): the header is no line of the row, and a question names what a row is.
                    var column = label.Column < columns.Count ? columns[label.Column] : string.Empty;
                    var named = column.Length > 0 ? $"{column}: {label.Text}" : label.Text;
                    var line = firstLine + at;
                    sections.Add(new IndexSection($"{title} › {named}", lines[at].Trim(), new LineSpan(line, line), Label: label.Text));
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
    /// What a row is about, and the column it is in: its first cell with text, without its code marks, as a generated
    /// index puts the name it answers for first; null for a row with none, which names nothing.
    /// </summary>
    private static (int Column, string Text)? Label(string row)
    {
        var cells = IndexRows.Cells(row.Trim());
        for (var column = 0; column < cells.Count; column++)
        {
            var text = IndexRows.Plain(cells[column]);
            if (text.Length > 0) return (column, text);
        }
        return null;
    }
}
