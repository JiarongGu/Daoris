using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// RECORDUNREAD1 over real git: a read that failed never forgets or retires a record. Git's quiet verification exits 1 on any failure
/// to resolve, a broken ref's too, and <c>for-each-ref</c> leaves a broken ref out of its list with a warning, exiting 0; only
/// <c>show-ref --exists</c>'s exit 2 says a ref is missing (FABRIC-END4a's fourth review, <c>ContentHolds.MissingAsync</c>). Each path
/// that forgot a record on a failed read is broken here, and keeps it.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class UnreadRecordGitTests : LandedFixture
{
    /// <summary>A branch git cannot read: its loose ref written over with something that is no commit.</summary>
    private static void Break(string root, string branch)
    {
        var loose = Path.Combine([root, ".git", "refs", "heads", .. branch.Split('/')]);
        Assert.True(File.Exists(loose), $"`{branch}` is a loose ref");
        File.WriteAllText(loose, "not a commit\n");
    }

    /// <summary>A session's tree with one commit, the tree removed by hand so its branch stands alone.</summary>
    private async Task<string> TreelessAsync(SessionTrees trees, string root)
    {
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "a.txt", "a\n", "work on a");
        await GitAsync(root, "worktree", "remove", opened.Path);
        Assert.NotNull(new SessionBranches(Home).Of("engine", opened.Branch));
        return opened.Branch;
    }

    /// <summary>🔴 LAND3's door (TreeTidies.cs:331): a branch that does not read is not "gone already", and its record stays.</summary>
    [Fact]
    public async Task The_doors_removal_of_a_branch_that_does_not_read_keeps_its_record_and_says_unread()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var branch = await TreelessAsync(trees, root);
        Break(root, branch);

        var removal = await trees.RemoveBranchAsync(root, "engine", branch);

        Assert.False(removal.Removed, removal.Message);
        Assert.Contains("unread", removal.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("gone already", removal.Message, StringComparison.Ordinal);
        Assert.NotNull(new SessionBranches(Home).Of("engine", branch));
    }

    /// <summary>
    /// 🔴 The landings' clean-up (LandedCleanups.cs:357, :311): a landing's branch that does not read is never retired as gone; it is kept,
    /// and its row says git could not read it.
    /// </summary>
    [Fact]
    public async Task The_clean_up_keeps_a_landing_whose_branch_does_not_read_and_says_unread()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s1", "q1", "First"));
        Break(root, landed.Branch!);

        var done = await trees.CleanAsync([("engine", "aurora", root)], new HashSet<string>());

        Assert.NotNull(new LandedBranches(Home).Of("engine", landed.Branch!));
        var result = Assert.Single(done.Landed, each => each.Item.Branch == landed.Branch);
        Assert.False(result.Removed, result.Message);
        Assert.Equal(LandedKind.Unknown, result.Item.Kind);
        Assert.Contains("unread", result.Item.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 The landings' clean-up (LandedCleanups.cs:365): whether the branch still holds the commit the landing made, where git could not
    /// answer, is not "no longer the landing's", so the record is never retired.
    /// </summary>
    [Fact]
    public async Task The_clean_up_keeps_a_landing_whose_ancestry_does_not_read()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s1", "q1", "First"));
        var made = new LandedBranches(Home).Of("engine", landed.Branch!)!.Tip;
        WorkingTree.Answered.Value = arguments => arguments is ["merge-base", "--is-ancestor", var ancestor, _] && ancestor == made
            ? (128, "", "fatal: could not read the object store")
            : null;
        SweepDone done;
        try
        {
            done = await trees.CleanAsync([("engine", "aurora", root)], new HashSet<string>());
        }
        finally
        {
            WorkingTree.Answered.Value = null;
        }

        Assert.NotNull(new LandedBranches(Home).Of("engine", landed.Branch!));
        var result = Assert.Single(done.Landed, each => each.Item.Branch == landed.Branch);
        Assert.False(result.Removed, result.Message);
        Assert.Equal(LandedKind.Unknown, result.Item.Kind);
        Assert.Contains("unread", result.Item.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 The record of where session branches grew from (BranchReplays.cs:422): the branch a gone tree moved onto, where it does not read,
    /// is not gone, so the entry naming where that tree's work is stays.
    /// </summary>
    [Fact]
    public async Task The_press_keeps_an_entry_whose_followed_branch_does_not_read()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        await GitAsync(root, "branch", "feature/mine");
        Break(root, "feature/mine");
        var from = (await GitAsync(root, "rev-parse", "main")).Trim();
        new SessionBranches(Home).Record(new GrownBranch("engine", "aurora", "daoris/gone-tree", "main", from, null, DateTimeOffset.UtcNow, Now: "feature/mine"));

        await trees.CleanAsync([("engine", "aurora", root)], new HashSet<string>());

        Assert.NotNull(new SessionBranches(Home).Of("engine", "daoris/gone-tree"));
    }

    /// <summary>
    /// 🔴 The same record (BranchReplays.cs:409): <c>for-each-ref</c> leaves a session branch that does not read out of its list, which is
    /// no word that it went, so its entry stays.
    /// </summary>
    [Fact]
    public async Task The_press_keeps_an_entry_whose_session_branch_does_not_read()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var branch = await TreelessAsync(trees, root);
        Break(root, branch);

        await trees.CleanAsync([("engine", "aurora", root)], new HashSet<string>());

        Assert.NotNull(new SessionBranches(Home).Of("engine", branch));
    }

    /// <summary>The rule's other half: a branch git says is missing still forgets its record, at the door and in the press.</summary>
    [Fact]
    public async Task A_branch_git_says_is_missing_is_still_forgotten()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var first = await TreelessAsync(trees, root);
        var second = await TreelessAsync(trees, root);
        await GitAsync(root, "branch", "-D", first);
        await GitAsync(root, "branch", "-D", second);

        var removal = await trees.RemoveBranchAsync(root, "engine", first);
        await trees.CleanAsync([("engine", "aurora", root)], new HashSet<string>());

        Assert.Contains("gone already", removal.Message, StringComparison.Ordinal);
        Assert.Null(new SessionBranches(Home).Of("engine", first));
        Assert.Null(new SessionBranches(Home).Of("engine", second));
    }
}
