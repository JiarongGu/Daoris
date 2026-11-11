using System.Security.Cryptography;

namespace Daoris.Driver;

/// <summary>
/// A folder copy's return, file by file against the manifest read at the copy's making (its base): a file changed only in the copy is
/// written into the folder, one changed only in the folder is left, one changed alike on both is nothing, and one changed differently on
/// both waits for the person's choice (<see cref="NodeWordActs.Choices"/>), never chosen here. Every write is conditioned on the live
/// file still holding what the return read, else that path waits too; whatever a write replaces or a removal takes is kept first in
/// the return's own folder under the home. Asked again, a return already done finds nothing more to do.
/// </summary>
public static class FolderReturn
{
    /// <summary>Where the files a return of <paramref name="copy"/> replaced or removed are kept.</summary>
    public static string KeptFolder(string home, FolderCopy copy) =>
        Path.Combine(DaorisHome.Entry(home, HomeEntry.Places, copy.Workspace, copy.Department), "returns", copy.Id);

    /// <summary>Bring <paramref name="copy"/>'s changes back into its folder, a file changed on both sides settled by <paramref name="choices"/>.</summary>
    /// <param name="choices">The person's choice by path, one of <see cref="NodeWordActs.Choices"/>.</param>
    public static FolderReturned Run(string home, FolderCopy copy, IReadOnlyDictionary<string, string> choices, ManifestBounds bounds)
    {
        var store = new ManifestStore(home, copy.Workspace, copy.Department);
        var based = FolderCopies.Base(home, copy) ?? throw new DriverException($"copy `{copy.Id}` has no manifest of its making, so nothing can be brought back.");
        var excludes = PlaceExcludes.Of(based.Excludes);
        // Both sides hashed afresh from their bytes: a return writes on what it read, never on a size and time that held still.
        var copied = FolderManifestReader.Read(copy.Folder, store.Content, null, bounds, excludes);
        var place = FolderManifestReader.Read(copy.Root, store.Content, null, bounds, excludes);
        if (copied.TooLarge || place.TooLarge) throw new DriverException($"copy `{copy.Id}` or its folder could not be read whole, so nothing is brought back yet.");
        var inCopy = FolderChanges.Between(based, copied).All;
        if (inCopy.Count == 0) return new([], [], Nothing: true);

        var inPlace = FolderChanges.Between(based, place).All.ToHashSet(StringComparer.Ordinal);
        List<string> written = [], waiting = [];
        foreach (var path in inCopy)
        {
            var (session, folder) = (copied.Listed(path)?.Sha256, place.Listed(path)?.Sha256);
            var unread = (copied.Listed(path) is not null && session is null) || (place.Listed(path) is not null && folder is null);
            if (!inPlace.Contains(path) && !unread)
            {
                Settle(path, Write(home, copy, path, folder, session), written, waiting);
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
                case NodeWordActs.KeepYours:
                    break;
                case NodeWordActs.TakeSession:
                    Settle(path, Write(home, copy, path, folder, session), written, waiting);
                    break;
                case NodeWordActs.KeepBoth when session is not null:
                    var beside = Beside(path, copy.Id);
                    var there = place.Listed(beside)?.Sha256;
                    if (!string.Equals(there, session, StringComparison.Ordinal)) Settle(beside, Write(home, copy, beside, there, session, from: path), written, waiting);
                    break;
                case NodeWordActs.KeepBoth:
                    break;
                default:
                    waiting.Add(path);
                    break;
            }
        }

        return new(written, waiting, Nothing: false);
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

    /// <summary>
    /// Write the copy's <paramref name="from"/> (or its removal, where <paramref name="session"/> is null) over <paramref name="path"/> in
    /// the folder, only where the live file still holds <paramref name="expected"/> (null: absent); what it replaces is kept first.
    /// </summary>
    private static bool Write(string home, FolderCopy copy, string path, string? expected, string? session, string? from = null)
    {
        var target = Path.Combine(copy.Root, path);
        var temporary = session is null ? null : target + ".daoris-return";
        try
        {
            if (temporary is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(copy.Folder, from ?? path), temporary, overwrite: true);
            }

            if (!string.Equals(Live(target), expected, StringComparison.Ordinal))
            {
                if (temporary is not null) File.Delete(temporary);
                return false;
            }

            if (File.Exists(target))
            {
                var kept = Path.Combine(KeptFolder(home, copy), path);
                Directory.CreateDirectory(Path.GetDirectoryName(kept)!);
                File.Copy(target, kept, overwrite: true);
            }

            if (temporary is null) File.Delete(target);
            else File.Move(temporary, target, overwrite: true);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            if (temporary is not null && File.Exists(temporary)) File.Delete(temporary);
            return false;
        }
    }

    private static string? Live(string file)
    {
        if (!File.Exists(file)) return null;
        using var stream = AtomicFile.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}

/// <summary>What a return came to: the folder's paths it wrote or removed, those that wait for the person, and whether the copy changed nothing.</summary>
public sealed record FolderReturned(IReadOnlyList<string> Written, IReadOnlyList<string> Waiting, bool Nothing);
