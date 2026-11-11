using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

/// <summary>
/// A question about how something works now is searched in the present first, and in the rest after it: on a long-lived
/// repository the decisions, fixes, finished tasks and dated designs sharing a question's words outnumber the one rule
/// that answers it, and bury it.
/// </summary>
/// <remarks>
/// A question asks about history when it names a decision (a D and its number) or a row (capitals and a number, a
/// part's letters after), or uses a word only history answers (<i>why</i>, <i>decided</i>, <i>root cause</i>,
/// <i>when</i> as its question). It, and a search naming its kinds, is ranked as the words rank it.
/// </remarks>
public static class PresentFirst
{
    /// <summary>The places a present question keeps at its answer's end for the rest, when the rest matched.</summary>
    public const int RestKept = 3;

    /// <summary>The folder name, anywhere on a document's path, that files it as history.</summary>
    internal const string ArchiveFolder = "archive";

    /// <summary>The kinds that are history whole.</summary>
    internal static readonly IReadOnlySet<EntryKind> HistoryKinds =
        new HashSet<EntryKind> { EntryKind.Decision, EntryKind.Fix, EntryKind.TaskOutcome, EntryKind.Record };

    /// <summary>The date a dated document's file or folder name carries, as SQLite's GLOB writes it.</summary>
    internal const string DateGlob = "[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]";

    private static readonly Regex Date = new(@"\d{4}-\d{2}-\d{2}", RegexOptions.CultureInvariant);

    private static readonly Regex Where = new(
        @"^\s*where\b|\b(?:which|what)\s+(?:file|folder|line|method|route)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex Identifier = new(
        @"\bD\d+\b|\b[A-Z]{3,}(?:-[A-Z]+)*\d+[a-z0-9]*(?:-[a-z0-9]+)*\b", RegexOptions.CultureInvariant);

    private static readonly Regex HistoryWords = new(
        @"\b(?:why|decided|rejected|chose|chosen|rationale|introduced|regression|root\s+cause)\b|^\s*when\b|\bwhen\s+(?:did|was|were|has|had)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// What an entry is: history by its kind or by an archive folder on its path, dated by a file or folder named for
    /// its date, else the present.
    /// </summary>
    public static EntryTense TenseOf(KnowledgeEntry entry)
    {
        var parts = entry.RelativePath.Replace('\\', '/').Split('/');
        if (HistoryKinds.Contains(entry.Kind)
            || parts[..^1].Any(folder => folder.Equals(ArchiveFolder, StringComparison.OrdinalIgnoreCase)))
        {
            return EntryTense.History;
        }

        return parts.Any(part => Date.IsMatch(part)) ? EntryTense.Dated : EntryTense.Present;
    }

    /// <summary>Whether a question names a decision or a row, or asks what only history answers.</summary>
    public static bool AsksHistory(string question) => Identifier.IsMatch(question) || HistoryWords.IsMatch(question);

    /// <summary>Whether a search is answered present first: it names no kinds and no tense, and asks nothing of history.</summary>
    internal static bool Applies(KnowledgeQuery query) =>
        query.Kinds is null && query.Present is null && !AsksHistory(query.Text);

    /// <summary>Whether a question asks where something is, which the index answers at the place it ranks.</summary>
    public static bool AsksWhere(string question) => Where.IsMatch(question);

    /// <summary>
    /// The present's hits first, then the rest's, keeping <see cref="RestKept"/> places for the rest where it matched
    /// and giving it every place the present leaves. Within the present, a question not asking where answers the
    /// documents ahead of the index, whose rows name places rather than say how a thing works.
    /// </summary>
    internal static SearchAnswer Order(string question, SearchAnswer present, SearchAnswer rest, int limit)
    {
        var kept = Math.Min(RestKept, rest.Hits.Count);
        var ahead = AsksWhere(question) ? present.Hits : [.. present.Hits.OrderBy(hit => hit.Entry.Kind == EntryKind.Index)];
        var hits = ahead.Take(Math.Max(0, limit - kept)).ToList();
        hits.AddRange(rest.Hits.Take(limit - hits.Count));
        var failures = new[] { present.Failure, rest.Failure }.OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        return new SearchAnswer(
            hits, present.Lexical && rest.Lexical, present.Semantic && rest.Semantic,
            failures.Count == 0 ? null : string.Join("; ", failures), present.MoreIndex || rest.MoreIndex, PresentAhead: true);
    }
}
