namespace Daoris.Driver;

/// <summary>
/// The files a branch's or a checkout's review record changed, listed between the commits the record names (<see cref="ReviewCommits"/>):
/// the line's merge base with the tip, or the first turn's start, to the tip or the newest turn's end. Read from the commits, so the list
/// is the record's whatever the checkout holds now.
/// </summary>
public static class ReviewCommitFiles
{
    private const string NoBlob = "0000000000000000000000000000000000000000";

    /// <summary>The record's changed files with their blob ids before and after; none where a side's commit is not known or git cannot read the range.</summary>
    public static async Task<IReadOnlyList<ReviewRow>> ListAsync(string root, ReviewRecord record, CancellationToken ct = default)
    {
        if (record.Kind is not (ReviewKinds.Branch or ReviewKinds.Checkout)) return [];
        var before = await ReviewCommits.CommitAsync(root, record, ReviewContent.Before, ct).ConfigureAwait(false);
        var after = await ReviewCommits.CommitAsync(root, record, ReviewContent.After, ct).ConfigureAwait(false);
        if (before is null || after is null) return [];
        var (code, raw, _) = await WorkingTree.GitAsync(root, ["--no-optional-locks", "diff", "--raw", "-z", "--no-abbrev", "--no-renames", "--no-textconv", before, after], ct).ConfigureAwait(false);
        return code == 0 ? Parse(raw) : [];
    }

    /// <summary>Rows from <c>git diff --raw -z</c>: <c>:mode mode before after STATUS</c>, then the path, each ended by NUL.</summary>
    public static IReadOnlyList<ReviewRow> Parse(string raw)
    {
        var rows = new List<ReviewRow>();
        var fields = raw.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (var at = 0; at + 1 < fields.Length; at += 2)
        {
            var meta = fields[at].TrimStart(':').Split(' ');
            if (meta.Length < 5) continue;
            var change = meta[4] switch { "A" => "added", "D" => "deleted", _ => "modified" };
            rows.Add(new ReviewRow(fields[at + 1], change) { Before = Blob(meta[2]), After = Blob(meta[3]) });
        }
        return rows;
    }

    private static string? Blob(string id) => id == NoBlob ? null : id;
}
