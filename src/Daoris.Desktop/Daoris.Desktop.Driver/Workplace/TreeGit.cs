namespace Daoris.Driver;

/// <summary>
/// The git words and path comparisons the session trees' parts share (PART10i): what git's answers say, and one location under
/// two spellings. They read nothing of the trees' context, so they are static; the parts import them with <c>using static</c>.
/// </summary>
internal static class TreeGit
{
    /// <summary>The separators a path is cut at, on every platform.</summary>
    internal static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <summary>
    /// The reference git compares <paramref name="branch"/> by here, by its full name: the local branch, else origin's copy,
    /// else null — a line this checkout has nowhere. Never the short name (AUTOTIDY1): git reads a tag of that name first.
    /// </summary>
    internal static async Task<string?> ComparableAsync(string root, string branch, CancellationToken ct, RefSnapshot? refs = null)
    {
        foreach (var reference in new[] { $"refs/heads/{branch}", $"refs/remotes/origin/{branch}" })
        {
            // PROC1d-ii: from the judgement's refs where they hold the answer.
            if (refs is not null && await refs.HasAsync(reference, ct).ConfigureAwait(false) is { } there)
            {
                if (there) return reference;
                continue;
            }

            // LANDGIT1c: spelled as the reads of its commit are, so one ref fact is one command.
            var (code, _, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", reference + "^{commit}"], ct).ConfigureAwait(false);
            if (code == 0) return reference;
        }

        return null;
    }

    /// <summary>
    /// What <see cref="CommonDirAndBranchAsync"/> read of a tree: git's exit code and words for the common dir, the common dir as
    /// git printed it, <c>rev-parse --abbrev-ref HEAD</c>'s answer (<c>HEAD</c> where it is detached; empty where it was not
    /// asked), and whether one start gave both, which says the branch's ask succeeded too.
    /// </summary>
    internal sealed record CommonDirAndBranch(int Code, string CommonDir, string Err, string Branch, bool Once);

    /// <summary>
    /// A tree's repository and branch in one git start (PROC1d-iii): a landing, a removal and an update asked
    /// <c>--git-common-dir</c> and then <c>--abbrev-ref HEAD</c>, a start each. Where the one start fails (a branch with no
    /// commit yet, a folder git does not know), the two are asked as before, one at a time, so each answer and each failure
    /// is the one they gave: the branch is asked only where the common dir was read.
    /// </summary>
    internal static async Task<CommonDirAndBranch> CommonDirAndBranchAsync(string tree, CancellationToken ct)
    {
        var (code, output, _) = await WorkingTree.GitAsync(
            tree, ["rev-parse", "--path-format=absolute", "--git-common-dir", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        var lines = output.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0).ToArray();
        if (code == 0 && lines.Length == 2)
        {
            GitFacts.SameRepository(tree, lines[0]);
            return new CommonDirAndBranch(0, lines[0], "", lines[1], Once: true);
        }

        var (rootCode, commonDir, rootErr) = await WorkingTree.GitAsync(
            tree, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false);
        if (rootCode != 0) return new CommonDirAndBranch(rootCode, commonDir, rootErr, "", Once: false);
        GitFacts.SameRepository(tree, commonDir.Trim());
        var (_, branch, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        return new CommonDirAndBranch(0, commonDir, rootErr, branch, Once: false);
    }

    internal static string FirstLine(string text)
    {
        var trimmed = text.Trim();
        var newline = trimmed.IndexOf('\n');
        return newline < 0 ? trimmed : trimmed[..newline].Trim();
    }

    /// <summary>
    /// git's reason, not its progress. `worktree add` says *"Preparing worktree (new branch …)"*
    /// before anything can fail, and a refusal that quoted the first line quoted that. The last
    /// `fatal:` or `error:` line is why; failing those, the last line said.
    /// </summary>
    internal static string Failure(string stderr)
    {
        var lines = stderr.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
        return lines.LastOrDefault(line => line.StartsWith("fatal:", StringComparison.Ordinal))
               ?? lines.LastOrDefault(line => line.StartsWith("error:", StringComparison.Ordinal))
               ?? lines.LastOrDefault()
               ?? "nothing";
    }

    /// <summary>
    /// One filesystem location under two spellings: git prints forward slashes on every platform, the
    /// caller passes whatever the registry holds, and Windows compares blind to case.
    /// </summary>
    internal static bool SamePath(string left, string right) => string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)).Replace('\\', '/'),
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)).Replace('\\', '/'),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>
    /// A path as it is on disk (AUTOTIDY1): full, with each link on the way resolved, so a tree a session names through a link
    /// and the same tree git names where it is compare as one. Where a link cannot be read, the path is kept as written.
    /// </summary>
    internal static string Normal(string path)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var root = Path.GetPathRoot(full) ?? "";
        var at = root;
        foreach (var part in full[root.Length..].Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            at = Path.Combine(at, part);
            try
            {
                var info = new DirectoryInfo(at);
                if (info.Exists && info.LinkTarget is not null && info.ResolveLinkTarget(returnFinalTarget: true) is { } target)
                {
                    at = Path.TrimEndingDirectorySeparator(Path.GetFullPath(target.FullName));
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Kept as written: a link that cannot be read is compared by its name.
            }
        }

        return Path.TrimEndingDirectorySeparator(at);
    }

    /// <summary>
    /// What the plugin's step came to, in the landing's words (D100). Anything short of a push says the
    /// branch stands and how the person pushes it themselves.
    /// </summary>
    internal static string Said(PluginLanding said, string branch)
    {
        var byHand = $"The branch stands — push it and open the pull request yourself: `git push -u origin {branch}`.";
        if (said.Failed) return $"Plugin `{said.Plugin}`'s step failed — {Sentence(said.Message)} {byHand}";
        if (!said.Pushed) return $"Plugin `{said.Plugin}` did not push it: {Sentence(said.Message)} {byHand}";

        var opened = said.PullRequest is { } pr && !said.Message.Contains(pr, StringComparison.Ordinal) ? $" The pull request: {pr}" : "";
        return $"Plugin `{said.Plugin}`: {Sentence(said.Message)}{opened}";
    }

    /// <summary>A plugin's words closed as a sentence, so the one after it does not run on.</summary>
    internal static string Sentence(string words) =>
        words.TrimEnd() is var trimmed && trimmed.Length > 0 && ".!?".Contains(trimmed[^1]) ? trimmed : $"{trimmed}.";
}
