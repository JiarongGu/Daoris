using System.Diagnostics;
using System.Security.Cryptography;

namespace Daoris.Driver;

/// <summary>
/// Reads the manifest of a folder that is no git repository: every file under its root but the excludes, with its size, its last write
/// time and its SHA-256, each text file's content kept in the place's store. The walk is a stat walk that opens no file; a file is hashed
/// only where its size or time moved since the place's last manifest, or its time lies within two seconds of that manifest's read (git's
/// racy-clean rule), so a second manifest costs the walk alone. Links are not followed, and a file that cannot be read is listed with its
/// size and time and no hash.
/// </summary>
public static class FolderManifestReader
{
    private const int Kept = 1 << 20;
    private static readonly TimeSpan Racy = TimeSpan.FromSeconds(2);

    private static readonly EnumerationOptions Walk = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        ReturnSpecialDirectories = false,
    };

    /// <summary>The manifest of the folder at <paramref name="root"/>, its hashes reused from <paramref name="last"/> where they still stand.</summary>
    /// <returns>The manifest, or one that lists nothing and says it is too large where <paramref name="bounds"/> stop it.</returns>
    public static RunManifest Read(
        string root, ContentStore content, RunManifest? last, ManifestBounds bounds, PlaceExcludes? excludes = null, CancellationToken ct = default)
    {
        excludes ??= PlaceExcludes.Default;
        var at = DateTimeOffset.UtcNow;
        var clock = Stopwatch.StartNew();
        var known = last is { Kind: PlaceKind.Folder, TooLarge: false }
            ? last.Entries.ToDictionary(entry => entry.Path, StringComparer.Ordinal)
            : [];
        var entries = new List<ManifestEntry>();
        foreach (var file in new DirectoryInfo(root).EnumerateFiles("*", Walk))
        {
            ct.ThrowIfCancellationRequested();
            if (entries.Count >= bounds.Files || clock.Elapsed > bounds.Time) return TooLarge(root, at);
            var path = Path.GetRelativePath(root, file.FullName).Replace(Path.DirectorySeparatorChar, '/');
            if (excludes.Passes(path)) continue;
            var time = new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero);
            var sha = known.TryGetValue(path, out var was) && was.Sha256 is not null && was.Size == file.Length && was.Time == time && last!.At - time > Racy
                ? was.Sha256
                : Hash(file, content);
            entries.Add(new ManifestEntry(path, "", sha, file.Length) { Time = time });
        }

        return new RunManifest(PlaceKind.Folder, null, null, at, [.. entries.OrderBy(entry => entry.Path, StringComparer.Ordinal)])
        {
            Root = root,
            Excludes = excludes.Own,
        };
    }

    private static RunManifest TooLarge(string root, DateTimeOffset at) =>
        new(PlaceKind.Folder, null, null, at, []) { Root = root, TooLarge = true };

    private static string? Hash(FileInfo file, ContentStore content)
    {
        try
        {
            if (file.Length > Kept)
            {
                using var stream = AtomicFile.OpenRead(file.FullName);
                return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            }

            var bytes = AtomicFile.ReadBytes(file.FullName);
            var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            content.Keep(sha, bytes);
            return sha;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
