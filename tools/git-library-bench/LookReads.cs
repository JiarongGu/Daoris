using System.Diagnostics;
using LibGit2Sharp;

namespace GitLibraryBench;

/// <summary>
/// The look's reads of one repository and its trees: the worktrees, the refs, the line, the session branches no other holds, and of
/// each tree its status, head and branch and what it holds the line lacks; through the binary, then in process.
/// </summary>
internal static class LookReads
{
    public static async Task<(int Starts, double Ms)> BinaryAsync(GitBinary git, Layout layout)
    {
        var clock = Stopwatch.StartNew();
        var starts = 0;
        async Task<string> Ask(string folder, params string[] arguments)
        {
            starts++;
            return (await git.RunAsync(folder, arguments)).Output;
        }

        await Ask(layout.Root, "worktree", "list", "--porcelain");
        await Ask(layout.Root, "rev-parse", "--show-toplevel");
        await Ask(layout.Root, "for-each-ref", ReviewReads.RefFormat, "refs/heads", "refs/remotes/origin");
        var line = (await Ask(layout.Root, "rev-parse", "--verify", "--quiet", $"{layout.Line}^{{commit}}")).Trim();
        await Ask(layout.Root, "rev-list", "--branches=daoris/*", "--not", "--exclude=daoris/*", "--branches", "--exclude=*/daoris/*", "--remotes");
        foreach (var tree in layout.Trees)
        {
            var status = await Ask(tree, "--no-optional-locks", "status", "--porcelain=v2", "--branch");
            var tip = status.Split('\n').First(each => each.StartsWith("# branch.oid ", StringComparison.Ordinal))["# branch.oid ".Length..].Trim();
            await Ask(layout.Root, "merge-base", tip, line);
            await Ask(layout.Root, "diff", "--no-ext-diff", "--no-renames", "--ignore-submodules=none", "--name-only", "-z", line, tip);
        }

        return (starts, clock.Elapsed.TotalMilliseconds);
    }

    /// <summary>The same look in process; <paramref name="open"/> gives the repository of a folder, held or opened anew.</summary>
    public static double Library(Layout layout, Func<string, Repository> open)
    {
        var clock = Stopwatch.StartNew();
        var root = open(layout.Root);
        _ = root.Worktrees.Select(each => each.Name).ToList();
        _ = LibraryRefs.List(root);
        var line = root.Lookup<Commit>(layout.Line);
        var sessions = root.Refs
            .Where(reference => reference.CanonicalName.StartsWith("refs/heads/daoris/", StringComparison.Ordinal))
            .Select(reference => reference.ResolveToDirectReference()?.Target).OfType<Commit>().ToList();
        _ = root.Commits.QueryBy(new CommitFilter { IncludeReachableFrom = sessions, ExcludeReachableFrom = LibraryRefs.HeldTips(root) }).Count();
        foreach (var path in layout.Trees)
        {
            var tree = open(path);
            var changed = tree.RetrieveStatus(new StatusOptions { IncludeUntracked = true, RecurseUntrackedDirs = false, IncludeIgnored = false }).Count();
            _ = (changed, tree.Head.FriendlyName);
            var tip = root.Lookup<Commit>(tree.Head.Tip.Id);
            _ = root.ObjectDatabase.FindMergeBase(tip, line);
            using var changes = root.Diff.Compare<TreeChanges>(line.Tree, tip.Tree, new CompareOptions { Similarity = SimilarityOptions.None });
            _ = changes.Count;
        }

        return clock.Elapsed.TotalMilliseconds;
    }
}
