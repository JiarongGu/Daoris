using System.Security.Cryptography;
using System.Text;
using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// The save step, a unit of its own: a tree's head pinned under a kept ref in its repository, then its staged, unstaged and
/// untracked-not-ignored files and the ignored paths the repository names as work committed onto that ref, and the record written. A
/// restore puts a save back into an empty folder and proves every listed file identical by hash.
/// </summary>
/// <remarks>
/// <para><b>Three commits under one ref.</b> The ref <c>refs/daoris/saves/&lt;branch&gt;/&lt;n&gt;</c> holds the save commit: the head's
/// tree with every listed file as git reads it (its filters applied, so the landed test compares it with the line), whose parents are the
/// head, the commit of the index as staged and the commit of every listed file's bytes as written (no filter, so a restore writes them
/// back byte for byte where a line-ending filter would rewrite them). Each is made through an index of its own beside the tree's, so the
/// tree's index and files are never written.</para>
/// <para><b>Holds the tree when it fails.</b> A failed save is a typed refusal that says why and leaves the tree as it is; the head's pin
/// goes again where this save made it, and a save whose record could not be written keeps its ref and says so. A file whose bytes move
/// during its save is refused by its blob, which is hashed as it is read and again as git stores it.</para>
/// <para>Nothing here removes a tree: a removal asks for a save that still reads the tree as it is (<see cref="TreeSaveDoors"/>).</para>
/// </remarks>
public sealed class TreeSaves(string home, TimeProvider clock)
{
    /// <summary>The namespace the saves' refs are kept under.</summary>
    public const string Namespace = "refs/daoris/saves/";

    /// <summary>The name a save's commits carry: they are this machine's, kept under its refs and never pushed.</summary>
    private static readonly IReadOnlyDictionary<string, string> Maker = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["GIT_AUTHOR_NAME"] = "Daoris", ["GIT_AUTHOR_EMAIL"] = "saves@daoris.invalid",
        ["GIT_COMMITTER_NAME"] = "Daoris", ["GIT_COMMITTER_EMAIL"] = "saves@daoris.invalid",
    };

    /// <summary>The unresolved states of <c>status --porcelain</c>.</summary>
    private static readonly HashSet<string> Unmerged = new(StringComparer.Ordinal) { "DD", "AU", "UD", "UA", "DU", "AA", "UU" };

    /// <summary>What a tree holds beyond its head, each file hashed as read; the ignored ones the save takes as work named apart.</summary>
    internal sealed record Listing(string Tree, string GitDir, string CommonDir, string Head, string? Branch, IReadOnlyList<SavedFile> Files, IReadOnlyList<string> Ignored);

    /// <summary>Saves the tree at <paramref name="tree"/>, taking the ignored paths <paramref name="work"/> names as work.</summary>
    public async Task<SaveOutcome> SaveAsync(string tree, string workspace, string repository, IReadOnlyList<string> work, CancellationToken ct = default)
    {
        var began = clock.GetTimestamp();
        var (listing, refused) = await ListAsync(tree, work, ct).ConfigureAwait(false);
        if (listing is null) return refused!;

        var name = listing.Branch ?? Path.GetFileName(listing.Tree);
        var prefix = $"{Namespace}{name}/";
        var kept = await GitRuns.GitAsync(listing.Tree, ["for-each-ref", "--format=%(refname)", prefix], ct).ConfigureAwait(false);
        if (kept.Code != 0) return SaveOutcome.No(SaveRefusal.Unread, $"git could not list the saves under `{prefix}`: {FirstLine(kept.Stderr)}");
        var number = kept.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(each => int.TryParse(each[Math.Min(prefix.Length, each.Length)..], out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
        var reference = prefix + number;

        // The head first, under a ref that must not stand yet: from here a kill leaves the head kept, never less.
        var pin = await GitRuns.GitAsync(listing.Tree, ["update-ref", "-m", "daoris save: the head pinned", reference, listing.Head, ""], ct).ConfigureAwait(false);
        if (pin.Code != 0) return SaveOutcome.No(SaveRefusal.Unread, $"git would not pin the head under `{reference}`: {FirstLine(pin.Stderr)}");

        var scratch = Path.Combine(listing.GitDir, $"index.daoris-save-{Guid.NewGuid():N}");
        (string Save, string Staged, string Raw) commits;
        try
        {
            var (made, why) = await CommitsAsync(listing, scratch, name, ct).ConfigureAwait(false);
            (int Code, string Stdout, string Stderr)? moved = made is null ? null : await GitRuns.GitAsync(
                listing.Tree, ["update-ref", "-m", "daoris save", reference, made.Value.Save, listing.Head], ct).ConfigureAwait(false);
            if (made is null || moved!.Value.Code != 0)
            {
                await GitRuns.GitAsync(listing.Tree, ["update-ref", "-d", reference, listing.Head], CancellationToken.None).ConfigureAwait(false);
                return why ?? SaveOutcome.No(SaveRefusal.Unread, $"git would not move `{reference}` to the save: {FirstLine(moved!.Value.Stderr)}");
            }

            commits = made.Value;
        }
        finally
        {
            foreach (var file in new[] { ".i", ".r", ".s" }) Forget(scratch + file);
        }

        var save = new TreeSave(
            reference, workspace, repository, Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(listing.CommonDir))!, listing.Tree, listing.Branch,
            listing.Head, commits.Save, commits.Staged, commits.Raw, work, listing.Files, listing.Files.Sum(file => file.Bytes),
            (long)clock.GetElapsedTime(began).TotalMilliseconds, clock.GetUtcNow());
        try
        {
            new TreeSaveRecords(home).Keep(save);
        }
        catch (DriverException error)
        {
            return SaveOutcome.No(SaveRefusal.Unrecorded, $"`{reference}` holds the save, but its record could not be written: {error.Message}");
        }

        return SaveOutcome.Of(save);
    }

    /// <summary>
    /// What the tree at <paramref name="tree"/> holds beyond its head: each changed tracked path, each untracked file git does not ignore and
    /// each ignored file under the paths <paramref name="work"/> names, hashed as read; or the refusal that holds it.
    /// </summary>
    internal static async Task<(Listing? Listing, SaveOutcome? Refused)> ListAsync(string tree, IReadOnlyList<string> work, CancellationToken ct)
    {
        var full = TreeSaveRecords.Key(tree);
        if (!Directory.Exists(full)) return (null, SaveOutcome.No(SaveRefusal.NoTree, $"there is no folder at {tree}."));
        var where = await GitRuns.GitAsync(full, ["rev-parse", "--path-format=absolute", "--show-toplevel", "--git-dir", "--git-common-dir", "HEAD"], ct).ConfigureAwait(false);
        var lines = where.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (where.Code != 0 || lines.Length < 4) return (null, SaveOutcome.No(SaveRefusal.NoTree, $"{tree} is no git working tree with a commit: {FirstLine(where.Stderr)}"));
        if (!SamePath(lines[0], full)) return (null, SaveOutcome.No(SaveRefusal.NoTree, $"{tree} is not the top folder of its working tree, {lines[0]}."));

        var branch = await GitRuns.GitAsync(full, ["symbolic-ref", "--short", "-q", "HEAD"], ct).ConfigureAwait(false);
        var status = await GitRuns.GitAsync(
            full, ["-c", "core.quotePath=false", "status", "--porcelain=v1", "-z", "--untracked-files=all", "--no-renames", "--ignore-submodules=none"], ct).ConfigureAwait(false);
        if (status.Code != 0) return (null, SaveOutcome.No(SaveRefusal.Unread, $"git could not say what the tree holds: {FirstLine(status.Stderr)}"));
        var entries = status.Stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(entry => entry.Length > 3).ToList();
        var unresolved = entries.Where(entry => Unmerged.Contains(entry[..2])).Select(entry => entry[3..]).ToList();
        if (unresolved.Count > 0) return (null, SaveOutcome.No(SaveRefusal.Conflicted, $"its index holds unresolved files, which no commit can hold: {string.Join(", ", unresolved)}."));

        IReadOnlyList<string> ignored = [];
        if (work.Count > 0)
        {
            var named = await GitRuns.GitAsync(full, ["-c", "core.quotePath=false", "ls-files", "-z", "--others", "--ignored", "--exclude-standard", "--", .. work], ct).ConfigureAwait(false);
            if (named.Code != 0) return (null, SaveOutcome.No(SaveRefusal.Unread, $"git could not list the ignored work: {FirstLine(named.Stderr)}"));
            ignored = named.Stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        }

        var head = lines[3];
        var files = new List<SavedFile>();
        foreach (var path in entries.Select(entry => entry[3..]).Concat(ignored).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var at = Path.Combine(full, path.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));
            if (path.EndsWith('/') || Directory.Exists(at)) return (null, SaveOutcome.No(SaveRefusal.Unsavable, $"{path} is a folder git keeps apart (a nested repository or a submodule), which a save cannot hold."));
            if (!File.Exists(at))
            {
                files.Add(new SavedFile(path, null, 0, null));
                continue;
            }

            if (File.GetAttributes(at).HasFlag(FileAttributes.ReparsePoint)) return (null, SaveOutcome.No(SaveRefusal.Unsavable, $"{path} is a link, which a save cannot hold as a file."));
            try
            {
                files.Add(Hashed(path, at, head.Length == 64));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return (null, SaveOutcome.No(SaveRefusal.Unread, $"{path} could not be read: {error.Message}"));
            }
        }

        var trimmed = branch.Code == 0 ? branch.Stdout.Trim() : "";
        return (new Listing(full, lines[1], lines[2], head, trimmed.Length > 0 ? trimmed : null, files, ignored), null);
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

    /// <summary>The three commits: the index as staged, the files as written, and the save on both and the head.</summary>
    private static async Task<((string Save, string Staged, string Raw)? Made, SaveOutcome? Refused)> CommitsAsync(
        Listing listing, string scratch, string name, CancellationToken ct)
    {
        var index = Path.Combine(listing.GitDir, "index");
        string staged = scratch + ".i", raw = scratch + ".r", saved = scratch + ".s";
        if (File.Exists(index))
        {
            File.Copy(index, staged);
            File.Copy(index, saved);
        }

        SaveOutcome Failed(string what, string stderr) => SaveOutcome.No(SaveRefusal.Unread, $"git could not {what}: {FirstLine(stderr)}");

        async Task<(int Code, string Out, string Err)> Git(string? indexFile, IReadOnlyList<string> arguments, string? input = null)
        {
            var environment = new Dictionary<string, string>(Maker, StringComparer.Ordinal);
            if (indexFile is not null) environment["GIT_INDEX_FILE"] = indexFile;
            var (code, stdout, stderr) = input is null
                ? await GitRuns.GitAsync(listing.Tree, arguments, environment, GitBounds.Work, ct).ConfigureAwait(false)
                : await GitRuns.GitAsync(listing.Tree, arguments, environment, input, ct).ConfigureAwait(false);
            return (code, stdout.Trim(), stderr);
        }

        if (!File.Exists(index) && (await Git(staged, ["read-tree", listing.Head]).ConfigureAwait(false)) is { Code: not 0 } unread) return (null, Failed("read the head into an index", unread.Err));
        var stagedTree = await Git(staged, ["write-tree"]).ConfigureAwait(false);
        if (stagedTree.Code != 0) return (null, Failed("write the index as a tree", stagedTree.Err));
        var stagedCommit = await Git(null, ["commit-tree", stagedTree.Out, "-p", listing.Head, "-m", $"daoris save of {name}: the index as staged"]).ConfigureAwait(false);
        if (stagedCommit.Code != 0) return (null, Failed("commit the index", stagedCommit.Err));

        var written = listing.Files.Where(file => !file.Deleted).ToList();
        if (written.Count > 0)
        {
            var stored = await Git(null, ["hash-object", "-w", "--no-filters", "--stdin-paths"], string.Join('\n', written.Select(file => file.Path)) + "\n").ConfigureAwait(false);
            var blobs = stored.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (stored.Code != 0 || blobs.Length != written.Count) return (null, Failed("store the files' bytes", stored.Err));
            for (var at = 0; at < written.Count; at++)
            {
                if (!string.Equals(blobs[at], written[at].Blob, StringComparison.Ordinal))
                {
                    return (null, SaveOutcome.No(SaveRefusal.Moving, $"{written[at].Path} changed while it was saved; save it again once nothing writes it."));
                }
            }

            var placed = await Git(raw, ["update-index", "--add", "--index-info"], string.Concat(written.Select(file => $"100644 {file.Blob}\t{file.Path}\n"))).ConfigureAwait(false);
            if (placed.Code != 0) return (null, Failed("place the files' bytes in an index", placed.Err));
        }

        var rawTree = await Git(raw, ["write-tree"]).ConfigureAwait(false);
        var rawCommit = rawTree.Code != 0 ? rawTree : await Git(null, ["commit-tree", rawTree.Out, "-m", $"daoris save of {name}: the files as written"]).ConfigureAwait(false);
        if (rawCommit.Code != 0) return (null, Failed("commit the files' bytes", rawCommit.Err));

        if (!File.Exists(index) && (await Git(saved, ["read-tree", listing.Head]).ConfigureAwait(false)) is { Code: not 0 } unreadSave) return (null, Failed("read the head into an index", unreadSave.Err));
        var all = await Git(saved, ["add", "-A"]).ConfigureAwait(false);
        if (all.Code != 0) return (null, Failed("add the tree's files to the save", all.Err));
        if (listing.Ignored.Count > 0)
        {
            var forced = await Git(saved, ["add", "-f", "--pathspec-from-file=-", "--pathspec-file-nul"], string.Join('\0', listing.Ignored)).ConfigureAwait(false);
            if (forced.Code != 0) return (null, Failed("add the ignored work to the save", forced.Err));
        }

        var saveTree = await Git(saved, ["write-tree"]).ConfigureAwait(false);
        if (saveTree.Code != 0) return (null, Failed("write the save as a tree", saveTree.Err));
        var saveCommit = await Git(null, ["commit-tree", saveTree.Out, "-p", listing.Head, "-p", stagedCommit.Out, "-p", rawCommit.Out, "-m", $"daoris save of {name}"]).ConfigureAwait(false);
        if (saveCommit.Code != 0) return (null, Failed("commit the save", saveCommit.Err));
        return ((saveCommit.Out, stagedCommit.Out, rawCommit.Out), null);
    }

    /// <summary>
    /// Puts <paramref name="save"/> back into the empty folder <paramref name="folder"/>: a working tree of its repository at the save's head,
    /// on its branch where that branch still stands there and is checked out nowhere, its index as staged and every listed file's bytes as
    /// written; then proves every listed file identical by hash, the head and the index.
    /// </summary>
    public static async Task<RestoreProof> RestoreAsync(TreeSave save, string folder, CancellationToken ct = default)
    {
        var full = TreeSaveRecords.Key(folder);
        if (Directory.Exists(full) && Directory.EnumerateFileSystemEntries(full).Any()) return new(false, [], $"{folder} is not empty: a save is restored only into an empty folder.");
        var held = await GitRuns.GitAsync(save.Root, ["rev-parse", "--verify", "--quiet", save.Id], ct).ConfigureAwait(false);
        if (held.Stdout.Trim() != save.Commit) return new(false, [], $"`{save.Id}` no longer holds this save in {save.Root}.");

        // Made with no checkout, so no hook of the repository's runs and writes into the folder before it is proved; the head's files are
        // written by the read that follows.
        var onBranch = false;
        if (save.Branch is { } branch
            && (await GitRuns.GitAsync(save.Root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], ct).ConfigureAwait(false)).Stdout.Trim() == save.Head)
        {
            onBranch = (await GitRuns.GitAsync(save.Root, ["worktree", "add", "--no-checkout", full, branch], ct).ConfigureAwait(false)).Code == 0;
        }

        if (!onBranch)
        {
            var added = await GitRuns.GitAsync(save.Root, ["worktree", "add", "--no-checkout", "--detach", full, save.Head], ct).ConfigureAwait(false);
            if (added.Code != 0) return new(false, [], $"git would not make a working tree at {folder}: {FirstLine(added.Stderr)}");
        }

        var checkedOut = await GitRuns.GitAsync(full, ["read-tree", "-u", "--reset", save.Head], ct).ConfigureAwait(false);
        if (checkedOut.Code != 0) return new(false, [], $"git would not write the head's files: {FirstLine(checkedOut.Stderr)}");
        var indexed = await GitRuns.GitAsync(full, ["read-tree", save.Staged], ct).ConfigureAwait(false);
        if (indexed.Code != 0) return new(false, [], $"git would not put the staged index back: {FirstLine(indexed.Stderr)}");
        foreach (var file in save.Files)
        {
            var at = Path.Combine(full, file.Path.Replace('/', Path.DirectorySeparatorChar));
            if (file.Deleted)
            {
                if (File.Exists(at)) File.Delete(at);
                continue;
            }

            var bytes = await GitRuns.GitBytesAsync(full, ["cat-file", "blob", file.Blob!], (int)Math.Min(file.Bytes + 1, int.MaxValue), ct).ConfigureAwait(false);
            if (bytes is not { } read) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(at)!);
            await File.WriteAllBytesAsync(at, read.Bytes.AsMemory(0, read.Count).ToArray(), ct).ConfigureAwait(false);
        }

        var differ = await DifferAsync(save, full, ct).ConfigureAwait(false);
        return differ.Count == 0
            ? new(true, [], $"restored `{save.Id}` into {folder}{(onBranch ? $" on `{save.Branch}`" : $" at {save.Head[..Math.Min(12, save.Head.Length)]}, detached")}: "
                + $"its head, its index and all {save.Files.Count} listed file(s) identical by hash.")
            : new(false, differ, $"restored `{save.Id}` into {folder}, but these do not read as saved: {string.Join(", ", differ)}.");
    }

    /// <summary>What in the tree at <paramref name="tree"/> does not read as <paramref name="save"/> holds it: the head, the index, a listed file.</summary>
    private static async Task<IReadOnlyList<string>> DifferAsync(TreeSave save, string tree, CancellationToken ct)
    {
        var differ = new List<string>();
        if ((await GitRuns.GitAsync(tree, ["rev-parse", "HEAD"], ct).ConfigureAwait(false)).Stdout.Trim() != save.Head) differ.Add("its head");
        var index = (await GitRuns.GitAsync(tree, ["write-tree"], ct).ConfigureAwait(false)).Stdout.Trim();
        if (index != (await GitRuns.GitAsync(tree, ["rev-parse", $"{save.Staged}^{{tree}}"], ct).ConfigureAwait(false)).Stdout.Trim()) differ.Add("its index");
        foreach (var file in save.Files)
        {
            var at = Path.Combine(tree, file.Path.Replace('/', Path.DirectorySeparatorChar));
            if (file.Deleted ? File.Exists(at) : !File.Exists(at) || Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(at, ct).ConfigureAwait(false))) != file.Sha256)
            {
                differ.Add(file.Path);
            }
        }

        return differ;
    }

    private static void Forget(string file)
    {
        try
        {
            File.Delete(file);
            File.Delete(file + ".lock");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A scratch index left in the git folder is named for its act and harmless.
        }
    }
}
