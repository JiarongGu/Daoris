namespace Daoris.Driver;

/// <summary>
/// A folder copy's return, file by file against the copy's base: changed only in the copy, written; only in the folder, left; alike, nothing;
/// differently on both, the person's choice (<see cref="ReturnChoices"/>). A path it cannot bring back without guessing is refused
/// (<see cref="ReturnRefusal"/>); every write holds only on what it read (<see cref="ReturnWrite"/>), journalled, and taken up first.
/// </summary>
public static class FolderReturn
{
    /// <summary>Bring <paramref name="copy"/>'s changes back into its folder, a file changed on both sides settled by <paramref name="choices"/>.</summary>
    /// <param name="choices">The person's choice by path, one of <see cref="ReturnChoices.All"/>.</param>
    public static FolderReturned Run(
        string home, FolderCopy copy, IReadOnlyDictionary<string, string> choices, ManifestBounds bounds, string journal, Action<string>? passed = null,
        IReadOnlyDictionary<string, ReturnShown>? bound = null)
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
        ReturnShown Present(string path) => new(copied.Listed(path)?.Sha256, place.Listed(path)?.Sha256);
        var lapsed = choices.Keys.Where(path => bound?.GetValueOrDefault(path) is { } shown && shown != Present(path)).ToList();
        choices = choices.Where(choice => !lapsed.Contains(choice.Key)).ToDictionary(StringComparer.Ordinal);

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

            var copyUnread = copied.Listed(path) is not null && session is null;
            if ((copyUnread && choices.GetValueOrDefault(path) != ReturnChoices.KeepYours) || (unread && !choices.ContainsKey(path)))
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
                    if (there is not null && !string.Equals(there, session, StringComparison.Ordinal)) refused[path] = ReturnRefusal.Collision;
                    else if (there is null) Settle(path, writes.Write(new(beside, null, session, From: path)), written, waiting, wrote: beside);
                    break;
                case ReturnChoices.KeepBoth:
                    break;
                default:
                    waiting.Add(path);
                    break;
            }
        }

        return new(written, waiting, Nothing: false)
        {
            Refused = refused, Lapsed = lapsed,
            Shown = waiting.Concat(refused.Keys).Distinct(StringComparer.Ordinal).ToDictionary(path => path, Present, StringComparer.Ordinal),
        };
    }

    /// <summary>The name the session's version of <paramref name="path"/> is written under beside the folder's: <c>&lt;name&gt; (&lt;copy&gt;)&lt;ext&gt;</c>.</summary>
    public static string Beside(string path, string copy)
    {
        var slash = path.LastIndexOf('/');
        var (folder, name) = (path[..(slash + 1)], path[(slash + 1)..]);
        var dot = name.LastIndexOf('.');
        return dot > 0 ? $"{folder}{name[..dot]} ({copy}){name[dot..]}" : $"{folder}{name} ({copy})";
    }

    private static void Settle(string path, bool done, List<string> written, List<string> waiting, string? wrote = null) =>
        (done ? written : waiting).Add(done ? wrote ?? path : path);
}

/// <summary>What a return came to: the folder's paths it wrote or removed, those that wait for the person, and whether the copy changed nothing.</summary>
public sealed record FolderReturned(IReadOnlyList<string> Written, IReadOnlyList<string> Waiting, bool Nothing)
{
    /// <summary>The paths refused rather than guessed at, by what the return found there (<see cref="ReturnRefusal"/>).</summary>
    public IReadOnlyDictionary<string, string> Refused { get; init; } = new Dictionary<string, string>();

    public IReadOnlyDictionary<string, ReturnShown> Shown { get; init; } = new Dictionary<string, ReturnShown>();

    /// <summary>The paths whose choice lapsed: a side holds another version than the choice named.</summary>
    public IReadOnlyList<string> Lapsed { get; init; } = [];
}
