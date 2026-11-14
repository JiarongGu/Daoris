using System.Security.Cryptography;
using Daoris.Ports.Repositories;

namespace Daoris.Driver;

/// <summary>Reads a place's manifest: git's status and the hash of each path it lists, whose text goes to the place's content store.</summary>
public static class ManifestReader
{
    private const int Kept = 1 << 20;

    /// <summary>The manifest of the git place at <paramref name="root"/>.</summary>
    /// <exception cref="InvalidOperationException">git could not read its status.</exception>
    public static async Task<RunManifest> OfPlaceAsync(string root, ContentStore content, CancellationToken ct)
    {
        var repository = MachinePorts.Repository;
        IReadOnlyList<GitStatusEntry> status;
        try
        {
            status = await repository.StatusEntriesAsync(root, ct).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            throw new InvalidOperationException($"git status failed in the place: {error.Message}", error);
        }

        var head = await repository.HeadAsync(root, ct).ConfigureAwait(false);
        var branch = await repository.BranchAsync(root, ct).ConfigureAwait(false);
        var entries = new List<ManifestEntry>();
        foreach (var each in status)
        {
            if (each.From is { Length: > 0 } from) entries.Add(new ManifestEntry(from, each.State, null, null));
            entries.Add(Entry(root, each.Path, each.State, content));
        }

        return new RunManifest("git", head, branch, DateTimeOffset.UtcNow, [.. entries.OrderBy(entry => entry.Path, StringComparer.Ordinal)]);
    }

    /// <summary>The paths the commits from <paramref name="from"/> to <paramref name="to"/> changed; none where either is unknown or both are one.</summary>
    public static async Task<IReadOnlyList<string>> CommittedBetweenAsync(string root, string? from, string? to, CancellationToken ct)
    {
        if (from is null || to is null || string.Equals(from, to, StringComparison.Ordinal)) return [];
        return await MachinePorts.Repository.ChangedAsync(root, from, to, ct).ConfigureAwait(false) ?? [];
    }

    private static ManifestEntry Entry(string root, string path, string status, ContentStore content)
    {
        var full = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
        var file = new FileInfo(full);
        if (!file.Exists) return new ManifestEntry(path, status, null, null);
        try
        {
            if (file.Length > Kept)
            {
                using var stream = AtomicFile.OpenRead(full);
                return new ManifestEntry(path, status, Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(), file.Length);
            }

            var bytes = AtomicFile.ReadBytes(full);
            var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            content.Keep(sha, bytes);
            return new ManifestEntry(path, status, sha, bytes.LongLength);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A file another program holds is still there: listed under a hash of its size and time, which names no stored text, never as gone.
            return new ManifestEntry(path, status, $"unread-{file.Length}-{file.LastWriteTimeUtc.Ticks}", file.Length);
        }
    }
}
