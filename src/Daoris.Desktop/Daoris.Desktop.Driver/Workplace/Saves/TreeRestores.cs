using System.Security.Cryptography;
using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// A save put back, in two steps of their own: making a tree of its repository in an empty folder (<see cref="MakeTreeAsync"/>), and
/// applying a save into a working tree of that repository (<see cref="ApplyAsync"/>), which the keeper's resume into a slot it already
/// holds takes alone. Each applied save is proved: its head, its index and every listed file identical by hash.
/// </summary>
/// <remarks>
/// A file is written by git's own <c>unpack-file</c>, the blob's bytes as written with no filter, so no blob is held whole in this
/// process; a deleted file goes before any is written, and a folder emptied by it with it, so a file may take a folder's place and a
/// folder a file's. A restore that does not prove whole removes the tree it made, which held nothing but the save's.
/// </remarks>
public static class TreeRestores
{
    /// <summary>Puts <paramref name="save"/> into the empty folder <paramref name="folder"/>: a tree made there, the save applied and proved.</summary>
    public static async Task<RestoreProof> RestoreAsync(TreeSave save, string folder, CancellationToken ct = default)
    {
        var full = TreeSaveRecords.Key(folder);
        if (Directory.Exists(full) && Directory.EnumerateFileSystemEntries(full).Any()) return new(false, [], $"{folder} is not empty: a save is restored only into an empty folder.");
        if (await HeldAsync(save, ct).ConfigureAwait(false) is { } gone) return new(false, [], gone);

        var (made, onBranch) = await MakeTreeAsync(save, full, ct).ConfigureAwait(false);
        if (made is not null) return new(false, [], made);

        RestoreProof proof;
        try
        {
            proof = await ApplyIntoAsync(save, full, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            proof = new(false, [], $"the save could not be written into {folder}: {error.Message}");
        }

        if (!proof.Whole)
        {
            var removed = await GitRuns.GitAsync(save.Root, ["worktree", "remove", "--force", full], CancellationToken.None).ConfigureAwait(false);
            return proof with
            {
                Said = proof.Said + (removed.Code == 0
                    ? " The tree it made is removed again; the save stays as it was."
                    : $" The tree it made at {folder} stays, since git would not remove it ({FirstLine(removed.Stderr)}); the save stays as it was."),
            };
        }

        return proof with
        {
            Said = $"restored `{save.Id}` into {folder}{(onBranch ? $" on `{save.Branch}`" : $" at {save.Head[..Math.Min(12, save.Head.Length)]}, detached")}: {proof.Said}",
        };
    }

    /// <summary>
    /// A working tree of the save's repository made in the empty folder <paramref name="full"/> at the save's head, with no checkout, so no
    /// hook writes into it before it is proved: on the save's branch where that branch still stands at its head and is checked out nowhere,
    /// detached otherwise. Null where it is made; else why not, nothing made.
    /// </summary>
    internal static async Task<(string? Refused, bool OnBranch)> MakeTreeAsync(TreeSave save, string full, CancellationToken ct)
    {
        if (save.Branch is { } branch
            && (await GitRuns.GitAsync(save.Root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], ct).ConfigureAwait(false)).Stdout.Trim() == save.Head
            && (await GitRuns.GitAsync(save.Root, ["worktree", "add", "--no-checkout", full, branch], ct).ConfigureAwait(false)).Code == 0)
        {
            return (null, true);
        }

        var added = await GitRuns.GitAsync(save.Root, ["worktree", "add", "--no-checkout", "--detach", full, save.Head], ct).ConfigureAwait(false);
        return added.Code == 0 ? (null, false) : ($"git would not make a working tree at {full}: {FirstLine(added.Stderr)}", false);
    }

    /// <summary>
    /// Applies <paramref name="save"/> into the working tree at <paramref name="tree"/> of its repository: its head, the head's files, the index
    /// as staged and every listed file's bytes as written; then proves them. A tree holding work no save holds is refused, nothing written.
    /// </summary>
    public static async Task<RestoreProof> ApplyAsync(TreeSave save, string tree, CancellationToken ct = default)
    {
        var full = TreeSaveRecords.Key(tree);
        if (await HeldAsync(save, ct).ConfigureAwait(false) is { } gone) return new(false, [], gone);
        var (listing, refused) = await TreeSaveListing.ListAsync(full, [], ct).ConfigureAwait(false);
        if (listing is null) return new(false, [], refused!.Said);
        if (listing.Files.Count > 0) return new(false, [], $"{tree} holds work of its own ({listing.Files.Count} file(s)); save it before a save is applied there.");
        var common = (await GitRuns.GitAsync(save.Root, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false)).Stdout.Trim();
        if (!SamePath(common, listing.CommonDir)) return new(false, [], $"{tree} is a tree of another repository than the save's, {save.Root}.");
        return await ApplyIntoAsync(save, full, ct).ConfigureAwait(false);
    }

    /// <summary>Why the save's ref no longer holds it, or null while it does.</summary>
    private static async Task<string?> HeldAsync(TreeSave save, CancellationToken ct)
    {
        var held = await GitRuns.GitAsync(save.Root, ["rev-parse", "--verify", "--quiet", save.Id], ct).ConfigureAwait(false);
        return held.Stdout.Trim() == save.Commit ? null : $"`{save.Id}` no longer holds this save in {save.Root}.";
    }

    private static async Task<RestoreProof> ApplyIntoAsync(TreeSave save, string full, CancellationToken ct)
    {
        var head = (await GitRuns.GitAsync(full, ["rev-parse", "--verify", "--quiet", "HEAD"], ct).ConfigureAwait(false)).Stdout.Trim();
        if (head != save.Head)
        {
            var moved = await GitRuns.GitAsync(full, ["update-ref", "--no-deref", "-m", "daoris: a save applied", "HEAD", save.Head], ct).ConfigureAwait(false);
            if (moved.Code != 0) return new(false, [], $"git would not move the tree's head to the save's: {FirstLine(moved.Stderr)}");
        }

        var checkedOut = await GitRuns.GitAsync(full, ["read-tree", "-u", "--reset", save.Head], ct).ConfigureAwait(false);
        if (checkedOut.Code != 0) return new(false, [], $"git would not write the head's files: {FirstLine(checkedOut.Stderr)}");
        var indexed = await GitRuns.GitAsync(full, ["read-tree", save.Staged], ct).ConfigureAwait(false);
        if (indexed.Code != 0) return new(false, [], $"git would not put the staged index back: {FirstLine(indexed.Stderr)}");

        foreach (var file in save.Files.Where(file => file.Deleted))
        {
            var at = At(full, file.Path);
            if (File.Exists(at)) File.Delete(at);
            Emptied(full, Path.GetDirectoryName(at)!);
        }

        var modes = await ModesAsync(save, ct).ConfigureAwait(false);
        var unwritten = new List<string>();
        foreach (var file in save.Files.Where(file => !file.Deleted))
        {
            var at = At(full, file.Path);
            if (Directory.Exists(at))
            {
                if (Directory.EnumerateFileSystemEntries(at).Any())
                {
                    unwritten.Add($"{file.Path} (a folder holding files stands in its place)");
                    continue;
                }

                Directory.Delete(at);
            }

            var unpacked = await GitRuns.GitAsync(full, ["unpack-file", file.Blob!], ct).ConfigureAwait(false);
            var written = Path.Combine(full, unpacked.Stdout.Trim());
            if (unpacked.Code != 0 || unpacked.Stdout.Trim().Length == 0 || !File.Exists(written))
            {
                unwritten.Add($"{file.Path} (git wrote no file of its blob: {FirstLine(unpacked.Stderr)})");
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(at)!);
            File.Move(written, at, overwrite: true);
            if (!OperatingSystem.IsWindows() && modes.GetValueOrDefault(file.Path) == "100755")
            {
                File.SetUnixFileMode(at, File.GetUnixFileMode(at) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
            }
        }

        var differ = unwritten.Concat((await DifferAsync(save, full, ct).ConfigureAwait(false)).Where(path => !unwritten.Any(each => each.StartsWith(path + " (", StringComparison.Ordinal)))).ToList();
        return differ.Count == 0
            ? new(true, [], $"its head, its index and all {save.Files.Count} listed file(s) identical by hash.")
            : new(false, differ, $"`{save.Id}` was applied into {full}, but these do not read as saved: {string.Join(", ", differ)}.");
    }

    private static string At(string tree, string path) => Path.Combine(tree, path.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Each folder from <paramref name="folder"/> up to the tree's top removed while it is empty, so a file may take its place.</summary>
    private static void Emptied(string tree, string folder)
    {
        var top = Path.TrimEndingDirectorySeparator(tree);
        while (folder.Length > top.Length && Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
        {
            Directory.Delete(folder);
            folder = Path.GetDirectoryName(folder)!;
        }
    }

    /// <summary>The modes of the files as written, from the save's commit of them.</summary>
    private static async Task<IReadOnlyDictionary<string, string>> ModesAsync(TreeSave save, CancellationToken ct)
    {
        var listed = await GitRuns.GitAsync(save.Root, ["-c", "core.quotePath=false", "ls-tree", "-r", "-z", save.Raw], ct).ConfigureAwait(false);
        return listed.Stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => (Mode: entry[..6], Path: entry[(entry.IndexOf('\t') + 1)..]))
            .ToDictionary(entry => entry.Path, entry => entry.Mode, StringComparer.Ordinal);
    }

    /// <summary>What in the tree at <paramref name="tree"/> does not read as <paramref name="save"/> holds it: the head, the index, a listed file, each hashed as it streams.</summary>
    private static async Task<IReadOnlyList<string>> DifferAsync(TreeSave save, string tree, CancellationToken ct)
    {
        var differ = new List<string>();
        if ((await GitRuns.GitAsync(tree, ["rev-parse", "HEAD"], ct).ConfigureAwait(false)).Stdout.Trim() != save.Head) differ.Add("its head");
        var index = (await GitRuns.GitAsync(tree, ["write-tree"], ct).ConfigureAwait(false)).Stdout.Trim();
        if (index != (await GitRuns.GitAsync(tree, ["rev-parse", $"{save.Staged}^{{tree}}"], ct).ConfigureAwait(false)).Stdout.Trim()) differ.Add("its index");
        foreach (var file in save.Files)
        {
            var at = At(tree, file.Path);
            if (file.Deleted ? File.Exists(at) : !File.Exists(at) || await ShaAsync(at, ct).ConfigureAwait(false) != file.Sha256) differ.Add(file.Path);
        }

        return differ;
    }

    private static async Task<string> ShaAsync(string file, CancellationToken ct)
    {
        await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16, useAsync: true);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false));
    }
}
