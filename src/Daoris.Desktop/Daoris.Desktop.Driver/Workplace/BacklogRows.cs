using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The declared backlog merged by its rows' ids. A row is a list line whose first bold token opens with an id
/// (<see cref="BacklogIds.RowOf"/>), keyed by that id, and each key is judged alone, the base against the line (ours) and the work
/// (theirs): a row only one side added, removed or rewrote takes that side, and one both changed alike, whitespace normalised, is once
/// in our text; so a row a side archived leaves only where the other's copy is the base's (or moved). Every other row is left for a person with both texts said: one both rewrote, even in words apart, since a merge of
/// words writes wording neither side wrote; one a side removed, archived or not, while the other changed it; one id both added in
/// different texts. A side that covers an id twice, or holds a row whose ids do not read, is refused before anything merges.
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

    // A row stands in the line merge as this mark and its key, and no line of text begins with a NUL.
    private const char Mark = '\0';

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

        var (was, mine, yours) = (rows[0], rows[1], rows[2]);
        var taken = new Dictionary<string, string?>(StringComparer.Ordinal);
        var differ = new List<Differs>();
        var (kept, dropped) = (0, 0);
        foreach (var key in was.Keys.Concat(mine.Keys).Concat(yours.Keys).Distinct(StringComparer.Ordinal))
        {
            var (b, o, t) = (was.GetValueOrDefault(key), mine.GetValueOrDefault(key), yours.GetValueOrDefault(key));
            string? text;
            if (Alike(o, t)) text = o;
            else if (Alike(o, b)) text = t;
            else if (Alike(t, b)) text = o;
            else
            {
                differ.Add(new Differs(key, o, t));
                continue;
            }

            taken[key] = text;
            if (b is null) continue;
            if (text is null) dropped++;
            else if (!Alike(text, b)) (kept, dropped) = (kept + 1, dropped + 1);
        }

        if (differ.Count > 0)
        {
            var lines = differ.Where(row => was.ContainsKey(row.Id)).Select(row => Array.FindIndex(sides[0], line => KeyOf(line) == row.Id) + 1).Order();
            return new BacklogMerge.Merged(null, kept, dropped, [.. lines]) { Differ = differ };
        }

        bool Stays(string line) => KeyOf(line) is not { } key || taken[key] is not null;
        var laid = sides.Select(lines => string.Join('\n', lines.Where(Stays).Select(line => KeyOf(line) is { } key ? Mark + key : line))).ToArray();
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

    private static string Has(string? row) => row is null ? "removed it" : $"has “{Trimmed(row)}”";

    private static string? KeyOf(string line) => BacklogIds.RowOf(line)?.Key;

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

    /// <summary>A side's rows by key; each id it covers twice added to <paramref name="twice"/>, each row whose ids do not read to <paramref name="unread"/>.</summary>
    private static Dictionary<string, string> RowsOf(string[] lines, List<string> twice, List<string> unread)
    {
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
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
            rows.TryAdd(row.Key, line);
        }

        return rows;
    }
}
