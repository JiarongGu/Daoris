namespace Daoris.Knowledge;

/// <summary>One heading and the text beneath it, up to the next heading of the same level.</summary>
/// <param name="Heading">The heading text, with its leading hashes and whitespace removed.</param>
/// <param name="Body">Everything under the heading, trimmed.</param>
public readonly record struct MarkdownSection(string Heading, string Body);

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
    /// such a fence, so the naive version fails on the first real input.
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
    public static string Preamble(string markdown, int level = 2) => Walk(markdown, level).Preamble;

    private static (string Preamble, List<MarkdownSection> Sections) Walk(string? markdown, int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        var marker = new string('#', level) + ' ';

        var preamble = new List<string>();
        var sections = new List<MarkdownSection>();
        string? heading = null;
        var body = new List<string>();
        var inFence = false;

        foreach (var raw in (markdown ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = raw.TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                inFence = !inFence;
            }
            else if (!inFence && raw.StartsWith(marker, StringComparison.Ordinal))
            {
                if (heading is not null) sections.Add(Build(heading, body));
                heading = raw[marker.Length..].Trim();
                body.Clear();
                continue;
            }

            (heading is null ? preamble : body).Add(raw);
        }

        if (heading is not null) sections.Add(Build(heading, body));
        return (string.Join('\n', preamble).Trim(), sections);
    }

    private static MarkdownSection Build(string heading, List<string> body) =>
        new(heading, string.Join('\n', body).Trim());
}
