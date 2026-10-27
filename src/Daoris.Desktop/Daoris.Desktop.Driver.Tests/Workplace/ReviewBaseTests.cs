using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// REVIEWBASE1 (D164's note): a session tree's review measures from the newest commit its work shares with the line, so a tree
/// that took in newer line commits shows its own changes alone, and says how far the latest known line is past it. Real git in
/// scratch repositories, `origin` a bare one beside each; the review fetches nothing, so what it reads is what the last fetch left.
/// </summary>
/// <remarks>
/// Seen 2026-10-13 on the install: a conversation's work merged by two pull requests, its tree then switched to the latest line,
/// and Review listed 26 files, everything the line took in since the session began, as the session's work.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class ReviewBaseTests : LandedFixture
{
    /// <summary>A repository with origin, a session's tree grown from its line, and the commit its record says it began at.</summary>
    private async Task<(string Root, string Origin, SessionTrees Trees, TreeOpened Tree, string Began)> TreeAsync()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        return (root, origin, trees, tree, await RevAsync(tree.Path, "HEAD"));
    }

    /// <summary>The line moves on by a commit of someone else's, here and on origin.</summary>
    private static async Task LineMovesAsync(string root, string file = "line.txt")
    {
        await CommitAsync(root, file, "theirs\n", "the line moves on");
        await GitAsync(root, "push", "--quiet", "origin", "main");
    }

    [Fact]
    public async Task A_tree_that_merged_the_line_shows_only_its_own_changes()
    {
        var (root, _, trees, tree, began) = await TreeAsync();
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work");
        await LineMovesAsync(root);
        await GitAsync(tree.Path, "merge", "--quiet", "--no-edit", "main");

        var range = await trees.ReviewRangeAsync(tree.Path, began);

        Assert.Equal(await RevAsync(root, "main"), range.From);
        Assert.Equal(["own.txt"], await ChangedAsync(tree.Path, range.From));
        Assert.Equal(new LineBehind("main", 0, 2), range.Behind);
    }

    [Fact]
    public async Task A_tree_rebased_onto_the_line_shows_only_its_own_changes()
    {
        var (root, _, trees, tree, began) = await TreeAsync();
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work");
        await LineMovesAsync(root);
        await GitAsync(tree.Path, "rebase", "--quiet", "main");

        var range = await trees.ReviewRangeAsync(tree.Path, began);

        Assert.Equal(await RevAsync(root, "main"), range.From);
        Assert.Equal(["own.txt"], await ChangedAsync(tree.Path, range.From));
        Assert.Equal(new LineBehind("main", 0, 1), range.Behind);
    }

    /// <summary>
    /// The owner's case: its work merged into the line, the line moved on again, and the tree switched to the line on origin. Its
    /// HEAD holds nothing of its own, so the review lists nothing, where the record's base listed everything the line took in.
    /// </summary>
    [Fact]
    public async Task A_tree_detached_at_the_line_shows_nothing_and_holds_nothing_beyond_it()
    {
        var (root, _, trees, tree, began) = await TreeAsync();
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work");
        await GitAsync(root, "merge", "--quiet", "--no-ff", "--no-edit", tree.Branch);
        await LineMovesAsync(root);
        await GitAsync(tree.Path, "switch", "--quiet", "--detach", "origin/main");

        var range = await trees.ReviewRangeAsync(tree.Path, began);

        Assert.Equal(await RevAsync(tree.Path, "HEAD"), range.From);
        Assert.Empty(await ChangedAsync(tree.Path, range.From));
        Assert.Equal(new LineBehind("main", 0, 0), range.Behind);
        // What it measured from before: the line's own work, read as the session's.
        Assert.Equal(["line.txt", "own.txt"], await ChangedAsync(tree.Path, began));
    }

    [Fact]
    public async Task A_tree_fast_forwarded_to_the_line_shows_nothing_and_holds_nothing_beyond_it()
    {
        var (root, _, trees, tree, began) = await TreeAsync();
        await LineMovesAsync(root);
        await GitAsync(tree.Path, "merge", "--quiet", "--ff-only", "main");

        var range = await trees.ReviewRangeAsync(tree.Path, began);

        Assert.Equal(await RevAsync(root, "main"), range.From);
        Assert.Empty(await ChangedAsync(tree.Path, range.From));
        Assert.Equal(new LineBehind("main", 0, 0), range.Behind);
    }

    /// <summary>
    /// A tree on an older line commit, with a commit of its own: measured from where it began, as before, and the line is counted
    /// at the newer of the branch here and its copy on origin, which a fetch moved past it. Nothing is fetched or moved by the read.
    /// </summary>
    [Fact]
    public async Task A_tree_behind_the_line_with_its_own_commits_shows_them_and_says_how_far_behind_it_is()
    {
        var (root, origin, trees, tree, began) = await TreeAsync();
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work");
        await LineMovesAsync(root);
        // Origin moves once more, and this checkout's last fetch saw it; its own `main` did not take it.
        await CommitOnOriginAsync(origin, "the line moves on again", ("later.txt", "later\n"));
        await GitAsync(root, "fetch", "--quiet", "origin");
        var refs = await GitAsync(root, "for-each-ref");

        var range = await trees.ReviewRangeAsync(tree.Path, began);

        Assert.Equal(began, range.From);
        Assert.Equal(["own.txt"], await ChangedAsync(tree.Path, range.From));
        Assert.Equal(new LineBehind("main", 2, 1), range.Behind);
        Assert.Equal(refs, await GitAsync(root, "for-each-ref"));
    }

    /// <summary>
    /// The record's base is gone from the tree's history (its parent was rewritten and the tree replayed onto the new one): the
    /// grown ledger's word on where it grows from now stands, as before, ahead of the line it is newer than.
    /// </summary>
    [Fact]
    public async Task A_tree_whose_base_is_gone_falls_back_to_where_its_branch_now_grows_from()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        await GitAsync(root, "branch", "topic");
        await CommitOnAsync(root, "topic", "parent.txt", "first take\n", "the parent's work");
        var tree = await trees.OpenAsync(root, "engine", "aurora", from: "topic");
        var began = await RevAsync(tree.Path, "HEAD");
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work");
        // The parent is written again, and the tree replayed onto it: the commit it began at is in its history no more.
        await GitAsync(root, "branch", "topic-again", "main");
        await CommitOnAsync(root, "topic-again", "parent.txt", "second take\n", "the parent's work, again");
        var again = await RevAsync(root, "topic-again");
        await GitAsync(tree.Path, "rebase", "--quiet", "--onto", again, began);
        trees.Grown.Record(new GrownBranch("engine", "aurora", tree.Branch, "main", again, "topic-again", DateTimeOffset.UtcNow));

        var range = await trees.ReviewRangeAsync(tree.Path, began);

        Assert.Equal(again, range.From);
        Assert.Equal(["own.txt"], await ChangedAsync(tree.Path, range.From));
        Assert.Equal(new LineBehind("main", 0, 2), range.Behind);
    }

    /// <summary>A chain's step grown from its parent's tip (`topic`, one commit the line does not hold), with a commit of its own.</summary>
    private async Task<(string Root, SessionTrees Trees, TreeOpened Tree, string Began)> StepAsync(string parentFile = "parent.txt")
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        await GitAsync(root, "branch", "topic");
        await CommitOnAsync(root, "topic", parentFile, "the parent's\n", "the parent's work");
        var tree = await trees.OpenAsync(root, "engine", "aurora", from: "topic");
        var began = await RevAsync(tree.Path, "HEAD");
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work");
        return (root, trees, tree, began);
    }

    /// <summary>
    /// REVIEWBASE3 (D164's REVIEWBASE1 note): a chain's step grew from its parent's tip and then merged the line, so the record's base
    /// and the line's are incomparable. Measured from the line's, the parent's work read as the step's; measured from a merge of the
    /// two (in trees, nothing on disk), the step's own changes stand alone. The base the page names is still the line's commit.
    /// </summary>
    [Fact]
    public async Task A_chain_step_that_merged_the_line_shows_only_its_own_changes()
    {
        var (root, trees, tree, began) = await StepAsync();
        await LineMovesAsync(root);
        await GitAsync(tree.Path, "merge", "--quiet", "--no-edit", "main");
        var status = await GitAsync(tree.Path, "status", "--porcelain", "--untracked-files=all");

        var range = await trees.ReviewRangeAsync(tree.Path, began);

        Assert.Equal(await RevAsync(root, "main"), range.From);
        Assert.NotNull(range.Against);
        Assert.Equal(["own.txt"], await ChangedAsync(tree.Path, range.Reads));
        // What it read before: the parent's work beside its own.
        Assert.Equal(["own.txt", "parent.txt"], await ChangedAsync(tree.Path, range.From));
        // The parent's commit, its own and its merge are beyond the line; nothing of the tree was touched by the read.
        Assert.Equal(new LineBehind("main", 0, 3), range.Behind);
        Assert.Equal(status, await GitAsync(tree.Path, "status", "--porcelain", "--untracked-files=all"));
    }

    /// <summary>
    /// The parent's work and the line's conflict, which the step resolved in its own merge: git's merged tree would hold conflict
    /// markers neither side wrote, so the review reads from the line's as before, the parent's work with it.
    /// </summary>
    [Fact]
    public async Task A_chain_step_whose_parent_conflicts_with_the_line_reads_from_the_line_as_before()
    {
        var (root, trees, tree, began) = await StepAsync(parentFile: "line.txt");
        await LineMovesAsync(root);
        await GitAsync(tree.Path, "merge", "--quiet", "--no-edit", "-X", "ours", "main");

        var range = await trees.ReviewRangeAsync(tree.Path, began);

        Assert.Equal(await RevAsync(root, "main"), range.From);
        Assert.Null(range.Against);
        Assert.Equal(range.From, range.Reads);
        Assert.Equal(["line.txt", "own.txt"], await ChangedAsync(tree.Path, range.Reads));
    }

    private static async Task<string> RevAsync(string cwd, string revision) => (await GitAsync(cwd, "rev-parse", revision)).Trim();

    /// <summary>The paths the review lists from <paramref name="from"/>, as the route reads them.</summary>
    private static async Task<string[]> ChangedAsync(string tree, string from) =>
        [.. Assert.IsType<WorkingTree.TreeDiff>(await WorkingTree.DiffAsync(tree, from)).Files.Select(file => file.Path).Order(StringComparer.Ordinal)];
}
