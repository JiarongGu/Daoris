using System.Security.Cryptography;
using System.Text;
using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>What a tree holds beyond its head, each file hashed as read; the ignored ones the save takes as work named apart.</summary>
internal sealed record SaveListing(string Tree, string GitDir, string CommonDir, string Head, string? Branch, IReadOnlyList<SavedFile> Files, IReadOnlyList<string> Ignored);

/// <summary>
/// What a save reads of a tree, a unit of its own the save and the removal behind it both read (<see cref="TreeSaves"/>,
/// <see cref="TreeSaveGate"/>): each changed tracked path, each untracked file git does not ignore and each ignored file under the paths
/// named as work, hashed as read; or the refusal that holds the tree, which names everything the tree holds that a save cannot carry.
/// </summary>
internal static class TreeSaveListing
{
    /// <summary>The unresolved states of <c>status --porcelain</c>.</summary>
    private static readonly HashSet<string> Unmerged = new(StringComparer.Ordinal) { "DD", "AU", "UD", "UA", "DU", "AA", "UU" };

    /// <summary>The tree's own state that goes with it and that no commit carries, by its file in the tree's git folder.</summary>
    private static readonly string[] Underway = ["MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "BISECT_LOG", "rebase-merge", "rebase-apply"];

    /// <summary>The listing of the tree at <paramref name="tree"/>, taking the ignored files under <paramref name="work"/>, each path read literally.</summary>
    internal static async Task<(SaveListing? Listing, SaveOutcome? Refused)> ListAsync(string tree, IReadOnlyList<string> work, CancellationToken ct)
    {
        var full = TreeSaveRecords.Key(tree);
        if (!Directory.Exists(full)) return (null, SaveOutcome.No(SaveRefusal.NoTree, $"there is no folder at {tree}."));
        var where = await GitRuns.GitAsync(full, ["rev-parse", "--path-format=absolute", "--show-toplevel", "--git-dir", "--git-common-dir", "HEAD"], ct).ConfigureAwait(false);
        var lines = where.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (where.Code != 0 || lines.Length < 4) return (null, SaveOutcome.No(SaveRefusal.NoTree, $"{tree} is no git working tree with a commit: {FirstLine(where.Stderr)}"));
        if (!SamePath(lines[0], full)) return (null, SaveOutcome.No(SaveRefusal.NoTree, $"{tree} is not the top folder of its working tree, {lines[0]}."));
        var (gitDir, head) = (lines[1], lines[3]);

        var branch = await GitRuns.GitAsync(full, ["symbolic-ref", "--short", "-q", "HEAD"], ct).ConfigureAwait(false);
        var status = await GitRuns.GitAsync(
            full, ["--no-optional-locks", "-c", "core.quotePath=false", "status", "--porcelain=v1", "-z", "--untracked-files=all", "--no-renames", "--ignore-submodules=none"], ct).ConfigureAwait(false);
        if (status.Code != 0) return (null, SaveOutcome.No(SaveRefusal.Unread, $"git could not say what the tree holds: {FirstLine(status.Stderr)}"));
        var entries = status.Stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(entry => entry.Length > 3).ToList();
        var unresolved = entries.Where(entry => Unmerged.Contains(entry[..2])).Select(entry => entry[3..]).ToList();
        if (unresolved.Count > 0) return (null, SaveOutcome.No(SaveRefusal.Conflicted, $"its index holds unresolved files, which no commit can hold: {string.Join(", ", unresolved)}."));

        if (await OwnStateAsync(full, gitDir, ct).ConfigureAwait(false) is { } own)
        {
            return (null, SaveOutcome.No(SaveRefusal.Unsavable, $"it holds state of its own a save cannot carry, which would go with it: {own}."));
        }

        IReadOnlyList<string> ignored = [];
        if (work.Count > 0)
        {
            var named = await GitRuns.GitAsync(
                full, ["--literal-pathspecs", "-c", "core.quotePath=false", "ls-files", "-z", "--others", "--ignored", "--exclude-standard", "--error-unmatch", "--", .. work], ct).ConfigureAwait(false);
            if (named.Code != 0)
            {
                return (null, SaveOutcome.No(SaveRefusal.Unsavable, $"a path named as work names no ignored file in it ({string.Join(", ", work)}): {FirstLine(named.Stderr)}"));
            }

            ignored = named.Stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        }

        var deleted = entries.Where(entry => entry[..2].Contains('D')).Select(entry => entry[3..]).ToHashSet(StringComparer.Ordinal);
        var files = new List<SavedFile>();
        foreach (var path in entries.Select(entry => entry[3..]).Concat(ignored).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (path.Contains('\n') || path.Contains('\r')) return (null, SaveOutcome.No(SaveRefusal.Unsavable, $"`{path.ReplaceLineEndings(" ")}` has a line break in its name, which git's lists of paths cannot carry."));
            var at = Path.Combine(full, path.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(at))
            {
                if (File.GetAttributes(at).HasFlag(FileAttributes.ReparsePoint)) return (null, SaveOutcome.No(SaveRefusal.Unsavable, $"{path} is a link, which a save cannot hold as a file."));
                try
                {
                    files.Add(Hashed(path, at, head.Length == 64));
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    return (null, SaveOutcome.No(SaveRefusal.Unread, $"{path} could not be read: {error.Message}"));
                }

                continue;
            }

            // A tracked file the tree deleted, a folder in its place included, whose own files are listed apart.
            if (!path.EndsWith('/') && (!Directory.Exists(at) || deleted.Contains(path)))
            {
                files.Add(new SavedFile(path, null, 0, null));
                continue;
            }

            return (null, SaveOutcome.No(SaveRefusal.Unsavable, $"{path} is a folder git keeps apart (a nested repository or a submodule), which a save cannot hold."));
        }

        var trimmed = branch.Code == 0 ? branch.Stdout.Trim() : "";
        return (new SaveListing(full, gitDir, lines[2], head, trimmed.Length > 0 ? trimmed : null, files, ignored), null);
    }

    /// <summary>
    /// The tree's own state no commit carries, each named, in a clause: an operation under way, refs of its own, a submodule's git folder, a
    /// path marked assume-unchanged or skip-worktree (whose edits git's status does not see); null where it holds none.
    /// </summary>
    private static async Task<string?> OwnStateAsync(string tree, string gitDir, CancellationToken ct)
    {
        var named = Underway.Where(name => File.Exists(Path.Combine(gitDir, name)) || Directory.Exists(Path.Combine(gitDir, name))).ToList();
        var modules = Path.Combine(gitDir, "modules");
        if (Directory.Exists(modules) && Directory.EnumerateFileSystemEntries(modules).Any()) named.Add("a submodule's git folder (modules)");

        var refs = await GitRuns.GitAsync(tree, ["for-each-ref", "--format=%(refname)", "refs/worktree/", "refs/bisect/"], ct).ConfigureAwait(false);
        if (refs.Code != 0) return $"its own refs, which git could not list: {FirstLine(refs.Stderr)}";
        named.AddRange(refs.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var index = await GitRuns.GitAsync(tree, ["-c", "core.quotePath=false", "ls-files", "-v", "-z"], ct).ConfigureAwait(false);
        if (index.Code != 0) return $"an index git could not read: {FirstLine(index.Stderr)}";
        // `ls-files -v` tags an assume-unchanged entry in lower case, and a skip-worktree one `S`.
        var hidden = index.Stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(entry => entry.Length > 2 && (char.IsLower(entry[0]) || entry[0] == 'S')).Select(entry => entry[2..]).ToList();
        if (hidden.Count > 0) named.Add($"{hidden.Count} path(s) marked assume-unchanged or skip-worktree ({string.Join(", ", hidden.Take(5))}{(hidden.Count > 5 ? " and more" : "")})");
        return named.Count == 0 ? null : string.Join(", ", named);
    }

    /// <summary>One file read once: the SHA-256 of its bytes and its git blob id, as <c>hash-object --no-filters</c> names it.</summary>
    private static SavedFile Hashed(string path, string at, bool sha256Objects)
    {
        using var stream = new FileStream(at, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16);
        var length = stream.Length;
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var blob = IncrementalHash.CreateHash(sha256Objects ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA1);
        blob.AppendData(Encoding.ASCII.GetBytes($"blob {length}\0"));
        var buffer = new byte[1 << 16];
        long read = 0;
        int count;
        while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            sha.AppendData(buffer, 0, count);
            blob.AppendData(buffer, 0, count);
            read += count;
        }

        // A file that grows or shrinks during the read names no blob git would store.
        var id = read == length ? Convert.ToHexStringLower(blob.GetHashAndReset()) : "";
        return new SavedFile(path, Convert.ToHexStringLower(sha.GetHashAndReset()), read, id);
    }
}
