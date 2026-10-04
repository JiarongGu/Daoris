namespace Daoris.Knowledge;

/// <summary>One entry of a generated index: a table row, a list item, or the file's prose.</summary>
/// <param name="Title">The row's first cell or the item's text, without its code marks; the file's heading for its prose.</param>
/// <param name="Body">The row with each cell labelled by its column, or the item with the items above it; then where it sits.</param>
/// <param name="Anchor">Unique within the file; null for the file's prose, which is the file itself.</param>
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
/// line and never inside a fence.</para>
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
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        string? title = null;
        string? heading = null;
        List<string>? headers = null;
        var expectSeparator = false;
        var parents = new List<(int Indent, string Text)>();
        var inFence = false;

        string Where() => string.Join(" › ", new[] { title, heading }.OfType<string>().Distinct(StringComparer.Ordinal));

        void Emit(string rowTitle, string line)
        {
            if (rowTitle.Length == 0) return;
            var key = $"{heading ?? title ?? fallbackTitle}: {rowTitle}";
            var seen = used.GetValueOrDefault(key) + 1;
            used[key] = seen;
            var where = Where();
            rows.Add(new IndexRow(rowTitle, where.Length == 0 ? line : $"{line}\n\nIn {where}", seen == 1 ? key : $"{key} ({seen})"));
        }

        foreach (var raw in (markdown ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = raw.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                inFence = !inFence;
                prose.Add(raw);
                continue;
            }
            if (inFence)
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
                Emit(Plain(item.Text), above.Count == 0 ? item.Text : $"{item.Text}\n\nUnder {string.Join(" › ", above)}");
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

    /// <summary>A list item's indent and text: <c>-</c>, <c>*</c>, <c>+</c> or a number and a dot, then a space.</summary>
    private static (int Indent, string Text)? Item(string raw)
    {
        var indent = raw.Length - raw.TrimStart().Length;
        var rest = raw[indent..];
        int marker;
        if (rest.Length > 1 && (rest[0] is '-' or '*' or '+') && rest[1] == ' ')
        {
            marker = 1;
        }
        else
        {
            var digits = rest.TakeWhile(char.IsDigit).Count();
            if (digits == 0 || rest.Length <= digits + 1 || rest[digits] != '.' || rest[digits + 1] != ' ') return null;
            marker = digits + 1;
        }

        var text = rest[marker..].Trim();
        return text.Length == 0 ? null : (indent, text);
    }

    /// <summary>
    /// A table row's cells, trimmed, split at each pipe that is neither escaped nor inside code: an index's
    /// cells hold commands, and a command's pipe is not a column.
    /// </summary>
    private static List<string> Cells(string row)
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
    private static bool IsRule(string cell) =>
        cell.Trim(':').Length > 0 && cell.Trim(':').All(c => c == '-');

    /// <summary>Text without the marks that dress it — code's backticks and emphasis's doubled stars — for a title.</summary>
    private static string Plain(string text) =>
        text.Replace("`", string.Empty).Replace("**", string.Empty).Trim();
}
