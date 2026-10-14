namespace Daoris.Knowledge;

/// <summary>
/// The doctrine region — where the always-loaded tier lives since D59, read from the file the
/// repository owns rather than from a directory of its own.
/// </summary>
/// <remarks>
/// <para><b>This is a TWIN of the CLI's <c>region.ts</c>, and the two share no code.</b> The FILE and
/// the LAYOUT are the contract, exactly as they are for the harness profiles: one side writes the
/// markers and the provenance lines, the other reads them, and neither can see the other's types.</para>
///
/// <para>🔴 <b>Why it exists at all.</b> `.claude/rules/` was read by exactly one of the three
/// harnesses this family drives, so the tier moved into `AGENTS.md`. The scanner kept reading the
/// directory, which was now empty, and <b>the eight canonical rules fell out of the index</b> — a
/// search for one returned only a repository's own local rule. Cross-repository search and
/// convergence are what this service is for, and they had lost the always-loaded tier silently,
/// because nothing breaks at compile time when the other artefact changes a layout.</para>
///
/// <para>Deliberately forgiving in one direction: anything it cannot make sense of yields nothing,
/// never an exception. A malformed region in one repository must not cost the whole corpus, which is
/// the same reasoning <see cref="DaorisLock"/> reads its own file with.</para>
/// </remarks>
public static class DoctrineRegion
{
    private const string Open = "<!-- daoris:rules";
    private const string Close = "<!-- /daoris:rules -->";
    private const string Provenance = "<!-- daoris: ";

    /// <summary>
    /// Every rule inside the region of <paramref name="file"/>, by the source its provenance names.
    /// </summary>
    /// <returns>
    /// Source path to body, empty when the file has no region. The source is <c>pack/source</c>,
    /// spelled as the provenance line spells it, which is what a lock entry's own fields reconstruct.
    /// </returns>
    public static IReadOnlyDictionary<string, string> Read(string file) =>
        ReadAt(file).ToDictionary(rule => rule.Key, rule => rule.Value.Body, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// <see cref="Read"/>, with the lines of the file each rule's body is (ORIENT2e): from its first line that
    /// holds anything after its provenance line to its last before the next.
    /// </summary>
    public static IReadOnlyDictionary<string, (string Body, LineSpan? Lines)> ReadAt(string file)
    {
        var empty = new Dictionary<string, (string Body, LineSpan? Lines)>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(file)) return empty;

        string[] lines;
        int top;
        try
        {
            // Through the same read boundary every document goes through, so a CRLF checkout and an
            // LF one index the same bytes — the property `Text.ReadDocument` exists to make true.
            var read = Text.ReadDocumentAt(file);
            lines = read.Text.Split('\n');
            top = read.First;
        }
        catch (IOException)
        {
            return empty;
        }

        // Whole lines, never a substring: a marker quoted inside a fenced code block — which this
        // family's own documentation contains — is prose, not a boundary.
        var start = Array.FindIndex(lines, line => line.StartsWith(Open, StringComparison.Ordinal));
        if (start < 0) return empty;

        var end = Array.FindIndex(lines, start + 1, line => line.Trim() == Close);
        if (end < 0) return empty;

        var rules = new Dictionary<string, (string Body, LineSpan? Lines)>(StringComparer.OrdinalIgnoreCase);
        string? current = null;
        var body = new List<(int Line, string Text)>();

        for (var at = start + 1; at < end; at++)
        {
            var line = lines[at];
            if (line.StartsWith(Provenance, StringComparison.Ordinal))
            {
                Flush();
                current = SourceOf(line);
                continue;
            }

            if (current is not null) body.Add((top + at, line));
        }

        Flush();
        return rules;

        void Flush()
        {
            if (current is not null && body.Count > 0)
            {
                var rule = MarkdownSections.Build(current, body);
                rules[current] = (rule.Body, rule.First > 0 ? new LineSpan(rule.First, rule.Last) : null);
            }
            body.Clear();
        }
    }

    /// <summary>
    /// The <c>pack/source</c> a provenance line names — the text between the marker and the ` @ `.
    /// </summary>
    private static string? SourceOf(string line)
    {
        var from = Provenance.Length;
        var to = line.IndexOf(" @ ", from, StringComparison.Ordinal);
        return to > from ? line[from..to].Trim() : null;
    }
}
