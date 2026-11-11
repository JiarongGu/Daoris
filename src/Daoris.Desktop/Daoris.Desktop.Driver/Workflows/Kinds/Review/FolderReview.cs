namespace Daoris.Driver;

/// <summary>
/// A folder's review record, worked in place or in a copy: the files changed from the run's baseline, its first turn's start, to its newest
/// turn's end, so every turn's work is in it and not the newest alone; a copy's from the copy's base. A path a later turn's start
/// found changed since the turn before is marked changed outside its turns, and a copy's file the folder changed too is marked so.
/// </summary>
internal static class FolderReview
{
    public static ReviewReference Reference(string run, IReadOnlyList<RunTurn> turns)
    {
        var newest = turns[^1];
        return new()
        {
            Workspace = newest.Workspace, Department = newest.Department, Baseline = RunTurns.BaselineOf(run), End = newest.Record, EndDigest = newest.Digest,
        };
    }

    public static ReviewReference CopyReference(string copy, RunTurn newest) => new()
    {
        Workspace = newest.Workspace, Department = newest.Department, Copy = copy, Baseline = copy, End = newest.Record, EndDigest = newest.Digest,
    };

    /// <summary>The rows from the baseline to the newest end; where the baseline is gone, from the earliest start still kept, said as such.</summary>
    public static (IReadOnlyList<ReviewRow>, IReadOnlyList<ReviewGap>, string) Rows(string home, ReviewReference reference, IReadOnlyList<RunTurn> turns)
    {
        var store = new ManifestStore(home, reference.Workspace!, reference.Department!);
        var coverage = ReviewCoverage.Whole;
        var from = store.Read(reference.Baseline!, ManifestMoment.Start);
        if (from is null)
        {
            from = turns.Select(turn => store.Read(turn.Record, ManifestMoment.Start)).OfType<RunManifest>().MinBy(start => start.At);
            coverage = ReviewCoverage.Retained;
        }

        var outside = turns.Skip(1).SelectMany(turn => turn.Outside).ToHashSet(StringComparer.Ordinal);
        var (rows, gaps) = Between(store, from, store.Read(reference.End!, ManifestMoment.End), path => outside.Contains(path) ? [ReviewMarks.Outside] : []);
        return (rows, gaps, coverage);
    }

    /// <summary>The copy's rows against its base, each the folder also changed since the copy's making marked so.</summary>
    public static (IReadOnlyList<ReviewRow>, IReadOnlyList<ReviewGap>, string) CopyRows(string home, ReviewReference reference)
    {
        var store = new ManifestStore(home, reference.Workspace!, reference.Department!);
        var bases = store.Read(reference.Baseline!, ManifestMoment.Start);
        var folder = bases?.Root is { } root ? store.Last(root) : null;
        var changedThere = bases is null || folder is null ? [] : FolderChanges.Between(bases, folder).All.ToHashSet(StringComparer.Ordinal);
        var (rows, gaps) = Between(store, bases, store.Read(reference.End!, ManifestMoment.End), path => changedThere.Contains(path) ? [ReviewMarks.FolderToo] : []);
        return (rows, gaps, ReviewCoverage.Whole);
    }

    private static (IReadOnlyList<ReviewRow>, IReadOnlyList<ReviewGap>) Between(
        ManifestStore store, RunManifest? from, RunManifest? end, Func<string, IReadOnlyList<string>> marks)
    {
        if (from is null || end is null) return ([], [new ReviewGap(ReviewGaps.NotRead, null, "The manifests this review reads from are no longer kept.")]);
        if (from.TooLarge || end.TooLarge) return ([], [new ReviewGap(ReviewGaps.OverBound, null, PlaceChanges.TooLarge)]);
        var changes = FolderChanges.Between(from, end);
        var rows = new List<ReviewRow>();
        var gaps = new List<ReviewGap>();
        foreach (var path in changes.All)
        {
            var (before, after) = (from.Listed(path), end.Listed(path));
            var change = changes.Added.Contains(path) ? "added" : changes.Removed.Contains(path) ? "deleted" : "modified";
            rows.Add(new ReviewRow(path, change) { Before = before?.Sha256, After = after?.Sha256, Marks = marks(path) });
            if ((before is not null && before.Sha256 is null) || (after is not null && after.Sha256 is null))
            {
                gaps.Add(new ReviewGap(ReviewGaps.Unreadable, path, "Its content could not be read, so it is listed with no hash."));
            }
            else if (new[] { before?.Sha256, after?.Sha256 }.OfType<string>().Any(sha => !File.Exists(store.Content.PathOf(sha))))
            {
                gaps.Add(new ReviewGap(ReviewGaps.NotKept, path, "The store keeps no content of it, too large or not text, so its diff is not drawn."));
            }
        }

        return (rows, gaps);
    }
}
