using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

/// <summary>One dated note under a decision, as the decisions digest gives it (ORIENT1g).</summary>
/// <param name="Label">The emphasised span that opens it, or its dated heading, cut to a row: the digest's label.</param>
/// <param name="First">Its first line in the file, from 1.</param>
/// <param name="Last">Its last line, the blank lines before the next note left out.</param>
/// <param name="Body">Its lines, its label's line first, so the label is searched in full.</param>
public sealed record DecisionNote(string Label, int First, int Last, string Body);

/// <summary>
/// A decision split at its dated notes (ORIENT1g; D134 §5 as amended): the decision's own text, then each note
/// appended under it, found and labelled as <c>tools/orient-index.mjs</c> finds and labels them for the decisions
/// digest, so a hit and the digest's row name a note alike.
/// </summary>
/// <remarks>
/// <para>🔴 <b>One entry per decision buried its notes.</b> D125 is one file and twenty-two dated notes; asked
/// what decided the probe lock, the search could not land on the TOOL6g note that did, because no entry was
/// that note (D24's ORIENT1c note). The digest already lists every note with its lines; the index now holds each
/// as an entry of its own.</para>
///
/// <para><b>A note</b> starts at a line after the decision's opening paragraph, outside a fence (read as markdown
/// reads one, <see cref="MarkdownFence"/>), after a blank
/// line, that is a <c>###</c> heading with a date in it, or a line that opens with an emphasised span which is
/// dated or in a form the record writes undated (<see cref="Label"/>, the digest's <c>noteLabel</c> and
/// <c>doc-duplicates</c>' <c>isNoteLabel</c>). It runs to the line before the next note. The opening is never a
/// note, dated or not (<c>**Decision (DOC8, …)**</c>). A label glued to the line above is that line's text, as
/// the digest reads it, and the record's own check refuses it.</para>
///
/// <para>A twin of the digest's reader with no code shared: both are held to one table,
/// <c>tools/orient-index-fixtures/decision-notes.json</c>, row for row (ORIENT1h), this one by
/// <c>DecisionNotesTests</c> and the digest by <c>tools/orient-index.test.mjs</c>.</para>
/// </remarks>
public static partial class DecisionNotes
{
    /// <summary>How long a label may be before it is cut at a word, as the digest cuts a row.</summary>
    private const int LabelLength = 80;

    /// <summary>The decision's own text before its first note, and each note.</summary>
    public static (string Entry, IReadOnlyList<DecisionNote> Notes) Split(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var inFence = Fenced(lines);
        var heading = FirstHeading(lines, inFence);
        var opening = -1;
        for (var i = heading + 1; i < lines.Length; i++)
        {
            if (lines[i].Trim().Length == 0) continue;
            opening = i;
            break;
        }

        var starts = new List<(int Line, string Label)>();
        for (var i = opening + 1; opening >= 0 && i < lines.Length; i++)
        {
            if (inFence[i] || (i > 0 && lines[i - 1].Trim().Length > 0)) continue;
            var dated = DatedHeading().Match(lines[i]);
            var label = dated.Success && IsoDate().IsMatch(dated.Groups[1].Value)
                ? Truncate(dated.Groups[1].Value)
                : Label(lines[i]);
            if (label is not null) starts.Add((i, label));
        }

        if (starts.Count == 0) return (text, []);

        var notes = new List<DecisionNote>();
        for (var k = 0; k < starts.Count; k++)
        {
            var from = starts[k].Line;
            var end = Until(lines, from, k + 1 < starts.Count ? starts[k + 1].Line : lines.Length);
            notes.Add(new DecisionNote(starts[k].Label, from + 1, end, string.Join('\n', lines[from..end])));
        }

        var entryEnd = Until(lines, Math.Max(heading, 0), starts[0].Line);
        return (string.Join('\n', lines[..entryEnd]), notes);
    }

    /// <summary>
    /// A note's label: the emphasised span that opens the line, bold or italic, when it holds a date or the line
    /// is in one of the forms the record writes undated, cut to a row; null for any other line.
    /// </summary>
    public static string? Label(string line)
    {
        string? span = null;
        if (line.StartsWith("**", StringComparison.Ordinal))
        {
            var end = line.IndexOf("**", 2, StringComparison.Ordinal);
            span = end > 0 ? line[2..end] : line[2..];
        }
        else if (line.StartsWith('*') && (line.Length < 2 || line[1] != ' '))
        {
            var end = -1;
            for (var k = 1; k < line.Length; k++)
            {
                if (line[k] != '*') continue;
                if ((k + 1 < line.Length && line[k + 1] == '*') || line[k - 1] == '*') continue;
                end = k;
                break;
            }
            span = end > 0 ? line[1..end] : line[1..];
        }

        if (string.IsNullOrWhiteSpace(span)) return null;
        if (!IsoDate().IsMatch(span) && !NoteForms.Any(form => form.IsMatch(line))) return null;
        return Truncate(span);
    }

    /// <summary>
    /// The forms the record writes a note's label in (D134 §3.4, <c>doc-duplicates</c>' <c>NOTE_LABEL</c>): the
    /// commonest words with or without a date, a rarer word only with one, and a note named by its task first.
    /// </summary>
    private static readonly Regex[] NoteForms =
    [
        CommonBold(),
        CommonItalic(),
        RarerDated(),
        TaskFirst(),
    ];

    [GeneratedRegex(@"^\*\*(As built|Built|Fixed|Read|Amended)\b", RegexOptions.ECMAScript)]
    private static partial Regex CommonBold();

    [GeneratedRegex(@"^\*(As built|Built|Amended)\b", RegexOptions.ECMAScript)]
    private static partial Regex CommonItalic();

    [GeneratedRegex(
        @"^\*\*?(Corrected|Measured|Landed|Proved|Proven|Probed|Reviewed|Superseded|Noted|Note|Revised|Decided|Extended|Accepted)\b.*[0-9]{4}-[0-9]{2}-[0-9]{2}",
        RegexOptions.ECMAScript)]
    private static partial Regex RarerDated();

    [GeneratedRegex(@"^\*\*[^*]*\b(built|landed|amended)\b[^*]*[0-9]{4}-[0-9]{2}-[0-9]{2}", RegexOptions.ECMAScript)]
    private static partial Regex TaskFirst();

    [GeneratedRegex(@"[0-9]{4}-[0-9]{2}-[0-9]{2}")]
    private static partial Regex IsoDate();

    [GeneratedRegex(@"^###\s+(.+)$", RegexOptions.ECMAScript)]
    private static partial Regex DatedHeading();

    /// <summary>Text cut at a word before <see cref="LabelLength"/> characters, with an ellipsis, as the digest cuts a row.</summary>
    private static string Truncate(string text)
    {
        var flat = Whitespace().Replace(text, " ").Trim();
        if (flat.Length <= LabelLength) return flat;
        var cut = flat[..LabelLength];
        var space = cut.LastIndexOf(' ');
        return (space > LabelLength / 2 ? cut[..space] : cut).TrimEnd(' ', '\t', '\n', ',', ';', ':') + "…";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>Where a part that starts at <paramref name="from"/> ends (exclusive), blank lines at its end left out.</summary>
    private static int Until(string[] lines, int from, int next)
    {
        var end = next - 1;
        while (end > from && lines[end].Trim().Length == 0) end--;
        return end + 1;
    }

    /// <summary>
    /// Whether a fenced block holds each line, its fences too, read as markdown reads a fence by the service's one
    /// rule, <see cref="MarkdownFence"/> (ORIENT2h4). The digest's <c>doc-duplicates</c> <c>fenced</c> reads it by
    /// the same rule with code of its own, and the twin table holds the two together: a four-backtick fence quoting
    /// a label, a tilde fence, a backtick run that is code inline and a run that closes nothing.
    /// </summary>
    /// <remarks>Toggling on any line opening with three backticks closed a longer fence on the example it quoted,
    /// so a label quoted inside split a note in two.</remarks>
    private static bool[] Fenced(string[] lines)
    {
        var fence = new MarkdownFence();
        var inFence = new bool[lines.Length];
        for (var i = 0; i < lines.Length; i++) inFence[i] = fence.Holds(lines[i]);
        return inFence;
    }

    /// <summary>The line of the file's first heading outside a fence, or -1: the decision's own, where it opens.</summary>
    private static int FirstHeading(string[] lines, bool[] inFence)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            if (inFence[i]) continue;
            var hashes = lines[i].Length - lines[i].TrimStart('#').Length;
            if (hashes is >= 1 and <= 6 && lines[i].Length > hashes && lines[i][hashes] == ' ') return i;
        }
        return -1;
    }
}
