using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>A file of a dependency's folder: its path inside the folder, its size, its content's hash and its write time.</summary>
internal sealed record ManifestFile(string Path, long Size, string Hash, long Written);

/// <summary>A link of a dependency's folder: its path inside the folder, and where it leads, relative to the tree's top.</summary>
internal sealed record ManifestLink(string Path, string Target, bool Folder);

/// <summary>
/// What a dependency's folder holds, read never following a link and passing over its rule's <c>caches</c>: each file's size and hash,
/// each folder, and each link's target within the tree. A store entry keeps one beside its copy; a placement's record keeps the copy's,
/// with each file's write time, so a take reads it again stat first and hashes only a file whose stat differs.
/// </summary>
internal sealed record DependencyManifest(IReadOnlyList<ManifestFile> Files, IReadOnlyList<string> Folders, IReadOnlyList<ManifestLink> Links)
{
    internal static readonly DependencyManifest Empty = new([], [], []);

    internal long Bytes => Files.Sum(each => each.Size);

    /// <summary>
    /// The folder <paramref name="folder"/> of <paramref name="tree"/> read whole; <paramref name="outside"/> names the first link that
    /// leads outside the tree, which no store entry or placement may hold.
    /// </summary>
    internal static DependencyManifest Read(string tree, string folder, IReadOnlyList<string> caches, out string? outside, CancellationToken ct = default)
    {
        outside = null;
        var top = Path.Combine(tree, folder);
        var files = new List<ManifestFile>();
        var folders = new List<string>();
        var links = new List<ManifestLink>();
        var options = new EnumerationOptions { IgnoreInaccessible = false, AttributesToSkip = 0, RecurseSubdirectories = false };
        var walk = new Stack<string>();
        walk.Push(top);
        while (walk.TryPop(out var at))
        {
            ct.ThrowIfCancellationRequested();
            foreach (var entry in new DirectoryInfo(at).EnumerateFileSystemInfos("*", options))
            {
                var relative = Path.GetRelativePath(top, entry.FullName).Replace('\\', '/');
                if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    var leads = DependencyLinks.Leads(entry);
                    if (leads is null || !DependencyLinks.Within(tree, leads))
                    {
                        outside ??= relative;
                        continue;
                    }

                    links.Add(new ManifestLink(relative, Path.GetRelativePath(tree, leads).Replace('\\', '/'), entry is DirectoryInfo));
                }
                else if (entry is DirectoryInfo)
                {
                    if (Cached(relative + "/", caches)) continue;
                    folders.Add(relative);
                    walk.Push(entry.FullName);
                }
                else
                {
                    var file = (FileInfo)entry;
                    files.Add(new ManifestFile(relative, file.Length, HashOf(file.FullName), file.LastWriteTimeUtc.Ticks));
                }
            }
        }

        return new([.. files.OrderBy(each => each.Path, StringComparer.Ordinal)], [.. folders.Order(StringComparer.Ordinal)], [.. links.OrderBy(each => each.Path, StringComparer.Ordinal)]);
    }

    /// <summary>
    /// Why <paramref name="folder"/> of <paramref name="tree"/> no longer holds what this record says, or null where it does: a file,
    /// folder or link missing, changed or added outside <paramref name="caches"/>, a file's stat read first and its hash only where the
    /// stat differs.
    /// </summary>
    internal string? Differs(string tree, string folder, IReadOnlyList<string> caches)
    {
        var top = Path.Combine(tree, folder);
        if (!Directory.Exists(top) || new DirectoryInfo(top).Attributes.HasFlag(FileAttributes.ReparsePoint)) return "its folder is gone";
        var files = Files.ToDictionary(each => each.Path, StringComparer.Ordinal);
        var folders = new HashSet<string>(Folders, StringComparer.Ordinal);
        var links = Links.ToDictionary(each => each.Path, StringComparer.Ordinal);
        var seen = 0;
        var options = new EnumerationOptions { IgnoreInaccessible = false, AttributesToSkip = 0, RecurseSubdirectories = false };
        var walk = new Stack<string>();
        walk.Push(top);
        while (walk.TryPop(out var at))
        {
            foreach (var entry in new DirectoryInfo(at).EnumerateFileSystemInfos("*", options))
            {
                var relative = Path.GetRelativePath(top, entry.FullName).Replace('\\', '/');
                if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    var leads = DependencyLinks.Leads(entry);
                    if (!links.TryGetValue(relative, out var link) || leads is null
                        || !string.Equals(Path.GetRelativePath(tree, leads).Replace('\\', '/'), link.Target, StringComparison.Ordinal))
                    {
                        return $"`{relative}` is a link it did not make";
                    }

                    seen++;
                }
                else if (entry is DirectoryInfo)
                {
                    if (Cached(relative + "/", caches)) continue;
                    if (!folders.Contains(relative)) return $"`{relative}/` was added";
                    seen++;
                    walk.Push(entry.FullName);
                }
                else
                {
                    var file = (FileInfo)entry;
                    if (!files.TryGetValue(relative, out var made)) return $"`{relative}` was added";
                    if (file.Length != made.Size || (file.LastWriteTimeUtc.Ticks != made.Written && HashOf(file.FullName) != made.Hash)) return $"`{relative}` was changed";
                    seen++;
                }
            }
        }

        return seen == files.Count + folders.Count + links.Count ? null : "a file it placed was removed";
    }

    /// <summary>This manifest with each file's write time as <paramref name="tree"/>'s copy at <paramref name="folder"/> has it.</summary>
    internal DependencyManifest WrittenIn(string tree, string folder) => this with
    {
        Files = [.. Files.Select(each => each with { Written = File.GetLastWriteTimeUtc(Path.Combine(tree, folder, each.Path)).Ticks })],
    };

    internal static bool Cached(string relative, IReadOnlyList<string> caches) => caches.Any(cache => RoomPaths.Declared(cache).IsMatch(relative));

    internal static string HashOf(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    internal JsonObject ToJson() => new()
    {
        ["files"] = new JsonArray([.. Files.Select(each => (JsonNode)new JsonArray(each.Path, each.Size, each.Hash, each.Written))]),
        ["folders"] = new JsonArray([.. Folders.Select(each => (JsonNode)JsonValue.Create(each)!)]),
        ["links"] = new JsonArray([.. Links.Select(each => (JsonNode)new JsonArray(each.Path, each.Target, each.Folder))]),
    };

    /// <summary>A manifest read back; null where it does not read, which no placement or entry is then trusted on.</summary>
    internal static DependencyManifest? FromJson(JsonElement json)
    {
        try
        {
            return new(
                [.. json.GetProperty("files").EnumerateArray().Select(each => new ManifestFile(each[0].GetString()!, each[1].GetInt64(), each[2].GetString()!, each[3].GetInt64()))],
                [.. json.GetProperty("folders").EnumerateArray().Select(each => each.GetString()!)],
                [.. json.GetProperty("links").EnumerateArray().Select(each => new ManifestLink(each[0].GetString()!, each[1].GetString()!, each[2].GetBoolean()))]);
        }
        catch (Exception error) when (error is InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException or FormatException)
        {
            return null;
        }
    }
}
