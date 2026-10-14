namespace Daoris.Knowledge;

/// <summary>
/// What kind of thing a knowledge entry is. The kind decides how it is read, not where it lives:
/// a decision record is a decision record whatever the repository calls the file.
/// </summary>
public enum EntryKind
{
    /// <summary>An always-loaded rule.</summary>
    Rule,

    /// <summary>An on-demand knowledge document.</summary>
    Knowledge,

    /// <summary>An invocable skill.</summary>
    Skill,

    /// <summary>One numbered decision and its reasoning.</summary>
    Decision,

    /// <summary>One recorded fix: symptom, root cause, verification.</summary>
    Fix,


    /// <summary>One completed task and its outcome.</summary>
    TaskOutcome,

    /// <summary>
    /// One entry of a repository's declared index of where things are (ORIENT2e; D151 §6): the prose or the list
    /// under one heading of a generated file, or one row of its tables (ORIENT2h), each naming a place in the code
    /// or the records. Last, so the kinds stored before it keep their numbers.
    /// </summary>
    Index,
}

/// <summary>
/// The lines of its file an entry is, counted from 1 (ORIENT2e; the orientation design §3.2): what a hit names as
/// <c>path:first-last</c>, and what a range is read against.
/// </summary>
/// <param name="First">The entry's first line in its file.</param>
/// <param name="Last">Its last, at least <paramref name="First"/>.</param>
public readonly record struct LineSpan(int First, int Last)
{
    /// <summary>How many lines it spans.</summary>
    public int Count => Last - First + 1;

    /// <summary>As a hit names it: <c>12-30</c>, or <c>7</c> for one line.</summary>
    public override string ToString() => First == Last ? $"{First}" : $"{First}-{Last}";

    /// <summary>A range as a caller writes it, <c>12-30</c> or <c>7</c>; null for anything else.</summary>
    /// <remarks>Refused rather than read as something near it: a range that names no line, or ends before it starts.</remarks>
    public static LineSpan? Parse(string? text)
    {
        var parts = (text ?? string.Empty).Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 2) return null;
        if (!int.TryParse(parts[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var first)) return null;
        var last = first;
        if (parts.Length == 2
            && !int.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out last))
        {
            return null;
        }
        return first >= 1 && last >= first ? new LineSpan(first, last) : null;
    }

    /// <summary>
    /// The span a body read from its file has, or null when the claim cannot be true: a span that names no line,
    /// or one of another length than the body's lines, would make a range read the wrong text.
    /// </summary>
    /// <param name="first">The first line claimed, or null for none.</param>
    /// <param name="last">The last line claimed, or null for none.</param>
    /// <param name="body">The text the span is claimed for.</param>
    public static LineSpan? Of(int? first, int? last, string body) =>
        first is { } from && last is { } to && from >= 1 && to >= from && to - from + 1 == LineCount(body)
            ? new LineSpan(from, to)
            : null;

    /// <summary>How many lines a text is, counting a line after its last newline as one.</summary>
    internal static int LineCount(string text) => text.Count(c => c == '\n') + 1;
}

/// <summary>
/// Where an entry came from, which is the question the lock already answers.
/// </summary>
/// <remarks>
/// This distinction is the reason the index is worth building at all. Canonical content is
/// <em>identical in every adopting repository by construction</em> — indexing it once per repository
/// would produce a dozen copies of the same rule and call that a corpus. What differs between
/// repositories, and therefore what is worth searching across them, is the local material: the
/// decisions, the fixes, the outcomes that only that repository knows.
/// </remarks>
public enum Provenance
{
    /// <summary>Materialized by daoris and recorded in the lock. The same everywhere it is installed.</summary>
    Canonical,

    /// <summary>The repository's own. Invisible to the tool, and the only thing that varies across repositories.</summary>
    Local,
}

/// <summary>
/// One addressable piece of knowledge: a rule, a skill, a decision, a fix, a task outcome.
/// </summary>
/// <remarks>
/// Entries are <em>sections</em> rather than files wherever a file holds many of them. A decisions
/// log is one file and twenty decisions; returning the file for a query about one of them buries the
/// answer in nineteen others.
/// </remarks>
/// <param name="Repository">The repository this came from, by its directory name.</param>
/// <param name="Kind">What sort of entry it is.</param>
/// <param name="Provenance">Canonical or the repository's own.</param>
/// <param name="Title">The heading, or the document name where the whole file is one entry.</param>
/// <param name="Body">The entry's text, without its heading.</param>
/// <param name="RelativePath">Path within the repository, always '/'-separated.</param>
/// <param name="Anchor">The heading this section was split at, when it was split from a larger file.</param>
/// <param name="Workspace">
/// Which circle this entry is searchable within (D48). Denormalized from the repository's registry row
/// and stamped at ingest, not carried by the file — the index is derived data, so re-wiring a
/// repository to another workspace re-stamps its entries on the next refresh rather than migrating
/// anything. Not part of <see cref="Id"/>: moving a repository between circles does not make its
/// decisions different decisions.
/// </param>
/// <param name="Lines">
/// The lines of its file the body is, as written and trimmed at its ends: line <c>i</c> of the body is line
/// <c>First + i</c> of the file (ORIENT2e). Null where the body is not a run of its file's lines (a row read with
/// its columns, ORIENT1c) or where nobody said (a feed that named none). Not part of <see cref="Id"/>: a
/// section that moved down the file is the same section.
/// </param>
public sealed record KnowledgeEntry(
    string Repository,
    EntryKind Kind,
    Provenance Provenance,
    string Title,
    string Body,
    string RelativePath,
    string? Anchor = null,
    string Workspace = Workspaces.Default,
    LineSpan? Lines = null)
{
    /// <summary>The line of the file that line <paramref name="offset"/> of the body is; null where it names no lines.</summary>
    public int? LineAt(int offset) =>
        Lines is { } span ? Math.Clamp(span.First + offset, span.First, span.Last) : null;

    /// <summary>
    /// The part of the body a range names, in the file's lines, and the lines it is; null where the range is
    /// wholly outside the entry or the entry names no lines it can be cut at.
    /// </summary>
    /// <remarks>
    /// Cut from the body, never from the file on disk: the body is what was indexed, at the lines a hit named, and
    /// a deployment fed by another machine has no file to read. A body that is not as many lines as its span
    /// claims is not cut, since each line would be named for another's text.
    /// </remarks>
    public (LineSpan Lines, string Text)? Cut(LineSpan range)
    {
        if (Lines is not { } span) return null;
        var lines = Body.Split('\n');
        if (lines.Length != span.Count) return null;

        var first = Math.Max(range.First, span.First);
        var last = Math.Min(range.Last, span.Last);
        if (first > last) return null;
        return (new LineSpan(first, last), string.Join('\n', lines[(first - span.First)..(last - span.First + 1)]));
    }

    /// <summary>
    /// A stable identity for the entry, so re-ingesting the same repository updates rather than
    /// duplicates. Deliberately derived from location rather than content: an entry whose text was
    /// edited is the same entry, and treating it as a new one is how an index accumulates ghosts.
    /// </summary>
    public string Id => Anchor is null
        ? $"{Repository}:{RelativePath}"
        : $"{Repository}:{RelativePath}#{Anchor}";
}
