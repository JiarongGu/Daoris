namespace Daoris.Knowledge;

/// <summary>A list item's line, read as <see cref="IndexRows.Item"/> reads it.</summary>
/// <param name="Indent">The spaces before its marker.</param>
/// <param name="Content">The column its text starts at: what a continuation or a nested item is indented to.</param>
/// <param name="Text">Its text on this line, trimmed.</param>
internal readonly record struct ListItem(int Indent, int Content, string Text);

/// <summary>One entry of a generated index: a table row, a list item, or the file's prose.</summary>
/// <param name="Title">The row's first cell or the item's text, without its code marks; the file's heading for its prose.</param>
/// <param name="Body">The row with each cell labelled by its column, or the item with the items above it; then where it sits.</param>
/// <param name="Anchor">Unique within the file (<see cref="EntryAnchors"/>), an item's past the lines it leads with (<see cref="IndexRows.Unnumbered"/>); null for the file's prose, which is the file itself.</param>
public readonly record struct IndexRow(string Title, string Body, string? Anchor);

/// <summary>
/// A generated index read a row at a time (ORIENT1c).
/// </summary>
/// <remarks>
/// <para>An index answers "where is X" with one row: the route and its handler, the verb and its file, a
/// method and its lines. Read as sections, as a document is, a search lands on a table of ninety-nine rows
/// and its excerpt shows whichever row held the query's first word. Read a row at a time, the hit is the
/// row, titled by what it is about, so the title's weight goes to the name the question asked for.</para>
///
/// <para>A row carries its column headers, since a cell means nothing without its column, and where it sits:
/// the file's title and the section's heading, so a search for a module's name finds its routes. A list item
/// carries the items above it, since an outline's method means nothing without its class. A heading, a
/// fence and the rows of a table are read as <see cref="MarkdownSections"/> reads them: at the start of a
/// line and never inside a fence, which closes as CommonMark closes one (<see cref="MarkdownFence"/>).</para>
/// </remarks>
public static class IndexRows
{
    /// <summary>The file's prose first, when it has any, then each row and item in the order they appear.</summary>
    /// <param name="markdown">The file's text.</param>
    /// <param name="fallbackTitle">What the prose is titled by when the file has no first-level heading.</param>
    public static IReadOnlyList<IndexRow> Read(string markdown, string fallbackTitle)
    {
        var rows = new List<IndexRow>();
        var prose = new List<string>();
        var anchors = new EntryAnchors();
        string? title = null;
        string? heading = null;
        List<string>? headers = null;
        var expectSeparator = false;
        var parents = new List<(int Indent, string Text)>();
        var fence = new MarkdownFence();

        string Where() => string.Join(" › ", new[] { title, heading }.OfType<string>().Distinct(StringComparer.Ordinal));

        // An item is anchored past the lines it leads with (ORIENT2h5), a row by its title as it always was.
        void Emit(string rowTitle, string line, string? anchored = null)
        {
            if (rowTitle.Length == 0) return;
            var where = Where();
            rows.Add(new IndexRow(
                rowTitle, where.Length == 0 ? line : $"{line}\n\nIn {where}",
                anchors.Next($"{heading ?? title ?? fallbackTitle}: {anchored ?? rowTitle}")));
        }

        foreach (var raw in (markdown ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = raw.Trim();
            // A fence's text is the file's prose, its tables and items included (ORIENT2h3: closed as CommonMark closes it).
            if (fence.Holds(raw))
            {
                prose.Add(raw);
                continue;
            }

            var hashes = raw.Length - raw.TrimStart('#').Length;
            if (hashes is >= 1 and <= 6 && raw.Length > hashes && raw[hashes] == ' ')
            {
                var text = raw[hashes..].Trim();
                if (hashes == 1 && title is null) title = text;
                else heading = text;
                headers = null;
                parents.Clear();
                continue;
            }

            if (trimmed.StartsWith('|'))
            {
                var cells = Cells(trimmed);
                if (headers is null)
                {
                    headers = cells;
                    expectSeparator = true;
                    continue;
                }
                if (expectSeparator)
                {
                    expectSeparator = false;
                    if (cells.All(IsRule)) continue;
                }

                var labelled = cells
                    .Select((cell, at) => (Header: at < headers.Count ? Plain(headers[at]) : string.Empty, Cell: cell))
                    .Where(pair => pair.Cell.Length > 0)
                    .Select(pair => pair.Header.Length > 0 ? $"{pair.Header}: {pair.Cell}" : pair.Cell);
                Emit(cells.Count > 0 ? Plain(cells[0]) : string.Empty, string.Join(" · ", labelled));
                continue;
            }
            headers = null;

            if (Item(raw) is { } item)
            {
                while (parents.Count > 0 && parents[^1].Indent >= item.Indent) parents.RemoveAt(parents.Count - 1);
                var above = parents.Select(parent => parent.Text).ToList();
                var itemTitle = Plain(item.Text);
                Emit(itemTitle, above.Count == 0 ? item.Text : $"{item.Text}\n\nUnder {string.Join(" › ", above)}", Unnumbered(itemTitle));
                parents.Add((item.Indent, item.Text));
                continue;
            }

            // A blank line keeps a list open, as markdown does; any other line ends it and is the file's prose.
            if (trimmed.Length == 0) continue;
            parents.Clear();
            prose.Add(raw);
        }

        var said = string.Join('\n', prose).Trim();
        if (said.Length > 0) rows.Insert(0, new IndexRow(title ?? fallbackTitle, said, null));
        return rows;
    }

    /// <summary>
    /// A list item's line: <c>-</c>, <c>*</c>, <c>+</c> or a number and a dot or a parenthesis, then a space, and text.
    /// A declared index's items are read by it too (<see cref="IndexSections"/>, ORIENT2h2), so an item is one item to
    /// both readers.
    /// </summary>
    /// <remarks>
    /// A thematic break, <c>- - -</c> or <c>* * *</c>, is no item, as markdown reads it. The content column is where
    /// the item's text starts: a line after a blank continues the item only when indented that far, and an item
    /// indented that far is nested in it (CommonMark's rule; five spaces or more after the marker are code, so the
    /// text starts one past it).
    /// </remarks>
    internal static ListItem? Item(string raw)
    {
        var indent = raw.Length - raw.TrimStart().Length;
        var rest = raw[indent..];
        if (ThematicBreak(rest)) return null;

        int marker;
        if (rest.Length > 1 && (rest[0] is '-' or '*' or '+') && rest[1] == ' ')
        {
            marker = 1;
        }
        else
        {
            var digits = rest.TakeWhile(char.IsDigit).Count();
            if (digits == 0 || rest.Length <= digits + 1 || rest[digits] is not ('.' or ')') || rest[digits + 1] != ' ') return null;
            marker = digits + 1;
        }

        var text = rest[marker..].Trim();
        if (text.Length == 0) return null;
        var spaces = rest.Length - marker - rest[marker..].TrimStart().Length;
        return new ListItem(indent, indent + marker + (spaces > 4 ? 1 : spaces), text);
    }

    /// <summary>Three or more of one of <c>-</c>, <c>*</c> or <c>_</c>, and nothing else but spaces.</summary>
    internal static bool ThematicBreak(string line)
    {
        var marks = line.Where(c => c is not (' ' or '\t')).ToList();
        return marks.Count >= 3 && marks[0] is ('-' or '*' or '_') && marks.All(c => c == marks[0]);
    }

    /// <summary>
    /// A table row's cells, trimmed, split at each pipe that is neither escaped nor inside code: an index's
    /// cells hold commands, and a command's pipe is not a column. A declared index's rows are read by it too
    /// (<see cref="IndexSections"/>, ORIENT2h), so a table is one table to both readers.
    /// </summary>
    internal static List<string> Cells(string row)
    {
        var cells = new List<string>();
        var cell = new System.Text.StringBuilder();
        var inCode = false;
        for (var i = 0; i < row.Length; i++)
        {
            var c = row[i];
            if (c == '\\' && i + 1 < row.Length && row[i + 1] == '|')
            {
                cell.Append(c).Append('|');
                i++;
                continue;
            }
            if (c == '`') inCode = !inCode;
            if (c == '|' && !inCode)
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
                continue;
            }
            cell.Append(c);
        }
        cells.Add(cell.ToString().Trim());

        // The pipes that open and close a row bound no cell.
        if (cells.Count > 0 && cells[0].Length == 0) cells.RemoveAt(0);
        if (cells.Count > 0 && cells[^1].Length == 0 && row.TrimEnd().EndsWith('|')) cells.RemoveAt(cells.Count - 1);
        return cells;
    }

    /// <summary>A separator cell: dashes, with a colon at either end for alignment.</summary>
    internal static bool IsRule(string cell) =>
        cell.Trim(':').Length > 0 && cell.Trim(':').All(c => c == '-');

    /// <summary>
    /// What an item is anchored by (ORIENT2h5): its label without the lines it leads with, where its first word is a
    /// line or a range of lines (<c>695-731 PublishAsync()</c>) or ends in one after a colon
    /// (<c>Tests.cs:284</c>, <c>D151:195-239 …</c>); the label as it is otherwise, and where nothing else would be left.
    /// </summary>
    /// <remarks>
    /// A generator puts first where a thing is, and an edit above it moves those lines while the thing stays what it
    /// is. Anchored on the lines, an item took a new id at every such edit, so a hit a session held named an entry the
    /// next refresh had replaced, and every outline's items were removed and added again. The title keeps them: a
    /// person reads the lines, and the id is only how an entry is found again. Digits are ASCII's, as a generator
    /// writes a line's number.
    /// </remarks>
    internal static string Unnumbered(string label)
    {
        var end = label.IndexOf(' ');
        var first = end < 0 ? label : label[..end];
        var colon = first.LastIndexOf(':');
        if (!IsLines(colon < 0 ? first : first[(colon + 1)..])) return label;

        var place = colon < 0 ? string.Empty : first[..colon];
        var rest = end < 0 ? string.Empty : label[(end + 1)..].TrimStart();
        var anchored = place.Length == 0 ? rest : rest.Length == 0 ? place : $"{place} {rest}";
        return anchored.Length > 0 ? anchored : label;
    }

    /// <summary>A line's number or a range of them: ASCII digits, then optionally a dash and more.</summary>
    private static bool IsLines(string text)
    {
        var dash = text.IndexOf('-');
        return dash < 0 ? Digits(text) : Digits(text[..dash]) && Digits(text[(dash + 1)..]);

        static bool Digits(string run) => run.Length > 0 && run.All(char.IsAsciiDigit);
    }

    /// <summary>Text without the marks that dress it — code's backticks and emphasis's doubled stars — for a title.</summary>
    internal static string Plain(string text) =>
        text.Replace("`", string.Empty).Replace("**", string.Empty).Trim();
}
