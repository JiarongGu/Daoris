using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A folder's review: the files its turn added, changed or removed against its start's manifest, read from its end's, or from the
/// folder as it stands while the turn runs. A text file comes with its diff from the content store; a file that is not text, or whose
/// content the store's bound left out, is a row with no hunks. There is no commit, so the range names no base. A git place's review is
/// the tree's, never this one.
/// </summary>
public static class PlaceChanges
{
    /// <summary>What a folder too large to follow says in place of its files.</summary>
    public const string TooLarge = "This folder is too large to follow, so the files its session changed are not listed.";

    /// <summary>The change set of <paramref name="record"/>'s turn in a folder, or null where it had none.</summary>
    public static TreeDiff? Of(string home, string record)
    {
        if (PlaceTurns.Find(home, record) is not { } found || found.Start is not { Kind: PlaceKind.Folder, Root: { } root } start) return null;
        var store = found.Store;
        var end = store.Read(record, ManifestMoment.End) is { } read && read.At >= start.At
            ? read
            : Directory.Exists(root) ? FolderManifestReader.Read(root, store.Content, start, ManifestBounds.Of(home),PlaceExcludes.Of(start.Excludes)) : null;
        if (end is null) return null;
        if (start.TooLarge || end.TooLarge) return new TreeDiff([], TooLarge, "");

        var from = FolderCopies.At(home, root) is { } copy ? FolderCopies.Base(home, copy) ?? start : start;
        var changes = FolderChanges.Between(from, end);
        var files = changes.All.Select(path => File(
            path,
            changes.Added.Contains(path) ? "added" : changes.Removed.Contains(path) ? "deleted" : "modified",
            from.Listed(path),
            end.Listed(path),
            store.Content));
        return new TreeDiff([.. files], null, "");
    }

    private static DiffFile File(string path, string status, ManifestEntry? was, ManifestEntry? now, ContentStore content)
    {
        var before = was is null ? null : Text(was, content);
        var after = now is null ? null : Text(now, content);
        if ((was is not null && before is null) || (now is not null && after is null)) return new DiffFile(path, status, null, null, null);
        var (patch, added, removed) = TextPatch.Unified(path, before, after);
        return new DiffFile(path, status, added, removed, patch);
    }

    private static readonly UTF8Encoding Strict = new(false, true);

    /// <summary>The kept text of <paramref name="entry"/>; null where it is not stored or is not UTF-8, which a diff of replacement characters would only misread.</summary>
    private static string? Text(ManifestEntry entry, ContentStore content)
    {
        if (entry.Sha256 is not { } sha || !System.IO.File.Exists(content.PathOf(sha))) return null;
        try
        {
            return Strict.GetString(AtomicFile.ReadBytes(content.PathOf(sha)));
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }
}
