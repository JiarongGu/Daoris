namespace Daoris.Driver;

/// <summary>
/// Which line a checkout treats as canonical, as far as it knows: asked of the remote's own HEAD first, which is what a clone records and
/// so the repository's own answer rather than this machine's guess; a checkout made with <c>git remote add</c> has no such ref, so the
/// conventional names are tried in turn. Null where none exists, which is permissive at a deployment: a repository that never said which
/// line is canonical has not asked for its branches to be judged.
/// </summary>
internal static class GitDefaultBranch
{
    internal static async Task<string?> AskAsync(GitRepository.Runner run, string root, CancellationToken ct)
    {
        var (code, stdout) = OriginHeadFile(root) ?? await OriginHeadAsync(run, root, ct).ConfigureAwait(false);
        if (ReviewChoices.Guess(code, stdout, null, null) is { } pointed) return pointed;

        // The order is ReviewChoices.Guess's, which Review's graph asks all three together and chooses by.
        int? main = null, master = null;
        foreach (var candidate in ReviewChoices.Conventional)
        {
            // Spelled as every read of a branch's commit is, so one ref is one command wherever it is asked.
            var (exists, _, _) = await run(root, ReviewChoices.ConventionalAsk(candidate), ct).ConfigureAwait(false);
            if (candidate == "main") main = exists;
            else master = exists;
            if (ReviewChoices.Guess(code, stdout, main, master) is { } guessed) return guessed;
        }

        return null;
    }

    private static async Task<(int Code, string Stdout)> OriginHeadAsync(GitRepository.Runner run, string root, CancellationToken ct)
    {
        var (code, stdout, _) = await run(root, WorkingTree.OriginHeadAsk, ct).ConfigureAwait(false);
        return (code, stdout);
    }

    /// <summary>
    /// <c>symbolic-ref --short refs/remotes/origin/HEAD</c> as git's own files answer it, no process: <c>origin/main</c> where the ref is a
    /// symbolic one, git's failure where the repository holds no such ref; null where the files cannot say (a reftable repository, a ref
    /// that is no symbolic one), and git is asked.
    /// </summary>
    private static (int Code, string Stdout)? OriginHeadFile(string root)
    {
        const string Remotes = "refs/remotes/";
        const string Symbolic = "ref: " + Remotes;
        if (RefFiles.CommonDirOf(root) is not { } common) return null;
        try
        {
            var file = Path.Combine(common, "refs", "remotes", "origin", "HEAD");
            if (File.Exists(file))
            {
                var text = File.ReadAllText(file).Trim();
                return text.StartsWith(Symbolic, StringComparison.Ordinal) ? (0, text[Symbolic.Length..] + "\n") : null;
            }

            return RefFiles.Absent(common, "refs/remotes/origin/HEAD") ? (128, "") : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}
