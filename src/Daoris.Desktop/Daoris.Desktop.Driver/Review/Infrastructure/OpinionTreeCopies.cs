namespace Daoris.Driver;

/// <summary>
/// A second opinion's copy made and removed (XAGENT1d, D155 point 5; the second-agent design §5.1): a clone at the candidate's tip
/// with its remote removed, and the folder taken away whatever happened, read-only objects cleared first.
/// </summary>
/// <remarks>
/// <para><b>Named where its callers always named it</b> (STRUCT3c, D166's note): <c>OpinionTree.OpenAsync</c> and
/// <c>RemoveAsync</c> are static members of a C# 14 extension block on the Domain's <see cref="OpinionTree"/>, which keeps where a
/// copy goes; git and the folder are this layer's. Only the loop and the tests ask them.</para>
/// </remarks>
internal static class OpinionTreeCopies
{
    private const int Attempts = 4;

    extension(OpinionTree)
    {
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
            var path = OpinionTree.PathFor(home, workspace, repository);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var (cloned, _, cloneError) = await WorkingTree.GitAsync(
                root, ["clone", "--no-hardlinks", "--no-checkout", "--quiet", "--", Path.GetFullPath(root), path], ct).ConfigureAwait(false);
            if (cloned != 0)
            {
                await OpinionTree.RemoveAsync(home, path).ConfigureAwait(false);
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
                await OpinionTree.RemoveAsync(home, path).ConfigureAwait(false);
                throw new DriverException(
                    $"could not make a copy of `{repository}` at `{Short(tip)}` for a second opinion — git said: {Failure(error)}");
            }

            var (headCode, head, _) = await WorkingTree.GitAsync(path, ["rev-parse", "HEAD"], ct).ConfigureAwait(false);
            if (headCode != 0 || !string.Equals(head.Trim(), tip, StringComparison.OrdinalIgnoreCase))
            {
                await OpinionTree.RemoveAsync(home, path).ConfigureAwait(false);
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
            if (!full.StartsWith(trees, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith(OpinionTree.Prefix, StringComparison.Ordinal))
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
