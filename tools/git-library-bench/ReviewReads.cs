using System.Diagnostics;
using LibGit2Sharp;

namespace GitLibraryBench;

/// <summary>
/// Review's reads of a session's tree, as Daoris asks them of git today (one start each, in the order its count test lists them) and
/// the same answers asked of LibGit2Sharp in process.
/// </summary>
internal static class ReviewReads
{
    public const string RefFormat = "--format=%(refname) %(objectname) %(objecttype) %(upstream) %(upstream:short) %(tree)";

    private static readonly string[] Diff =
    [
        "--no-optional-locks", "-c", "diff.autoRefreshIndex=false", "-c", "core.quotePath=false", "diff",
        "--no-color", "--no-ext-diff", "--src-prefix=a/", "--dst-prefix=b/", "-M", "-z", "--raw", "--numstat", "-p",
    ];

    /// <summary>One Review through the binary: the time of each start, in order.</summary>
    public static async Task<List<(string Ask, double Ms)>> BinaryAsync(GitBinary git, Layout layout)
    {
        var tree = layout.Review;
        var head = (await Fixtures.MustAsync(git, tree, ["rev-parse", "HEAD"])).Trim();
        var line = (await Fixtures.MustAsync(git, tree, ["rev-parse", layout.Line])).Trim();
        var times = new List<(string, double)>();

        async Task<string> Ask(string name, params string[] arguments) => await Private(name, null, null, arguments);

        async Task<string> Private(string name, string? index, string? input, params string[] arguments)
        {
            var variables = index is null ? null : new Dictionary<string, string> { ["GIT_INDEX_FILE"] = index, ["GIT_LITERAL_PATHSPECS"] = "1" };
            var (_, output, sample) = await git.RunAsync(tree, arguments, variables: variables, input: input);
            times.Add((name, sample.WallMs));
            return output;
        }

        await Ask("layout", "rev-parse", "--path-format=absolute", "--git-common-dir", "--show-toplevel", "--abbrev-ref", "HEAD");
        await Ask("branch", "symbolic-ref", "--short", "-q", "HEAD");
        await Ask("base is an ancestor", "merge-base", "--is-ancestor", layout.Base, "HEAD");
        await Ask("uncommitted diff", [.. Diff, "HEAD"]);
        await Ask("origin's head", "symbolic-ref", "--short", "refs/remotes/origin/HEAD");
        await Ask("refs", "for-each-ref", RefFormat, "refs/heads", "refs/remotes/origin");
        var status = await Ask("status, untracked all", "--no-optional-locks", "status", "--porcelain", "-z", "--untracked-files=all");
        await Ask("merge-base with the line", "merge-base", "HEAD", layout.Line);
        await Ask("behind the line", "rev-list", "--count", layout.Line, "--not", "HEAD");
        await Ask("ahead of the line", "rev-list", "--count", "HEAD", "--not", layout.Line);
        // The untracked files' patches in two starts, as Daoris's UntrackedPatches reads them: intents into a private index, then its diff.
        var untracked = Untracked(status).ToArray();
        if (untracked.Length > 0)
        {
            var index = Path.Combine((await Fixtures.MustAsync(git, tree, ["rev-parse", "--absolute-git-dir"])).Trim(), $"index.bench-{Guid.NewGuid():N}");
            try
            {
                await Private("the untracked files' intents", index, string.Concat(untracked.Select(path => path + "\0")),
                    "-c", "core.splitIndex=false", "add", "--intent-to-add", "--pathspec-from-file=-", "--pathspec-file-nul");
                await Private("the untracked files' patches", index, null, "--no-optional-locks", "-c", "diff.autoRefreshIndex=false", "-c", "core.quotePath=false",
                    "diff", "--no-color", "--no-ext-diff", "-O/dev/null", "--src-prefix=a/", "--dst-prefix=b/");
            }
            finally
            {
                File.Delete(index);
            }
        }

        await Ask("committed diff", [.. Diff, $"{layout.Base}..HEAD"]);
        await Ask("status, head and branch", "--no-optional-locks", "status", "--porcelain=v2", "--branch");
        await Ask("commits no branch holds", "log", "--oneline", head, "--not", "--exclude=daoris/*", "--branches", "--exclude=*/daoris/*", "--remotes");
        await Ask("merge-base with the line's tip", "merge-base", head, line);
        await Ask("files the line lacks", "diff", "--no-ext-diff", "--no-renames", "--ignore-submodules=none", "--name-only", "-z", line, head);
        return times;
    }

    public static IEnumerable<string> Untracked(string porcelainZ)
    {
        var fields = porcelainZ.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index];
            if (field.StartsWith("?? ", StringComparison.Ordinal)) yield return field[3..];
            else if (field.Length > 0 && (field[0] is 'R' or 'C')) index++;
        }
    }

    /// <summary>What the library refused in a Review, each said once however many passes asked it.</summary>
    public static HashSet<string> Failures { get; } = [];

    /// <summary>One Review in process, over <paramref name="repository"/> (held or opened for it): the time of each read, in order.</summary>
    public static List<(string Ask, double Ms)> Library(Repository repository, Layout layout)
    {
        var times = new List<(string, double)>();
        var clock = new Stopwatch();

        T? Ask<T>(string name, Func<T> read)
        {
            clock.Restart();
            try
            {
                var answer = read();
                times.Add((name, clock.Elapsed.TotalMilliseconds));
                return answer;
            }
            catch (LibGit2SharpException error)
            {
                times.Add((name, clock.Elapsed.TotalMilliseconds));
                Failures.Add($"{name}: {error.Message}");
                return default;
            }
        }

        var compare = new CompareOptions { Similarity = SimilarityOptions.Renames, IndentHeuristic = true };
        Ask("layout", () => (repository.Info.WorkingDirectory, repository.Info.Path, repository.Info.IsHeadDetached));
        Ask("branch", () => (repository.Refs.Head as SymbolicReference)?.TargetIdentifier);
        var head = repository.Head.Tip;
        var baseCommit = repository.Lookup<Commit>(layout.Base);
        Ask("base is an ancestor", () => repository.ObjectDatabase.FindMergeBase(baseCommit, head)?.Id == baseCommit.Id);
        Ask("uncommitted diff", () =>
        {
            using var patch = repository.Diff.Compare<Patch>(head.Tree, DiffTargets.Index | DiffTargets.WorkingDirectory, null, null, compare);
            return patch.Content.Length;
        });
        Ask("origin's head", () => (repository.Refs["refs/remotes/origin/HEAD"] as SymbolicReference)?.TargetIdentifier);
        Ask("refs", () => LibraryRefs.List(repository).Count);
        var status = Ask("status, untracked all", () => repository.RetrieveStatus(new StatusOptions
        {
            IncludeUntracked = true, RecurseUntrackedDirs = true, IncludeIgnored = false, DetectRenamesInIndex = true, DetectRenamesInWorkDir = false,
        }));
        var line = repository.Lookup<Commit>(layout.Line);
        Ask("merge-base with the line", () => repository.ObjectDatabase.FindMergeBase(head, line)?.Sha);
        Ask("ahead and behind the line", () => repository.ObjectDatabase.CalculateHistoryDivergence(head, line) is var divergence
            ? (divergence.AheadBy, divergence.BehindBy) : default);
        var untracked = status?.Untracked.Select(entry => entry.FilePath).ToArray() ?? [];
        if (untracked.Length > 0)
        {
            Ask("the untracked files' patches", () =>
            {
                using var patch = UntrackedPatches(repository, untracked, compare);
                return patch.Content.Length;
            });
        }

        Ask("committed diff", () =>
        {
            using var patch = repository.Diff.Compare<Patch>(baseCommit.Tree, head.Tree, compare);
            return patch.Content.Length;
        });
        Ask("status, head and branch", () => (repository.RetrieveStatus(new StatusOptions { IncludeUntracked = true, RecurseUntrackedDirs = false, IncludeIgnored = false }).Count(),
            repository.Head.Tip?.Sha, repository.Head.FriendlyName));
        Ask("commits no branch holds", () => repository.Commits.QueryBy(new CommitFilter
        {
            IncludeReachableFrom = head, ExcludeReachableFrom = LibraryRefs.HeldTips(repository),
        }).Count());
        Ask("merge-base with the line's tip", () => repository.ObjectDatabase.FindMergeBase(head, line)?.Sha);
        Ask("files the line lacks", () =>
        {
            using var changes = repository.Diff.Compare<TreeChanges>(line.Tree, head.Tree, new CompareOptions { Similarity = SimilarityOptions.None });
            return changes.Count;
        });
        return times;
    }

    /// <summary>The untracked files as the patches that add them, through the attributes' filters, in one read of the working tree.</summary>
    public static Patch UntrackedPatches(Repository repository, IReadOnlyList<string> paths, CompareOptions compare) =>
        repository.Diff.Compare<Patch>(paths, true, null, compare);
}

/// <summary>The refs Review and the look list, read in process as <c>for-each-ref</c> names them.</summary>
internal static class LibraryRefs
{
    /// <summary>Each branch and origin's copy: name, commit, type, upstream and tree, as the listing's format asks.</summary>
    public static List<(string Name, string Id, string Type, string? Upstream, string? Tree)> List(Repository repository)
    {
        var listed = new List<(string, string, string, string?, string?)>();
        foreach (var reference in repository.Refs)
        {
            var name = reference.CanonicalName;
            var local = name.StartsWith("refs/heads/", StringComparison.Ordinal);
            if (!local && !name.StartsWith("refs/remotes/origin/", StringComparison.Ordinal)) continue;
            var target = reference.ResolveToDirectReference()?.Target;
            if (target is null) continue;
            var upstream = local ? repository.Branches[name]?.UpstreamBranchCanonicalName : null;
            listed.Add((name, target.Sha, target.GetType().Name.ToLowerInvariant(), upstream, (target as Commit)?.Tree.Sha));
        }

        return listed;
    }

    /// <summary>The tips <c>--exclude=daoris/* --branches --exclude=*/daoris/* --remotes</c> names: every branch and remote one past Daoris's own.</summary>
    public static List<Commit> HeldTips(Repository repository) =>
    [
        .. repository.Refs
            .Where(reference => reference.CanonicalName is var name
                && ((name.StartsWith("refs/heads/", StringComparison.Ordinal) && !name.StartsWith("refs/heads/daoris/", StringComparison.Ordinal))
                    || (name.StartsWith("refs/remotes/", StringComparison.Ordinal) && !name.Contains("/daoris/", StringComparison.Ordinal))))
            .Select(reference => reference.ResolveToDirectReference()?.Target)
            .OfType<Commit>(),
    ];
}
