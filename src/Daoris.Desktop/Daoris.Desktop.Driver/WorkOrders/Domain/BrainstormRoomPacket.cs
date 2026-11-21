using System.Globalization;
using System.Text;

namespace Daoris.Driver;

/// <summary>One file of a pass's room: its path from the room, slashes forward, and its text.</summary>
public sealed record RoomFile(string Path, string Text);

/// <summary>
/// A frozen packet as a pass reads it: an index, <c>index.md</c>, naming the question and each source's file with where
/// it came from, its size, its relevance and why it is in; and one Markdown file per source under <c>sources/</c>. A pass reads the index
/// and opens only the files it needs, never one large JSON. A source the search matched by words alone that holds under a third of the
/// question's terms is off the question: listed apart, after the others. Laid with no model and no I/O; the packet's record and its digest
/// stay the driver's <c>packet.json</c>, which no pass is handed.
/// </summary>
public static class BrainstormRoomPacket
{
    public const string IndexFile = "index.md";

    public const string SourcesFolder = "sources";

    /// <summary>The heading the sources off the question are listed under.</summary>
    public const string ApartHeading = "## Listed apart: matched by words alone, off the question";

    public static string SourceFile(string id) => $"{SourcesFolder}/{id}.md";

    /// <summary>The room's packet files: the index first, then each source's file in the packet's order.</summary>
    public static IReadOnlyList<RoomFile> Lay(BrainstormPacket packet)
    {
        var terms = BrainstormWordings.Terms(packet.Question);
        return [new RoomFile(IndexFile, Index(packet, terms)), .. packet.Sources.Select(source => new RoomFile(SourceFile(source.Id), Source(source, terms)))];
    }

    /// <summary>How many of the question's terms a source holds, in its text or its path, case ignored.</summary>
    public static int Matched(IReadOnlyList<string> terms, BrainstormSource source) =>
        terms.Count(term => source.Text.Contains(term, StringComparison.OrdinalIgnoreCase) || source.Path.Contains(term, StringComparison.OrdinalIgnoreCase));

    /// <summary>A source found by words alone that holds under a third of the question's terms; a question with no term finds none off it.</summary>
    public static bool OffQuestion(IReadOnlyList<string> terms, BrainstormSource source) =>
        source.Tier == BrainstormPackets.Words && terms.Count > 0 && Matched(terms, source) * 3 < terms.Count;

    private static string Index(BrainstormPacket packet, IReadOnlyList<string> terms)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"# Packet `{packet.Digest}`\n\n");
        text.Append("Daoris laid this brainstorm's packet out as files: this index, and one file per source under `")
            .Append(SourcesFolder).Append("/`. Read this index first, then open only the sources you need.\n\n");
        text.Append("## The question\n\n").Append(packet.Question.Trim()).Append("\n\n");
        text.Append(packet.Repository is { } repository
            ? $"Its sources are from `{repository}`{(packet.Tier is { } tier ? $", found by {tier}" : "")}.\n\n"
            : "It asks about no repository: the question is all there is.\n\n");

        var apart = packet.Sources.Where(source => OffQuestion(terms, source)).ToList();
        var kept = packet.Sources.Where(source => !OffQuestion(terms, source)).ToList();
        if (packet.Sources.Count > 0)
        {
            text.Append("## Sources\n\n");
            text.Append(terms.Count > 0
                ? $"Each source by its file, where it came from, its size and why it is in. Its relevance is how many of the question's {terms.Count} terms "
                  + $"({string.Join(", ", terms.Select(term => $"`{term}`"))}) it holds.\n\n"
                : "Each source by its file, where it came from, its size and why it is in.\n\n");
            Table(text, kept, terms);
            if (apart.Count > 0)
            {
                text.Append('\n').Append(ApartHeading).Append("\n\n")
                    .Append("The search found these by words alone, and each holds under a third of the question's terms. Open one only where the "
                            + "sources above leave you short.\n\n");
                Table(text, apart, terms);
            }

            text.Append('\n');
        }

        if (packet.LeftOut.Count > 0)
        {
            text.Append("## Left out\n\nFound and not in this packet, with why: `bound` past the packet's bounds, `machine-path` no repository's path.\n\n");
            foreach (var left in packet.LeftOut) text.Append(CultureInfo.InvariantCulture, $"- `{left.Path}`: {left.Why}\n");
        }

        return text.ToString();
    }

    private static void Table(StringBuilder text, IReadOnlyList<BrainstormSource> sources, IReadOnlyList<string> terms)
    {
        text.Append("| Source | File | From | Size | Relevance | Why it is in |\n|---|---|---|---|---|---|\n");
        foreach (var source in sources)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"| {source.Id} | `{SourceFile(source.Id)}` | {Cell(From(source))} | {Size(source)} | "
                + $"{(terms.Count > 0 ? $"{Matched(terms, source)} of {terms.Count} terms" : "not counted")} | {Cell(Why(source))} |\n");
        }
    }

    private static string Source(BrainstormSource source, IReadOnlyList<string> terms)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"# {source.Id}: {From(source)}\n\n");
        text.Append(CultureInfo.InvariantCulture, $"- Kind: {source.Kind}\n");
        if (source.Revision is { } revision) text.Append(CultureInfo.InvariantCulture, $"- Read at: `{revision}`\n");
        text.Append(CultureInfo.InvariantCulture, $"- Why it is in: {Why(source)}\n");
        if (terms.Count > 0) text.Append(CultureInfo.InvariantCulture, $"- Relevance: {Matched(terms, source)} of the question's {terms.Count} terms\n");
        text.Append(CultureInfo.InvariantCulture, $"- Size: {Size(source)}{(source.Kept < source.Of ? ", cut at the packet's bound" : "")}\n\n");
        text.Append("---\n\n").Append(source.Text);
        if (!source.Text.EndsWith('\n')) text.Append('\n');
        return text.ToString();
    }

    private static string From(BrainstormSource source) =>
        source.First is { } first ? $"`{source.Path}` lines {first}–{source.Last ?? first}" : $"`{source.Path}`";

    private static string Size(BrainstormSource source) =>
        string.Create(CultureInfo.InvariantCulture, $"{source.Kept:N0} of {source.Of:N0} characters");

    private static string Why(BrainstormSource source)
    {
        var how = source.Tier == BrainstormPackets.NamedTier ? "named by the asker"
            : source.Words.Length > 0 ? $"found by {source.Tier}, from the words \"{source.Words}\""
            : $"found by {source.Tier}";
        return source.Reason is { Length: > 0 } reason ? $"{reason} ({how})" : how;
    }

    /// <summary>A table cell's text on one line, its pipes escaped.</summary>
    private static string Cell(string text) => text.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal)
        .Replace("|", "\\|", StringComparison.Ordinal);
}
