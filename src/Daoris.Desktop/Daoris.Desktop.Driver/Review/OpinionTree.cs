namespace Daoris.Driver;

/// <summary>A reviewer's copy as it was made (XAGENT1d): where it is, and the candidate's tip it stands at.</summary>
public sealed record OpinionTreeOpened(string Path, string Tip);

/// <summary>
/// A second opinion's own tree (XAGENT1d, D155 point 5; the second-agent design §5.1): a clone of the repository at the
/// candidate's tip, made by Daoris under the home's trees, with no remote, and removed when the pass ends. Nothing in it is read
/// back: the only thing a pass takes is the opinion said through its tool.
/// </summary>
/// <remarks>
/// <para><b>A clone, never a worktree.</b> A worktree shares its repository's refs, so a branch or a tag the reviewer made would
/// be the person's. A clone keeps its refs, its configuration and its hooks to itself, and with its one remote removed it has
/// nowhere configured to push. <c>--no-hardlinks</c> copies the objects rather than linking them, so no file of the copy's object
/// store is one the repository holds too. What the copy cannot hold alone is a push that names the repository's own path: the
/// rules handed (<c>no-push</c>) and the instruction hold that one.</para>
///
/// <para><b>Where it lives</b>: <c>trees/&lt;workspace&gt;/&lt;repository&gt;/o-&lt;8 hex&gt;</c>, the layout a session tree has
/// (D51), so every reader of that layout finds a workspace and a repository where it looks, and the tree guard can hold the
/// reviewer to it. <c>o-</c> beside a session tree's <c>s-</c>.</para>
///
/// <para><b>Removed whatever happened</b>, read-only objects cleared first, which a recursive delete refuses on Windows. A folder
/// something still holds is said, never left silently.</para>
/// </remarks>
public static class OpinionTree
{
    /// <summary>What a copy's folder name starts with, beside a session tree's <c>s-</c>.</summary>
    public const string Prefix = "o-";

    private const int Attempts = 4;

    /// <summary>A new copy's folder for <paramref name="repository"/> in <paramref name="workspace"/>, the default one where none is named.</summary>
    public static string PathFor(string home, string? workspace, string repository) =>
        Path.Combine(
            home, "trees", workspace is { } named && !string.IsNullOrWhiteSpace(named) ? named.Trim() : RemoteTarget.DefaultWorkspace,
            repository.Trim(), Prefix + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>
    /// Make the copy: a clone of <paramref name="root"/> with no hard links and nothing checked out, its remote removed, then
    /// checked out detached at <paramref name="tip"/>. A failure removes what was made and throws, in git's words.
    /// </summary>
    /// <param name="root">The repository's registered checkout, where the candidate's commits are.</param>
    /// <param name="tip">The candidate's tip, by its full id.</param>
    /// <exception cref="DriverException">The copy could not be made; nothing of it is left.</exception>
    public static async Task<OpinionTreeOpened> OpenAsync(
        string home, string root, string repository, string? workspace, string tip, CancellationToken ct = default)
    {
        var path = PathFor(home, workspace, repository);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var (cloned, _, cloneError) = await WorkingTree.GitAsync(
            root, ["clone", "--no-hardlinks", "--no-checkout", "--quiet", "--", Path.GetFullPath(root), path], ct).ConfigureAwait(false);
        if (cloned != 0)
        {
            await RemoveAsync(home, path).ConfigureAwait(false);
            throw new DriverException($"could not make a copy of `{repository}` for a second opinion — git said: {Failure(cloneError)}");
        }

        // Its one remote goes, and with it every branch the person's repository had: the copy has nowhere configured to push.
        // Long paths for the reviewer's own git too: a copy's prefix is longer than its repository's (2026-09-28's finding).
        string[][] steps =
        [
            ["remote", "remove", "origin"],
            ["config", "core.longpaths", "true"],
            ["checkout", "--quiet", "--detach", tip],
        ];
        foreach (var step in steps)
        {
            var (code, _, error) = await WorkingTree.GitAsync(path, step, ct).ConfigureAwait(false);
            if (code == 0) continue;
            await RemoveAsync(home, path).ConfigureAwait(false);
            throw new DriverException(
                $"could not make a copy of `{repository}` at `{Short(tip)}` for a second opinion — git said: {Failure(error)}");
        }

        var (headCode, head, _) = await WorkingTree.GitAsync(path, ["rev-parse", "HEAD"], ct).ConfigureAwait(false);
        if (headCode != 0 || !string.Equals(head.Trim(), tip, StringComparison.OrdinalIgnoreCase))
        {
            await RemoveAsync(home, path).ConfigureAwait(false);
            throw new DriverException($"the copy of `{repository}` for a second opinion does not stand at `{Short(tip)}`, so it was removed.");
        }

        return new OpinionTreeOpened(path, tip);
    }

    /// <summary>
    /// Remove a copy, and only a copy: a folder named as one under the home's trees. Null where it is gone; otherwise why it
    /// stays, in a sentence a line can carry.
    /// </summary>
    public static async Task<string?> RemoveAsync(string home, string path)
    {
        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return $"{path} is not a second opinion's copy: {error.Message}";
        }

        var trees = Path.GetFullPath(Path.Combine(home, "trees")) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(trees, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith(Prefix, StringComparison.Ordinal))
        {
            return $"{path} is not a second opinion's copy under {trees}, so it was not removed.";
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (!Directory.Exists(full)) return null;
                Writable(full);
                Directory.Delete(full, recursive: true);
                return null;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Something on this machine may still hold a file a moment after the reviewer's process ended.
                if (attempt >= Attempts)
                {
                    return $"the second opinion's copy at {full} is left: {error.Message} Nothing reads it, and it can be deleted once "
                        + "nothing holds it.";
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt)).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Every file under <paramref name="folder"/> made writable, links not followed: git's objects are read-only.</summary>
    private static void Writable(string folder)
    {
        var walk = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = true,
        };
        foreach (var file in Directory.EnumerateFiles(folder, "*", walk))
        {
            var attributes = File.GetAttributes(file);
            if (attributes.HasFlag(FileAttributes.ReadOnly)) File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
        }
    }

    private static string Short(string commit) => commit.Length > 7 ? commit[..7] : commit;

    /// <summary>git's reason: its last <c>fatal:</c> or <c>error:</c> line, else its last line.</summary>
    private static string Failure(string stderr)
    {
        var lines = stderr.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
        return lines.LastOrDefault(line => line.StartsWith("fatal:", StringComparison.Ordinal))
               ?? lines.LastOrDefault(line => line.StartsWith("error:", StringComparison.Ordinal))
               ?? lines.LastOrDefault()
               ?? "nothing";
    }
}
