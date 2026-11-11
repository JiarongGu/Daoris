namespace Daoris.Driver;

/// <summary>
/// A folder copy's return, file by file against the manifest read at the copy's making (its base): a file changed only in the copy is
/// written into the folder, one changed only in the folder is left, one changed alike on both is nothing, and one changed differently on
/// both waits for the person's choice (<see cref="ReturnChoices"/>), never chosen here. A path it cannot bring back without
/// guessing (a link, a file turned folder, a rename onto the same name, a path outside the folder) is refused with what was found
/// (<see cref="ReturnRefusal"/>), and settled by the person's <i>Keep yours</i> alone. Every write is conditioned on the folder's file
/// still holding what the return read, else that path waits too (<see cref="ReturnWrite"/>); each is journalled beside the run's record
/// with what it replaced or removed kept there, and a return cut off is taken up from its journal first. Asked again, a return already
/// done finds nothing more to do.
/// </summary>
public static class FolderReturn
{
    /// <summary>Bring <paramref name="copy"/>'s changes back into its folder, a file changed on both sides settled by <paramref name="choices"/>.</summary>
    /// <param name="choices">The person's choice by path, one of <see cref="ReturnChoices.All"/>.</param>
    /// <param name="journal">The folder of this return's journal, which the run that returns it keeps beside its record.</param>
    /// <param name="passed">Told each boundary of a file's write as it is passed (<see cref="ReturnSteps"/>).</param>
    public static FolderReturned Run(
        string home, FolderCopy copy, IReadOnlyDictionary<string, string> choices, ManifestBounds bounds, string journal, Action<string>? passed = null)
    {
        var writes = new ReturnWrite(copy, new ReturnJournal(journal), passed);
        // Taken up before either side is read, so nothing a cut-off write left beside a file reads as the person's.
        List<string> written = [.. writes.Resume()];
        var store = new ManifestStore(home, copy.Workspace, copy.Department);
        var based = FolderCopies.Base(home, copy) ?? throw new DriverException($"copy `{copy.Id}` has no manifest of its making, so nothing can be brought back.");
        var excludes = PlaceExcludes.Of(based.Excludes);
        // Both sides hashed afresh from their bytes: a return writes on what it read, never on a size and time that held still.
        var copied = FolderManifestReader.Read(copy.Folder, store.Content, null, bounds, excludes);
        var place = FolderManifestReader.Read(copy.Root, store.Content, null, bounds, excludes);
        if (copied.TooLarge || place.TooLarge) throw new DriverException($"copy `{copy.Id}` or its folder could not be read whole, so nothing is brought back yet.");
        var inCopy = FolderChanges.Between(based, copied).All;
        if (inCopy.Count == 0) return new(written, [], Nothing: written.Count == 0);

        var inPlace = FolderChanges.Between(based, place).All.ToHashSet(StringComparer.Ordinal);
        var refusals = ReturnRefusal.Of(copy, based, copied, place, inCopy);
        List<string> waiting = [];
        var refused = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in inCopy)
        {
            if (refusals.TryGetValue(path, out var found))
            {
                if (choices.GetValueOrDefault(path) != ReturnChoices.KeepYours) refused[path] = found;
                continue;
            }

            var (session, folder) = (copied.Listed(path)?.Sha256, place.Listed(path)?.Sha256);
            var unread = (copied.Listed(path) is not null && session is null) || (place.Listed(path) is not null && folder is null);
            if (!inPlace.Contains(path) && !unread)
            {
                Settle(path, writes.Write(new(path, folder, session)), written, waiting);
                continue;
            }

            if (unread && !choices.ContainsKey(path))
            {
                waiting.Add(path);
                continue;
            }

            if (string.Equals(session, folder, StringComparison.Ordinal)) continue;
            switch (choices.GetValueOrDefault(path))
            {
                case ReturnChoices.KeepYours:
                    break;
                case ReturnChoices.TakeSession:
                    Settle(path, writes.Write(new(path, folder, session)), written, waiting);
                    break;
                case ReturnChoices.KeepBoth when session is not null:
                    var beside = Beside(path, copy.Id);
                    var there = place.Listed(beside)?.Sha256;
                    // A file of the person's already under the beside name is never written over.
                    if (there is not null && !string.Equals(there, session, StringComparison.Ordinal)) refused[path] = ReturnRefusal.Collision;
                    else if (there is null) Settle(beside, writes.Write(new(beside, null, session, From: path)), written, waiting);
                    break;
                case ReturnChoices.KeepBoth:
                    break;
                default:
                    waiting.Add(path);
                    break;
            }
        }

        return new(written, waiting, Nothing: false) { Refused = refused };
    }

    /// <summary>The name the session's version of <paramref name="path"/> is written under beside the folder's: <c>&lt;name&gt; (&lt;copy&gt;)&lt;ext&gt;</c>.</summary>
    public static string Beside(string path, string copy)
    {
        var slash = path.LastIndexOf('/');
        var (folder, name) = (path[..(slash + 1)], path[(slash + 1)..]);
        var dot = name.LastIndexOf('.');
        return dot > 0 ? $"{folder}{name[..dot]} ({copy}){name[dot..]}" : $"{folder}{name} ({copy})";
    }

    private static void Settle(string path, bool done, List<string> written, List<string> waiting) => (done ? written : waiting).Add(path);
}

/// <summary>What a return came to: the folder's paths it wrote or removed, those that wait for the person, and whether the copy changed nothing.</summary>
public sealed record FolderReturned(IReadOnlyList<string> Written, IReadOnlyList<string> Waiting, bool Nothing)
{
    /// <summary>The paths refused rather than guessed at, by what was found there (<see cref="ReturnRefusal"/>).</summary>
    public IReadOnlyDictionary<string, string> Refused { get; init; } = new Dictionary<string, string>();
}
