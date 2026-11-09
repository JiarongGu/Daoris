namespace Daoris.Driver;

/// <summary>
/// The record's bases and their markers kept together: an entry is named by the record before its marker is written away and after its
/// marker is written in, so a step cut off between the two leaves a refusal; the take's plan writes them, the clearing forgets those it
/// moved, the undo takes back what its claim wrote, and the door's build releases one.
/// </summary>
internal static class TreeBases
{
    /// <summary>The planned bases' markers written, then the record naming them beside every other entry whose folder still stands.</summary>
    internal static void Write(string home, string slot, IReadOnlyList<TreeBase> planned)
    {
        var record = TreeFilesRecord.Read(home, slot);
        if (planned.Count == 0 && record?.Bases is not { Count: > 0 }) return;
        foreach (var each in planned) BaseMarker.Write(slot, each.Path, each.From);
        var paths = planned.Select(each => each.Path).ToHashSet(StringComparer.Ordinal);
        TreeFilesRecord.Change(home, slot, state => state with
        {
            Bases = [.. state.Bases.Where(each => !paths.Contains(each.Path) && Directory.Exists(BaseMarker.FolderOf(slot, each.Path))), .. planned],
        });
    }

    /// <summary>The entries under a top the clearing moved removed, and their markers, which moved with them, deleted from the set-aside folder.</summary>
    internal static void Forget(string home, string slot, string aside, IReadOnlyList<string> moved)
    {
        if (TreeFilesRecord.Read(home, slot)?.Bases is not { Count: > 0 } bases) return;
        var gone = bases.Where(each => moved.Any(top => Under(each.Path, top))).ToList();
        if (gone.Count == 0) return;
        TreeFilesRecord.Change(home, slot, state => state with { Bases = [.. state.Bases.Where(each => !gone.Contains(each))] });
        foreach (var each in gone) BaseMarker.Remove(aside, each.Path);
    }

    /// <summary>What the claim <paramref name="job"/> wrote taken back: an entry it added removed with its marker, one it stamped again put back as it stood.</summary>
    internal static void Undo(string home, string slot, string job)
    {
        if (job.Length == 0 || TreeFilesRecord.Read(home, slot)?.Bases is not { Count: > 0 } bases) return;
        var own = bases.Where(each => each.Job == job).ToList();
        if (own.Count == 0) return;
        foreach (var was in own.Select(each => each.Was).OfType<TreeBase>()) BaseMarker.Write(slot, was.Path, was.From);
        TreeFilesRecord.Change(home, slot, state => state with
        {
            Bases = [.. state.Bases.Select(each => each.Job == job ? each.Was : each).OfType<TreeBase>()],
        });
        foreach (var added in own.Where(each => each.Was is null)) BaseMarker.Remove(slot, added.Path);
    }

    /// <summary>One base released: the record lets it go and keeps what the door did in the same write, then its marker goes.</summary>
    internal static void Release(string home, string slot, TreeBase released, TreeBaseRelease done)
    {
        TreeFilesRecord.Change(home, slot, state => state with
        {
            Bases = [.. state.Bases.Where(each => each.Path != released.Path)],
            Released = [.. state.Released.Where(each => each.Path != done.Path), done],
        });
        BaseMarker.Remove(slot, released.Path);
    }

    /// <summary>Whether <paramref name="path"/> is <paramref name="top"/> or lies under it, both repository-relative.</summary>
    internal static bool Under(string path, string top) =>
        top.EndsWith('/') ? path.StartsWith(top, StringComparison.Ordinal) : string.Equals(path, top, StringComparison.Ordinal);
}
