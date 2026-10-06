namespace Daoris.Devkit;

/// <summary>
/// Documents that are supposed to describe code, checked against the code they describe.
/// </summary>
/// <remarks>
/// <para>Freshness is judged by the LAST COMMIT that touched each side, not by file modification times.
/// A checkout sets every mtime to the moment of the clone, so an mtime comparison reports every document
/// as current on a fresh clone and as stale after a rebase — noise in both directions, which is how a
/// gate teaches people to ignore it.</para>
///
/// <para>It reports rather than blocks by default in one specific case: a document that has never been
/// committed alongside anything it describes is more likely to be newly declared than stale. The first
/// run of a new declaration should not fail the build.</para>
///
/// <para>🔴 <b>With a merge open, the last commit is the one the merge would make</b> (GATE1b). A merge gated
/// before it is committed still has HEAD at the branch it merges into, where neither side has moved, so the
/// gate passed a README the merge left behind and failed it once committed (FIX-LOG, GATE1). The history
/// says what it read from, and the verdict carries it.</para>
/// </remarks>
public sealed class DocsGate(IGitHistory history) : IGate
{
    public string Name => "docs";

    public GateResult Run(GateContext context)
    {
        var tracked = context.Declaration.Docs.Tracked ?? [];
        var grace = context.Declaration.Docs.GraceDays;
        if (tracked.Count == 0)
        {
            return GateResult.Skip(Name, "no 'docs.tracked' declared — nothing claims to describe anything");
        }

        var problems = new List<string>();
        foreach (var entry in tracked)
        {
            if (!File.Exists(context.Path(entry.Document)))
            {
                problems.Add($"{entry.Document}: declared as a tracked document but does not exist");
                continue;
            }

            var documentAt = history.LastCommitDate(entry.Document);
            if (documentAt is null)
            {
                // Never committed: it is new, and a new document cannot be stale.
                continue;
            }

            foreach (var described in entry.Describes)
            {
                var codeAt = history.LastCommitDate(described);
                if (codeAt is null) continue;

                // Compared by DAY, not by instant. Within one working session a document and the code
                // it describes are edited in whatever order the work happened, often in separate
                // commits minutes apart — and a gate that fires on that ordering fires on every normal
                // session. It would be technically correct and completely useless, which is how a gate
                // teaches people to pass `--no-verify`.
                var behind = (codeAt.Value.Date - documentAt.Value.Date).TotalDays;
                if (behind <= grace) continue;

                problems.Add(
                    $"{entry.Document} last changed {documentAt:yyyy-MM-dd}, but {described} changed "
                    + $"{codeAt:yyyy-MM-dd} ({behind:0} days later) — the document is supposed to describe it");
            }
        }

        // GATE1b: dates read from a commit nobody has made yet are said to be, or a person holding the verdict
        // against `git log` finds dates that are not there.
        var basis = history.Basis;
        return problems.Count == 0
            ? GateResult.Pass(Name, $"{tracked.Count} document(s) keeping up{(basis is null ? "" : $", {basis}")}")
            : GateResult.Fail(Name, string.Join('\n', basis is null ? problems : [.. problems, basis]));
    }
}

/// <summary>When a path last changed, according to history rather than the filesystem.</summary>
public interface IGitHistory
{
    /// <summary>The author date of the last commit touching this path, or null if it has never been committed.</summary>
    DateTimeOffset? LastCommitDate(string relativePath);

    /// <summary>What the dates were read from when it is not HEAD, in words for the verdict; null when it is HEAD.</summary>
    string? Basis => null;
}

/// <summary>git log, through the command line.</summary>
/// <remarks>
/// Outside a merge it runs exactly what it always ran, `git log -1 --format=%aI -- &lt;path&gt;`, from HEAD.
/// With a merge open it logs the same path from the commit the merge would make (<see cref="ProposedMerge"/>),
/// built once for the run.
/// </remarks>
public sealed class CommandLineGitHistory(string repositoryRoot) : IGitHistory
{
    private readonly Lazy<ProposedMerge?> _merge = new(() => ProposedMerge.Build(repositoryRoot));

    public DateTimeOffset? LastCommitDate(string relativePath)
    {
        string[] arguments = _merge.Value is { } merge
            ? ["log", "-1", "--format=%aI", merge.Commit, "--", relativePath]
            : ["log", "-1", "--format=%aI", "--", relativePath];
        var result = Process.Run("git", arguments, repositoryRoot);

        if (result.ExitCode != 0) return null;
        var text = result.Output.Trim();
        return DateTimeOffset.TryParse(text, out var when) ? when : null;
    }

    public string? Basis => _merge.Value?.Said;
}

/// <summary>The commit an open merge would make, built without making it (GATE1b).</summary>
/// <remarks>
/// <para>Its tree is the checkout's content as `git add -A` would stage it, staged into a copy of the index
/// so an unstaged fix made in the merge is in it; its parents are HEAD and each MERGE_HEAD; no ref names
/// it. `git log` from it then simplifies history as it will from the real merge: a path the merge took
/// from one side keeps that side's last commit, and only a path the merge itself changes is dated now.
/// Dating every path the merge changes as now would pass a branch whose README had already fallen behind.</para>
///
/// <para>The person's index, HEAD and open merge are never written; the commit and the blobs it adds are
/// unreachable objects git collects in time. `tools/as-merged.mjs` builds the same commit for any command
/// (GATE1), and under it this sees no merge, since the git folder it is handed holds none.</para>
/// </remarks>
internal sealed record ProposedMerge(string Commit, string Head, IReadOnlyList<string> Merging)
{
    /// <summary>Who the commit would be by is never read, and a checkout with no identity must still build it.</summary>
    private static readonly Dictionary<string, string> Identity = new()
    {
        ["GIT_AUTHOR_NAME"] = "Proposed merge",
        ["GIT_AUTHOR_EMAIL"] = "proposed-merge@daoris.invalid",
        ["GIT_COMMITTER_NAME"] = "Proposed merge",
        ["GIT_COMMITTER_EMAIL"] = "proposed-merge@daoris.invalid",
    };

    public string Said =>
        $"judged as the open merge would commit it (the tree as `git add -A` would stage it, on {Short(Head)} and "
        + $"{string.Join(", ", Merging.Select(Short))})";

    private static string Short(string sha) => sha[..Math.Min(8, sha.Length)];

    /// <summary>
    /// Null outside a merge, and outside a checkout, where there is no merge to judge. Throws when the merge
    /// has unmerged paths: it has no commit yet, and judging HEAD or conflict markers would be a guess.
    /// </summary>
    public static ProposedMerge? Build(string root)
    {
        var located = Process.Run("git", ["rev-parse", "--absolute-git-dir"], root);
        if (located.ExitCode != 0) return null;
        var gitDir = located.Output.Trim();
        var mergeHead = Path.Combine(gitDir, "MERGE_HEAD");
        if (!File.Exists(mergeHead)) return null;
        var merging = File.ReadAllLines(mergeHead).Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
        if (merging.Count == 0) return null;

        var unmerged = Git(root, null, "diff", "--name-only", "--diff-filter=U", "-z")
            .Split('\0', StringSplitOptions.RemoveEmptyEntries);
        if (unmerged.Length > 0)
        {
            var named = string.Join(", ", unmerged.Take(5)) + (unmerged.Length > 5 ? $" and {unmerged.Length - 5} more" : "");
            throw new DevkitException(
                $"the merge has unmerged paths, so there is no commit to judge yet: {named}. Resolve and add them first.");
        }

        var index = Path.GetFullPath(Path.Combine(root, Git(root, null, "rev-parse", "--git-path", "index").Trim()));
        var staging = Path.Combine(gitDir, $"daoris-devkit-proposed-{Environment.ProcessId}.index");
        try
        {
            if (File.Exists(index)) File.Copy(index, staging, overwrite: true);
            var onStaging = new Dictionary<string, string> { ["GIT_INDEX_FILE"] = staging };
            Git(root, onStaging, "add", "-A");
            var tree = Git(root, onStaging, "write-tree").Trim();
            var head = Git(root, null, "rev-parse", "HEAD").Trim();
            string[] parents = [head, .. merging];
            var commit = Git(root, Identity,
                ["commit-tree", tree, .. parents.SelectMany(parent => new[] { "-p", parent }), "-m", "The commit this merge would make (GATE1b)"])
                .Trim();
            return new ProposedMerge(commit, head, merging);
        }
        finally
        {
            File.Delete(staging);
        }
    }

    private static string Git(string root, IReadOnlyDictionary<string, string>? environment, params string[] arguments)
    {
        var result = Process.Run("git", arguments, root, environment);
        if (result.ExitCode != 0)
        {
            var said = (result.Error.Trim().Length > 0 ? result.Error : result.Output).Trim();
            throw new DevkitException($"could not build the commit the open merge would make: git {arguments[0]} failed: {said}");
        }

        return result.Output;
    }
}
