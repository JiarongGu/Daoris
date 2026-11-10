using System.Security.Cryptography;

namespace Daoris.Driver;

/// <summary>
/// Reads a place's manifest. A git place costs git's status, whose index already caches the stat data, plus the hash of each path it
/// lists; ignored paths are never listed. Each listed file's text goes to the place's content store.
/// </summary>
public static class ManifestReader
{
    /// <summary>The manifest of the git place at <paramref name="root"/>.</summary>
    /// <exception cref="InvalidOperationException">git could not read its status.</exception>
    public static async Task<RunManifest> GitAsync(string root, ContentStore content, CancellationToken ct)
    {
        var (code, stdout, stderr) = await WorkingTree.GitAsync(
            root, ["--no-optional-locks", "status", "--porcelain=v2", "-z", "--branch", "--untracked-files=all"], ct).ConfigureAwait(false);
        if (code != 0) throw new InvalidOperationException($"git status failed in the place: {stderr.Trim()}");

        string? head = null, branch = null;
        var entries = new List<ManifestEntry>();
        var fields = stdout.Split('\0');
        for (var i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            if (field.StartsWith("# branch.oid ", StringComparison.Ordinal))
            {
                head = field["# branch.oid ".Length..] is var oid && oid != "(initial)" ? oid : null;
                continue;
            }

            if (field.StartsWith("# branch.head ", StringComparison.Ordinal))
            {
                branch = field["# branch.head ".Length..] is var named && named != "(detached)" ? named : null;
                continue;
            }

            var (status, path) = field.Length < 2 ? ("", "") : field[0] switch
            {
                '1' => (field.Split(' ', 9)[1], field.Split(' ', 9)[8]),
                '2' => (field.Split(' ', 10)[1], field.Split(' ', 10)[9]),
                'u' => (field.Split(' ', 11)[1], field.Split(' ', 11)[10]),
                '?' => ("??", field[2..]),
                _ => ("", ""),
            };
            if (field.StartsWith('2')) i++;
            if (path.Length == 0) continue;
            entries.Add(Entry(root, path, status, content));
        }

        return new RunManifest("git", head, branch, DateTimeOffset.UtcNow, [.. entries.OrderBy(entry => entry.Path, StringComparer.Ordinal)]);
    }

    /// <summary>The paths the commits from <paramref name="from"/> to <paramref name="to"/> changed; none where either is unknown or both are one.</summary>
    public static async Task<IReadOnlyList<string>> CommittedBetweenAsync(string root, string? from, string? to, CancellationToken ct)
    {
        if (from is null || to is null || string.Equals(from, to, StringComparison.Ordinal)) return [];
        var (code, stdout, _) = await WorkingTree.GitAsync(root, ["--no-optional-locks", "diff", "--name-only", "-z", from, to], ct).ConfigureAwait(false);
        return code == 0 ? stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries) : [];
    }

    private static ManifestEntry Entry(string root, string path, string status, ContentStore content)
    {
        var full = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(full)) return new ManifestEntry(path, status, null, null);
        var bytes = AtomicFile.ReadBytes(full);
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        content.Keep(sha, bytes);
        return new ManifestEntry(path, status, sha, bytes.LongLength);
    }
}
