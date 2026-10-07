namespace Daoris.Knowledge;

/// <summary>
/// One entry of a file of a repository's declared index: a run of prose under a heading, one row of a table, or one
/// list item; where it sits, its text, and the lines it is.
/// </summary>
/// <param name="Title">The headings above it and its own, joined by <c>›</c>, then a row's label named by its column (<c>Route: STATE</c>), or the labels of an item's parents and its own; the file's path for text before any heading.</param>
/// <param name="Body">The text under its heading, trimmed, its heading left out; a row's line as written; an item's lines as written, its nested items left out.</param>
/// <param name="Lines">The lines of the file the body is: line <c>i</c> of the body is line <c>First + i</c>.</param>
/// <param name="Opening">Whether it is prose before any heading: the file's own, which no heading names.</param>
/// <param name="Label">A row's label, its first cell with text, or an item's, each without its code marks; null for prose.</param>
public readonly record struct IndexSection(string Title, string Body, LineSpan Lines, bool Opening = false, string? Label = null);

/// <summary>
/// A file of a repository's declared index of where things are, split at its headings, each table row and each list
/// item an entry of its own (ORIENT2e, ORIENT2h, ORIENT2h2; D151 §6, the orientation design §3.1).
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
/// names that line. The header and its separator are no entry: a column's name tells no row from another.</para>
///
/// <para><b>A list is read an item at a time</b> (ORIENT2h2), for the same reason: read as its section, a fixture's
/// item was answered by the 61 lines around it. An item is its line and its continuation lines, as markdown reads
/// them: a line with no blank before it continues the item it follows, and after a blank only a line indented to
/// the item's text does. A nested item is an entry of its own, titled by its parents' labels as well as the
/// headings, so read alone it keeps its place: an outline's method names its class, which its line as written does
/// not. An item's label is its leading code span or link text where it opens with one, as a generated index puts
/// the place it answers for first (<c>`AnswerContinuesTickTests.cs:284` class ParkStandIn</c>), and its text
/// otherwise (<c>9-12 Widget()</c>). It is not named as a row's is: a list has no header, and what its items are
/// is said by the headings above them, which the title carries. Text of an item after its nested items is a second
/// run of it, titled as the item is.</para>
///
/// <para>The prose around a table or a list stays a section, each run of it between them an entry of its own,
/// since an entry is one run of lines. The headings, the table and the items are read as
/// <see cref="MarkdownSections"/> and <see cref="IndexRows"/> read them, at the start of a line and never inside a
/// fence, which closes as CommonMark closes one (<see cref="MarkdownFence"/>, ORIENT2h3); a fence indented into an
/// item is the item's. Each entry keeps its lines, so a hit names <c>path:first-last</c> and a range reads part of
/// it. A row or an item in an index names lines in another file (an outline's <c>412-417 test …</c>); the entry's
/// lines are where it sits in the index, and the body keeps the lines it names as written.</para>
///
/// <para>Any generator's markdown reads this way: the service parses headings, tables, lists and text, never one
/// format every repository's generator must write (the design's §7).</para>
/// </remarks>
public static class IndexSections
{
    /// <summary>Every section with text, every table row with a label and every list item, in the order the file holds them.</summary>
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
        // The list's open items, outermost first; only the innermost has lines not yet an entry (ORIENT2h2).
        var items = new List<OpenItem>();
        // Blank lines inside a list, which belong to the item the next line continues, or to none.
        var blanks = new List<(int Line, string Text)>();
        List<(int Line, string Text)> fenced = body;
        var fence = new MarkdownFence();
        var lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Split('\n');

        for (var at = 0; at < lines.Length; at++)
        {
            var raw = lines[at];
            var line = firstLine + at;
            // A fence's text is its section's, or the item's its opening line is indented into (ORIENT2h3: closed as
            // CommonMark closes it). A row opens with a pipe and a fence never does, so the rows a table reads past
            // below leave the fence as it was.
            var inside = fence.Open;
            if (fence.Holds(raw))
            {
                if (!inside) fenced = Continued(Indent(raw)) ?? body;
                fenced.Add((line, raw));
                continue;
            }

            if (Heading(raw) is { } heading)
            {
                EndList();
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
                EndList();
                Flush();
                var columns = IndexRows.Cells(lines[at].Trim()).Select(IndexRows.Plain).ToList();
                // Past the header and its separator, each line that opens with a pipe is a row, to the first that does not.
                for (at += 2; at < lines.Length && lines[at].TrimStart().StartsWith('|'); at++)
                {
                    if (Label(lines[at]) is not { } label) continue;
                    // Named by its column (ORIENT2h): the header is no line of the row, and a question names what a row is.
                    var column = label.Column < columns.Count ? columns[label.Column] : string.Empty;
                    var named = column.Length > 0 ? $"{column}: {label.Text}" : label.Text;
                    var row = firstLine + at;
                    sections.Add(new IndexSection($"{title} › {named}", lines[at].Trim(), new LineSpan(row, row), Label: label.Text));
                }
                at--;   // the line that ended the table is read by the loop's own step
                continue;
            }

            if (IndexRows.Item(raw) is { } item)
            {
                // The prose before a list is a run of its own; an item closes every item it is not indented into, and
                // its parent's lines so far are the parent's entry, since an entry is one run of lines.
                if (items.Count == 0) Flush();
                while (items.Count > 0 && item.Indent < items[^1].Content) Close();
                var parent = items.Count > 0 ? items[^1] : null;
                if (parent is not null) Emit(parent);
                blanks.Clear();
                var label = ItemLabel(item.Text);
                items.Add(new OpenItem(item.Content, $"{parent?.Title ?? title} › {label}", label, [(line, raw)]));
                continue;
            }

            if (items.Count == 0)
            {
                body.Add((line, raw));
                continue;
            }
            if (raw.Trim().Length == 0)
            {
                blanks.Add((line, raw));
                continue;
            }
            if (IndexRows.ThematicBreak(raw))
            {
                EndList();
                body.Add((line, raw));
                continue;
            }
            // With no blank before it, a line continues the innermost item, lazily as markdown has it; after one, the
            // item its indent reaches, and a line indented into none ends the list and is the section's prose.
            var receiver = blanks.Count == 0 ? items[^1].Lines : Continued(Indent(raw));
            (receiver ?? body).Add((line, raw));
        }

        EndList();
        Flush();
        return sections;

        // A heading with nothing under it before the next says nothing a section does not.
        void Flush()
        {
            var built = MarkdownSections.Build(title, body);
            if (built.Body.Length > 0) sections.Add(new IndexSection(title, built.Body, new LineSpan(built.First, built.Last), opening));
            body.Clear();
        }

        // An item's lines not yet an entry become one, titled by the item.
        void Emit(OpenItem open)
        {
            var built = MarkdownSections.Build(open.Title, open.Lines);
            if (built.Body.Length > 0) sections.Add(new IndexSection(open.Title, built.Body, new LineSpan(built.First, built.Last), Label: open.Label));
            open.Lines.Clear();
        }

        void Close()
        {
            Emit(items[^1]);
            items.RemoveAt(items.Count - 1);
        }

        void EndList()
        {
            while (items.Count > 0) Close();
            blanks.Clear();
        }

        // The lines of the innermost item a line indented this far is inside, the blanks before it with it; null, the
        // list ended, where it is inside none.
        List<(int Line, string Text)>? Continued(int indent)
        {
            while (items.Count > 0 && items[^1].Content > indent) Close();
            if (items.Count == 0)
            {
                blanks.Clear();
                return null;
            }
            items[^1].Lines.AddRange(blanks);
            blanks.Clear();
            return items[^1].Lines;
        }
    }

    /// <summary>An item the list holds open: the column its text starts at, its title and label, and its lines not yet an entry.</summary>
    private sealed record OpenItem(int Content, string Title, string Label, List<(int Line, string Text)> Lines);

    private static int Indent(string raw) => raw.Length - raw.TrimStart().Length;

    /// <summary>
    /// What an item is about (ORIENT2h2): its leading code span or link text, without its code marks, where it opens
    /// with one, as a generated index puts the place it answers for first; its text otherwise, as ORIENT1c titles an
    /// item.
    /// </summary>
    internal static string ItemLabel(string text)
    {
        var lead = text.StartsWith('`') ? LeadingCode(text) : text.StartsWith('[') ? LeadingLink(text) : null;
        return lead is { Length: > 0 } ? lead : IndexRows.Plain(text);
    }

    /// <summary>A code span the text opens with, closed by a run of as many backticks: its content, trimmed.</summary>
    private static string? LeadingCode(string text)
    {
        var run = text.TakeWhile(c => c == '`').Count();
        for (var at = run; at < text.Length; at++)
        {
            if (text[at] != '`') continue;
            var length = text[at..].TakeWhile(c => c == '`').Count();
            if (length == run) return text[run..at].Trim();
            at += length - 1;
        }
        return null;
    }

    /// <summary>A link the text opens with, <c>[text](target)</c> or <c>[text][name]</c>: its text, without its code marks.</summary>
    private static string? LeadingLink(string text)
    {
        var depth = 0;
        var code = false;
        for (var at = 0; at < text.Length; at++)
        {
            var c = text[at];
            if (c == '`') code = !code;
            if (code) continue;
            if (c == '[') depth++;
            if (c != ']' || --depth > 0) continue;
            return at + 1 < text.Length && text[at + 1] is '(' or '[' ? IndexRows.Plain(text[1..at]) : null;
        }
        return null;
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
