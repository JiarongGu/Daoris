using System.Globalization;
using System.Text.Json.Nodes;
using static Daoris.Driver.AccountFileNodes;

namespace Daoris.Driver;

/// <summary>One file a version question starts, as it stands: its whole path, its size and its last write to the tick.</summary>
public sealed record BinaryFile(string Path, long Size, string Written)
{
    /// <summary>The file at <paramref name="path"/> as it stands now, or null where there is none.</summary>
    public static BinaryFile? Of(string path)
    {
        try
        {
            var file = new FileInfo(path);
            return file.Exists ? new BinaryFile(file.FullName, file.Length, file.LastWriteTimeUtc.ToString("O", CultureInfo.InvariantCulture)) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}

/// <summary>What a door's binary answered its version question, of which files, and when.</summary>
/// <param name="Files">The files the question started, as they stand; none where there is nothing to start.</param>
/// <param name="Present">Whether it answered.</param>
/// <param name="Version">The first line it answered with.</param>
/// <param name="Problem">Why it is absent.</param>
public sealed record BinaryRead(IReadOnlyList<BinaryFile> Files, bool Present, string? Version, string? Problem, DateTimeOffset At)
{
    /// <summary>Whether this reading is of the files a question would start now.</summary>
    public bool Of(IReadOnlyList<BinaryFile> files) => Files.SequenceEqual(files);
}

/// <summary>
/// What each door's binary last answered its version question, kept in <c>reads.json</c> beside the accounts' readings: under
/// the door's name, its <c>binary</c>. A version is read again only where the files a question would start differ from the ones it
/// came from, at a press, or where an install, an update or a pin changed it, so a look, a list and a restart start no process.
/// </summary>
/// <remarks>
/// The entry's shape: <c>{ "files": [ { "path": "…", "size": 1234, "written": "…" } ], "present": true, "version": "…", "read": "…Z" }</c>,
/// with <c>problem</c> where it is absent. Written atomically with the accounts' readings' writer, keeping what it has no field for.
/// </remarks>
public static class BinaryReads
{
    private const string Field = "binary";

    /// <summary>What <paramref name="door"/>'s binary last answered; null where nothing is read, or it does not read.</summary>
    public static BinaryRead? Of(string home, string door)
    {
        if (Child(Child(Load(AccountReads.PathOf(home)), door), Field) is not { } entry
            || entry["files"] is not JsonArray listed
            || entry["present"] is not JsonValue present || !present.TryGetValue<bool>(out var answered)
            || Moment(entry, "read") is not { } at)
        {
            return null;
        }

        var files = new List<BinaryFile>();
        foreach (var node in listed)
        {
            if (node is not JsonObject file || Json.Text(file, "path") is not { } path || Json.Text(file, "written") is not { } written
                || file["size"] is not JsonValue size || !size.TryGetValue<long>(out var bytes))
            {
                return null;
            }

            files.Add(new BinaryFile(path, bytes, written));
        }

        return new BinaryRead(files, answered, Json.Text(entry, "version"), Json.Text(entry, "problem"), at);
    }

    /// <summary>A reading of <paramref name="door"/>'s binary, in place of the one before.</summary>
    public static void Keep(string home, string door, BinaryRead read)
    {
        var entry = new JsonObject
        {
            ["files"] = new JsonArray([.. read.Files.Select(file => (JsonNode)new JsonObject
            {
                ["path"] = file.Path,
                ["size"] = file.Size,
                ["written"] = file.Written,
            })]),
            ["present"] = read.Present,
        };
        if (read.Version is { } version) entry["version"] = version;
        if (read.Problem is { } problem) entry["problem"] = problem;
        entry["read"] = AccountCooling.Stamp(read.At);

        lock (AccountReads.Gate)
        {
            var root = Load(AccountReads.PathOf(home));
            var held = Made(root, door);
            if (Key(held, Field) is { } old) held.Remove(old);
            held[Field] = entry;
            JsonFile.Write(AccountReads.PathOf(home), root);
        }
    }
}
