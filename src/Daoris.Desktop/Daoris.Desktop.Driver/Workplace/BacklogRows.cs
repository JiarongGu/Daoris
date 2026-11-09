using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The declared backlog merged by its rows' ids. A row is a list line whose first bold token opens with a list of ids
/// (<see cref="BacklogIds.RowOf"/>), and rows that share any id, on any side, are one row, judged the base against the line (ours) and
/// the work (theirs): each id stands where only one side changed whether it does, or both alike, and the rest of the line (all but its
/// ids) takes the one side that changed it, or both alike, whitespace normalised. So a row a side archived leaves only where the other's
/// copy is the base's or only moved, and a row whose ids both sides archived in part keeps the ids neither archived, its list written
/// again. Every other row is left for a person with both texts said: one whose rest both rewrote, even in words apart, since a merge of
/// words writes wording neither side wrote; one a side removed while the other changed its rest; one both added in different texts;
/// one a side split or joined. A side that covers an id twice, or holds a row whose ids do not read, is refused before anything merges.
/// </summary>
/// <remarks>
/// The lines that are no row merge by the merge base line by line (<see cref="BacklogMerge.ByBase"/>) with each row standing as its key
/// and the rows that leave left out of every side, so a row rewritten in place never meets its neighbours and one moved goes where its
/// side moved it; there two sides adding at one place stand together only where both add rows alone. A twin of
/// <c>tools/merge-branch/backlog-merge.mjs</c>'s <c>mergeByRows</c>, row for row of <c>fixtures/backlog-merges.json</c>.
/// </remarks>
internal static class BacklogRows
{
    /// <summary>A row both sides changed: its key and each side's text, null where that side removed it.</summary>
    internal sealed record Differs(string Id, string? Ours, string? Theirs);

    /// <summary>A row of one side: its line and its list.</summary>
    private sealed record Row(string Line, BacklogIds.Listed Listed)
    {
        internal IReadOnlyList<string> Ids => Listed.Ids!;

        /// <summary>The line with its list taken out, which is what its text is judged by.</summary>
        internal string Rest => Line[..Listed.Span.Start] + Hole + Line[Listed.Span.End..];
    }

    // A row stands in the line merge as this mark and its key, and no line of text begins with a NUL.
    private const char Mark = '\0';

    // Where a row's list stood in its rest.
    private const char Hole = '\u0001';

    /// <summary>The backlog merged by its rows' ids; its text null where a row is left for a person or a side is refused.</summary>
    internal static BacklogMerge.Merged Merge(string @base, string ours, string theirs)
    {
        var sides = new[] { @base, ours, theirs }.Select(text => text.Split('\n')).ToArray();
        var (twice, unread) = (new List<string>(), new List<string>());
        var rows = sides.Select(lines => RowsOf(lines, twice, unread)).ToArray();
        if (twice.Count > 0 || unread.Count > 0)
        {
            return new BacklogMerge.Merged(null, 0, 0, []) { Twice = twice, Unread = [.. unread.Distinct(StringComparer.Ordinal)] };
        }

        var keyOf = Families(rows);
        var taken = new Dictionary<string, string?>(StringComparer.Ordinal);
        var differ = new List<Differs>();
        var (kept, dropped) = (0, 0);
        foreach (var key in keyOf.Values.Distinct(StringComparer.Ordinal))
        {
            var of = rows.Select(side => side.Where(row => keyOf[row.Listed.Key] == key).ToList()).ToArray();
            var b = of[0].SingleOrDefault();
            if (of.Any(side => side.Count > 1) || Taken(of[0].SingleOrDefault(), of[1].SingleOrDefault(), of[2].SingleOrDefault()) is not { } text)
            {
                differ.Add(new Differs(key, Joined(of[1]), Joined(of[2])));
                continue;
            }

            taken[key] = text.Line;
            if (b is null) continue;
            if (text.Line is null) dropped++;
            else if (!Alike(text.Line, b.Line)) (kept, dropped) = (kept + 1, dropped + 1);
        }

        if (differ.Count > 0)
        {
            var lines = differ.Select(row => Array.FindIndex(sides[0], line => KeyOf(line, keyOf) == row.Id) + 1).Where(line => line > 0).Order();
            return new BacklogMerge.Merged(null, kept, dropped, [.. lines]) { Differ = differ };
        }

        bool Stays(string line) => KeyOf(line, keyOf) is not { } key || taken[key] is not null;
        var laid = sides.Select(lines => string.Join('\n', lines.Where(Stays).Select(line => KeyOf(line, keyOf) is { } key ? Mark + key : line))).ToArray();
        var layout = BacklogMerge.ByBase(laid[0], laid[1], laid[2], Collide);
        (kept, dropped) = (kept + layout.Kept, dropped + layout.Dropped);
        if (layout.Text is null)
        {
            var at = sides[0].Select((line, index) => (line, index)).Where(each => Stays(each.line)).Select(each => each.index + 1).ToList();
            return new BacklogMerge.Merged(null, kept, dropped, [.. layout.Both.Select(line => at[line - 1])]);
        }

        var output = new List<string>();
        var placed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in layout.Text.Split('\n'))
        {
            if (line.Length == 0 || line[0] != Mark) output.Add(line);
            else if (placed.Add(line[1..])) output.Add(taken[line[1..]]!);
            // A row both sides added alike at two places stands once, at the first.
            else kept--;
        }

        return new BacklogMerge.Merged(string.Join('\n', output), kept, dropped, []);
    }

    /// <summary>
    /// What a merge refused says of the backlog's rows, beside git's conflict: each id a side covers twice, each row whose ids do not
    /// read, and each row both sides changed with both texts. Nothing where it refused none of them.
    /// </summary>
    internal static string Refusal(string path, BacklogMerge.Merged merged) =>
        string.Concat(merged.Twice.Select(id => $" `{path}` holds the row `{id}` twice on a side, so none of it was settled by its rows."))
        + string.Concat(merged.Unread.Select(line => $" `{path}` holds a row whose ids do not read, so none of it was settled by its rows: “{line}”."))
        + string.Concat(merged.Differ.Select(row =>
            $" `{path}`'s row `{row.Id}` changed on both sides and is left for a person: the line {Has(row.Ours)}, and the work {Has(row.Theirs)}."));

    /// <summary>A line with its line ending and trailing spaces cut, as rows are compared.</summary>
    internal static string Trimmed(string line) => line.TrimEnd(' ', '\t', '\r');

    /// <summary>
    /// The row a family of rows makes; null where it is left for a person. Each id stands as only one side
    /// changed it, or both alike; the rest takes the side that changed it. Where every id leaves, the row leaves only where no side's rest
    /// changed. A side's own line is kept where the row is that side's; else its list is written again.
    /// </summary>
    private static Made? Taken(Row? b, Row? o, Row? t)
    {
        var ids = new[] { b, o, t }.SelectMany(row => row?.Ids ?? []).Distinct(StringComparer.Ordinal)
            .Where(id => Stands(Covers(b, id), Covers(o, id), Covers(t, id))).ToList();
        if (ids.Count == 0) return (o is null || Alike(o.Rest, b?.Rest)) && (t is null || Alike(t.Rest, b?.Rest)) ? new Made(null) : null;
        var side = Alike(o?.Rest, t?.Rest) ? o : Alike(o?.Rest, b?.Rest) ? t : Alike(t?.Rest, b?.Rest) ? o : null;
        if (side is null) return null;
        if (side.Ids.SequenceEqual(ids, StringComparer.Ordinal)) return new Made(side.Line);
        return BacklogIds.Written(ids) is { } list ? new Made(side.Rest.Replace(Hole.ToString(), list)) : null;
    }

    /// <summary>The line a family of rows makes; null where it leaves.</summary>
    private sealed record Made(string? Line);

    private static bool Covers(Row? row, string id) => row is not null && row.Ids.Contains(id, StringComparer.Ordinal);

    private static bool Stands(bool b, bool o, bool t) => o == t ? o : o == b ? t : o;

    private static string? Joined(List<Row> rows) => rows.Count == 0 ? null : string.Join("\n", rows.Select(row => row.Line));

    private static string Has(string? row) => row is null ? "removed it" : $"has “{Trimmed(row)}”";

    private static string? KeyOf(string line, Dictionary<string, string> keyOf) => BacklogIds.RowOf(line) is { Ids: not null } row ? keyOf[row.Key] : null;

    /// <summary>
    /// The rows that share an id, on any side, joined into one family, each keyed by the first id met of it (the base's rows first, then
    /// ours, then theirs, each list in its order): every id a row opens with mapped to its family's key.
    /// </summary>
    private static Dictionary<string, string> Families(List<Row>[] rows)
    {
        var parent = new Dictionary<string, string>(StringComparer.Ordinal);
        var order = new List<string>();
        string Find(string id)
        {
            while (parent[id] != id) id = parent[id] = parent[parent[id]];
            return id;
        }

        foreach (var row in rows.SelectMany(side => side))
        {
            foreach (var id in row.Ids.Where(id => parent.TryAdd(id, id))) order.Add(id);
            foreach (var id in row.Ids.Skip(1)) parent[Find(id)] = Find(row.Ids[0]);
        }

        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in order) keys.TryAdd(Find(id), id);
        var keyOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in rows.SelectMany(side => side)) keyOf.TryAdd(row.Listed.Key, keys[Find(row.Listed.Key)]);
        return keyOf;
    }

    private static bool Alike(string? x, string? y) => x is null || y is null ? x is null && y is null : Normal(x) == Normal(y);

    private static readonly Regex Spaces = new("[ \t]+", RegexOptions.CultureInvariant);

    /// <summary>A row with its whitespace normalised: each run of spaces one space, its line ending and trailing spaces cut.</summary>
    private static string Normal(string line) => Spaces.Replace(Trimmed(line), " ");

    /// <summary>
    /// Whether two changes meet: where the line merge says (<see cref="BacklogMerge.Collide"/>), and where both add at one place unless
    /// both add rows alone, which each stand by their ids. One add both made alike never reaches here.
    /// </summary>
    private static bool Collide(BacklogMerge.Hunk x, BacklogMerge.Hunk y) =>
        BacklogMerge.Collide(x, y)
        || (x.Start == x.End && y.Start == y.End && x.Start == y.Start && !x.Lines.Concat(y.Lines).All(line => line.Length > 0 && line[0] == Mark));

    /// <summary>A side's rows; each id it covers twice added to <paramref name="twice"/>, each row whose ids do not read to <paramref name="unread"/>.</summary>
    private static List<Row> RowsOf(string[] lines, List<string> twice, List<string> unread)
    {
        var rows = new List<Row>();
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            if (BacklogIds.RowOf(line) is not { } row) continue;
            if (row.Ids is null)
            {
                unread.Add(Trimmed(line));
                continue;
            }

            foreach (var id in row.Ids.Where(id => !covered.Add(id) && !twice.Contains(id))) twice.Add(id);
            rows.Add(new Row(line, row));
        }

        return rows;
    }
}
