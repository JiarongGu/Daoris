using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// REVIEW2 (D113): the review and the preview of a session whose landing tidied its tree away. The review says where
/// the work landed and reads its changes from the landed branch in the repository's own checkout; the preview reads a
/// file from that branch; a branch gone since is said, with whether its work reads on the line. Real git throughout,
/// in scratch repositories, and each reads without writing: the checkout's status is the same before and after.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class LandedReviewTests : LandedFixture
{
    /// <summary>A branch rule that tidies: the landing takes the tree away, as on the installed window.</summary>
    private void Tidies() =>
        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: true))
            .Save(Path.Combine(Home, "driver.json"));

    private static readonly LandingSubject Subject = new("s1a2b3c4", "0fda18", "Fix the API gap");

    /// <summary>
    /// The installed window's case: the landing tidied the tree, so the tree's range cannot be read. The review reads
    /// the landed branch from where its work grew from, in the checkout, which may be dirty and on any branch and is
    /// left exactly so.
    /// </summary>
    [Fact]
    public async Task A_tidied_landing_is_reviewed_from_its_branch_in_the_checkout_which_is_left_as_it_was()
    {
        var root = await RepositoryAsync("engine");
        Tidies();
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", Subject, extra: ("added.txt", "new\n"));
        Assert.Contains("Cleaned up", landed.Message);
        // The person's checkout, mid-work on a branch of their own.
        await GitAsync(root, "checkout", "-q", "-b", "person/elsewhere");
        await File.WriteAllTextAsync(Path.Combine(root, "in-flight.txt"), "mine\n");
        var status = await GitAsync(root, "status", "--porcelain", "--branch");

        var entry = trees.Recorded.Landing("s1a2b3c4")!;
        var review = await trees.LandedReviewAsync(root, entry);

        Assert.Equal(LandedState.Standing, review.State);
        Assert.True(review.ReadsAsLanded(treeGone: true));
        var diff = Assert.IsType<WorkingTree.TreeDiff>(review.Changes);
        Assert.Equal(["added.txt", "shared.txt"], diff.Files.Select(file => file.Path).Order(StringComparer.Ordinal));
        Assert.Equal(entry.From, diff.Base);
        Assert.Contains("+two", diff.Files.Single(file => file.Path == "shared.txt").Patch);
        Assert.Equal(status, await GitAsync(root, "status", "--porcelain", "--branch"));
    }

    /// <summary>A landing recorded before its `from` was (WSR6) is read from where its branch leaves the line.</summary>
    [Fact]
    public async Task A_landing_with_no_recorded_start_is_read_from_where_its_branch_leaves_the_line()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        await LandAsync(trees, root, "shared.txt", "one\ntwo\n", Subject);
        var entry = trees.Recorded.Landing("s1a2b3c4")! with { From = null };
        // The line moves on after the landing: its own change is not the session's.
        await CommitAsync(root, "line.txt", "the line's own\n", "the line moves");

        var review = await trees.LandedReviewAsync(root, entry);

        Assert.Equal(["shared.txt"], review.Changes!.Files.Select(file => file.Path));
    }

    /// <summary>
    /// The branch squash-merged and removed by the clean-up: the review says it is gone, what the clean-up proved,
    /// and — while git still holds its commits — that its work reads on the line now.
    /// </summary>
    [Fact]
    public async Task A_branch_the_clean_up_removed_is_gone_and_its_work_is_said_to_read_on_the_line()
    {
        var root = await RepositoryAsync("engine");
        Tidies();
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", Subject);
        await SquashAsync(root, landed.Branch!);
        await trees.CleanAsync([("engine", "aurora", root)], new HashSet<string>());
        Assert.Empty(await GitAsync(root, "branch", "--list", landed.Branch!));

        var entry = trees.Recorded.Landing("s1a2b3c4")!;
        var review = await trees.LandedReviewAsync(root, entry);

        Assert.Equal(LandedState.Gone, review.State);
        Assert.Null(review.Changes);
        Assert.Equal(LandedKind.OnLine, entry.RemovedAs);
        Assert.Equal("main", entry.RemovedOn);
        Assert.Equal(LandedKind.OnLine, review.Reads!.Kind);
        Assert.Equal("main", review.Reads.Where);
        Assert.True(review.ReadsAsLanded(treeGone: true));
        Assert.Contains("that branch is gone now", LandedReviewWords.Describe("s1a2b3c4", review));
    }

    /// <summary>A branch deleted by hand, whose content the line never took, is gone and said to differ, file by file.</summary>
    [Fact]
    public async Task A_branch_deleted_by_hand_is_gone_and_the_files_the_line_lacks_are_named()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", Subject);
        await GitAsync(root, "branch", "-D", landed.Branch!);

        var review = await trees.LandedReviewAsync(root, trees.Recorded.Landing("s1a2b3c4")!);

        Assert.Equal(LandedState.Gone, review.State);
        Assert.Equal(LandedKind.Differs, review.Reads!.Kind);
        Assert.Equal(["shared.txt"], review.Reads.Files);
    }

    /// <summary>Once git no longer holds the landing's commit, whether its work reads on the line is not guessed.</summary>
    [Fact]
    public async Task A_gone_branch_whose_commits_git_pruned_is_not_guessed_about()
    {
        var root = await RepositoryAsync("engine");
        Tidies();
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", Subject);
        await GitAsync(root, "branch", "-D", landed.Branch!);
        await GitAsync(root, "reflog", "expire", "--expire=now", "--expire-unreachable=now", "--all");
        await GitAsync(root, "gc", "--quiet", "--prune=now");

        var review = await trees.LandedReviewAsync(root, trees.Recorded.Landing("s1a2b3c4")!);

        Assert.Equal(LandedState.Gone, review.State);
        Assert.Equal(LandedReads.CommitsGone, review.Reads!.Kind);
    }

    /// <summary>A branch of the landing's name that no longer holds its commit — rebased or replaced by hand — is not read as its work.</summary>
    [Fact]
    public async Task A_branch_rebased_by_hand_is_not_read_as_the_sessions_work()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", Subject);
        await GitAsync(root, "branch", "-f", landed.Branch!, "main");

        var review = await trees.LandedReviewAsync(root, trees.Recorded.Landing("s1a2b3c4")!);

        Assert.Equal(LandedState.NotOurs, review.State);
        Assert.Null(review.Changes);
    }

    /// <summary>
    /// 🔴 git walks UP: a registered root that is a folder inside a repository, and not one of its own, is no checkout,
    /// and git is asked nothing of the repository around it.
    /// </summary>
    [Fact]
    public async Task A_root_that_is_not_a_repository_of_its_own_is_no_checkout()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        await LandAsync(trees, root, "shared.txt", "one\ntwo\n", Subject);
        var inside = Path.Combine(root, "sub");
        Directory.CreateDirectory(inside);
        var entry = trees.Recorded.Landing("s1a2b3c4")!;

        Assert.Equal(LandedState.NoCheckout, (await trees.LandedReviewAsync(inside, entry)).State);
        Assert.Equal(LandedState.NoCheckout, (await trees.LandedReviewAsync(null, entry)).State);
        Assert.Equal(FilePreviewRefusal.NoTree, (await FilePreview.ReadLandedAsync(inside, entry, "shared.txt")).Refusal);
    }

    // ——— the preview

    /// <summary>With the tree gone, a file is read from the landed branch as git holds it, and the answer names the branch.</summary>
    [Fact]
    public async Task A_file_is_previewed_from_the_landed_branch_once_the_tree_is_gone()
    {
        var root = await RepositoryAsync("engine");
        Tidies();
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "shared.txt"), "one\ntwo\n");
        Directory.CreateDirectory(Path.Combine(tree.Path, "src"));
        await File.WriteAllBytesAsync(Path.Combine(tree.Path, "src", "blob.bin"), [1, 0, 2, 3]);
        await GitAsync(tree.Path, "add", "-A");
        await GitAsync(tree.Path, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", "work");
        Assert.True((await trees.LandAsync(tree.Path, Subject)).Landed);
        Assert.True(SessionTrees.TreeGone(tree.Path));
        // The person's checkout holds something else under that name now: the branch's copy is what is shown.
        await File.WriteAllTextAsync(Path.Combine(root, "shared.txt"), "the person's own\n");
        var status = await GitAsync(root, "status", "--porcelain");
        var entry = trees.Recorded.Landing("s1a2b3c4")!;

        var read = await FilePreview.ReadLandedAsync(root, entry, "shared.txt", tree.Path);
        Assert.Equal(FilePreviewRefusal.None, read.Refusal);
        Assert.Equal("one\ntwo\n", read.File!.Text);
        Assert.Equal("shared.txt", read.File.Path);
        Assert.Equal(entry.Branch, read.File.Branch);

        // A path the card named in the tree that is gone is the same file on the branch.
        var absolute = await FilePreview.ReadLandedAsync(root, entry, Path.Combine(tree.Path, "shared.txt"), tree.Path);
        Assert.Equal("one\ntwo\n", absolute.File!.Text);

        var binary = await FilePreview.ReadLandedAsync(root, entry, "src/blob.bin", tree.Path);
        Assert.True(binary.File!.Binary);
        Assert.Equal(4, binary.File.Size);

        Assert.Equal(FilePreviewRefusal.NotOnBranch, (await FilePreview.ReadLandedAsync(root, entry, "src", tree.Path)).Refusal);
        Assert.Equal(FilePreviewRefusal.NotOnBranch, (await FilePreview.ReadLandedAsync(root, entry, "gone.txt", tree.Path)).Refusal);
        Assert.Equal(FilePreviewRefusal.GitFolder, (await FilePreview.ReadLandedAsync(root, entry, ".git/config", tree.Path)).Refusal);
        Assert.Equal(FilePreviewRefusal.Outside, (await FilePreview.ReadLandedAsync(root, entry, "../other.txt", tree.Path)).Refusal);
        Assert.Equal(status, await GitAsync(root, "status", "--porcelain"));
    }

    /// <summary>A file past the bound is cut at a line's end, its whole size said, as a file on disk is.</summary>
    [Fact]
    public async Task A_file_on_the_branch_past_the_bound_is_cut_at_a_lines_end()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var line = new string('x', 99) + "\n";
        var text = string.Concat(Enumerable.Repeat(line, (FilePreview.Budget / line.Length) + 50));
        await LandAsync(trees, root, "big.txt", text, Subject);

        var read = await FilePreview.ReadLandedAsync(root, trees.Recorded.Landing("s1a2b3c4")!, "big.txt");

        Assert.True(read.File!.Truncated);
        Assert.Equal(text.Length, read.File.Size);
        Assert.EndsWith("\n", read.File.Text);
        Assert.True(read.File.Text!.Length <= FilePreview.Budget);
    }

    /// <summary>Neither the tree nor the landing's branch: the no-tree answer, as before.</summary>
    [Fact]
    public async Task With_the_branch_gone_too_there_is_no_file_to_preview()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", Subject);
        await GitAsync(root, "branch", "-D", landed.Branch!);

        Assert.Equal(FilePreviewRefusal.NoTree, (await FilePreview.ReadLandedAsync(root, trees.Recorded.Landing("s1a2b3c4")!, "shared.txt")).Refusal);
    }
}
