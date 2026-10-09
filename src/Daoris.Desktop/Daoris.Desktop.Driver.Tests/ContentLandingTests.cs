using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SQUASHTIDY1f (D102's SQUASHTIDY1b and SQUASHTIDY1f notes): a landing of a tree whose work the line, or a branch of the
/// person's, already holds by content is refused at the one path every door lands through. SQUASHTIDY1b took the offer off the
/// session's head, but the review's own Accept, the terminal's <c>trees land</c> and the look's automatic landing still reached
/// <see cref="SessionTrees.LandAsync"/>, which would land a second copy of the work. Now it asks SQUASHTIDY1b's judgement, refuses
/// in the head's clause with its own code, and writes nothing. Real git in a scratch repository, so this is the suite's Process
/// half; nothing reaches a network.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class ContentLandingTests : LandedFixture
{
    private static readonly LandingSubject Subject = new("s1a2b3c4", "0fda18", "Fix the gap");

    /// <summary>
    /// The owner's case on the branch form: the session's work went in as a squash merge and its tree still stands. The press is
    /// refused in the head's own clause, by its code, and nothing is made: no branch, no record, the line where it was, the tree and
    /// its branch as they were, and no recovery ref, since nothing was discarded.
    /// </summary>
    [Fact]
    public async Task A_squash_merged_trees_landing_is_refused_saying_where_its_work_is_and_writes_nothing()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await CommitAsync(tree.Path, "shared.txt", "one\ntwo\n", "more of the work");
        await SquashAsync(root, tree.Branch);
        var before = await StateAsync(root, tree);

        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.False(landed.Landed, landed.Message);
        Assert.Equal(AutoLandingCode.Carried, landed.Refusal);
        Assert.Equal(
            "Its work is on `main` by content (a squash merge), so it is not landed again: that would make a second copy of it. "
            + "Discard its tree instead.",
            landed.Message);
        Assert.Null(landed.Branch);
        Assert.Equal(before, await StateAsync(root, tree));
        Assert.Empty(trees.Recorded.All());
    }

    /// <summary>The merge form: the same refusal before the person's checkout is touched, so the line gains no second copy.</summary>
    [Fact]
    public async Task A_squash_merged_trees_merge_is_refused_and_the_line_does_not_move()
    {
        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Merge, Tidy: true)).Save(Path.Combine(Home, "driver.json"));
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await SquashAsync(root, tree.Branch);
        await CommitAsync(root, "later.txt", "the line moves on\n", "the line moves on");
        var before = await StateAsync(root, tree);

        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.False(landed.Landed, landed.Message);
        Assert.Equal(AutoLandingCode.Carried, landed.Refusal);
        Assert.StartsWith("Its work is on `main` by content (a squash merge), so it is not landed again", landed.Message);
        Assert.Equal(before, await StateAsync(root, tree));
        Assert.True(Directory.Exists(tree.Path), "the rule's tidy never runs for a landing that did not happen");
    }

    /// <summary>A cherry-pick onto a branch of the person's: refused naming that branch, which keeps its tip.</summary>
    [Fact]
    public async Task A_cherry_picked_trees_landing_is_refused_naming_the_branch_that_holds_it()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await CherryPickAsync(root, tree.Branch, "feature/x");
        var before = await StateAsync(root, tree);

        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.Equal(AutoLandingCode.Carried, landed.Refusal);
        Assert.StartsWith(
            "Its work is on `feature/x` by content (each file it changed reads the same there), so it is not landed again", landed.Message);
        Assert.Equal(before, await StateAsync(root, tree));
    }

    /// <summary>
    /// A held tree that holds what no commit does (SQUASHTIDY1c's keep): still refused, and the sentence says why its tree stays,
    /// as the head says it, rather than offering a discard that would not go unforced.
    /// </summary>
    [Fact]
    public async Task A_held_tree_that_stays_is_refused_saying_why_it_stays()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, ".gitignore", "*.db\n", "ignore databases");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await SquashAsync(root, tree.Branch);
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "local.db"), "rows only this tree has\n");

        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.Equal(AutoLandingCode.Carried, landed.Refusal);
        Assert.Contains("so it is not landed again: that would make a second copy of it. Its tree stays: ", landed.Message);
        Assert.Contains("local.db", landed.Message);
        Assert.DoesNotContain("Discard its tree instead", landed.Message);
    }

    /// <summary>
    /// The control: one file it changed reads otherwise everywhere, so nothing holds the whole of it, and it lands as before.
    /// A branch a landing made here never vouches either (SQUASHTIDY1c), so a redo after a landing the line reverted lands too.
    /// </summary>
    [Fact]
    public async Task Work_nothing_holds_whole_lands_as_before_and_a_landings_own_branch_never_vouches()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var first = await LandAsync(trees, root, "shared.txt", "two\n", new LandingSubject("s0a1b2c3", "0fda17", "Two"));
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", "merge the landing", first.Branch!);
        await CommitAsync(root, "shared.txt", "one\n", "revert it on the line");
        var redo = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(redo.Path, "shared.txt", "two\n", "the redo");
        await CommitAsync(redo.Path, "notes.txt", "the notes\n", "the notes");
        await GitAsync(root, "checkout", "--quiet", "-b", "feature/y");
        await CommitAsync(root, "notes.txt", "other notes\n", "the notes, taken otherwise");
        await GitAsync(root, "checkout", "--quiet", "main");

        var landed = await trees.LandAsync(redo.Path, Subject);

        Assert.True(landed.Landed, landed.Message);
        Assert.Null(landed.Refusal);
    }

    /// <summary>What a refused landing must leave as it was: the line's tip, every branch and its tip, the tree's status, and every ref under <c>refs/daoris/</c>.</summary>
    private static async Task<string> StateAsync(string root, TreeOpened tree) =>
        string.Join("\n",
            (await GitAsync(root, "for-each-ref", "--format=%(objectname) %(refname)", "refs/heads/", "refs/daoris/")).Trim(),
            (await GitAsync(root, "status", "--porcelain")).Trim(),
            Directory.Exists(tree.Path) ? (await GitAsync(tree.Path, "status", "--porcelain")).Trim() : "gone",
            Directory.Exists(tree.Path) ? (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim() : "gone");
}
