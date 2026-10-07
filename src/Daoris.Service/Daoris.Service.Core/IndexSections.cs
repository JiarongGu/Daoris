namespace Daoris.Knowledge;

/// <summary>One section of a file of a repository's declared index: where it sits, its text, and the lines it is.</summary>
/// <param name="Title">The headings above it and its own, joined by <c>›</c>; the file's path for text before any heading.</param>
/// <param name="Body">The text under its heading, trimmed, its heading left out.</param>
/// <param name="Lines">The lines of the file the body is: line <c>i</c> of the body is line <c>First + i</c>.</param>
/// <param name="Opening">Whether it is the text before any heading: the file's own, which no heading names.</param>
public readonly record struct IndexSection(string Title, string Body, LineSpan Lines, bool Opening = false);

/// <summary>
/// A file of a repository's declared index of where things are, split at its headings (ORIENT2e; D151 §6, the
/// orientation design §3.1).
/// </summary>
/// <remarks>
/// <para>At every heading, of every level, where a log splits at one level: an index is written to be looked up,
/// and its sections are the places a lookup lands, a lane's table or a bridge's routes, however deep the
/// generator put them. Each section is titled by the headings above it too, so a search for a file's title
/// finds its sections and a hit says where it sits.</para>
///
/// <para>The headings are read as <see cref="MarkdownSections"/> reads them, at the start of a line and never
/// inside a fence. Each section keeps its lines, so a hit names <c>path:first-last</c> and a range reads part
/// of it. A row in an index names lines in another file (an outline's <c>412-417 test …</c>); the section's
/// lines are where the row sits in the index, and the body keeps the row's own as written.</para>
///
/// <para>Any generator's markdown reads this way: the service parses headings and text, never one format every
/// repository's generator must write (the design's §7).</para>
/// </remarks>
public static class IndexSections
{
    /// <summary>Every section with text, in the order the file holds them.</summary>
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
        var line = firstLine - 1;

        foreach (var raw in (markdown ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
        {
            line++;
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

            body.Add((line, raw));
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
}
