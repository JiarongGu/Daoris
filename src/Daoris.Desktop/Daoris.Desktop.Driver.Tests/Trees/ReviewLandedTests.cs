using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// REVIEWLANDED1 (D102's REVIEWLANDED1 note): a session whose landing replayed its commits onto a line that had moved, and merged them,
/// kept its review row and its tree could not be removed without <c>--force</c>, since the review and the removal judged by ancestry while
/// <c>trees land --plan</c> read it landed; and a session landed on a <c>quest/</c> branch the parent merged and pruned was judged against
/// a branch that no longer existed. Now the content proof reads the landing's own per-commit reading (<see cref="ReplayHolds"/>), so the
/// review, the tidy and the removal read one answer; a commit truly not on the line is still kept and named as work to land.
/// </summary>
/// <remarks>Real git in a scratch repository: the landing's replay is made by hand as the parent's was, with a cherry-pick and a merge.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class ReviewLandedTests : LandedFixture
{
    private const string Ten = "1\n2\n3\n4\n5\n6\n7\n8\n9\n10\n";

    private const string Replayed = "its work is on `main` by content (a landing replayed each of its commits there)";

    /// <summary>
    /// The install's case: the line moved in a file the session changed, and its commits were replayed onto the line. It leaves the
    /// review at once, and its tree goes without <c>--force</c>, saying where its work is, its commits kept at the recovery ref.
    /// </summary>
    [Fact]
    public async Task A_session_landed_by_replay_leaves_the_review_and_its_tree_goes_unforced()
    {
        var (root, trees, tree) = await SessionAsync();
        await GitAsync(root, "cherry-pick", "-x", $"main..{tree.Branch}");
        var tip = (await GitAsync(root, "rev-parse", tree.Branch)).Trim();

        var work = await trees.WorkAsync(tree.Path);
        var removal = await trees.RemoveAsync(tree.Path);

        Assert.NotNull(work);
        Assert.False(work.Holds, "a session whose work landed by replay holds nothing to review");
        Assert.Equal(Replayed, work.Held?.Held.Said);
        Assert.True(removal.Removed, removal.Message);
        Assert.Contains(Replayed, removal.Message);
        Assert.False(Directory.Exists(tree.Path));
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", $"refs/daoris/discarded/{tree.Branch}")).Trim());
    }

    /// <summary>
    /// This repository's rule: the landing replayed the session onto its own <c>quest/</c> branch, the parent merged that branch into the
    /// line and pruned it. Its work is on the line, so it leaves the review and is not landed again.
    /// </summary>
    [Fact]
    public async Task A_session_landed_on_a_quest_branch_since_pruned_leaves_the_review()
    {
        var (root, trees, tree) = await SessionAsync();
        await LandOnQuestBranchAsync(root, tree.Branch);

        var work = await trees.WorkAsync(tree.Path);
        var landing = await trees.LandAsync(tree.Path, new LandingSubject("s1a2b3c4", "0fda20", "Work"));

        Assert.NotNull(work);
        Assert.False(work.Holds, "a session whose quest branch the line merged holds nothing to review");
        Assert.Equal(Replayed, work.Held?.Held.Said);
        Assert.Equal(AutoLandingCode.Carried, landing.Refusal);
    }

    /// <summary>
    /// Told to rebase after its landing, the session made a commit the line never got: it stays to review, its count and its offer are
    /// that commit alone, and an unforced removal is refused naming it, never the commits the line already holds.
    /// </summary>
    [Fact]
    public async Task A_session_with_a_commit_not_on_the_line_is_kept_and_says_that_commit()
    {
        var (root, trees, tree) = await SessionAsync();
        await LandOnQuestBranchAsync(root, tree.Branch);
        await CommitAsync(tree.Path, "after.txt", "the work after its landing\n", "the work after its landing");

        var work = await trees.WorkAsync(tree.Path);
        var removal = await trees.RemoveAsync(tree.Path);

        Assert.NotNull(work);
        Assert.True(work.Holds);
        Assert.Null(work.Held);
        Assert.Equal(1, work.Commits);
        Assert.Equal(1, LandOffer.Of(tree.Path, work)?.Commits);
        Assert.False(removal.Removed, removal.Message);
        Assert.Contains("the work after its landing", removal.Message);
        Assert.DoesNotContain("the session's own", removal.Message);
        Assert.True(Directory.Exists(tree.Path));
    }

    /// <summary>
    /// The line as it stands, never a commit it passed (D102, D148): the line took the replay and changed the session's file again, so it
    /// holds the patch but not the work, and the session stays to review.
    /// </summary>
    [Fact]
    public async Task Work_the_line_changed_again_after_the_replay_is_not_judged_landed()
    {
        var (root, trees, tree) = await SessionAsync();
        await GitAsync(root, "cherry-pick", "-x", $"main..{tree.Branch}");
        await CommitAsync(root, "f.txt", Ten.Replace("1\n", "one, the line's\n").Replace("10\n", "ten, changed again\n"), "the line changes it again");

        var work = await trees.WorkAsync(tree.Path);

        Assert.NotNull(work);
        Assert.True(work.Holds);
        Assert.Null(work.Held);
        Assert.False((await trees.RemoveAsync(tree.Path)).Removed);
    }

    /// <summary>
    /// A session's tree whose two commits change the tenth line of a file and add one of its own; then the line changes the file's
    /// first line, so no tree on the line is the session's and the file reads otherwise there whatever is replayed.
    /// </summary>
    private async Task<(string Root, SessionTrees Trees, TreeOpened Tree)> SessionAsync()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "f.txt", Ten, "the file");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "f.txt", Ten.Replace("10\n", "ten, the session's\n"), "the session's own change");
        await CommitAsync(tree.Path, "own.txt", "the session's own file\n", "the session's own file");
        await CommitAsync(root, "f.txt", Ten.Replace("1\n", "one, the line's\n"), "the line moves on");
        return (root, trees, tree);
    }

    /// <summary>The landing's replay onto a <c>quest/</c> branch of the line, merged into the line by the parent, the branch then pruned.</summary>
    private static async Task LandOnQuestBranchAsync(string root, string branch)
    {
        await GitAsync(root, "checkout", "--quiet", "-b", "quest/work-0fda20");
        await GitAsync(root, "cherry-pick", "-x", $"main..{branch}");
        await GitAsync(root, "checkout", "--quiet", "main");
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", "Merge quest/work-0fda20", "quest/work-0fda20");
        await GitAsync(root, "branch", "-D", "quest/work-0fda20");
    }
}
